using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace UITests.Shared;

public class ReplTests : BaseTest
{
    public ReplTests(TestDevice testDevice) : base(testDevice)
    {
    }

    // Opens the interactive REPL, run it on its own: dotnet test --filter ReplTests
    // It's explicit so it never runs (and waits for you) as part of a regular test run.
    [Test, Explicit("Opens an interactive REPL and waits until you close it with 'exit'.")]
    public void ExploreTheApp()
    {
        App.WaitForElement("CounterBtn");

        App.Repl();
    }
}
