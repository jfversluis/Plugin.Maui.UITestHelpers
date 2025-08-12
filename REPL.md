# REPL (Read-Eval-Print Loop) for UI Testing

## Overview

The Plugin.Maui.UITestHelpers.Appium package now includes a REPL (Read-Eval-Print Loop) feature that allows for interactive UI inspection and testing, similar to what was available in Xamarin.UITest.

## Environment Requirements

The interactive REPL requires:
- A console environment with stdin/stdout support
- Interactive terminal/console session
- **Not suitable for headless environments, CI/CD, or automated test runners**

For automated testing scenarios, use the programmatic API instead (see [Programmatic Usage](#programmatic-usage)).

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

## Troubleshooting

### REPL in Test Environment (`dotnet test`)

**Problem**: When running `ReplInteractiveTest` with `dotnet test`, the REPL immediately exits with "Input stream ended. Exiting REPL...".

**Cause**: Test runners like `dotnet test` redirect console input/output, making interactive input impossible.

**Platform Requirements for New Console Window**:
- **Windows**: Requires Windows Terminal (preferred), PowerShell, or Command Prompt
- **macOS**: Requires Terminal.app (built-in) or iTerm2; may need accessibility permissions for AppleScript
- **Linux**: Requires a terminal emulator (gnome-terminal, konsole, xterm, etc.) and X11/Wayland display
- **Headless environments**: Console window opening will fail, but programmatic API remains available

**Current Behavior**: The REPL now:
1. Detects console redirection in test environments
2. **Automatically attempts to open a new console window** on supported platforms
3. Falls back to programmatic guidance if window opening fails
4. Provides clear error messages with platform-specific solutions

**Solutions**:

1. **New Console Window** (Automatic in supported environments):
   - The REPL will attempt to open a new terminal/console window automatically
   - **Windows**: Tries Windows Terminal, PowerShell, or Command Prompt
   - **macOS**: Uses AppleScript to open Terminal.app or iTerm2
   - **Linux**: Attempts to launch gnome-terminal, konsole, xterm, or other available terminals
   - If successful, a new window will open with instructions and demonstrative content
   - The original test will continue while the new window remains open

2. **Use Programmatic API** (Recommended for test environments):
   ```csharp
   [Test]
   public void ReplProgrammaticUsage()
   {
       var result = App.ExecuteReplCommand("id CounterBtn");
       var help = App.ExecuteReplCommand("help");
       var screenshot = App.ExecuteReplCommand("screenshot test.png");
   }
   ```

3. **Run in IDE with Debugging**:
   - Set a breakpoint after `App.StartRepl()`
   - Use the debugger console for interactive commands
   - Step through and interact with the REPL

4. **Run Tests Outside Test Runner**:
   - Execute test methods manually in a console application
   - Use interactive development environments
   - Run single tests with full console access

5. **Alternative Test Approach**:
   ```csharp
   [Test]
   public void InteractiveDebugHelper()
   {
       // This test is for manual debugging only
       // Run this specific test in your IDE with debugger
       var element = App.FindElement("CounterBtn");
       
       // Set breakpoint here and use immediate window:
       // App.ExecuteReplCommand("tree")
       // App.ExecuteReplCommand("click CounterBtn")
       
       App.StartRepl(); // Will show helpful guidance in test runners
   }
   ```

### REPL Blocked in CI/CD

**Problem**: The REPL refuses to start in CI/CD environments.

**Cause**: This is intentional behavior to prevent tests from hanging in automated builds.

**Solutions**:
1. Use `ExecuteReplCommand()` for programmatic access in CI/CD
2. Mark interactive tests with conditional attributes for local-only execution:
   ```csharp
   [Test]
   [Category("Interactive")] // Skip in CI with --filter "Category!=Interactive"
   public void ReplInteractiveTest()
   {
       App.StartRepl();
   }
   ```

### Environment Detection

The REPL uses improved environment detection:
- **CI/CD Detection**: Checks for CI environment variables (CI, GITHUB_ACTIONS, JENKINS_URL, etc.)
- **Console Redirection Detection**: Detects when running in test runners like `dotnet test`
- **Graceful Error Handling**: Provides clear guidance instead of hanging or failing silently
- **Programmatic Fallback**: Always provides programmatic access regardless of environment

### Best Practices

1. **Local Development**: Use `StartRepl()` for interactive debugging (now works with `dotnet test`)
2. **Automated Tests**: Use `ExecuteReplCommand()` for programmatic access
3. **CI/CD**: Use programmatic commands only (REPL will refuse to start)
4. **Test Organization**: Consider environment-specific test execution strategies

This allows you to pause test execution and interactively inspect the UI state, making debugging much easier.