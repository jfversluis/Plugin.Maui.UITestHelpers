# Plugin.Maui.UITestHelpers - Copilot Instructions

## Project Overview

This is a .NET MAUI plugin that provides helpers for UI testing .NET MAUI apps with Appium. It targets Android, iOS, macOS (Catalyst), and Windows.

## Architecture

**Test helper library** with three packages:

- `Plugin.Maui.UITestHelpers.Core` - Abstractions (`IApp`, `IQuery`)
- `Plugin.Maui.UITestHelpers.Appium` - Appium implementation
- `Plugin.Maui.UITestHelpers.NUnit` - NUnit integration

Platform drivers: `AppiumAndroidApp`, `AppiumIOSApp`, `AppiumCatalystApp`, `AppiumWindowsApp`.

This provides a migration path from Xamarin.UITest to Appium.

## Code Conventions

### Namespace
All code uses: `Plugin.Maui.UITestHelpers`

### File Naming
- `*.shared.cs` - Cross-platform code
- `*.android.cs` - Android-specific code
- `*.macios.cs` - iOS/macOS-specific code
- `*.windows.cs` - Windows-specific code
- `*.net.cs` - Generic .NET fallback

### Standards
- File-scoped namespaces
- `camelCase` for private fields, `PascalCase` for public
- XML docs required on all public APIs
- Null-conditional operators for platform interop

## Building

```bash
dotnet build src/Plugin.Maui.UITestHelpers.Appium/Plugin.Maui.UITestHelpers.Appium.csproj -c Release
```

## When Making Changes
1. Ensure all three packages build
2. If adding public API, update the interface in Core
3. Implement in the Appium package
4. Update sample app and README
