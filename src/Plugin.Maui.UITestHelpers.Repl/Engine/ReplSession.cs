using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using OpenQA.Selenium;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;
using Spectre.Console;
using TextCopy;

namespace Plugin.Maui.UITestHelpers.Repl.Engine
{
	/// <summary>
	/// One REPL session: the C# engine plus the built-in commands, in the test process where the app lives.
	/// </summary>
	internal sealed partial class ReplSession
	{
		static readonly (string Name, string Usage, string Description)[] Commands =
		{
			("tree", "tree [xpath] [--all] [--depth N]", "Show the element tree of the screen. Hides invisible and anonymous elements unless --all is used. An XPath shows just the matching subtrees."),
			("source", "source", "Print the raw page source (XML) of the screen."),
			("screenshot", "screenshot [file.png]", "Save a screenshot of the device. Without a file name one is created in the current directory."),
			("copy", "copy", "Copy everything you successfully ran in this session to the clipboard, ready to paste into a test."),
			("history", "history", "Show what you ran successfully in this session."),
			("trace", "trace", "Show the full details of the last error."),
			("clear", "clear", "Clear the screen."),
			("help", "help", "Show this help."),
			("exit", "exit | quit", "Close the REPL and continue running the test. Ctrl+C twice does the same."),
		};

		readonly IApp _app;
		readonly ReplEngine _engine;
		readonly List<string> _history = new();
		Exception? _lastException;

		public ReplSession(IApp app, ReplOptions options)
		{
			_app = app;
			_engine = new ReplEngine(app, options.Imports);
		}

		/// <summary>
		/// Prepares the compiler so the first completion and the first command don't have to wait for it.
		/// </summary>
		public void WarmUp()
		{
			try
			{
				_engine.GetCompletions("app.", 4);
			}
			catch
			{
				// Only an optimization.
			}
		}

		public IReadOnlyList<ReplCompletion> Complete(string text, int caret)
		{
			try
			{
				return _engine.GetCompletions(text, caret);
			}
			catch
			{
				return Array.Empty<ReplCompletion>();
			}
		}

		public void WriteBanner(IAnsiConsole console)
		{
			var grid = new Grid().AddColumn(new GridColumn().NoWrap().PadRight(2)).AddColumn();

			void Row(string label, string? value)
			{
				if (!string.IsNullOrWhiteSpace(value))
				{
					grid.AddRow(new Markup($"[grey]{label}[/]"), new Markup(Markup.Escape(value)));
				}
			}

			Row("Platform", Platform());
			Row("Device", Capability("deviceName") ?? Capability("appium:deviceName") ?? Capability("avd"));
			Row("App", SafeAppId());
			Row("Session", (_app as AppiumApp)?.Driver.SessionId?.ToString());
			Row("App type", _app.GetType().Name);

			grid.AddEmptyRow();
			grid.AddRow(
				new Markup("[grey]Try[/]"),
				new Markup("[bold]app[/].Tap([green]\"CounterBtn\"[/])   [bold]app[/].FindElements([green]\"id\"[/])   [cyan]tree[/]   [cyan]help[/]   [cyan]exit[/]"));

			var panel = new Panel(grid)
			{
				Header = new PanelHeader("[bold cyan] Plugin.Maui.UITestHelpers REPL [/]"),
				Border = BoxBorder.Rounded,
				BorderStyle = new Style(Color.Grey),
				Padding = new Padding(1, 0),
				Expand = false,
			};

			console.WriteLine();
			console.Write(panel);
			console.MarkupLine("[grey]Any C# works. [bold]app[/] (also [bold]App[/]) is the running app" + (_app is AppiumApp ? ", [bold]driver[/] the Appium driver" : "") + ". Tab completes, ↑ shows history.[/]");
			console.WriteLine();
		}

		string? Platform()
		{
			var name = Capability("platformName");
			var version = Capability("platformVersion");
			return name is null ? _app.GetType().Name : version is null ? name : $"{name} {version}";
		}

		string? Capability(string name)
		{
			try
			{
				return (_app as AppiumApp)?.Driver.Capabilities.GetCapability(name)?.ToString();
			}
			catch
			{
				return null;
			}
		}

		string? SafeAppId()
		{
			try
			{
				return _app.GetAppId();
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Runs one submission and writes the outcome to <paramref name="console"/>.
		/// </summary>
		/// <returns><see langword="false"/> when the user wants to leave the REPL.</returns>
		public async Task<bool> ExecuteAsync(string code, IAnsiConsole console, bool redirectConsole, CancellationToken cancellationToken)
		{
			code = code.Trim();
			if (code.Length == 0)
			{
				return true;
			}

			var originalOut = Console.Out;
			var originalError = Console.Error;

			if (redirectConsole)
			{
				// So Console.WriteLine from the scripts shows up in the REPL window rather than the test output.
				Console.SetOut(console.Profile.Out.Writer);
				Console.SetError(console.Profile.Out.Writer);
			}

			try
			{
				if (TryParseCommand(code, out var command, out var arguments))
				{
					return RunCommand(command, arguments, console);
				}

				var stopwatch = Stopwatch.StartNew();
				var result = await _engine.EvaluateAsync(code, cancellationToken).ConfigureAwait(false);
				stopwatch.Stop();

				switch (result.Outcome)
				{
					case ReplOutcome.Value:
						_history.Add(code);
						console.Write(ReplFormatter.Format(result.Value, _app));
						console.WriteLine();
						break;
					case ReplOutcome.NoValue:
						_history.Add(code);
						break;
					case ReplOutcome.CompilationError:
						WriteDiagnostics(console, code, result.Diagnostics!);
						break;
					case ReplOutcome.Exception:
						_lastException = result.Exception;
						WriteException(console, result.Exception!);
						break;
					case ReplOutcome.Cancelled:
						console.MarkupLine("[yellow]Cancelled[/]");
						break;
				}

				if (stopwatch.Elapsed > TimeSpan.FromMilliseconds(500) && result.Outcome is ReplOutcome.Value or ReplOutcome.NoValue)
				{
					console.MarkupLine($"[grey]({FormatElapsed(stopwatch.Elapsed)})[/]");
				}

				return true;
			}
			finally
			{
				if (redirectConsole)
				{
					Console.SetOut(originalOut);
					Console.SetError(originalError);
				}
			}
		}

		static string FormatElapsed(TimeSpan elapsed) =>
			elapsed.TotalSeconds < 10 ? $"{elapsed.TotalSeconds:0.0}s" : $"{elapsed.TotalSeconds:0}s";

		static bool TryParseCommand(string code, out string command, out string arguments)
		{
			command = string.Empty;
			arguments = string.Empty;

			var firstSpace = code.IndexOfAny(new[] { ' ', '\t', '\n' });
			var first = (firstSpace < 0 ? code : code[..firstSpace]).ToLowerInvariant();
			var rest = firstSpace < 0 ? string.Empty : code[(firstSpace + 1)..].Trim();

			if (first == "quit")
			{
				first = "exit";
			}

			if (!Commands.Any(c => c.Name == first))
			{
				return false;
			}

			// Something like 'copy = 3' or 'tree.Foo()' is C#, not a command.
			if (rest.StartsWith('=') || rest.StartsWith('.') || rest.StartsWith('(') || rest.EndsWith(';') || code.Contains('\n') && first != "tree")
			{
				return false;
			}

			command = first;
			arguments = rest;
			return true;
		}

		bool RunCommand(string command, string arguments, IAnsiConsole console)
		{
			try
			{
				switch (command)
				{
					case "exit":
						return false;
					case "help":
						WriteHelp(console);
						break;
					case "clear":
						console.Clear();
						break;
					case "tree":
						WriteTree(console, arguments);
						break;
					case "source":
						console.WriteLine(_app.ElementTree);
						break;
					case "screenshot":
						Screenshot(console, arguments);
						break;
					case "copy":
						Copy(console);
						break;
					case "history":
						WriteHistory(console);
						break;
					case "trace":
						if (_lastException is null)
						{
							console.MarkupLine("[grey]No errors so far.[/]");
						}
						else
						{
							console.WriteException(_lastException, ExceptionFormats.ShortenEverything);
						}

						break;
				}
			}
			catch (Exception ex)
			{
				_lastException = ex;
				WriteException(console, ex);
			}

			return true;
		}

		void WriteHelp(IAnsiConsole console)
		{
			var table = new Table { Border = TableBorder.Rounded, BorderStyle = new Style(Color.Grey) };
			table.AddColumn("[bold]Command[/]");
			table.AddColumn("[bold]What it does[/]");

			foreach (var (_, usage, description) in Commands)
			{
				table.AddRow(new Markup($"[cyan]{Markup.Escape(usage)}[/]"), new Markup(Markup.Escape(description)));
			}

			console.Write(table);
			console.MarkupLine("Everything else is C#, for example:");
			console.MarkupLine("  [grey]>[/] app.Tap(\"CounterBtn\")");
			console.MarkupLine("  [grey]>[/] var buttons = app.FindElements(\"CounterBtn\")");
			console.MarkupLine("  [grey]>[/] app.WaitForElement(\"CounterBtn\").GetText()");
			console.MarkupLine("[grey]Variables you declare stay available. Shift+Enter adds a new line. Ctrl+C cancels a running command, unless it is waiting on the device.[/]");
			console.WriteLine();
		}

		void WriteTree(IAnsiConsole console, string arguments)
		{
			bool all = false;
			int? depth = null;
			var xpath = new List<string>();

			var tokens = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < tokens.Length; i++)
			{
				switch (tokens[i])
				{
					case "--all" or "-a":
						all = true;
						break;
					case "--depth" or "-d" when i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out var d):
						depth = d;
						i++;
						break;
					default:
						xpath.Add(tokens[i]);
						break;
				}
			}

			var stopwatch = Stopwatch.StartNew();
			var source = _app.ElementTree;
			var tree = ElementTreeRenderer.Render(source, new TreeOptions(all, depth, xpath.Count == 0 ? null : string.Join(' ', xpath)), out var count);
			console.Write(tree);
			console.MarkupLine($"[grey]{count} element{(count == 1 ? "" : "s")} shown in {FormatElapsed(stopwatch.Elapsed)}. Use [bold]tree --all[/] for every element, [bold]tree //xpath[/] to zoom in.[/]");
			console.WriteLine();
		}

		void Screenshot(IAnsiConsole console, string fileName)
		{
			if (_app is not IScreenshotSupportedApp screenshots)
			{
				console.MarkupLine("[red]This app doesn't support screenshots.[/]");
				return;
			}

			if (string.IsNullOrWhiteSpace(fileName))
			{
				fileName = $"repl-screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png";
			}

			var file = screenshots.Screenshot(fileName.Trim('"'));
			console.MarkupLine($"Screenshot saved to [underline]{Markup.Escape(file.FullName)}[/]");
		}

		void Copy(IAnsiConsole console)
		{
			if (_history.Count == 0)
			{
				console.MarkupLine("[grey]Nothing to copy yet. Run something first.[/]");
				return;
			}

			var text = new StringBuilder();
			foreach (var entry in _history)
			{
				var line = AppRegex().Replace(entry, "App.");
				if (!line.EndsWith(';') && !line.EndsWith('}'))
				{
					line += ";";
				}

				text.AppendLine(line);
			}

			ClipboardService.SetText(text.ToString());
			console.MarkupLine($"Copied [bold]{_history.Count}[/] line{(_history.Count == 1 ? "" : "s")} to the clipboard. [grey](app. became App. to match UITestBase)[/]");
		}

		void WriteHistory(IAnsiConsole console)
		{
			if (_history.Count == 0)
			{
				console.MarkupLine("[grey]Nothing ran successfully yet.[/]");
				return;
			}

			for (int i = 0; i < _history.Count; i++)
			{
				console.MarkupLine($"[grey]{i + 1,3}[/]  {Markup.Escape(_history[i])}");
			}
		}

		static void WriteDiagnostics(IAnsiConsole console, string code, IReadOnlyList<Diagnostic> diagnostics)
		{
			bool multiline = code.Contains('\n');
			foreach (var diagnostic in diagnostics.Take(8))
			{
				var position = multiline ? $" [grey](line {diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1})[/]" : string.Empty;
				console.MarkupLine($"[red]✗ {diagnostic.Id}[/] {Markup.Escape(diagnostic.GetMessage())}{position}");
			}

			if (diagnostics.Count > 8)
			{
				console.MarkupLine($"[grey]… and {diagnostics.Count - 8} more errors[/]");
			}
		}

		static void WriteException(IAnsiConsole console, Exception exception)
		{
			var lines = exception.Message.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
			var first = lines.FirstOrDefault() ?? string.Empty;

			console.MarkupLine($"[red]✗ {Markup.Escape(exception.GetType().Name)}[/] {Markup.Escape(first)}");

			// Selenium messages carry useful context (the query, the timeout...) but also a lot of noise.
			foreach (var line in lines.Skip(1).Take(exception is WebDriverException ? 4 : 2))
			{
				console.MarkupLine($"  [grey]{Markup.Escape(line.Trim())}[/]");
			}

			console.MarkupLine("[grey]Type [bold]trace[/] for the full details.[/]");
		}

		[GeneratedRegex(@"(?<![\w.])app\.")]
		private static partial Regex AppRegex();
	}
}
