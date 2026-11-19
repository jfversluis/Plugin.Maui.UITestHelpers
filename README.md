![](https://raw.githubusercontent.com/jfversluis/Plugin.Maui.UITestHelpers/main/nuget.png)
# Plugin.Maui.UITestHelpers

`Plugin.Maui.UITestHelpers` provides a set of helpers to support UI testing your .NET MAUI app with Appium and migration from Xamarin.UITest to Appium. It consists of three packages, see below for more information.

The set of helpers are extracted from the .NET MAUI codebase ([here](https://github.com/dotnet/maui/tree/main/src/TestUtils/src), the folders prefixed UITest) this code can help with writing UI tests with Appium and/or transition your existing Xamarin.UITest tests to Appium.

> [!WARNING]  
> At this time this project is not officially supported and experimental.
> Feel free to let us know your feedback and provide pull requests to make it even better.
> There is no guarantee this will become something more official.

<sub>But if you like what you see and help make it better... It might! </sub>

## Install Plugin

This project consists of three packages. please find the details below. All packages are available on NuGet.

| Package Name | Description | NuGet Package |
|----------|------------|---------------|
| Plugin.Maui.UITestHelpers.Core   | Shared, core types that are used across the different projects | [![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.Core)](https://www.nuget.org/packages/Plugin.Maui.UITestHelpers.Core/1.0.0-preview1) |
| Plugin.Maui.UITestHelpers.Appium | This package contains code specific to UI testing with Appium. It's filled with helpers that will make writing UI tests easier, as well as methods that (mostly) mimic Xamarin.UITest to make transitioning your current UI tests easier | [![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.Appium)](https://www.nuget.org/packages/Plugin.Maui.UITestHelpers.Appium/1.0.0-preview1) |
| Plugin.Maui.UITestHelpers.NUnit | This package contains helpers that will make it easier to write UI tests based on NUnit | [![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.NUnit)](https://www.nuget.org/packages/Plugin.Maui.UITestHelpers.NUnit/1.0.0-preview1) |

Install with the dotnet CLI, for example: `dotnet add package Plugin.Maui.UITestHelpers.Appium`, or through the NuGet Package Manager in Visual Studio.

### Supported Platforms

All platforms that are supported by the cross section of the support of Appium and .NET MAUI.

<!--## API Usage

TBD -->

On Android, app data is preserved by default when `AppId` is set. Set `FullReset` to `true`
and provide an `AppPath` to reinstall the app when the Appium session starts. Do not enable
`NoReset` and `FullReset` together.

`FullReset` applies when an Appium session starts, not before each test. `UITestBase` reuses one
session across tests and only starts a new one when `Reset()` is called, for example after a crash.
To start every test from a freshly launched app, add a `[SetUp]` method to your test class that
derives from `UITestBase`:

```csharp
using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Appium;

[SetUp]
public void RestartApp() => App.ResetApp();
```

`ResetApp()` closes and relaunches the app, but keeps its data. Leave the app running at the end
of a test: if the app is closed when the test finishes, `UITestBase` treats it as a crash and fails
the test.

## Passing other Appium capabilities

To set an Appium capability that doesn't have its own config property, add it to the
`AdditionalCapabilities` dictionary in your test config:

```csharp
config.SetProperty("AdditionalCapabilities", new Dictionary<string, object>
{
    ["systemPort"] = 8201,
    ["disableWindowAnimation"] = true,
});
```

Names without a vendor prefix get the `appium:` prefix. These capabilities are applied last, so
they also override values the library sets itself, such as `newCommandTimeout`. Standard W3C
capabilities (like `platformName`) and capabilities that have their own config property (`app`,
`deviceName` and `platformVersion`; use `AppPath`, `DeviceName` and `PlatformVersion`) can't be set
this way. See the driver documentation for the available capabilities, for example the
[UiAutomator2 capabilities](https://github.com/appium/appium-uiautomator2-driver#capabilities).

## Testing webview content

On Android and iOS you can interact with the content of a `WebView` (or a Blazor Hybrid
`BlazorWebView`) by switching to its webview context:

```csharp
App.SwitchContextToWebview();
App.TapWebviewButtonByText("Sign in");
App.ScrollWebview(down: true);
App.SwitchContextToNative();
```

For Appium to find the webview, your app has to make it debuggable: on Android call
`Android.Webkit.WebView.SetWebContentsDebuggingEnabled(true)`, on iOS 16.4 and later set
`Inspectable = true` on the `WKWebView` (see the sample's `MauiProgram.cs`). Only do this in
builds you test, not in production builds. On Android, Appium also needs a Chromedriver that
matches the WebView version on the device, for example by starting Appium with
`--allow-insecure uiautomator2:chromedriver_autodownload`.

# Related resources

To make it easier to add UI test projects to your existing solution, I also have a template that you can use: https://github.com/jfversluis/Template.Maui.UITesting/

Additionally, please find some blog posts and samples that we have made available to get you started:

* https://devblogs.microsoft.com/dotnet/dotnet-maui-ui-testing-appium/
* https://devblogs.microsoft.com/dotnet/browserstack-appium-dotnet-maui/
* https://learn.microsoft.com/samples/dotnet/maui-samples/uitest-appium-nunit/ 
* https://learn.microsoft.com/samples/dotnet/maui-samples/uitest-browserstack/ 

# Acknowledgements

This project could not have came to be without these projects and people, thank you! <3

The original source was extracted from the [.NET MAUI codebase](https://github.com/dotnet/maui) so all credit goes out to that team, namely @sbanni, @mattleibow, @PureWeen, @MartyIX, @jsuarezruiz, @jfversluis and @rmarinho.
