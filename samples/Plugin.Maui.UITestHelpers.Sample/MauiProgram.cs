using Plugin.Maui.UITestHelpers.Backdoor;

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

		// The backdoor lets UI tests call methods in this app directly with app.Invoke(...).
		// This sample always enables it because the UI tests also run against Release builds;
		// in your own app only enable it in builds you use for UI testing, for example with #if DEBUG.
		builder.UseUITestBackdoor(backdoor =>
		{
			// All methods of App marked with [BackdoorMethod]
			backdoor.AddMethods<App>();

			// Delegates work as well
			backdoor.AddMethod("GetPlatform", () => DeviceInfo.Current.Platform.ToString());
		});

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
