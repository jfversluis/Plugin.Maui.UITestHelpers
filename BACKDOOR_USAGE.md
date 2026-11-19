# Backdoor Support in Plugin.Maui.UITestHelpers

This document explains how to use the backdoor functionality in Plugin.Maui.UITestHelpers.Appium, which provides support for invoking app methods directly from UI tests, similar to Xamarin.UITest's backdoor feature.

## Overview

Backdoors allow UI tests to invoke methods in the app under test without going through the UI. This is useful for:
- Setting up test data
- Changing app state for specific test scenarios
- Retrieving internal app state for assertions
- Bypassing complex UI flows for test setup

## Basic Usage

### Simple Method Invocation

```csharp
// Invoke a method without return value
app.Invoke("SetupTestData");

// Invoke a method with parameters
app.Invoke("SetUserPreference", "theme", "dark");
```

### Typed Method Invocation

```csharp
// Invoke a method that returns a string
string result = app.Invoke<string>("GetCurrentUser");

// Invoke a method that returns an integer
int count = app.Invoke<int>("GetItemCount");

// Invoke a method that returns a complex object
var settings = app.Invoke<AppSettings>("GetSettings");
```

## App-Side Implementation

To support backdoor calls in your .NET MAUI app, you need to implement a handler that responds to the `mobile: backdoor` script execution. This can be done using custom handlers or dependency injection.

### Example Implementation

Here's a basic example of how to implement backdoor support in your MAUI app:

```csharp
public class BackdoorService
{
    public string GetCurrentUser() => "testuser@example.com";
    
    public void SetupTestData()
    {
        // Initialize test data
    }
    
    public int GetItemCount() => 42;
}

// Register in MauiProgram.cs
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
            });

#if DEBUG
        // Register backdoor service for testing
        builder.Services.AddSingleton<BackdoorService>();
#endif

        return builder.Build();
    }
}
```

## Platform-Specific Considerations

The backdoor implementation uses Appium's `ExecuteScript` functionality with the `mobile: backdoor` command. Different platforms may handle this differently:

- **Android**: May require custom implementation in the Android driver
- **iOS**: May require custom WDA (WebDriverAgent) implementation  
- **Windows**: May require custom implementation in WinAppDriver

## Error Handling

If a backdoor method fails or doesn't exist, the `Invoke` methods will:
- Return `null` for untyped invocations
- Return `default(T)` for typed invocations
- Not throw exceptions (fail gracefully)

## Best Practices

1. **Use sparingly**: Backdoors should complement, not replace, UI testing
2. **Guard with preprocessor directives**: Only include backdoor handlers in test builds
3. **Document your backdoor methods**: Make it clear what each method does
4. **Keep methods simple**: Backdoor methods should be straightforward and fast
5. **Avoid UI operations**: Don't manipulate UI directly from backdoor methods

## Testing the Implementation

You can test that the backdoor functionality is working with a simple test:

```csharp
[Test]
public void BackdoorBasicTest()
{
    // Test that backdoor calls don't throw exceptions
    Assert.DoesNotThrow(() => app.Invoke("NonExistentMethod"));
    
    // Test with parameters
    Assert.DoesNotThrow(() => app.Invoke("TestMethod", "arg1", 123));
    
    // Test typed return (will be null/default if not implemented)
    var result = app.Invoke<string>("GetTestValue");
    Assert.That(result, Is.Null.Or.InstanceOf<string>());
}
```

## Troubleshooting

If backdoor calls aren't working:

1. **Check app implementation**: Ensure your app properly handles `mobile: backdoor` script execution
2. **Verify method names**: Method names are case-sensitive
3. **Check platform support**: Some platforms may require additional setup
4. **Review logs**: Enable verbose logging to see what's happening with script execution

For more information, see the [Xamarin.UITest backdoor documentation](https://learn.microsoft.com/en-us/appcenter/test-cloud/frameworks/uitest/features/backdoors) for comparison and additional context.