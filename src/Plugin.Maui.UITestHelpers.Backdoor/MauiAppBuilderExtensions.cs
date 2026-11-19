using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.UITestHelpers.Backdoor;

/// <summary>
/// Extension methods to enable the UI test backdoor in a .NET MAUI app.
/// </summary>
public static class MauiAppBuilderExtensions
{
	/// <summary>
	/// Starts the UI test backdoor when the app starts, so UI tests can call the configured
	/// methods with <c>app.Invoke(...)</c>. The backdoor lets anything on the device invoke
	/// the methods, so only enable it in builds you use for UI testing, for example inside
	/// <c>#if DEBUG</c>.
	/// </summary>
	/// <param name="builder">The app builder.</param>
	/// <param name="configure">Registers the backdoor methods and other options.</param>
	/// <returns>The app builder, to chain calls.</returns>
	public static MauiAppBuilder UseUITestBackdoor(this MauiAppBuilder builder, Action<BackdoorOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new BackdoorOptions();
		configure(options);

		builder.Services.AddSingleton(options);
		builder.Services.AddSingleton<IMauiInitializeService, BackdoorInitializeService>();

		return builder;
	}
}

sealed class BackdoorInitializeService : IMauiInitializeService
{
	public void Initialize(IServiceProvider services)
	{
		var options = services.GetRequiredService<BackdoorOptions>();
		var logger = services.GetService<ILoggerFactory>()?.CreateLogger("Plugin.Maui.UITestHelpers.Backdoor");

		// Initialize runs on the UI thread, so this is the dispatcher that backdoor methods run on
		var dispatcher = Dispatcher.GetForCurrentThread();

		Task<InvocationResult> Dispatch(Func<Task<InvocationResult>> invoke) =>
			dispatcher is not null
				? dispatcher.DispatchAsync(invoke)
				: MainThread.InvokeOnMainThreadAsync(invoke);

		var server = new BackdoorServer(options, Dispatch, logger);
		try
		{
			Server?.Dispose();
			server.Start();
			Server = server;
		}
		catch (Exception ex)
		{
			server.Dispose();
			var message = $"UI test backdoor could not listen on port {options.Port}: {ex.Message}";
			if (logger is not null)
				logger.LogError(ex, "{Message}", message);
			else
				System.Diagnostics.Debug.WriteLine(message);
		}
	}

	// Keeps the server alive for the lifetime of the app
	internal static BackdoorServer? Server { get; private set; }
}
