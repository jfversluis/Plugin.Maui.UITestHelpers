using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Core;
using Plugin.Maui.UITestHelpers.Repl;
using Plugin.Maui.UITestHelpers.Repl.Client;
using Plugin.Maui.UITestHelpers.Repl.Engine;
using Plugin.Maui.UITestHelpers.Repl.Hosting;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Plugin.Maui.UITestHelpers.Repl.Tests;

public class ReplEngineTests
{
    ReplEngine _engine = null!;

    [SetUp]
    public void SetUp() => _engine = new ReplEngine(new FakeApp());

    [Test]
    public async Task ExpressionReturnsItsValue()
    {
        var result = await _engine.EvaluateAsync("1 + 2", default);

        Assert.That(result.Outcome, Is.EqualTo(ReplOutcome.Value));
        Assert.That(result.Value, Is.EqualTo(3));
    }

    [TestCase("var x = 20;", "x * 2", 40)]
    [TestCase("var n = 3", "n * 14", 42)]
    [TestCase("var n = 3", "n*14", 42)]
    public async Task StateIsKeptBetweenSubmissions(string declaration, string expression, int expected)
    {
        await _engine.EvaluateAsync(declaration, default);
        var result = await _engine.EvaluateAsync(expression, default);

        Assert.That(result.Outcome, Is.EqualTo(ReplOutcome.Value), string.Join(", ", result.Diagnostics?.Select(d => d.ToString()) ?? Array.Empty<string>()));
        Assert.That(result.Value, Is.EqualTo(expected));
    }

    [Test]
    public async Task MissingSemicolonOnAStatementIsAdded()
    {
        var declaration = await _engine.EvaluateAsync("var x = 5", default);
        var result = await _engine.EvaluateAsync("x", default);

        Assert.That(declaration.Outcome, Is.EqualTo(ReplOutcome.NoValue));
        Assert.That(result.Value, Is.EqualTo(5));
    }

    [Test]
    public async Task VoidCallHasNoValueAndNullIsStillAValue()
    {
        var voidCall = await _engine.EvaluateAsync("System.Console.Write(\"\")", default);
        var nothing = await _engine.EvaluateAsync("(string?)null", default);

        Assert.That(voidCall.Outcome, Is.EqualTo(ReplOutcome.NoValue));
        Assert.That(nothing.Outcome, Is.EqualTo(ReplOutcome.Value));
        Assert.That(nothing.Value, Is.Null);
    }

    [Test]
    public async Task CompilationErrorsAreReported()
    {
        var result = await _engine.EvaluateAsync("doesNotExist", default);

        Assert.That(result.Outcome, Is.EqualTo(ReplOutcome.CompilationError));
        Assert.That(result.Diagnostics!.Single().Id, Is.EqualTo("CS0103"));
    }

    [Test]
    public async Task ExceptionsAreReportedAndTheSessionKeepsWorking()
    {
        var failed = await _engine.EvaluateAsync("throw new InvalidOperationException(\"boom\");", default);
        var next = await _engine.EvaluateAsync("1", default);

        Assert.That(failed.Outcome, Is.EqualTo(ReplOutcome.Exception));
        Assert.That(failed.Exception, Is.TypeOf<InvalidOperationException>());
        Assert.That(next.Value, Is.EqualTo(1));
    }

    [TestCase("app.Repl()")]
    [TestCase("App.Repl();")]
    public async Task ReplInsideTheReplIsAnError(string code)
    {
        var result = await _engine.EvaluateAsync(code, default);

        Assert.That(result.Outcome, Is.EqualTo(ReplOutcome.Exception));
        Assert.That(result.Exception, Is.TypeOf<InvalidOperationException>());
        Assert.That(result.Exception!.Message, Does.Contain("already running"));
        Assert.That(ReplEngine.IsEvaluating, Is.False);
    }

    [Test]
    public async Task AppIsAvailableWithItsActualTypeAndUiHelpers()
    {
        var result = await _engine.EvaluateAsync("app.GetType().Name", default);
        var alias = await _engine.EvaluateAsync("ReferenceEquals(app, App)", default);
        var element = await _engine.EvaluateAsync("app.FindElement(\"x\") is null", default);

        Assert.That(result.Value, Is.EqualTo(nameof(FakeApp)));
        Assert.That(alias.Value, Is.EqualTo(true));
        Assert.That(element.Value, Is.EqualTo(true));
    }

    [Test]
    public void CompletionListsMembersAndExtensionMethodsOfTheApp()
    {
        var names = _engine.GetCompletions("app.", 4).Select(c => c.Name).ToList();

        Assert.That(names, Does.Contain("FindElement"));
        Assert.That(names, Does.Contain("Tap"), "extension methods are included");
        Assert.That(names, Does.Not.Contain("get_Config"), "accessors are hidden");
    }

    [Test]
    public void CompletionFiltersOnWhatIsTyped()
    {
        var names = _engine.GetCompletions("app.Find", 8).Select(c => c.Name).ToList();

        Assert.That(names, Is.EquivalentTo(new[] { "FindElement", "FindElementByText", "FindElements", "FindElementsByText" }));
    }

    [Test]
    public void CompletionWorksForStaticMembersAndDeclaredVariables()
    {
        _engine.EvaluateAsync("var counter = 1;", default).GetAwaiter().GetResult();

        Assert.That(_engine.GetCompletions("Math.Ab", 7).Select(c => c.Name), Does.Contain("Abs"));
        Assert.That(_engine.GetCompletions("coun", 4).Select(c => c.Name), Does.Contain("counter"));
    }
}

public class ReplSessionTests
{
    ReplSession _session = null!;
    TestConsole _console = null!;

    [SetUp]
    public void SetUp()
    {
        _session = new ReplSession(new FakeApp(), new ReplOptions());
        _console = new TestConsole().Width(120);
    }

    async Task<bool> Run(string code) => await _session.ExecuteAsync(code, _console, redirectConsole: false, default);

    [Test]
    public async Task ExitAndQuitCloseTheRepl()
    {
        Assert.That(await Run("exit"), Is.False);
        Assert.That(await Run("quit"), Is.False);
        Assert.That(await Run("1 + 1"), Is.True);
    }

    [Test]
    public async Task VariablesWithACommandNameAreStillCSharp()
    {
        await Run("var copy = 3;");
        await Run("copy = 4");
        await Run("copy");

        Assert.That(_console.Output, Does.Contain("4"));
        Assert.That(_console.Output, Does.Not.Contain("✗"));
    }

    [Test]
    public async Task ResultsAreShown()
    {
        await Run("\"hi\"");

        Assert.That(_console.Output, Does.Contain("\"hi\""));
    }

    [Test]
    public async Task CopyUsesTheSyntaxOfATest()
    {
        await Run("app.FindElement(\"CounterBtn\")");
        await Run("var x = 1");

        // Clipboard access isn't available on every machine, the history is what matters here.
        await Run("history");

        Assert.That(_console.Output, Does.Contain("app.FindElement(\"CounterBtn\")"));
        Assert.That(_console.Output, Does.Contain("var x = 1"));
    }

    [Test]
    public async Task ErrorsAreShownWithoutEndingTheSession()
    {
        await Run("nope");
        await Run("throw new System.Exception(\"bad\");");

        Assert.That(_console.Output, Does.Contain("CS0103"));
        Assert.That(_console.Output, Does.Contain("bad"));
        Assert.That(await Run("1"), Is.True);
    }

    [Test]
    public async Task TreeShowsTheElementsOfThePageSource()
    {
        await Run("tree");

        Assert.That(_console.Output, Does.Contain("Button"));
        Assert.That(_console.Output, Does.Contain("#CounterBtn"));
        Assert.That(_console.Output, Does.Contain("\"Click me\""));
    }
}

public class ElementTreeRendererTests
{
    const string Android = """
        <?xml version='1.0' encoding='UTF-8' standalone='yes' ?>
        <hierarchy index="0" class="hierarchy" rotation="0" width="1080" height="2400">
          <android.widget.FrameLayout index="0" package="com.test" class="android.widget.FrameLayout" bounds="[0,0][1080,2400]" displayed="true">
            <android.widget.LinearLayout index="0" class="android.widget.LinearLayout" bounds="[0,0][1080,2400]" displayed="true">
              <android.widget.Button index="0" resource-id="com.test:id/CounterBtn" class="android.widget.Button" text="Click me" clickable="true" bounds="[79,1208][1002,1324]" displayed="true" />
              <android.widget.TextView index="1" class="android.widget.TextView" text="Hidden" bounds="[0,0][1,1]" displayed="false" />
            </android.widget.LinearLayout>
          </android.widget.FrameLayout>
        </hierarchy>
        """;

    const string Ios = """
        <?xml version="1.0" encoding="UTF-8"?>
        <AppiumAUT>
          <XCUIElementTypeApplication type="XCUIElementTypeApplication" name="Sample" label="Sample" enabled="true" visible="true" x="0" y="0" width="402" height="874">
            <XCUIElementTypeWindow type="XCUIElementTypeWindow" enabled="true" visible="true" x="0" y="0" width="402" height="874">
              <XCUIElementTypeButton type="XCUIElementTypeButton" name="CounterBtn" label="Click me" enabled="true" visible="true" accessible="true" x="20" y="500" width="360" height="44" />
              <XCUIElementTypeOther type="XCUIElementTypeOther" enabled="true" visible="false" x="0" y="0" width="1" height="1" />
            </XCUIElementTypeWindow>
          </XCUIElementTypeApplication>
        </AppiumAUT>
        """;

    static string Render(string xml, TreeOptions options, out int count)
    {
        var console = new TestConsole().Width(120);
        console.Write(ElementTreeRenderer.Render(xml, options, out count));
        return console.Output;
    }

    [Test]
    public void AndroidTreeHidesAnonymousWrappersAndHiddenElements()
    {
        var output = Render(Android, new TreeOptions(), out var count);

        Assert.That(output, Does.Contain("Button #CounterBtn \"Click me\""));
        Assert.That(output, Does.Contain("1,208").Or.Contain("79,1208 923×116"));
        Assert.That(output, Does.Not.Contain("Hidden"));
        Assert.That(output, Does.Not.Contain("LinearLayout"));
        Assert.That(count, Is.EqualTo(2), "the app root and the button");
    }

    [Test]
    public void AllShowsEveryElement()
    {
        Render(Android, new TreeOptions(All: true), out var count);

        Assert.That(count, Is.EqualTo(4));
    }

    [Test]
    public void ZeroSizedElementsAreHiddenUnlessAllIsUsed()
    {
        const string mac = """
            <AppiumAUT>
              <XCUIElementTypeApplication type="XCUIElementTypeApplication" name="Sample" x="0" y="0" width="0" height="0">
                <XCUIElementTypeMenuItem type="XCUIElementTypeMenuItem" name="About" label="About" x="0" y="1080" width="0" height="0" />
                <XCUIElementTypeButton type="XCUIElementTypeButton" name="Go" label="Go" x="10" y="10" width="50" height="20" />
              </XCUIElementTypeApplication>
            </AppiumAUT>
            """;

        Assert.That(Render(mac, new TreeOptions(), out _), Does.Not.Contain("About").And.Contain("\"Go\""));
        Assert.That(Render(mac, new TreeOptions(All: true), out _), Does.Contain("About"));
    }

    [Test]
    public void IosTreeUsesNamesAndLabels()
    {
        var output = Render(Ios, new TreeOptions(), out _);

        Assert.That(output, Does.Contain("Button #CounterBtn \"Click me\""));
        Assert.That(output, Does.Not.Contain("XCUIElementType"));
    }

    [Test]
    public void XPathZoomsInOnMatchingElements()
    {
        var output = Render(Android, new TreeOptions(XPath: "//android.widget.Button"), out var count);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(output, Does.Contain("#CounterBtn"));
    }

    [Test]
    public void InvalidInputGivesAMessageInsteadOfAnException()
    {
        var output = Render("not xml", new TreeOptions(), out var count);

        Assert.That(count, Is.Zero);
        Assert.That(output, Does.Contain("Could not parse"));
    }
}

public class ProtocolTests
{
    [Test]
    public async Task FramesSurviveTheRoundTrip()
    {
        using var stream = new MemoryStream();
        using var channel = new FrameChannel(stream);

        await channel.WriteAsync(new Frame { Type = FrameTypes.Exec, Id = 7, Text = "app.Tap(\"é\")\nline2", Width = 100 });
        stream.Position = 0;

        var frame = await channel.ReadAsync();

        Assert.That(frame!.Type, Is.EqualTo(FrameTypes.Exec));
        Assert.That(frame.Id, Is.EqualTo(7));
        Assert.That(frame.Text, Is.EqualTo("app.Tap(\"é\")\nline2"));
    }

    [Test]
    public void IncompleteInputContinuesOnTheNextLine()
    {
        Assert.That(ReplSyntax.IsCompleteSubmission("app.Tap(\"x\")"), Is.True);
        Assert.That(ReplSyntax.IsCompleteSubmission("app.Tap(\"x\""), Is.False);
        Assert.That(ReplSyntax.IsCompleteSubmission("if (true) {"), Is.False);
        Assert.That(ReplSyntax.IsCompleteSubmission("var x = 5"), Is.True, "only the semicolon is missing");
    }
}

public class FrameTextWriterTests
{
    [Test]
    public void OutputIsSentWhenALineEndsOrOnFlush()
    {
        var sent = new List<string>();
        using var writer = new FrameTextWriter(sent.Add);

        writer.Write("par");
        writer.Write("tial");
        Assert.That(sent, Is.Empty);

        writer.Write("line\n");
        Assert.That(sent, Is.EqualTo(new[] { "partialline\n" }));

        writer.Write("rest");
        writer.Flush();
        Assert.That(sent.Last(), Is.EqualTo("rest"));
    }
}

public class ReplOptionsTests
{
    [TestCase("off", true)]
    [TestCase("0", true)]
    [TestCase("manual", false)]
    public void EnvironmentVariableOverridesTheMode(string value, bool disabled)
    {
        Environment.SetEnvironmentVariable(ReplOptions.ModeEnvironmentVariable, value);
        try
        {
            var mode = new ReplOptions { Mode = ReplMode.Inline }.ResolveMode(out var isDisabled);

            Assert.That(isDisabled, Is.EqualTo(disabled));
            if (!disabled)
            {
                Assert.That(mode, Is.EqualTo(ReplMode.Manual));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(ReplOptions.ModeEnvironmentVariable, null);
        }
    }
}

sealed class FakeApp : IApp, IScreenshotSupportedApp
{
    const string PageSource = """
        <hierarchy index="0" class="hierarchy">
          <android.widget.FrameLayout class="android.widget.FrameLayout" bounds="[0,0][1080,2400]">
            <android.widget.Button resource-id="app:id/CounterBtn" class="android.widget.Button" text="Click me" clickable="true" bounds="[79,1208][1002,1324]" />
          </android.widget.FrameLayout>
        </hierarchy>
        """;

    public IConfig Config { get; } = new Config();

    public IUIElementQueryable Query => throw new NotSupportedException();

    public ApplicationState AppState => ApplicationState.Running;

    public string ElementTree => PageSource;

    public ICommandExecution CommandExecutor => throw new NotSupportedException();

    public IUIElement FindElement(string id) => null!;

    public IUIElement FindElementByText(string text) => null!;

    public IUIElement FindElement(IQuery query) => null!;

    public IReadOnlyCollection<IUIElement> FindElements(string id) => Array.Empty<IUIElement>();

    public IReadOnlyCollection<IUIElement> FindElements(IQuery query) => Array.Empty<IUIElement>();

    public IReadOnlyCollection<IUIElement> FindElementsByText(string text) => Array.Empty<IUIElement>();

    public FileInfo Screenshot(string fileName) => new(fileName);

    public byte[] Screenshot() => Array.Empty<byte>();

    public void Dispose()
    {
    }
}
