using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Plugin.Maui.UITestHelpers.Repl.Engine;
using Plugin.Maui.UITestHelpers.Repl.Hosting;
using Spectre.Console;

namespace Plugin.Maui.UITestHelpers.Repl.Client
{
	/// <summary>
	/// What the prompt needs to evaluate code, either directly (inline) or through the test process (window).
	/// </summary>
	internal interface IReplBackend
	{
		/// <summary>
		/// Runs the code and writes its output to the console.
		/// </summary>
		/// <returns><see langword="false"/> when the REPL should close.</returns>
		Task<bool> ExecuteAsync(string code, CancellationToken cancellationToken);

		Task<IReadOnlyList<ReplCompletion>> CompleteAsync(string text, int caret, CancellationToken cancellationToken);
	}

	/// <summary>
	/// Evaluates in this process, used when the REPL runs in the console of the test.
	/// </summary>
	internal sealed class LocalReplBackend : IReplBackend
	{
		readonly ReplSession _session;

		public LocalReplBackend(ReplSession session)
		{
			_session = session;
		}

		public Task<bool> ExecuteAsync(string code, CancellationToken cancellationToken) =>
			_session.ExecuteAsync(code, AnsiConsole.Console, redirectConsole: false, cancellationToken);

		public Task<IReadOnlyList<ReplCompletion>> CompleteAsync(string text, int caret, CancellationToken cancellationToken) =>
			Task.FromResult(_session.Complete(text, caret));
	}

	/// <summary>
	/// Sends the code to the test process, and shows what comes back.
	/// </summary>
	internal sealed class PipeReplBackend : IReplBackend
	{
		readonly FrameChannel _channel;

		// One request at a time: they all read from the same pipe, and the prompt abandons completions when you keep typing.
		readonly SemaphoreSlim _gate = new(1, 1);
		int _nextId;

		public PipeReplBackend(FrameChannel channel)
		{
			_channel = channel;
		}

		/// <summary>
		/// Raised when the test process goes away.
		/// </summary>
		public bool Disconnected { get; private set; }

		/// <summary>
		/// Introduces this window to the test process and shows the banner it answers with.
		/// </summary>
		public async Task<bool> HandshakeAsync(string token)
		{
			await _gate.WaitAsync().ConfigureAwait(false);
			try
			{
				return await HandshakeCoreAsync(token).ConfigureAwait(false);
			}
			finally
			{
				_gate.Release();
			}
		}

		async Task<bool> HandshakeCoreAsync(string token)
		{
			var profile = AnsiConsole.Profile;

			if (!await _channel.WriteAsync(new Frame
			{
				Type = FrameTypes.Hello,
				Token = token,
				Width = profile.Width,
				Height = profile.Height,
				Colors = (int)profile.Capabilities.ColorSystem,
				Unicode = profile.Capabilities.Unicode,
			}).ConfigureAwait(false))
			{
				return false;
			}

			return await PumpAsync(0).ConfigureAwait(false) is not null;
		}

		async Task<bool?> PumpAsync(int id)
		{
			while (true)
			{
				var frame = await _channel.ReadAsync().ConfigureAwait(false);
				if (frame is null || frame.Type == FrameTypes.Bye)
				{
					Disconnected = true;
					return null;
				}

				switch (frame.Type)
				{
					case FrameTypes.Output:
						Console.Out.Write(frame.Text);
						Console.Out.Flush();
						break;
					case FrameTypes.Done when frame.Id == id:
						return frame.Exit;
				}
			}
		}

		public async Task<bool> ExecuteAsync(string code, CancellationToken cancellationToken)
		{
			await _gate.WaitAsync().ConfigureAwait(false);
			try
			{
				return await ExecuteCoreAsync(code, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_gate.Release();
			}
		}

		async Task<bool> ExecuteCoreAsync(string code, CancellationToken cancellationToken)
		{
			int id = ++_nextId;
			var profile = AnsiConsole.Profile;

			if (!await _channel.WriteAsync(new Frame
			{
				Type = FrameTypes.Exec,
				Id = id,
				Text = code,
				Width = profile.Width,
				Height = profile.Height,
				Colors = (int)profile.Capabilities.ColorSystem,
				Unicode = profile.Capabilities.Unicode,
			}).ConfigureAwait(false))
			{
				Disconnected = true;
				return false;
			}

			bool cancelSent = false;
			using var registration = cancellationToken.Register(() =>
			{
				if (!cancelSent)
				{
					cancelSent = true;
					_ = _channel.WriteAsync(new Frame { Type = FrameTypes.Cancel, Id = id });
				}
			});

			return await PumpAsync(id).ConfigureAwait(false) is false;
		}

		public async Task<IReadOnlyList<ReplCompletion>> CompleteAsync(string text, int caret, CancellationToken cancellationToken)
		{
			try
			{
				await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				return Array.Empty<ReplCompletion>();
			}

			try
			{
				// Once asked, the answer is always read, even when the prompt lost interest, so it can't end up in the next request.
				return await CompleteCoreAsync(text, caret).ConfigureAwait(false);
			}
			finally
			{
				_gate.Release();
			}
		}

		async Task<IReadOnlyList<ReplCompletion>> CompleteCoreAsync(string text, int caret)
		{
			int id = ++_nextId;

			if (!await _channel.WriteAsync(new Frame { Type = FrameTypes.Complete, Id = id, Text = text, Caret = caret }).ConfigureAwait(false))
			{
				Disconnected = true;
				return Array.Empty<ReplCompletion>();
			}

			while (true)
			{
				var frame = await _channel.ReadAsync().ConfigureAwait(false);
				if (frame is null || frame.Type == FrameTypes.Bye)
				{
					Disconnected = true;
					return Array.Empty<ReplCompletion>();
				}

				if (frame.Type == FrameTypes.Completions && frame.Id == id)
				{
					return frame.Items?.Select(i => new ReplCompletion(i.Name, i.Kind, i.Detail)).ToList() ?? new List<ReplCompletion>();
				}
			}
		}
	}

	internal static class ReplSyntax
	{
		static readonly CSharpParseOptions ScriptOptions = new(languageVersion: LanguageVersion.Latest, kind: SourceCodeKind.Script);

		public static bool IsCompleteSubmission(string text)
		{
			if (SyntaxFactory.IsCompleteSubmission(CSharpSyntaxTree.ParseText(text, ScriptOptions)))
			{
				return true;
			}

			// 'var x = 5' is only missing its semicolon, which the REPL adds, so that's complete too.
			var withSemicolon = CSharpSyntaxTree.ParseText(text.TrimEnd() + ";", ScriptOptions);
			return !withSemicolon.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error);
		}

		public static IReadOnlyCollection<PrettyPrompt.Highlighting.FormatSpan> Highlight(string text)
		{
			var spans = new List<PrettyPrompt.Highlighting.FormatSpan>();
			if (text.Length == 0)
			{
				return spans;
			}

			var tree = CSharpSyntaxTree.ParseText(text, ScriptOptions);
			var tokens = tree.GetRoot().DescendantTokens().ToList();

			for (int i = 0; i < tokens.Count; i++)
			{
				var token = tokens[i];

				foreach (var trivia in token.GetAllTrivia())
				{
					if (trivia.Kind() is SyntaxKind.SingleLineCommentTrivia or SyntaxKind.MultiLineCommentTrivia && trivia.Span.Length > 0)
					{
						spans.Add(new(trivia.Span.Start, trivia.Span.Length, PrettyPrompt.Highlighting.AnsiColor.BrightBlack));
					}
				}

				if (token.Span.Length == 0)
				{
					continue;
				}

				var kind = token.Kind();
				PrettyPrompt.Highlighting.AnsiColor? color = null;

				if (SyntaxFacts.IsKeywordKind(kind) || SyntaxFacts.IsContextualKeyword(kind) && kind is SyntaxKind.VarKeyword or SyntaxKind.AwaitKeyword)
				{
					color = PrettyPrompt.Highlighting.AnsiColor.BrightBlue;
				}
				else if (kind is SyntaxKind.StringLiteralToken or SyntaxKind.CharacterLiteralToken or SyntaxKind.InterpolatedStringTextToken or SyntaxKind.InterpolatedStringStartToken or SyntaxKind.InterpolatedStringEndToken or SyntaxKind.InterpolatedVerbatimStringStartToken)
				{
					color = PrettyPrompt.Highlighting.AnsiColor.BrightGreen;
				}
				else if (kind == SyntaxKind.NumericLiteralToken)
				{
					color = PrettyPrompt.Highlighting.AnsiColor.BrightMagenta;
				}
				else if (kind == SyntaxKind.IdentifierToken)
				{
					bool nextIsCall = i + 1 < tokens.Count && tokens[i + 1].IsKind(SyntaxKind.OpenParenToken);
					if (nextIsCall)
					{
						color = PrettyPrompt.Highlighting.AnsiColor.BrightYellow;
					}
					else if (char.IsUpper(token.Text[0]))
					{
						color = PrettyPrompt.Highlighting.AnsiColor.BrightCyan;
					}
				}

				if (color is { } c)
				{
					spans.Add(new(token.Span.Start, token.Span.Length, c));
				}
			}

			return spans;
		}
	}
}
