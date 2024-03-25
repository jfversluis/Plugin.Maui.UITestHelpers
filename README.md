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
| Plugin.Maui.UITestHelpers.Core   | Shared, core types that are used across the different projects | ![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.Core) |
| Plugin.Maui.UITestHelpers.Appium | This package contains code specific to UI testing with Appium. It's filled with helpers that will make writing UI tests easier, as well as methods that (mostly) mimic Xamarin.UITest to make transitioning your current UI tests easier | ![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.Appium) |
| Plugin.Maui.UITestHelpers.NUnit | This package contains helpers that will make it easier to write UI tests based on NUnit | ![NuGet Version](https://img.shields.io/nuget/vpre/Plugin.Maui.UITestHelpers.NUnit) |

Install with the dotnet CLI, for example: `dotnet add package Plugin.Maui.UITestHelpers.Appium`, or through the NuGet Package Manager in Visual Studio.

### Supported Platforms

All platforms that are supported by the cross section of the support of Appium and .NET MAUI.

<!--## API Usage

TBD -->

# Acknowledgements

This project could not have came to be without these projects and people, thank you! <3

The original source was extracted from the [.NET MAUI codebase](https://github.com/dotnet/maui) so all credit goes out to that team, namely @sbanni, @mattleibow, @PureWeen, @MartyIX, @jsuarezruiz, @jfversluis and @rmarinho.
