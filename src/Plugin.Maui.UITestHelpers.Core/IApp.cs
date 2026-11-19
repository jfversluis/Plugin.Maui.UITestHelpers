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
	/// An app that supports backdoors: methods in the app that UI tests can invoke directly,
	/// like Xamarin.UITest's <c>app.Invoke</c>. The app has to expose the methods with the
	/// Plugin.Maui.UITestHelpers.Backdoor package.
	/// </summary>
	public interface IBackdoorSupportedApp : IApp
	{
		/// <summary>
		/// Invokes a backdoor method in the app.
		/// </summary>
		/// <param name="methodName">Name of the backdoor method.</param>
		/// <param name="argument">Argument to pass, or <see langword="null"/> for no argument.</param>
		/// <returns>The return value: a <see cref="string"/>, <see cref="bool"/>, <see cref="long"/>,
		/// <see cref="double"/>, <see langword="null"/>, or a <see cref="System.Text.Json.JsonElement"/>
		/// for objects and lists.</returns>
		/// <exception cref="BackdoorException">The method could not be invoked or threw an exception.</exception>
		object? Invoke(string methodName, object? argument = null);

		/// <summary>
		/// Invokes a backdoor method in the app with multiple arguments.
		/// </summary>
		/// <remarks>
		/// Like in Xamarin.UITest, <paramref name="arguments"/> is the list of arguments. Because
		/// of array covariance, an array of a reference type such as <c>string[]</c> is also used
		/// as the list of arguments. To pass an array as a single argument, wrap it:
		/// <c>app.Invoke("Method", new object[] { myArray })</c>.
		/// </remarks>
		/// <param name="methodName">Name of the backdoor method.</param>
		/// <param name="arguments">Arguments to pass.</param>
		/// <returns>The return value, see <see cref="Invoke(string, object?)"/>.</returns>
		/// <exception cref="BackdoorException">The method could not be invoked or threw an exception.</exception>
		object? Invoke(string methodName, object?[] arguments);

		/// <summary>
		/// Invokes a backdoor method in the app and converts its return value to <typeparamref name="T"/>.
		/// </summary>
		/// <remarks>
		/// To pass an array as a single argument, wrap it: <c>app.Invoke&lt;T&gt;("Method", new object[] { myArray })</c>.
		/// </remarks>
		/// <typeparam name="T">Type of the return value.</typeparam>
		/// <param name="methodName">Name of the backdoor method.</param>
		/// <param name="arguments">Arguments to pass.</param>
		/// <returns>The return value.</returns>
		/// <exception cref="BackdoorException">The method could not be invoked or threw an exception.</exception>
		T? Invoke<T>(string methodName, params object?[] arguments);
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
		/// <inheritdoc cref="IBackdoorSupportedApp.Invoke(string, object?)"/>
		public static object? Invoke(this IApp app, string methodName, object? argument = null) =>
			app.As<IBackdoorSupportedApp>().Invoke(methodName, argument);

		/// <inheritdoc cref="IBackdoorSupportedApp.Invoke(string, object?[])"/>
		public static object? Invoke(this IApp app, string methodName, object?[] arguments) =>
			app.As<IBackdoorSupportedApp>().Invoke(methodName, arguments);

		/// <inheritdoc cref="IBackdoorSupportedApp.Invoke{T}(string, object?[])"/>
		public static T? Invoke<T>(this IApp app, string methodName, params object?[] arguments) =>
			app.As<IBackdoorSupportedApp>().Invoke<T>(methodName, arguments);
	}
}
