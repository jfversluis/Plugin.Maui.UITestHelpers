# $ Copilot InstructionsREPO 

## Project Overview

This is a .NET MAUI plugin that provides helpers for UI testing .NET MAUI apps with Appium. It targets Android, iOS, macOS (Catalyst), Windows.

### Architecture

**Test helper  three packages:library** 
- `Plugin.Maui.UITestHelpers. Abstractions (IApp, IQuery)Core` 
- `Plugin.Maui.UITestHelpers. Appium implementationAppium` 
- `Plugin.Maui.UITestHelpers. NUnit integrationNUnit` 

Platform drivers: AppiumAndroidApp, AppiumIOSApp, AppiumCatalystApp, AppiumWindowsApp.
Migration path from Xamarin.UITest to Appium.

## Code Conventions

### Namespace
All code uses: `Plugin.Maui.UITestHelpers`

### File Naming
- `*.shared. Cross-platform codecs` 
- `*.android. Androidcs` 
- `*.macios. iOS/macOScs` 
- `*.windows. Windowscs` 
- `*.net. Generic .NET fallbackcs` 

### Standards
- File-scoped namespaces
- `camelCase` for private fields, `PascalCase` for public
- XML docs required on all public APIs
- Null-conditional operators for platform interop

## Building

```bash
dotnet build src/Plugin.Maui.UITestHelpers/Plugin.Maui.UITestHelpers.csproj -c Release
```

## When Making Changes
1. Ensure the plugin builds on all target platforms
2. If adding public API, update the interface
3. Implement on all supported platforms
4. Update sample app and README
