using NUnit.Framework;
using NUnit.Framework.Legacy;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace UITests.Shared;

public class MainPageTests : BaseTest
{
    public MainPageTests(TestDevice testDevice) : base(testDevice)
    {
    }

    [Test]
    public void AppLaunches()
    {
        App.Screenshot($"{nameof(AppLaunches)}.png");
    }

    [Test]
    public void ClickCounterTest()
    {
        const string elementId = "CounterBtn";

        // Arrange
        // Find elements with the value of the AutomationId property
        var element = App.FindElement(elementId);
        App.WaitForElement(elementId);

        // Act
        element.Click();
        Task.Delay(500).Wait(); // Wait for the click to register and show up on the screenshot

        // Assert
        App.Screenshot($"{nameof(ClickCounterTest)}.png");
        Assert.That(element.GetText(), Is.EqualTo("Clicked 1 time"));
    }

    [Test]
    public void CheckBox_CheckboxChecked_IsCheckedReturnsTrue()
    {
        const string elementId = "CB1";

        // Arrange
        var element = App.FindElement(elementId);
        App.WaitForElement(elementId);

        // Act
        bool isChecked = element.IsChecked(App.GetTestDevice());

        // Assert
        App.Screenshot($"{nameof(CheckBox_CheckboxChecked_IsCheckedReturnsTrue)}.png");
        ClassicAssert.IsTrue(isChecked);
    }

    [Test]
    public void CheckBox_CheckboxNotChecked_IsCheckedReturnsFalse()
    {
        const string elementId = "CB2";

        // Arrange
        var element = App.FindElement(elementId);
        App.WaitForElement(elementId);

        // Act
        bool isChecked = element.IsChecked(App.GetTestDevice());

        // Assert
        App.Screenshot($"{nameof(CheckBox_CheckboxNotChecked_IsCheckedReturnsFalse)}.png");
        ClassicAssert.IsFalse(isChecked);
    }

    [Test]
    public void RadioButton_RadioButtonChecked_IsCheckedReturnsTrue()
    {
        const string elementId = "RB1";

        // Arrange
        var element = App.FindElement(elementId);
        App.WaitForElement(elementId);

        // Act
        bool isChecked = element.IsChecked(App.GetTestDevice());

        // Assert
        App.Screenshot($"{nameof(RadioButton_RadioButtonChecked_IsCheckedReturnsTrue)}.png");
        ClassicAssert.IsTrue(isChecked);
    }

    [Test]
    public void RadioButton_RadioButtonNotChecked_IsCheckedReturnsFalse()
    {
        const string elementId = "RB2";

        // Arrange
        var element = App.FindElement(elementId);
        App.WaitForElement(elementId);

        // Act
        bool isChecked = element.IsSelected();

        // Assert
        App.Screenshot($"{nameof(RadioButton_RadioButtonNotChecked_IsCheckedReturnsFalse)}.png");
        ClassicAssert.IsFalse(isChecked);
    }

    [Test]
    [Ignore("Interactive test - uncomment to use REPL for debugging")]
    public void ReplInteractiveTest()
    {
        // This test demonstrates how to use REPL for interactive debugging
        // Uncomment the [Ignore] attribute above to run this test
        
        const string elementId = "CounterBtn";

        // Arrange - find the counter button
        var element = App.FindElement(elementId);
        App.WaitForElement(elementId);
        
        // Use REPL to interactively inspect and test the UI
        // This will start an interactive session where you can:
        // - Inspect the UI tree: tree
        // - Find elements: id CounterBtn
        // - Click elements: click CounterBtn
        // - Check element text: text CounterBtn
        // - Take screenshots: screenshot
        // - Get help: help
        
        App.StartRepl();
        
        // After the REPL session ends, continue with automated test
        element.Click();
        Task.Delay(500).Wait();
        App.Screenshot($"{nameof(ReplInteractiveTest)}.png");
        Assert.That(element.GetText(), Is.EqualTo("Clicked 1 time"));
    }

    [Test]
    public void ReplProgrammaticUsage()
    {
        // This test demonstrates programmatic usage of REPL commands
        
        // Execute REPL commands programmatically
        var helpResult = App.ExecuteReplCommand("help");
        Assert.That(helpResult, Contains.Substring("Available REPL commands"));
        
        var infoResult = App.ExecuteReplCommand("info");
        Assert.That(infoResult, Contains.Substring("App Information"));
        
        // Find an element using REPL command
        var findResult = App.ExecuteReplCommand("id CounterBtn");
        Assert.That(findResult, Is.Not.EqualTo("Element not found with ID: CounterBtn"));
        
        // Take a screenshot via REPL
        var screenshotResult = App.ExecuteReplCommand("screenshot repl_test.png");
        Assert.That(screenshotResult, Contains.Substring("Screenshot saved"));
    }
}