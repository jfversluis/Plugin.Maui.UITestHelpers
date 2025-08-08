# REPL (Read-Eval-Print Loop) for UI Testing

## Overview

The Plugin.Maui.UITestHelpers.Appium package now includes a REPL (Read-Eval-Print Loop) feature that allows for interactive UI inspection and testing, similar to what was available in Xamarin.UITest.

## Getting Started

To start a REPL session with your app:

```csharp
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;

// After creating your AppiumApp instance
var app = AppiumApp.CreateAndroidApp(driver, config);

// Start an interactive REPL session
app.StartRepl();
```

## Available Commands

### General Commands
- `help` or `?` - Show available commands
- `exit` or `quit` - Exit the REPL
- `clear` - Clear the console
- `info` - Show app information

### UI Inspection
- `tree` - Show the current UI element tree
- `screenshot [filename]` - Take a screenshot (alias: `ss`)
- `logs [logtype]` - Show logs (optional logtype filter)

### Element Finding
- `find <selector>` - Find element using general selector
- `id <id>` - Find element by ID
- `xpath <xpath>` - Find element by XPath
- `class <classname>` - Find element by class name
- `name <name>` - Find element by name
- `accessibility <id>` - Find element by accessibility ID
- `query <query>` - Execute a custom query

### Element Actions
- `click <selector>` - Click an element
- `text <selector>` - Get text from an element
- `type <selector> <text>` - Type text into an element

## Usage Examples

### Basic Element Finding
```
uitest> id CounterBtn
Text: "Click me" Tag: android.widget.Button Enabled: True Displayed: True Location: (100, 200) Size: 120x40

uitest> xpath //button[@text='Click me']
Text: "Click me" Tag: android.widget.Button Enabled: True Displayed: True Location: (100, 200) Size: 120x40
```

### Element Interaction
```
uitest> click CounterBtn
Clicked element: CounterBtn

uitest> text CounterBtn
Text: "Clicked 1 time"

uitest> type MyEntry "Hello World"
Typed "Hello World" into element: MyEntry
```

### UI Inspection
```
uitest> tree
<?xml version="1.0" encoding="UTF-8"?>
<hierarchy rotation="0">
  <android.widget.FrameLayout index="0" package="com.companyname.sample">
    <android.widget.LinearLayout index="0">
      <android.widget.Button resource-id="CounterBtn" text="Click me" />
    </android.widget.LinearLayout>
  </android.widget.FrameLayout>
</hierarchy>

uitest> screenshot
Screenshot saved to: /path/to/repl_screenshot_20231215_143022.png

uitest> info
App Information:
  State: Running
  Driver: AppiumDriver
  Capabilities:
    platformName: Android
    deviceName: emulator-5554
    platformVersion: 11.0
    automationName: UiAutomator2
```

## Programmatic Usage

You can also execute REPL commands programmatically:

```csharp
// Execute a single command
var result = app.ExecuteReplCommand("id CounterBtn");
Console.WriteLine(result);

// Get help
var help = app.GetReplHelp();
Console.WriteLine(help);
```

## Tips

1. **Element Selectors**: Most commands that accept selectors can use element IDs directly or more complex query strings.

2. **Screenshots**: Screenshots are saved to the current working directory by default. You can specify a custom filename.

3. **XPath Expressions**: Use XPath for complex element selection. Remember to escape special characters when needed.

4. **Error Handling**: If a command fails, an error message will be displayed explaining what went wrong.

5. **Element Information**: When finding elements, the REPL shows useful information like text content, location, size, and state.

## Integration with Existing Tests

The REPL can be integrated into existing test workflows:

```csharp
[Test]
public void DebugTest()
{
    var element = App.FindElement("CounterBtn");
    
    // Start REPL for interactive debugging
    App.StartRepl();
    
    // Continue with test after REPL session ends
    element.Click();
    Assert.That(element.GetText(), Is.EqualTo("Clicked 1 time"));
}
```

This allows you to pause test execution and interactively inspect the UI state, making debugging much easier.