using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Core;
using Plugin.Maui.UITestHelpers.Appium;

namespace UITests.Shared;

public class AppiumActionsTests : BaseTest
{
    public AppiumActionsTests(TestDevice testDevice) : base(testDevice)
    {
    }

    [Test]
    public void GetOrientationTest()
    {
        // Act & Assert - should not throw
        var orientation = App.GetOrientation();
        
        // Verify we get a valid orientation value
        Assert.That(orientation, Is.AnyOf(
            OpenQA.Selenium.ScreenOrientation.Portrait,
            OpenQA.Selenium.ScreenOrientation.Landscape
        ));
    }

    [Test]
    public void ClipboardTest()
    {
        // Only test on platforms that support clipboard operations
        if (App is not AppiumAndroidApp && App is not AppiumIOSApp)
        {
            Assert.Ignore("Clipboard operations are only supported on Android and iOS");
        }

        // Arrange
        const string testText = "Test clipboard content";
        
        // Act
        App.SetClipboardText(testText);
        var retrievedText = App.GetClipboardText();
        
        // Assert
        Assert.That(retrievedText, Is.EqualTo(testText));
    }

    [Test]
    public void ToggleDataTest()
    {
        // Only test on Android
        if (App is not AppiumAndroidApp)
        {
            Assert.Ignore("ToggleData is only supported on Android");
        }

        // Act & Assert - should not throw
        Assert.DoesNotThrow(() => App.ToggleData());
    }

    [Test]
    public void GetSystemBarsTest()
    {
        // Only test on Android
        if (App is not AppiumAndroidApp)
        {
            Assert.Ignore("GetSystemBars is only supported on Android");
        }

        // Act
        var systemBars = App.GetSystemBars();
        
        // Assert
        Assert.That(systemBars, Is.Not.Null);
        Assert.That(systemBars, Is.Not.Empty);
    }
}