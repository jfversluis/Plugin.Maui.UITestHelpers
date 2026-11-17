using PrettyPrompt;
using PrettyPrompt.Completion;
using PrettyPrompt.Consoles;
using PrettyPrompt.Documents;
using PrettyPrompt.Highlighting;
using Spectre.Console;

namespace Plugin.Maui.UITestHelpers.Repl.Client
{
	/// <summary>
	/// The interactive part of the REPL: reads what you type (with highlighting, completion and history),
	/// hands it to a backend and loops until you exit.
	/// </summary>
	internal static class ReplClient
	{
		public static async Task RunAsync(IReplBackend backend, Func<bool>? isDisconnected = null)
		{
			var historyPath = GetHistoryPath();
			var configuration = new PromptConfiguration(
				prompt: new FormattedString("❯ ", new FormatSpan(0, 1, AnsiColor.BrightCyan)));

			await using var prompt = new Prompt(historyPath, new ReplPromptCallbacks(backend), configuration: configuration);

			bool ctrlCPending = false;

			while (true)
			{
				var response = await prompt.ReadLineAsync().ConfigureAwait(false);

				if (!response.IsSuccess)
				{
					if (ctrlCPending)
					{
						break;
					}

					ctrlCPending = true;
					AnsiConsole.MarkupLine("[grey]Press Ctrl+C again or type [bold]exit[/] to close the REPL.[/]");
					continue;
				}

				ctrlCPending = false;

				bool keepGoing = await backend.ExecuteAsync(response.Text, response.CancellationToken).ConfigureAwait(false);
				if (!keepGoing)
				{
					break;
				}
			}
		}

		static string? GetHistoryPath()
		{
			try
			{
				var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Plugin.Maui.UITestHelpers");
				Directory.CreateDirectory(directory);
				return Path.Combine(directory, "repl-history");
			}
			catch
			{
				return null;
			}
		}
	}

	sealed class ReplPromptCallbacks : PromptCallbacks
	{
		readonly IReplBackend _backend;

		public ReplPromptCallbacks(IReplBackend backend)
		{
			_backend = backend;
		}

		protected override Task<IReadOnlyCollection<FormatSpan>> HighlightCallbackAsync(string text, CancellationToken cancellationToken) =>
			Task.FromResult(ReplSyntax.Highlight(text));

		protected override Task<TextSpan> GetSpanToReplaceByCompletionAsync(string text, int caret, CancellationToken cancellationToken)
		{
			int start = caret;
			while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
			{
				start--;
			}

			int end = caret;
			while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
			{
				end++;
			}

			return Task.FromResult(TextSpan.FromBounds(start, end));
		}

		protected override async Task<IReadOnlyList<CompletionItem>> GetCompletionItemsAsync(string text, int caret, TextSpan spanToBeReplaced, CancellationToken cancellationToken)
		{
			var completions = await _backend.CompleteAsync(text, caret, cancellationToken).ConfigureAwait(false);

			return completions
				.Select(c => new CompletionItem(
					replacementText: c.Name,
					displayText: new FormattedString(c.Name, new FormatSpan(0, c.Name.Length, Color(c.Kind))),
					getExtendedDescription: _ => Task.FromResult(new FormattedString(c.Kind + " " + c.Detail))))
				.ToList();
		}

		// Completion opens after a '.' (or on Ctrl+Space), not on every letter: otherwise Enter would pick a suggestion
		// instead of running what you typed, which is annoying for commands like 'tree'.
		protected override Task<bool> ShouldOpenCompletionWindowAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken) =>
			Task.FromResult(keyPress.ConsoleKeyInfo.KeyChar == '.' && caret > 0);

		protected override Task<KeyPress> TransformKeyPressAsync(string text, int caret, KeyPress keyPress, CancellationToken cancellationToken)
		{
			// Enter inside an unfinished statement (an open parenthesis or brace) continues on the next line instead of submitting.
			if (keyPress.ConsoleKeyInfo is { Key: ConsoleKey.Enter, Modifiers: 0 } &&
				caret == text.Length &&
				text.Length > 0 &&
				!ReplSyntax.IsCompleteSubmission(text))
			{
				return Task.FromResult(new KeyPress(new ConsoleKeyInfo('\n', ConsoleKey.Enter, shift: true, alt: false, control: false)));
			}

			return Task.FromResult(keyPress);
		}

		static AnsiColor Color(string kind) => kind switch
		{
			"method" => AnsiColor.BrightYellow,
			"property" or "field" => AnsiColor.BrightCyan,
			"type" or "enum" => AnsiColor.BrightGreen,
			"namespace" => AnsiColor.BrightBlack,
			_ => AnsiColor.White,
		};
	}
}
