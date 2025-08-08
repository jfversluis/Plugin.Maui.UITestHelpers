using System.Diagnostics;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	/// <summary>
	/// Windows-specific implementation for theme change actions using Appium.
	/// </summary>
	public class AppiumWindowsThemeChangeAction : ICommandExecutionGroup
	{
		const string SetLightTheme = "setLightTheme";
		const string SetDarkTheme = "setDarkTheme";

		readonly List<string> _commands = new()
		{
			SetLightTheme,
			SetDarkTheme
		};

		/// <summary>
		/// Executes the specified theme change command.
		/// </summary>
		/// <param name="commandName">The name of the command to execute.</param>
		/// <param name="parameters">The parameters for the command.</param>
		/// <returns>A <see cref="CommandResponse"/> indicating the result of the command execution.</returns>
		public CommandResponse Execute(string commandName, IDictionary<string, object> parameters)
		{
			if (commandName == SetLightTheme)
			{
				ExecuteCommand($"start C://Windows/Resources/Themes/aero.theme");
				return CommandResponse.SuccessEmptyResponse;
			}
			else if (commandName == SetDarkTheme)
			{
				ExecuteCommand($"start C://Windows/Resources/Themes/dark.theme");
				return CommandResponse.SuccessEmptyResponse;
			}

			return CommandResponse.FailedEmptyResponse;
		}

		/// <summary>
		/// Determines whether the specified command is supported by this action group.
		/// </summary>
		/// <param name="commandName">The name of the command to check.</param>
		/// <returns><see langword="true"/> if the command is supported; otherwise, <see langword="false"/>.</returns>
		public bool IsCommandSupported(string commandName)
		{
			return _commands.Contains(commandName, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Executes a shell command using cmd.exe.
		/// </summary>
		/// <param name="command">The command to execute.</param>
		private static void ExecuteCommand(string command)
		{
			var shell = GetShell();
			var shellArgument = GetShellArgument(shell, command);

			var processInfo = new ProcessStartInfo(shell, shellArgument)
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			var process = new Process { StartInfo = processInfo };

			process.Start();
			process.WaitForExit();
		}

		/// <summary>
		/// Gets the shell executable to use.
		/// </summary>
		/// <returns>The path to cmd.exe.</returns>
		private static string GetShell()
		{
			return "cmd.exe";
		}

		/// <summary>
		/// Gets the shell argument for executing the command.
		/// </summary>
		/// <param name="shell">The shell to use.</param>
		/// <param name="command">The command to execute.</param>
		/// <returns>The formatted shell argument.</returns>
		private static string GetShellArgument(string shell, string command)
		{
			return $"/C {command}";
		}
	}
}