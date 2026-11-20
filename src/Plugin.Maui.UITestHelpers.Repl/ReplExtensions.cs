using Plugin.Maui.UITestHelpers.Repl;
using Plugin.Maui.UITestHelpers.Repl.Engine;
using Plugin.Maui.UITestHelpers.Repl.Hosting;

namespace Plugin.Maui.UITestHelpers.Core
{
	public static class ReplExtensions
	{
		static readonly string[] ContinuousIntegrationVariables =
		{
			"CI", "TF_BUILD", "GITHUB_ACTIONS", "JENKINS_URL", "TEAMCITY_VERSION", "APPVEYOR", "CIRCLECI", "GITLAB_CI", "BITRISE_IO", "BUILDKITE",
		};

		/// <summary>
		/// Starts an interactive REPL (read-eval-print loop) for the app, and pauses the test until it is closed with
		/// <c>exit</c>. Everything you can do with the app in a test, you can try out here first: any C# works, with
		/// <c>app</c> being the running app. Type <c>help</c> in the REPL to see the built-in commands, like <c>tree</c>.
		/// <para>
		/// While debugging, call it from code rather than from the Immediate Window: the debugger freezes the other
		/// threads during an evaluation, and the REPL needs them. In Visual Studio, <c>App.Repl(), run_all_threads</c> lets them run.
		/// </para>
		/// </summary>
		/// <param name="app">The app to explore.</param>
		/// <param name="options">Optional settings, for example where the REPL window is shown.</param>
		public static void Repl(this IApp app, ReplOptions? options = null)
		{
			ArgumentNullException.ThrowIfNull(app);
			options ??= new ReplOptions();

			var mode = options.ResolveMode(out bool disabled);
			if (disabled)
			{
				ReplHost.Notify($"[REPL] Skipped, because {ReplOptions.ModeEnvironmentVariable} is set to off.");
				return;
			}

			if (!options.RunInContinuousIntegration && IsContinuousIntegration())
			{
				ReplHost.Notify("[REPL] Skipped, because this looks like a continuous integration run. Set ReplOptions.RunInContinuousIntegration to start it anyway.");
				return;
			}

			var session = new ReplSession(app, options);

			if (mode == ReplMode.Inline)
			{
				if (Console.IsInputRedirected || Console.IsOutputRedirected)
				{
					throw new InvalidOperationException(
						"The REPL can't run inline because the console of this process is redirected, as it is under 'dotnet test' or in an IDE. " +
						"Use ReplMode.Window (or Auto), which opens a separate terminal window.");
				}

				ReplHost.RunInline(session);
			}
			else
			{
				ReplHost.RunRemote(session, options, mode);
			}
		}

		static bool IsContinuousIntegration() =>
			ContinuousIntegrationVariables.Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)) &&
				!string.Equals(Environment.GetEnvironmentVariable(name), "false", StringComparison.OrdinalIgnoreCase));
	}
}
