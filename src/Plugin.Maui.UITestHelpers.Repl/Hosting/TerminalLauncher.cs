using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Plugin.Maui.UITestHelpers.Repl.Hosting
{
	/// <summary>
	/// The command that starts the REPL window: this same assembly, run with the runtime of the test process.
	/// </summary>
	internal sealed record ReplWindowCommand(string FileName, IReadOnlyList<string> Arguments)
	{
		public string ToDisplayString() =>
			string.Join(' ', new[] { FileName }.Concat(Arguments).Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
	}

	internal static class TerminalLauncher
	{
		public static ReplWindowCommand CreateCommand(string pipeName, string token)
		{
			var assemblyPath = typeof(TerminalLauncher).Assembly.Location;

			return new ReplWindowCommand(
				ResolveDotnetHost(),
				new[]
				{
					"exec",
					"--runtimeconfig", WriteRuntimeConfig(),
					assemblyPath,
					"--pipe", pipeName,
					"--token", token,
				});
		}

		static string ResolveDotnetHost()
		{
			var exeName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

			if (Environment.ProcessPath is { } processPath && string.Equals(Path.GetFileName(processPath), exeName, StringComparison.OrdinalIgnoreCase))
			{
				return processPath;
			}

			if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } hostPath && File.Exists(hostPath))
			{
				return hostPath;
			}

			// <dotnet root>/shared/Microsoft.NETCore.App/<version>/
			var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var root = Path.GetFullPath(Path.Combine(runtimeDirectory, "..", "..", ".."));
			var fromRuntime = Path.Combine(root, exeName);

			return File.Exists(fromRuntime) ? fromRuntime : exeName;
		}

		// The window has to run on the same runtime as the test. Pinning to the exact version also works for preview runtimes.
		static string WriteRuntimeConfig()
		{
			var version = typeof(object).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Environment.Version.ToString(3);
			var plus = version.IndexOf('+');
			if (plus >= 0)
			{
				version = version[..plus];
			}

			var path = Path.Combine(GetWorkDirectory(), $"repl-{version}.runtimeconfig.json");
			if (!File.Exists(path))
			{
				// Written to a temporary file first, so a test process that starts at the same time never sees half of it.
				var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

				File.WriteAllText(temporaryPath,
					$$"""
					{
					  "runtimeOptions": {
					    "tfm": "net{{Environment.Version.Major}}.{{Environment.Version.Minor}}",
					    "framework": { "name": "Microsoft.NETCore.App", "version": "{{version}}" },
					    "rollForward": "LatestPatch"
					  }
					}
					""");

				File.Move(temporaryPath, path, overwrite: true);
			}

			return path;
		}

		// Scripts and configuration the window is started with live in a folder only the current user can use,
		// so another user of the machine can't replace them with something else.
		static string GetWorkDirectory()
		{
			if (OperatingSystem.IsWindows())
			{
				var windowsDirectory = Path.Combine(Path.GetTempPath(), "uitest-repl");
				Directory.CreateDirectory(windowsDirectory);
				return windowsDirectory;
			}

			var root = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } runtimeDirectory && Directory.Exists(runtimeDirectory)
				? runtimeDirectory
				: Path.GetTempPath();

			const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
			var directory = Path.Combine(root, "uitest-repl-" + Environment.UserName);
			Directory.CreateDirectory(directory, OwnerOnly);

			if (File.GetUnixFileMode(directory) != OwnerOnly)
			{
				throw new InvalidOperationException($"'{directory}' exists but is accessible to other users, so it can't be trusted to start the REPL window from.");
			}

			return directory;
		}

		/// <summary>
		/// Opens a terminal window that runs <paramref name="command"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">No terminal could be started.</exception>
		public static void Launch(ReplWindowCommand command, ReplOptions options)
		{
			if (OperatingSystem.IsWindows())
			{
				LaunchWindows(command);
			}
			else if (OperatingSystem.IsMacOS())
			{
				LaunchMac(command, options);
			}
			else
			{
				LaunchLinux(command);
			}
		}

		static void LaunchWindows(ReplWindowCommand command)
		{
			// A console application started through the shell gets its own console window.
			var startInfo = new ProcessStartInfo(command.FileName)
			{
				UseShellExecute = true,
				Arguments = string.Join(' ', command.Arguments.Select(QuoteForWindows)),
				WorkingDirectory = Environment.CurrentDirectory,
			};

			Process.Start(startInfo);
		}

		static string QuoteForWindows(string argument) =>
			argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0 ? argument : "\"" + argument.Replace("\"", "\\\"") + "\"";

		static void LaunchMac(ReplWindowCommand command, ReplOptions options)
		{
			// 'open' on a .command file runs it in a new terminal window, without needing the Automation permission AppleScript does.
			var script = WriteScript(command, ".command");

			var application = options.TerminalApplication ?? Environment.GetEnvironmentVariable("TERM_PROGRAM") switch
			{
				"iTerm.app" => "iTerm",
				"ghostty" => "Ghostty",
				"WezTerm" => "WezTerm",
				"WarpTerminal" => "Warp",
				_ => "Terminal",
			};

			var startInfo = new ProcessStartInfo("/usr/bin/open") { RedirectStandardError = true, RedirectStandardOutput = true };
			startInfo.ArgumentList.Add("-a");
			startInfo.ArgumentList.Add(application);
			startInfo.ArgumentList.Add(script);

			using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start 'open'.");
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new InvalidOperationException($"Could not open {application}: {process.StandardError.ReadToEnd().Trim()}");
			}
		}

		static void LaunchLinux(ReplWindowCommand command)
		{
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
			{
				throw new InvalidOperationException("There is no display to open a terminal window on (DISPLAY and WAYLAND_DISPLAY are not set).");
			}

			var script = WriteScript(command, ".sh");

			var candidates = new List<(string Terminal, string[] Prefix)>();
			if (Environment.GetEnvironmentVariable("TERMINAL") is { Length: > 0 } terminal)
			{
				candidates.Add((terminal, new[] { "-e" }));
			}

			candidates.AddRange(new (string, string[])[]
			{
				("x-terminal-emulator", new[] { "-e" }),
				("gnome-terminal", new[] { "--" }),
				("konsole", new[] { "-e" }),
				("xfce4-terminal", new[] { "-x" }),
				("kitty", Array.Empty<string>()),
				("alacritty", new[] { "-e" }),
				("xterm", new[] { "-e" }),
			});

			foreach (var (name, prefix) in candidates)
			{
				try
				{
					var startInfo = new ProcessStartInfo(name) { UseShellExecute = false };
					foreach (var argument in prefix)
					{
						startInfo.ArgumentList.Add(argument);
					}

					startInfo.ArgumentList.Add("bash");
					startInfo.ArgumentList.Add(script);

					using var process = Process.Start(startInfo);

					// A terminal that can't open a window (no display, wrong options) exits right away with an error.
					if (process is not null && process.WaitForExit(1500) && process.ExitCode != 0)
					{
						continue;
					}

					return;
				}
				catch (System.ComponentModel.Win32Exception)
				{
					// Not installed, try the next one.
				}
			}

			throw new InvalidOperationException("Could not find a terminal to open (tried $TERMINAL, x-terminal-emulator, gnome-terminal, konsole, xfce4-terminal, kitty, alacritty and xterm).");
		}

		static string WriteScript(ReplWindowCommand command, string extension)
		{
			var path = Path.Combine(GetWorkDirectory(), $"repl-{Guid.NewGuid():N}{extension}");

			var script = new StringBuilder();
			script.AppendLine("#!/bin/bash");
			script.AppendLine($"cd {ShellQuote(Environment.CurrentDirectory)}");
			script.AppendLine("clear");
			script.AppendLine(string.Join(' ', new[] { command.FileName }.Concat(command.Arguments).Select(ShellQuote)));
			script.AppendLine("status=$?");
			script.AppendLine("rm -f \"$0\"");
			script.AppendLine("exit $status");

			File.WriteAllText(path, script.ToString());
			if (!OperatingSystem.IsWindows())
			{
				File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
			}

			return path;
		}

		static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";
	}
}
