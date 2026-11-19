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
