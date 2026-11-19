namespace Plugin.Maui.UITestHelpers.Repl
{
	/// <summary>
	/// Where the REPL user interface is shown.
	/// </summary>
	public enum ReplMode
	{
		/// <summary>
		/// Runs inline when the test process has a real console, otherwise opens a new terminal window.
		/// </summary>
		Auto,

		/// <summary>
		/// Runs the REPL in the console of the test process. Only works when the console is not redirected.
		/// </summary>
		Inline,

		/// <summary>
		/// Opens a new terminal window that runs the REPL and talks to the test process.
		/// </summary>
		Window,

		/// <summary>
		/// Doesn't open a window. Prints the command to run in a terminal of your choice (for example over SSH)
		/// and waits for it to connect.
		/// </summary>
		Manual,
	}

	/// <summary>
	/// Options for <see cref="ReplExtensions.Repl(Core.IApp, ReplOptions?)"/>.
	/// </summary>
	public sealed class ReplOptions
	{
		/// <summary>
		/// Environment variable that overrides <see cref="Mode"/>: <c>inline</c>, <c>window</c>, <c>manual</c>, <c>auto</c>
		/// or <c>off</c> (skips the REPL altogether).
		/// </summary>
		public const string ModeEnvironmentVariable = "UITEST_REPL_MODE";

		/// <summary>
		/// Gets or sets where the REPL is shown. Defaults to <see cref="ReplMode.Auto"/>.
		/// </summary>
		public ReplMode Mode { get; set; } = ReplMode.Auto;

		/// <summary>
		/// Gets or sets whether the REPL starts on a continuous integration server. By default <c>Repl()</c> returns
		/// immediately there, so a forgotten call can't hang a build.
		/// </summary>
		public bool RunInContinuousIntegration { get; set; }

		/// <summary>
		/// Gets or sets how long to wait for the REPL window to connect. Defaults to 60 seconds.
		/// </summary>
		public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(60);

		/// <summary>
		/// Gets the extra namespaces that are imported in the REPL, in addition to the default ones.
		/// </summary>
		public IList<string> Imports { get; } = new List<string>();

		/// <summary>
		/// Gets or sets the terminal application used to open the REPL window on macOS (for example <c>iTerm</c>).
		/// Defaults to the terminal the test run was started from, or Terminal.
		/// </summary>
		public string? TerminalApplication { get; set; }

		internal ReplMode ResolveMode(out bool disabled)
		{
			disabled = false;
			var mode = Mode;

			var fromEnvironment = Environment.GetEnvironmentVariable(ModeEnvironmentVariable);
			if (!string.IsNullOrWhiteSpace(fromEnvironment))
			{
				switch (fromEnvironment.Trim().ToLowerInvariant())
				{
					case "off":
					case "skip":
					case "false":
					case "0":
						disabled = true;
						break;
					case "inline":
						mode = ReplMode.Inline;
						break;
					case "window":
						mode = ReplMode.Window;
						break;
					case "manual":
						mode = ReplMode.Manual;
						break;
					case "auto":
						mode = ReplMode.Auto;
						break;
				}
			}

			if (mode == ReplMode.Auto)
			{
				mode = Console.IsInputRedirected || Console.IsOutputRedirected ? ReplMode.Window : ReplMode.Inline;
			}

			return mode;
		}
	}
}
