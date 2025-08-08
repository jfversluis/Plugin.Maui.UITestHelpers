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
}