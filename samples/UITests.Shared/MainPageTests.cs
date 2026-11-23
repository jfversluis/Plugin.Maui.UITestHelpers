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
    public void ExistsTest()
    {
        const string elementId = "CounterBtn";

        // Arrange
        App.WaitForElement("MainPage");

        // Act
        bool elementExistsById = App.Exists(elementId);
        bool elementExistsByText = App.Exists("Hello, World!");

        // Assert
        Assert.That(elementExistsById, Is.True, "element must have been found by AutomationId");
        Assert.That(elementExistsByText, Is.True, "element must have been found by text");
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
    public void Marked_AutomationId_FindsElement()
    {
        const string elementId = "CounterBtn";

        // Arrange
        App.WaitForElement(elementId);

        // Act
        var element = App.FindElement(App.Marked(elementId));

        // Assert
        Assert.That(element, Is.Not.Null, "element must have been found by AutomationId");
    }

    [Test]
    public void Marked_Text_FindsElement()
    {
        // Arrange
        App.WaitForElement("CounterBtn");

        // Act
        var element = App.FindElement(App.Marked("Hello, World!"));

        // Assert
        Assert.That(element, Is.Not.Null, "element must have been found by text");
    }

    [Test]
    public void Marked_UnknownValue_FindsNoElement()
    {
        // Arrange
        App.WaitForElement("CounterBtn");

        // Act
        var elements = App.FindElements(App.Marked("DoesNotExist"));

        // Assert
        Assert.That(elements, Is.Empty, "no element must have been found");
    }

    [Test]
    public void Marked_WaitForElement_FindsElement()
    {
        const string elementId = "CB1";

        // Act
        var element = App.WaitForElement(App.Marked(elementId));

        // Assert
        Assert.That(element, Is.Not.Null, "WaitForElement must have found the element via Marked()");
        Assert.That(element.IsChecked(App.GetTestDevice()), Is.True, "the found element must be the checked check box");
    }

    [Test]
    public void CombinedQuery_FindsElementWithinElement()
    {
        // Arrange
        App.WaitForElement("CB2");

        // Act: CB2 within the page's ScrollView; IQuery's methods refine a query
        IQuery scrollView = AppiumQuery.ByClass(GetScrollViewClass());
        IQuery checkBoxInScrollView = CheckBoxQuery(scrollView, "CB2");

        var elements = App.FindElements(checkBoxInScrollView);
        var element = App.FindElement(checkBoxInScrollView);

        // Assert
        Assert.That(elements.Select(GetAutomationId), Is.EqualTo(new[] { "CB2" }), "FindElements must run all parts of the query");
        Assert.That(element, Is.Not.Null, "FindElement must find the check box");
        Assert.That(GetAutomationId(element), Is.EqualTo("CB2"));
    }

    [Test]
    public void CombinedQuery_ElementNotWithinElement_FindsNothing()
    {
        // Arrange
        App.WaitForElement("CB2");

        // Act: CB2 isn't within CB1, so the second part of the query finds nothing
        IQuery firstCheckBox = CheckBoxQuery(null, "CB1");
        IQuery secondWithinFirst = CheckBoxQuery(firstCheckBox, "CB2");

        // Assert
        Assert.That(App.FindElements(firstCheckBox), Has.Count.EqualTo(1), "the first part alone must find CB1");
        Assert.That(App.FindElements(secondWithinFirst), Is.Empty);
        Assert.That(App.FindElement(secondWithinFirst), Is.Null);
    }

    /// <summary>
    /// Returns a query for a check box by its AutomationId, optionally within another query: on Android by
    /// the resource ID including the app's package, on the other platforms by the accessibility ID.
    /// </summary>
    IQuery CheckBoxQuery(IQuery? within, string automationId)
    {
        if (App is AppiumAndroidApp)
        {
            string id = $"{App.Config.GetProperty<string>("AppId")}:id/{automationId}";
            return within?.ById(id) ?? AppiumQuery.ById(id);
        }

        return within?.ByAccessibilityId(automationId) ?? AppiumQuery.ByAccessibilityId(automationId);
    }

    string GetScrollViewClass()
    {
        // the class of the page's ScrollView, as reported by each platform
        return App switch
        {
            AppiumAndroidApp => "android.widget.ScrollView",
            AppiumWindowsApp => "ScrollViewer",
            _ => "XCUIElementTypeScrollView",
        };
    }

    string? GetAutomationId(IUIElement element)
    {
        // the attribute that contains the AutomationId differs per platform
        return App switch
        {
            AppiumAndroidApp => element.GetAttribute<string>("resource-id")?.Split(":id/").Last(),
            AppiumWindowsApp => element.GetAttribute<string>("AutomationId"),
            AppiumCatalystApp => element.GetAttribute<string>("identifier"),
            _ => element.GetAttribute<string>("name"),
        };
    }
}
