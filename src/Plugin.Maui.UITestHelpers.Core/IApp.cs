namespace Plugin.Maui.UITestHelpers.Core
{
	public interface IApp : IDisposable
	{
		IConfig Config { get; }
		IUIElementQueryable Query { get; }
		ApplicationState AppState { get; }

		IUIElement FindElement(string id);
		IUIElement FindElementByText(string text);
        IUIElement FindElement(IQuery query);
		IReadOnlyCollection<IUIElement> FindElements(string id);
		IReadOnlyCollection<IUIElement> FindElements(IQuery query);
		IReadOnlyCollection<IUIElement> FindElementsByText(string text);
		string ElementTree { get; }

		ICommandExecution CommandExecutor { get; }
	}

	public interface IScreenshotSupportedApp : IApp
	{
		FileInfo Screenshot(string fileName);
		byte[] Screenshot();
	}

	public interface ILogsSupportedApp : IApp
	{
		IEnumerable<string> GetLogTypes();
		IEnumerable<string> GetLogEntries(string logType);
	}

	/// <summary>
	/// Interface for apps that support backdoor method invocation.
	/// Backdoors allow tests to invoke app methods directly without going through the UI.
	/// </summary>
	public interface IBackdoorSupportedApp : IApp
	{
		/// <summary>
		/// Invokes a backdoor method in the app with the specified method name and arguments.
		/// </summary>
		/// <param name="methodName">The name of the method to invoke</param>
		/// <param name="args">Arguments to pass to the method</param>
		/// <returns>The result of the method invocation, or null if no result</returns>
		object? Invoke(string methodName, params object[] args);
		
		/// <summary>
		/// Invokes a backdoor method in the app with the specified method name and arguments,
		/// returning a strongly typed result.
		/// </summary>
		/// <typeparam name="T">The expected return type</typeparam>
		/// <param name="methodName">The name of the method to invoke</param>
		/// <param name="args">Arguments to pass to the method</param>
		/// <returns>The result of the method invocation, or default(T) if no result</returns>
		T? Invoke<T>(string methodName, params object[] args);
	}

	public static class AppExtensions
	{
		public static void Click(this IApp app, float x, float y)
		{
			app.CommandExecutor.Execute("click", new Dictionary<string, object>()
			{
				{ "x", x },
				{ "y", y }
			});
		}


		internal static T As<T>(this IApp app)
			where T : IApp
		{
			if (app is not T derivedApp)
				throw new NotImplementedException($"The app '{app}' does not implement '{typeof(T).FullName}'.");

			return derivedApp;
		}
	}

	public static class ScreenshotSupportedAppExtensions
	{
		public static FileInfo Screenshot(this IApp app, string fileName) =>
			app.As<IScreenshotSupportedApp>().Screenshot(fileName);

		public static byte[] Screenshot(this IApp app) =>
			app.As<IScreenshotSupportedApp>().Screenshot();
	}

	public static class LogsSupportedAppExtensions
	{
		public static IEnumerable<string> GetLogTypes(this IApp app) =>
			app.As<ILogsSupportedApp>().GetLogTypes();

		public static IEnumerable<string> GetLogEntries(this IApp app, string logType) =>
			app.As<ILogsSupportedApp>().GetLogEntries(logType);
	}

	public static class BackdoorSupportedAppExtensions
	{
		/// <summary>
		/// Invokes a backdoor method in the app with the specified method name and arguments.
		/// </summary>
		/// <param name="app">The app instance</param>
		/// <param name="methodName">The name of the method to invoke</param>
		/// <param name="args">Arguments to pass to the method</param>
		/// <returns>The result of the method invocation, or null if no result</returns>
		public static object? Invoke(this IApp app, string methodName, params object[] args) =>
			app.As<IBackdoorSupportedApp>().Invoke(methodName, args);

		/// <summary>
		/// Invokes a backdoor method in the app with the specified method name and arguments,
		/// returning a strongly typed result.
		/// </summary>
		/// <typeparam name="T">The expected return type</typeparam>
		/// <param name="app">The app instance</param>
		/// <param name="methodName">The name of the method to invoke</param>
		/// <param name="args">Arguments to pass to the method</param>
		/// <returns>The result of the method invocation, or default(T) if no result</returns>
		public static T? Invoke<T>(this IApp app, string methodName, params object[] args) =>
			app.As<IBackdoorSupportedApp>().Invoke<T>(methodName, args);
	}
}
