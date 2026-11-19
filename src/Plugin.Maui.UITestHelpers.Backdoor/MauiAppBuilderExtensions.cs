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
	/// methods with <c>app.Invoke(...)</c>. Unless you set <see cref="BackdoorOptions.AccessKey"/>,
	/// any app on the device can invoke the methods, so only call this in builds you use for UI
	/// testing, for example inside <c>#if DEBUG</c> or a custom compilation symbol, and never in
	/// a build you distribute.
	/// </summary>
	/// <param name="builder">The app builder.</param>
	/// <param name="configure">Registers the backdoor methods and other options.</param>
	/// <returns>The app builder, to chain calls.</returns>
	public static MauiAppBuilder UseUITestBackdoor(this MauiAppBuilder builder, Action<BackdoorOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// A problem with the backdoor must never keep the app from starting: the error is logged
		// when the app starts, the backdoor stays off and the UI tests that use it fail
		var options = new BackdoorOptions();
		Exception? configurationError = null;
		try
		{
			configure(options);
		}
		catch (Exception ex)
		{
			configurationError = ex;
		}

		builder.Services.AddSingleton(new BackdoorStartup(options, configurationError));
		builder.Services.AddSingleton<IMauiInitializeService, BackdoorInitializeService>();

		return builder;
	}
}

sealed record BackdoorStartup(BackdoorOptions Options, Exception? ConfigurationError);

sealed class BackdoorInitializeService : IMauiInitializeService
{
	public void Initialize(IServiceProvider services)
	{
		ILogger? logger = null;
		try
		{
			logger = services.GetService<ILoggerFactory>()?.CreateLogger("Plugin.Maui.UITestHelpers.Backdoor");
			var startup = services.GetRequiredService<BackdoorStartup>();
			if (startup.ConfigurationError is not null)
			{
				Log(logger, startup.ConfigurationError, $"UI test backdoor is off because UseUITestBackdoor failed: {startup.ConfigurationError.Message}");
				return;
			}

			Start(startup.Options, logger);
		}
		catch (Exception ex)
		{
			Log(logger, ex, $"UI test backdoor is off because it failed to start: {ex.Message}");
		}
	}

	static void Log(ILogger? logger, Exception exception, string message)
	{
		if (logger is not null)
			logger.LogError(exception, "{Message}", message);
		else
			System.Diagnostics.Debug.WriteLine(message);
	}

	static void Start(BackdoorOptions options, ILogger? logger)
	{
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
			if (OperatingSystem.IsMacCatalyst())
				message += " Sandboxed Mac Catalyst apps need the com.apple.security.network.server entitlement.";
			Log(logger, ex, message);
		}
	}

	// Keeps the server alive for the lifetime of the app
	internal static BackdoorServer? Server { get; private set; }
}
