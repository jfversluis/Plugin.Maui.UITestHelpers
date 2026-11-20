using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Repl.Engine
{
	/// <summary>
	/// The object the REPL scripts run against. The public members become script globals.
	/// </summary>
	public sealed class ReplGlobals
	{
		public ReplGlobals(IApp app)
		{
			__replApp = app;
		}

		public IApp __replApp { get; }
	}

	internal enum ReplOutcome
	{
		Value,
		NoValue,
		CompilationError,
		Exception,
		Cancelled,
	}

	internal sealed record ReplResult(
		ReplOutcome Outcome,
		object? Value = null,
		Exception? Exception = null,
		IReadOnlyList<Diagnostic>? Diagnostics = null);

	internal sealed record ReplCompletion(string Name, string Kind, string? Detail);

	/// <summary>
	/// Evaluates C# submissions against the app, keeping state (variables, usings) between submissions.
	/// </summary>
	internal sealed class ReplEngine
	{
		static readonly string[] DefaultImports =
		{
			"System",
			"System.Collections.Generic",
			"System.Drawing",
			"System.IO",
			"System.Linq",
			"System.Threading",
			"System.Threading.Tasks",
			"OpenQA.Selenium",
			"Plugin.Maui.UITestHelpers.Core",
			"Plugin.Maui.UITestHelpers.Appium",
		};

		static readonly AsyncLocal<bool> s_isEvaluating = new();

		/// <summary>
		/// Gets whether the current code runs as a REPL submission, for example <c>app.Repl()</c> typed into the REPL.
		/// </summary>
		public static bool IsEvaluating => s_isEvaluating.Value;

		readonly object _gate = new();
		readonly ReplGlobals _globals;
		readonly ScriptOptions _options;
		ScriptState<object>? _state;

		public ReplEngine(IApp app, IEnumerable<string>? extraImports = null)
		{
			_globals = new ReplGlobals(app);

			LoadReferencedAssemblies(app);

			var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
				.ToList();

			var imports = DefaultImports
				.Concat(extraImports ?? Enumerable.Empty<string>())
				.Distinct()
				.ToList();

			_options = ScriptOptions.Default
				.WithReferences(GetReferences(loadedAssemblies))
				.WithImports(imports)
				.WithLanguageVersion(LanguageVersion.Latest)
				.WithEmitDebugInformation(false);

			Initialize(app);
		}

		// The runtime loads assemblies when they are first used, but scripts should be able to use everything the tests can.
		static void LoadReferencedAssemblies(IApp app)
		{
			var pending = new Queue<Assembly>();
			pending.Enqueue(app.GetType().Assembly);
			pending.Enqueue(typeof(AppiumApp).Assembly);
			pending.Enqueue(typeof(IApp).Assembly);

			if (Assembly.GetEntryAssembly() is { } entry)
			{
				pending.Enqueue(entry);
			}

			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			while (pending.TryDequeue(out var assembly))
			{
				foreach (var reference in assembly.GetReferencedAssemblies())
				{
					if (reference.Name is not { } name ||
						name.StartsWith("System.", StringComparison.Ordinal) ||
						name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
						name is "System" or "netstandard" or "mscorlib" ||
						!seen.Add(name))
					{
						continue;
					}

					try
					{
						pending.Enqueue(Assembly.Load(reference));
					}
					catch
					{
						// An optional dependency that isn't there, nothing to reference.
					}
				}
			}
		}

		static IEnumerable<MetadataReference> GetReferences(List<Assembly> loadedAssemblies)
		{
			var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (var assembly in loadedAssemblies)
			{
				var name = assembly.GetName().Name;
				if (name is not null)
				{
					paths.TryAdd(name, assembly.Location);
				}
			}

			// The runtime assemblies that haven't been loaded yet, so scripts can use any part of the BCL.
			if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
			{
				foreach (var path in trusted.Split(Path.PathSeparator))
				{
					var name = Path.GetFileNameWithoutExtension(path);
					if (name.StartsWith("System.", StringComparison.Ordinal) ||
						name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
						name is "System" or "netstandard" or "mscorlib" or "WindowsBase")
					{
						paths.TryAdd(name, path);
					}
				}
			}

			return paths.Values.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
		}

		void Initialize(IApp app)
		{
			// Scripts see 'app' (and 'App', as in UITestBase) typed as the actual platform app, so its specific members show up.
			var appType = app.GetType();
			var typeName = appType.IsVisible && !appType.IsGenericType
				? "global::" + appType.FullName!.Replace('+', '.')
				: "global::Plugin.Maui.UITestHelpers.Core.IApp";

			var prelude = $"var app = ({typeName})__replApp; var App = app;";

			if (app is AppiumApp)
			{
				prelude += " var driver = ((global::Plugin.Maui.UITestHelpers.Appium.AppiumApp)app).Driver;";
			}

			var script = CSharpScript.Create<object>(prelude, _options, typeof(ReplGlobals));
			_state = script.RunAsync(_globals).GetAwaiter().GetResult();
		}

		public async Task<ReplResult> EvaluateAsync(string code, CancellationToken cancellationToken)
		{
			Script<object> script;
			bool returnsValue;
			ScriptState<object>? state;

			lock (_gate)
			{
				state = _state;
				script = state!.Script.ContinueWith<object>(code, _options);

				var errors = GetErrors(script, cancellationToken);

				// 'var x = 5' is natural to type in a REPL, but a statement needs its semicolon.
				if (errors.Count > 0 && errors.Any(e => e.Id == "CS1002") && !code.TrimEnd().EndsWith(';'))
				{
					var withSemicolon = state.Script.ContinueWith<object>(code.TrimEnd() + ";", _options);
					if (GetErrors(withSemicolon, cancellationToken).Count == 0)
					{
						script = withSemicolon;
						errors = new List<Diagnostic>();
					}
				}

				if (errors.Count > 0)
				{
					return new ReplResult(ReplOutcome.CompilationError, Diagnostics: errors);
				}

				returnsValue = ReturnsValue(script);
			}

			ScriptState<object> next;
			try
			{
				s_isEvaluating.Value = true;
				next = await script.RunFromAsync(state!, _ => true, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				return new ReplResult(ReplOutcome.Cancelled);
			}
			finally
			{
				s_isEvaluating.Value = false;
			}

			lock (_gate)
			{
				_state = next;
			}

			if (next.Exception is not null)
			{
				return next.Exception is OperationCanceledException
					? new ReplResult(ReplOutcome.Cancelled)
					: new ReplResult(ReplOutcome.Exception, Exception: Unwrap(next.Exception));
			}

			return returnsValue
				? new ReplResult(ReplOutcome.Value, next.ReturnValue)
				: new ReplResult(ReplOutcome.NoValue);
		}

		static List<Diagnostic> GetErrors(Script script, CancellationToken cancellationToken) =>
			script.Compile(cancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

		static Exception Unwrap(Exception exception)
		{
			while (exception is TargetInvocationException or AggregateException && exception.InnerException is not null)
			{
				exception = exception.InnerException;
			}

			return exception;
		}

		// A trailing expression without a semicolon is the "result" of the submission, unless it's void.
		static bool ReturnsValue(Script script)
		{
			var compilation = script.GetCompilation();
			var tree = compilation.SyntaxTrees.Last();
			var root = tree.GetCompilationUnitRoot();

			if (root.Members.LastOrDefault() is GlobalStatementSyntax { Statement: ExpressionStatementSyntax { SemicolonToken.IsMissing: true } statement })
			{
				var type = compilation.GetSemanticModel(tree).GetTypeInfo(statement.Expression).Type;
				return type is not null && type.SpecialType != SpecialType.System_Void;
			}

			return false;
		}

		public IReadOnlyList<ReplCompletion> GetCompletions(string text, int caret)
		{
			caret = Math.Clamp(caret, 0, text.Length);

			int start = caret;
			while (start > 0 && IsIdentifierChar(text[start - 1]))
			{
				start--;
			}

			var partial = text[start..caret];
			bool isMember = start > 0 && text[start - 1] == '.';

			if (!isMember && partial.Length == 0)
			{
				return Array.Empty<ReplCompletion>();
			}

			lock (_gate)
			{
				var script = _state!.Script.ContinueWith<object>(text, _options);
				var compilation = script.GetCompilation();
				var tree = compilation.SyntaxTrees.Last();
				var model = compilation.GetSemanticModel(tree);

				IEnumerable<ISymbol> symbols;

				if (isMember)
				{
					var dot = tree.GetRoot().FindToken(start - 1);
					ExpressionSyntax? receiver = dot.Parent switch
					{
						MemberAccessExpressionSyntax m => m.Expression,
						MemberBindingExpressionSyntax b => b.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression,
						_ => null,
					};

					if (receiver is null)
					{
						return Array.Empty<ReplCompletion>();
					}

					var info = model.GetSymbolInfo(receiver);
					var receiverSymbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();

					if (receiverSymbol is INamespaceOrTypeSymbol container)
					{
						symbols = model.LookupSymbols(caret, container, null, includeReducedExtensionMethods: false)
							.Where(s => s.IsStatic || s is INamedTypeSymbol or INamespaceSymbol);
					}
					else if (model.GetTypeInfo(receiver).Type is { TypeKind: not TypeKind.Error } type)
					{
						symbols = model.LookupSymbols(caret, type, null, includeReducedExtensionMethods: true)
							.Where(s => !s.IsStatic && s is not INamedTypeSymbol);
					}
					else
					{
						return Array.Empty<ReplCompletion>();
					}
				}
				else
				{
					symbols = model.LookupSymbols(caret)
						.Where(s => s.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase));
				}

				var items = symbols
					.Where(IsInteresting)
					.GroupBy(s => s.Name)
					.Select(g => ToCompletion(g.Key, g.ToList()))
					.Where(c => partial.Length == 0 || c.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
					.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
					.Take(400)
					.ToList();

				return items;
			}
		}

		static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

		static bool IsInteresting(ISymbol symbol)
		{
			if (symbol.IsImplicitlyDeclared || symbol.Name.Length == 0 || !char.IsLetter(symbol.Name[0]) && symbol.Name[0] != '_' ||
				symbol.Name.StartsWith("__", StringComparison.Ordinal))
			{
				return false;
			}

			return symbol switch
			{
				IMethodSymbol m => m.MethodKind is MethodKind.Ordinary or MethodKind.ReducedExtension,
				IPropertySymbol or IFieldSymbol or IEventSymbol or ILocalSymbol or INamedTypeSymbol or INamespaceSymbol => true,
				_ => false,
			};
		}

		static ReplCompletion ToCompletion(string name, List<ISymbol> symbols)
		{
			var first = symbols[0];
			var kind = first switch
			{
				IMethodSymbol => "method",
				IPropertySymbol => "property",
				IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } => "enum",
				IFieldSymbol or ILocalSymbol => "field",
				IEventSymbol => "event",
				INamespaceSymbol => "namespace",
				INamedTypeSymbol => "type",
				_ => "other",
			};

			var detail = first.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
			if (symbols.Count > 1)
			{
				detail += $" (+{symbols.Count - 1} overload{(symbols.Count > 2 ? "s" : "")})";
			}

			return new ReplCompletion(name, kind, detail);
		}
	}
}
