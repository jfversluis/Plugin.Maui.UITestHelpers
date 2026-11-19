#if UITEST_BACKDOOR
using Plugin.Maui.UITestHelpers.Backdoor;
#endif

namespace Plugin.Maui.UITestHelpers.Sample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if UITEST_BACKDOOR
		// The backdoor lets UI tests call methods in this app directly with app.Invoke(...).
		// It's only compiled in when the UITEST_BACKDOOR symbol is defined, see the .csproj.
		builder.Services.AddSingleton<TestDataBackdoor>();
		builder.UseUITestBackdoor(backdoor =>
		{
			// All methods of App marked with [BackdoorMethod]
			backdoor.AddMethods<App>();

			// Services are resolved from the app's dependency injection container
			backdoor.AddMethods<TestDataBackdoor>();

			// Delegates work as well
			backdoor.AddMethod("GetPlatform", () => DeviceInfo.Current.Platform.ToString());

			// LESS SECURE, opt-in: only when built with -p:BackdoorAccessKey=<key> (see the .csproj),
			// the backdoor accepts connections from the network, for example for physical iOS devices
			var accessKey = typeof(MauiProgram).Assembly
				.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
				.OfType<System.Reflection.AssemblyMetadataAttribute>()
				.FirstOrDefault(a => a.Key == "BackdoorAccessKey")?.Value;
			if (!string.IsNullOrEmpty(accessKey))
				backdoor.EnableNetworkAccess(accessKey);
		});
#endif

		// Webviews must be debuggable/inspectable for Appium to see them as a WEBVIEW context
#if ANDROID
		Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true);
#elif IOS || MACCATALYST
		Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("Inspectable", (handler, view) =>
		{
			if (OperatingSystem.IsIOSVersionAtLeast(16, 4) || OperatingSystem.IsMacCatalystVersionAtLeast(16, 4))
				handler.PlatformView.Inspectable = true;
		});
#endif

		return builder.Build();
	}
}
