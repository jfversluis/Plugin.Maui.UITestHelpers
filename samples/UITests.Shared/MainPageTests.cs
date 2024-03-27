using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Core;
using Plugin.Maui.UITestHelpers.Appium;

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
		// Arrange
		// Find elements with the value of the AutomationId property
		var element = App.FindElement("CounterBtn");

		// Act
		element.Click();
		Task.Delay(500).Wait(); // Wait for the click to register and show up on the screenshot

		// Assert
		App.Screenshot($"{nameof(ClickCounterTest)}.png");
		Assert.That(element.GetText(), Is.EqualTo("Clicked 1 time"));
	}
}