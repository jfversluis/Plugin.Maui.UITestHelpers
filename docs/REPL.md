# The REPL

The REPL (read-eval-print loop) lets you try out UI test code against your running app, one line at a time, and then paste what worked into a test. If you've used `app.Repl()` in Xamarin.UITest, this is the same idea for Appium.

```text
╭─ Plugin.Maui.UITestHelpers REPL ──────────────────────────────────────────────╮
│ Platform  Android 15                                                          │
│ Device    emulator-5554                                                       │
│ App       com.companyname.uitesthelperssample                                 │
│ Session   1a148be7-0b40-46bc-b70d-7f2ef4a5252b                                │
│ App type  AppiumAndroidApp                                                    │
│                                                                               │
│ Try       app.Tap("CounterBtn")   app.FindElements("id")   tree   help   exit │
╰───────────────────────────────────────────────────────────────────────────────╯
❯ tree
App
└── FrameLayout 0,0 1080×2400
    └── ...
        ├── TextView "Hello, World!" 289,818 502×115
        ├── Button #CounterBtn "Click me" 79,1208 923×116 clickable
        └── CheckBox #CB1 79,1376 116×116 clickable checked
❯ app.Tap("CounterBtn")
❯ app.FindElement("CounterBtn").GetText()
"Clicked 1 time"
```

## Install

```shell
dotnet add package Plugin.Maui.UITestHelpers.Repl
```

The package works on top of `Plugin.Maui.UITestHelpers.Appium`, which your UI test project already uses.

## Start it

Call `Repl()` anywhere in a test, on the `App` you already have:

```csharp
using Plugin.Maui.UITestHelpers.Core;

[Test, Explicit("Opens the REPL")]
public void ExploreTheApp()
{
    App.WaitForElement("CounterBtn");   // get the app in the state you want to explore

    App.Repl();                         // the test pauses here
}
```

Run it like any other test, for example `dotnet test --filter ExploreTheApp`. A terminal window opens with the REPL. When you close it with `exit`, the test continues. Marking the test `[Explicit]` makes sure it never runs, and waits for you, as part of a regular test run.

> [!TIP]
> Since the REPL starts wherever you call it, you can use your tests to get the app to the screen you're working on, and explore from there.

## Using the REPL

Anything you type is C#. `app` (and `App`, as in your tests) is the running app, and `driver` is the Appium driver. All the helpers you use in tests are there: `Tap`, `EnterText`, `WaitForElement`, `FindElements`, `Screenshot`, and so on. Variables you declare stay around, and a missing semicolon is added for you.

```text
❯ var buttons = app.FindElements("CounterBtn")
❯ buttons.Count
1
❯ app.EnterText("NameEntry", "Gerald")
❯ app.WaitForElement("Saved", timeout: TimeSpan.FromSeconds(2))
✗ TimeoutException Timed out waiting for element...
  Type trace for the full details.
```

Helpful keys:

| Key | What it does |
|---|---|
| <kbd>.</kbd> | Opens the list of members. Keep typing to filter, <kbd>Enter</kbd> or <kbd>(</kbd> to pick. |
| <kbd>Ctrl</kbd>+<kbd>Space</kbd> | Opens suggestions for the word you're typing, for example a variable or a type |
| <kbd>↑</kbd> / <kbd>↓</kbd> | Earlier and later commands. The history is kept between sessions |
| <kbd>Shift</kbd>+<kbd>Enter</kbd> | New line, for multi-line code. Unfinished code (an open `{` or `(`) continues automatically |
| <kbd>Ctrl</kbd>+<kbd>C</kbd> | Cancels the command that is running, when it can be cancelled. Twice at the prompt closes the REPL |

### Commands

These are not C#, you type them by themselves:

| Command | What it does |
|---|---|
| `tree` | Shows the element tree of the screen, with ids, texts and bounds. Invisible elements and anonymous layout containers are left out. |
| `tree --all` | Every element. `--depth N` limits how deep to go. |
| `tree //XCUIElementTypeButton` | Zooms in: an XPath selects the part of the tree to show. |
| `source` | The raw page source (XML). |
| `screenshot [file.png]` | Saves a screenshot. |
| `copy` | Copies everything that worked in this session to the clipboard, ready to paste in a test. `app.` becomes `App.`. |
| `history` | Shows what worked so far. |
| `trace` | The full details of the last error. |
| `clear`, `help` | Clears the screen, shows the help. |
| `exit`, `quit` | Closes the REPL and continues the test. |

A typical session: use `tree` to find the id of something, try `app.Tap("id")` until it does what you want, then run `copy` and paste it in your test.

## Where the REPL runs

Test runners such as `dotnet test` and the IDEs redirect the console of the test process, so the REPL can't use it. Instead, `Repl()` opens a terminal window that runs the REPL and talks to the test process, which is where your app and driver live. This needs nothing to be installed. On macOS the window is opened with `open` (Terminal by default, or the terminal the test run was started from), on Windows it's a new console window, and on Linux the first terminal of `$TERMINAL`, `x-terminal-emulator`, `gnome-terminal`, `konsole`, `xfce4-terminal`, `kitty`, `alacritty` and `xterm` that exists.

You can change this with `ReplOptions`, or the `UITEST_REPL_MODE` environment variable (which wins):

| Mode | |
|---|---|
| `Auto` (default) | Inline when the console is a real one, otherwise a window. |
| `Window` | Always open a terminal window. |
| `Inline` | Use the console of the test process. For hosts that don't redirect it (for example running the test project as an executable). |
| `Manual` | Doesn't open anything. It prints a command that you can run in any terminal, for example in an SSH session on a Mac that has the device. |
| `off` (environment variable only) | Skips `Repl()`. |

```csharp
App.Repl(new ReplOptions
{
    Mode = ReplMode.Manual,
    Imports = { "MyApp.UITests.Pages" },   // extra namespaces for your own helpers
});
```

Everything your test project references is available in the REPL, so your own page objects and constants work too.

### Continuous integration

On a build server, nobody can type, so `Repl()` returns immediately when it detects one (`CI`, `TF_BUILD`, `GITHUB_ACTIONS` and others). A `Repl()` call that's accidentally left in a test can't hang a build. Set `ReplOptions.RunInContinuousIntegration` to start it anyway.

## Troubleshooting

**The window doesn't open or doesn't connect.** `Repl()` waits 60 seconds (`ReplOptions.ConnectTimeout`) and then fails the test with the command to start the window yourself. Run that command in a terminal, or use `Manual` mode.

**The test fails after a while on the `Repl()` line.** Test runners can have a timeout, for example NUnit's `[Timeout]`. Remove it from the test you use the REPL in.

**A command never returns.** Calls that wait on the device (a `WaitForElement` with a long timeout, a `while (true)`) can't be interrupted from the REPL. The prompt says so after <kbd>Ctrl</kbd>+<kbd>C</kbd>. Closing the window gives control back to the test.

**The commands I type run slowly.** Every call goes to the device through Appium, so the speed is that of your tests. `tree` on screens with web views can be large, use `tree //xpath` or `--depth`.

**Completion is empty.** The list comes from the same compiler the REPL uses, it needs a moment the first time.

## Differences with Xamarin.UITest

* It's C#: lambdas, `var`, loops and your own helpers work. There is no `AppQuery`, use the same string ids and `IQuery` as in your Appium tests.
* `app.Flash(...)` isn't available. Use `tree`, `screenshot` or `app.FindElements(...)` to see what a query finds.
* The window talks to your test over a named pipe that only your user can use, and the REPL has no network access.
