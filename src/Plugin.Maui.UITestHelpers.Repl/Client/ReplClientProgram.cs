using System.IO.Pipes;
using System.Text;
using Plugin.Maui.UITestHelpers.Repl.Hosting;
using Spectre.Console;

namespace Plugin.Maui.UITestHelpers.Repl.Client
{
	/// <summary>
	/// Entry point of the REPL window: <c>dotnet exec Plugin.Maui.UITestHelpers.Repl.dll --pipe name --token token</c>.
	/// It's started by <c>app.Repl()</c> and connects back to the test process.
	/// </summary>
	internal static class ReplClientProgram
	{
		public static async Task<int> Main(string[] args)
		{
			string? pipe = null;
			string? token = null;

			for (int i = 0; i < args.Length - 1; i++)
			{
				switch (args[i])
				{
					case "--pipe":
						pipe = args[++i];
						break;
					case "--token":
						token = args[++i];
						break;
				}
			}

			if (pipe is null || token is null)
			{
				Console.Error.WriteLine("This is the window of the UI test REPL. It's started by calling app.Repl() in a test.");
				Console.Error.WriteLine("Usage: dotnet exec Plugin.Maui.UITestHelpers.Repl.dll --pipe <name> --token <token>");
				return 2;
			}

			Console.OutputEncoding = Encoding.UTF8;
			Console.InputEncoding = Encoding.UTF8;

			try
			{
				Console.Title = "UI test REPL";
			}
			catch
			{
				// Not every terminal allows setting a title.
			}

			try
			{
				using var stream = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

				AnsiConsole.MarkupLine("[grey]Connecting to the test…[/]");
				await stream.ConnectAsync(15_000).ConfigureAwait(false);

				using var channel = new FrameChannel(stream);
				var backend = new PipeReplBackend(channel);

				if (!await backend.HandshakeAsync(token).ConfigureAwait(false))
				{
					AnsiConsole.MarkupLine("[red]The test didn't accept the connection.[/]");
					return 1;
				}

				await ReplClient.RunAsync(backend).ConfigureAwait(false);

				if (backend.Disconnected)
				{
					AnsiConsole.MarkupLine("[yellow]The test ended, closing the REPL.[/]");
					await Task.Delay(1500).ConfigureAwait(false);
				}
				else
				{
					await channel.WriteAsync(new Frame { Type = FrameTypes.Bye }).ConfigureAwait(false);
					AnsiConsole.MarkupLine("[green]REPL closed, the test continues. You can close this window.[/]");
				}

				return 0;
			}
			catch (Exception ex)
			{
				AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
				AnsiConsole.MarkupLine("[grey]Press any key to close this window.[/]");

				try
				{
					Console.ReadKey(true);
				}
				catch
				{
					// Nothing to wait for without a console.
				}

				return 1;
			}
		}
	}
}
