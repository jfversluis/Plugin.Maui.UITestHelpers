using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Plugin.Maui.UITestHelpers.Repl.Client;
using Plugin.Maui.UITestHelpers.Repl.Engine;
using Spectre.Console;

namespace Plugin.Maui.UITestHelpers.Repl.Hosting
{
	internal static class ReplHost
	{
		public static void RunInline(ReplSession session)
		{
			session.WriteBanner(AnsiConsole.Console);
			ReplClient.RunAsync(new LocalReplBackend(session)).GetAwaiter().GetResult();
			AnsiConsole.MarkupLine("[green]REPL closed, the test continues.[/]");
		}

		/// <summary>
		/// Evaluates code for a REPL window that runs in its own process, and returns when the window closes.
		/// </summary>
		public static void RunRemote(ReplSession session, ReplOptions options, ReplMode mode)
		{
			// Kept short: on macOS and Linux a pipe name becomes a socket path, which has a small length limit.
			var pipeName = "uitr-" + Guid.NewGuid().ToString("N")[..10];
			var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

			using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
			var command = TerminalLauncher.CreateCommand(pipeName, token);

			var warmUp = Task.Run(session.WarmUp);

			if (mode == ReplMode.Window)
			{
				try
				{
					TerminalLauncher.Launch(command, options);
					Notify("[REPL] Opened a REPL window. The test continues when you close it with 'exit'.");
				}
				catch (Exception ex)
				{
					Notify($"[REPL] Could not open a terminal window: {ex.Message}");
					PrintManualInstructions(command);
				}
			}
			else
			{
				PrintManualInstructions(command);
			}

			using var connectTimeout = new CancellationTokenSource(options.ConnectTimeout);

			try
			{
				server.WaitForConnectionAsync(connectTimeout.Token).GetAwaiter().GetResult();
			}
			catch (OperationCanceledException)
			{
				throw new TimeoutException(
					$"The REPL window didn't connect within {options.ConnectTimeout.TotalSeconds:0} seconds. " +
					$"To start it yourself, run this in a terminal: {command.ToDisplayString()}");
			}

			using var channel = new FrameChannel(server);

			var hello = channel.ReadAsync(connectTimeout.Token).GetAwaiter().GetResult();
			if (hello is not { Type: FrameTypes.Hello } || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hello.Token ?? string.Empty), Encoding.UTF8.GetBytes(token)))
			{
				throw new InvalidOperationException("Something connected to the REPL that didn't identify itself correctly. The REPL was not started.");
			}

			warmUp.GetAwaiter().GetResult();

			// Reads whatever the window sends, so a cancel request or the window closing gets through while code is running.
			var inbox = new BlockingCollection<Frame>();
			var windowClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var runningLock = new object();
			CancellationTokenSource? running = null;
			int runningId = 0;

			_ = Task.Run(async () =>
			{
				try
				{
					while (true)
					{
						var frame = await channel.ReadAsync().ConfigureAwait(false);
						if (frame is null || frame.Type == FrameTypes.Bye)
						{
							break;
						}

						if (frame.Type == FrameTypes.Cancel)
						{
							lock (runningLock)
							{
								if (running is not null && runningId == frame.Id)
								{
									running.Cancel();
								}
							}

							continue;
						}

						inbox.Add(frame);
					}
				}
				catch (Exception)
				{
					// Whatever went wrong, the window is of no use anymore.
				}
				finally
				{
					windowClosed.TrySetResult();
					inbox.Add(new Frame { Type = FrameTypes.Bye });
				}
			});

			void Send(Frame frame) => channel.WriteAsync(frame).GetAwaiter().GetResult();

			using (var writer = new FrameTextWriter(text => Send(new Frame { Type = FrameTypes.Output, Text = text })))
			{
				session.WriteBanner(CreateConsole(writer, hello));
				writer.Flush();
			}

			Send(new Frame { Type = FrameTypes.Done, Id = 0 });

			foreach (var frame in inbox.GetConsumingEnumerable())
			{
				if (frame.Type == FrameTypes.Bye)
				{
					break;
				}

				if (frame.Type == FrameTypes.Complete)
				{
					var items = session.Complete(frame.Text ?? string.Empty, frame.Caret).Select(CompletionDto.From).ToList();
					Send(new Frame { Type = FrameTypes.Completions, Id = frame.Id, Items = items });
				}
				else if (frame.Type == FrameTypes.Exec)
				{
					var cancellation = new CancellationTokenSource();
					lock (runningLock)
					{
						running = cancellation;
						runningId = frame.Id;
					}

					var writer = new FrameTextWriter(text => Send(new Frame { Type = FrameTypes.Output, Text = text }));
					var console = CreateConsole(writer, frame);
					var evaluation = Task.Run(() => session.ExecuteAsync(frame.Text ?? string.Empty, console, redirectConsole: true, cancellation.Token));

					// Scripts can't be interrupted, so don't let one that never returns keep the test (and the window) stuck:
					// closing the window gives the test back.
					bool closedWhileRunning = false;
					bool warned = false;
					var cancelRequestedAt = (DateTime?)null;

					while (!evaluation.IsCompleted)
					{
						if (Task.WaitAny(new Task[] { evaluation, windowClosed.Task }, 500) == 1)
						{
							closedWhileRunning = true;
							break;
						}

						if (cancellation.IsCancellationRequested)
						{
							cancelRequestedAt ??= DateTime.UtcNow;

							if (!warned && DateTime.UtcNow - cancelRequestedAt > TimeSpan.FromSeconds(3))
							{
								warned = true;
								console.MarkupLine("[yellow]Still running. A command that is waiting on the device can't be interrupted, it ends when the call returns. Close this window to continue the test anyway.[/]");
								writer.Flush();
							}
						}
					}

					if (closedWhileRunning)
					{
						cancellation.Cancel();
						Notify("[REPL] The window was closed while a command was still running, continuing the test.");
						return;
					}

					bool keepGoing;
					try
					{
						keepGoing = evaluation.GetAwaiter().GetResult();
					}
					catch (Exception ex)
					{
						console.MarkupLine($"[red]✗ {Markup.Escape(ex.GetType().Name)}[/] {Markup.Escape(ex.Message)}");
						keepGoing = true;
					}

					writer.Flush();
					writer.Dispose();

					lock (runningLock)
					{
						running = null;
					}

					cancellation.Dispose();
					Send(new Frame { Type = FrameTypes.Done, Id = frame.Id, Exit = !keepGoing });

					if (!keepGoing)
					{
						break;
					}
				}
			}

			Send(new Frame { Type = FrameTypes.Bye });
		}

		static void PrintManualInstructions(ReplWindowCommand command)
		{
			Notify("[REPL] Waiting for a REPL window. To open one yourself, run this in a terminal:");
			Notify("  " + command.ToDisplayString());
		}

		/// <summary>
		/// Test runners hold back what a test writes to the console until the test is done, and the REPL is waiting
		/// for the user right now. So the message also goes to NUnit's live progress output when NUnit is around,
		/// and to the terminal the tests were started from, when there is one.
		/// </summary>
		internal static void Notify(string message)
		{
			Console.WriteLine(message);

			try
			{
				var progress = AppDomain.CurrentDomain.GetAssemblies()
					.FirstOrDefault(a => a.GetName().Name == "nunit.framework")
					?.GetType("NUnit.Framework.TestContext")
					?.GetProperty("Progress")
					?.GetValue(null) as TextWriter;

				if (progress is not null)
				{
					progress.WriteLine(message);
					return;
				}
			}
			catch
			{
				// Fall through to the terminal.
			}

			if (OperatingSystem.IsWindows())
			{
				return;
			}

			try
			{
				using var tty = new FileStream("/dev/tty", FileMode.Open, FileAccess.Write);
				using var writer = new StreamWriter(tty, new UTF8Encoding(false));
				writer.Write("\r\n" + message + "\r\n");
			}
			catch
			{
				// There's no terminal, for example on a build agent.
			}
		}

		static IAnsiConsole CreateConsole(TextWriter writer, Frame client)
		{
			var colorSystem = client.Colors switch
			{
				0 => ColorSystemSupport.NoColors,
				1 => ColorSystemSupport.Legacy,
				2 => ColorSystemSupport.Standard,
				3 => ColorSystemSupport.EightBit,
				_ => ColorSystemSupport.TrueColor,
			};

			var console = AnsiConsole.Create(new AnsiConsoleSettings
			{
				Ansi = AnsiSupport.Yes,
				ColorSystem = colorSystem,
				Interactive = InteractionSupport.No,
				Out = new AnsiConsoleOutput(writer),
			});

			console.Profile.Width = client.Width > 0 ? client.Width : 100;
			console.Profile.Height = client.Height > 0 ? client.Height : 30;
			console.Profile.Capabilities.Unicode = client.Unicode;
			console.Profile.Encoding = Encoding.UTF8;

			return console;
		}
	}

	/// <summary>
	/// Collects what's written and sends it in chunks.
	/// </summary>
	internal sealed class FrameTextWriter : TextWriter
	{
		readonly Action<string> _send;
		readonly StringBuilder _buffer = new();
		readonly object _lock = new();

		public FrameTextWriter(Action<string> send)
		{
			_send = send;
		}

		public override Encoding Encoding => Encoding.UTF8;

		public override void Write(char value)
		{
			lock (_lock)
			{
				_buffer.Append(value);
			}
		}

		public override void Write(string? value)
		{
			lock (_lock)
			{
				_buffer.Append(value);
				if (_buffer.Length > 16 * 1024 || value?.Contains('\n') == true)
				{
					FlushCore();
				}
			}
		}

		public override void Write(char[] buffer, int index, int count)
		{
			lock (_lock)
			{
				_buffer.Append(buffer, index, count);
			}
		}

		public override void Flush()
		{
			lock (_lock)
			{
				FlushCore();
			}
		}

		void FlushCore()
		{
			if (_buffer.Length == 0)
			{
				return;
			}

			var text = _buffer.ToString();
			_buffer.Clear();
			_send(text);
		}
	}
}
