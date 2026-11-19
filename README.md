![](https://raw.githubusercontent.com/jfversluis/Plugin.Maui.UITestHelpers/main/nuget.png)
# Plugin.Maui.UITestHelpers

`Plugin.Maui.UITestHelpers` provides a set of helpers to support UI testing your .NET MAUI app with Appium and migration from Xamarin.UITest to Appium. It consists of four packages, see below for more information.

The set of helpers are extracted from the .NET MAUI codebase ([here](https://github.com/dotnet/maui/tree/main/src/TestUtils/src), the folders prefixed UITest) this code can help with writing UI tests with Appium and/or transition your existing Xamarin.UITest tests to Appium.

> [!WARNING]  
> At this time this project is not officially supported and experimental.
> Feel free to let us know your feedback and provide pull requests to make it even better.
> There is no guarantee this will become something more official.

<sub>But if you like what you see and help make it better... It might! </sub>

## Install Plugin

This project consists of four packages. please find the details below. All packages are available on NuGet.

| Package Name | Description | NuGet Package |
|----------|------------|---------------|
| Plugin.Maui.UITestHelpers.Core   | Shared, core types that are used across the different projects | [![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.Core)](https://www.nuget.org/packages/Plugin.Maui.UITestHelpers.Core/1.0.0-preview1) |
| Plugin.Maui.UITestHelpers.Appium | This package contains code specific to UI testing with Appium. It's filled with helpers that will make writing UI tests easier, as well as methods that (mostly) mimic Xamarin.UITest to make transitioning your current UI tests easier | [![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.Appium)](https://www.nuget.org/packages/Plugin.Maui.UITestHelpers.Appium/1.0.0-preview1) |
| Plugin.Maui.UITestHelpers.NUnit | This package contains helpers that will make it easier to write UI tests based on NUnit | [![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.NUnit)](https://www.nuget.org/packages/Plugin.Maui.UITestHelpers.NUnit/1.0.0-preview1) |
| Plugin.Maui.UITestHelpers.Backdoor | Install this package in your .NET MAUI **app** (not the test project) to expose backdoor methods that UI tests can call with `app.Invoke(...)`, see [Backdoors](#backdoors) | [![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.Backdoor)](https://www.nuget.org/packages/Plugin.Maui.UITestHelpers.Backdoor) |

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
they also override values the library sets itself, such as `newCommandTimeout`. For capabilities
that have their own config property, such as `AppId` (`appPackage`/`bundleId`), use that property:
the library also relies on it after the session has started. Standard W3C capabilities (like
`platformName`), `automationName` (set by the library for each platform) and `app`, `deviceName`
and `platformVersion` (use `AppPath`, `DeviceName` and `PlatformVersion`) can't be set this way.
See the driver documentation for the available capabilities, for example the
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

## Backdoors

Backdoors let UI tests call methods in your app directly, without going through the UI. Use them
to set up test data, change app state, or read internal state to assert on. This works like
`app.Invoke` in Xamarin.UITest.

Appium has no way to call into a .NET MAUI app, so the app has to expose the methods itself with
the `Plugin.Maui.UITestHelpers.Backdoor` package. It runs a small HTTP server inside your app that
only accepts connections from the device itself, and rejects requests from web pages. The UI tests
connect to it through the Appium helpers.

### 1. Expose methods in your app

Add the `Plugin.Maui.UITestHelpers.Backdoor` package to your .NET MAUI app project and enable the
backdoor in `MauiProgram.cs`:

```csharp
using Plugin.Maui.UITestHelpers.Backdoor;

var builder = MauiApp.CreateBuilder();
builder.UseMauiApp<App>();

#if UITEST_BACKDOOR
builder.UseUITestBackdoor(backdoor =>
{
    // Methods marked with [BackdoorMethod] on App. For other types, the instance is
    // resolved from the app's services, or pass it: backdoor.AddMethods(myService)
    backdoor.AddMethods<App>();

    // Or register a delegate
    backdoor.AddMethod("GetPlatform", () => DeviceInfo.Current.Platform.ToString());
});
#endif
```

Then mark the methods, for example in `App.xaml.cs`:

```csharp
[BackdoorMethod]
public void SignIn(string userName) => /* set up the signed in state */;

[BackdoorMethod]
public async Task<int> GetCartItemCountAsync() => await _cartService.GetCountAsync();
```

> [!WARNING]
> Unless you set `backdoor.AccessKey`, any app on the device can call the backdoor methods while
> the backdoor is enabled. Never ship it in a build you distribute.

Make sure the backdoor can't end up in a production build by only compiling it in when you ask for
it, instead of relying on `#if DEBUG` alone (UI tests often run against Release builds). In your app
project:

```xml
<PropertyGroup Condition="'$(EnableUITestBackdoor)' == 'true'">
  <DefineConstants>$(DefineConstants);UITEST_BACKDOOR</DefineConstants>
</PropertyGroup>

<ItemGroup Condition="'$(EnableUITestBackdoor)' == 'true'">
  <PackageReference Include="Plugin.Maui.UITestHelpers.Backdoor" Version="..." />
</ItemGroup>
```

Then build the app you test with `-p:EnableUITestBackdoor=true`, and put the backdoor methods in a
separate file inside `#if UITEST_BACKDOOR`, like the sample's `App.Backdoor.cs`.

Backdoor methods run on the UI thread. They can be static or instance methods, have parameters
(including optional parameters), and return a value. If they return a `Task`, it's awaited
before the result is returned. Supported parameter and return types:

* Strings, numbers, booleans, enums, `char`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan` and
  `Uri`, and nullable versions of them.
* `JsonElement`, `JsonNode` and `object`.
* Objects (classes and records, also nested), arrays, lists and dictionaries with string keys,
  for example `SeedOrders(Order[] orders)`. Return values can also be anonymous types.
* Objects are converted with `System.Text.Json`, so its attributes such as `[JsonIgnore]` and
  `[JsonPropertyName]` apply. When reflection-based serialization is disabled
  (`JsonSerializerIsReflectionEnabledByDefault=false`, which NativeAOT also sets), the backdoor
  creates objects itself through their public constructor and settable properties. That works as
  long as the app assembly itself isn't fully trimmed, which is the .NET MAUI default
  (`TrimMode=partial`). With `TrimMode=full` or NativeAOT, set a source-generated
  `JsonSerializerContext` as `backdoor.JsonSerializerOptions.TypeInfoResolver` for these types.

Arguments are converted strictly: a number isn't converted to a `string` parameter, and `2.5`
isn't rounded for an `int` parameter. If you overload a backdoor method, at most one overload may
accept the arguments of a call; otherwise the call fails, so give such overloads different names.

### 2. Call them from your UI tests

`Invoke` is available on every `IApp` created by the Appium helpers:

```csharp
App.Invoke("SignIn", "jfversluis");
var count = App.Invoke<int>("GetCartItemCountAsync");
var result = App.Invoke("MyMethod", new object[] { "first", 2 });
```

Like in Xamarin.UITest, an `object[]` is the list of arguments. Because of array covariance that
also applies to other arrays of reference types, such as `string[]`. To pass an array as one
argument, wrap it: `App.Invoke("SetTags", new object[] { tags })`.

`Invoke` without a type argument returns a `string`, `bool`, `long`, `double` or `null`, or a
`JsonElement` for objects and lists. `Invoke<T>` converts the result to `T`. If the method doesn't
exist, its arguments can't be converted, or it throws, a `BackdoorException` is thrown. When the
method throws in the app, `AppExceptionType` and `AppStackTrace` describe that exception.

For tests migrated from Xamarin.UITest, the method name is first matched exactly. If there's no
exact match, it's matched case-insensitively and anything after the first `:` is ignored. That
way existing calls like `app.Invoke("myMethod:", arg)` still find `MyMethod`.

### Connecting to the backdoor

| Platform | How the test reaches the app |
|----------|------------------------------|
| Android emulators and devices | The Appium helpers run `adb forward` automatically. adb is found through the `AdbPath` configuration property, `ANDROID_HOME`, `ANDROID_SDK_ROOT` or `PATH`. |
| iOS simulators, Mac Catalyst and Windows | Directly on `127.0.0.1`, because the app runs on the same machine as the tests. |
| Physical iOS devices (USB) | Forward the port yourself, for example with `iproxy 37265 37265`, and keep the default `BackdoorHost` (`127.0.0.1`). |
| Devices over the network | Opt in to network access in the app and set `BackdoorHost` to the device's IP address, see [Network access](#network-access-less-secure). |

Platform requirements:

* **Android**: the app needs the `INTERNET` permission, also for the default loopback-only mode.
* **Mac Catalyst**: sandboxed apps (with `com.apple.security.app-sandbox` in `Entitlements.plist`,
  as in the .NET MAUI template) also need `com.apple.security.network.server`, even for loopback.
* **Windows**: with network access, Windows Firewall may block the port or ask for permission;
  on CI machines add an inbound rule for it.
* Apps on the same machine (iOS simulators, Mac Catalyst, Windows) share `127.0.0.1`. If two apps
  with a backdoor run at the same time, give them different ports. When `AppId` is configured, the
  test checks that the backdoor belongs to the app under test.

Without network access, the test project must run on the same machine as the emulator, simulator or
device. Cloud device services aren't supported. You can configure the connection with these
`IConfig` properties:

| Property | Default | Description |
|----------|---------|-------------|
| `BackdoorPort` | `37265` | Port the app listens on. If you change it, also set `backdoor.Port` in the app. |
| `BackdoorHost` | `127.0.0.1` | Host to connect to. On Android, setting this turns off the automatic `adb forward`. |
| `BackdoorTimeout` | 30 seconds | Maximum duration of a call, as a `TimeSpan` or a number of seconds. |
| `BackdoorAccessKey` | | Access key, when the app sets `AccessKey` or enables network access. |
| `AdbPath` | | Path to adb, Android only. |

### Network access (less secure)

By default the backdoor only accepts connections from the device itself. To reach it from another
machine, for example a physical iOS device over Wi-Fi like `DeviceIp` in Xamarin.UITest, opt in:

```csharp
#if UITEST_NETWORK
builder.UseUITestBackdoor(backdoor =>
{
    backdoor.AddMethods<App>();
    backdoor.EnableNetworkAccess(accessKey); // at least 16 characters
});
#endif
```

and in the tests:

```csharp
config.SetProperty("BackdoorHost", "192.168.1.42"); // IP address of the device
config.SetProperty("BackdoorAccessKey", accessKey);
```

> [!CAUTION]
> Network access makes your app **less secure**. Anyone on the same network who has the access key
> can call your backdoor methods, and the traffic isn't encrypted, so anyone who can see it can
> read the key and all arguments and return values.
> * Only use it on networks you trust, never on public Wi-Fi.
> * Use a long random key, for example from `openssl rand -hex 32`, and pass it in at build time
>   instead of committing it. The sample reads it from `-p:BackdoorAccessKey=...`. The key is then
>   part of the app, so anyone who has the app package can read it: use a new key per test build.
> * Never enable it in a build that you distribute, including TestFlight and internal app stores.

You can also set `backdoor.AccessKey` without network access, so other apps on the device can't call
the backdoor either.

The sample app exposes several backdoor methods in `App.xaml.cs`, and `BackdoorTests.cs` in the
sample UI tests shows how to use them.

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
