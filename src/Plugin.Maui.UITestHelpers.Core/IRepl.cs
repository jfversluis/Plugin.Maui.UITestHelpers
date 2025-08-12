namespace Plugin.Maui.UITestHelpers.Core
{
	/// <summary>
	/// Interface for a Read-Eval-Print Loop (REPL) that allows interactive UI inspection and testing.
	/// </summary>
	public interface IRepl
	{
		/// <summary>
		/// Start the interactive REPL session.
		/// </summary>
		void Start();

		/// <summary>
		/// Execute a single command and return the result.
		/// </summary>
		/// <param name="command">The command to execute</param>
		/// <returns>The result of the command execution</returns>
		string ExecuteCommand(string command);

		/// <summary>
		/// Stop the REPL session.
		/// </summary>
		void Stop();

		/// <summary>
		/// Get help text for available commands.
		/// </summary>
		/// <returns>Help text describing available commands</returns>
		string GetHelp();
	}

	/// <summary>
	/// Interface for apps that support REPL functionality.
	/// </summary>
	public interface IReplSupportedApp : IApp
	{
		/// <summary>
		/// Get the REPL interface for this app.
		/// </summary>
		IRepl Repl { get; }
	}

	/// <summary>
	/// Extension methods for REPL-supported apps.
	/// </summary>
	public static class ReplSupportedAppExtensions
	{
		/// <summary>
		/// Start a REPL session for the app.
		/// </summary>
		/// <param name="app">The app to start REPL for</param>
		public static void StartRepl(this IApp app) =>
			app.As<IReplSupportedApp>().Repl.Start();

		/// <summary>
		/// Execute a REPL command on the app.
		/// </summary>
		/// <param name="app">The app to execute command on</param>
		/// <param name="command">The command to execute</param>
		/// <returns>The result of the command execution</returns>
		public static string ExecuteReplCommand(this IApp app, string command) =>
			app.As<IReplSupportedApp>().Repl.ExecuteCommand(command);

		/// <summary>
		/// Get REPL help for the app.
		/// </summary>
		/// <param name="app">The app to get help for</param>
		/// <returns>Help text describing available commands</returns>
		public static string GetReplHelp(this IApp app) =>
			app.As<IReplSupportedApp>().Repl.GetHelp();
	}
}