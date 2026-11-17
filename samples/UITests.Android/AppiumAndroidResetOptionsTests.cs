using System.Reflection;
using NUnit.Framework;
using OpenQA.Selenium.Appium;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace UITests.Android;

public class AppiumAndroidResetOptionsTests
{
    [TestCase(null, null, null, "true")]
    [TestCase(false, false, null, "true")]
    [TestCase(true, null, "true", null)]
    [TestCase(null, true, null, "true")]
    [TestCase(true, true, "true", "true")]
    public void ResetCapabilitiesFollowConfiguration(bool? fullReset, bool? noReset, string? expectedFullReset, string? expectedNoReset)
    {
        var config = new Config();
        config.SetProperty("AppId", "com.example.app");
        if (fullReset.HasValue)
            config.SetProperty("FullReset", fullReset.Value);
        if (noReset.HasValue)
            config.SetProperty("NoReset", noReset.Value);

        var capabilities = GetOptions(config).ToCapabilities();

        Assert.That(capabilities.GetCapability("appium:fullReset")?.ToString(), Is.EqualTo(expectedFullReset));
        Assert.That(capabilities.GetCapability("appium:noReset")?.ToString(), Is.EqualTo(expectedNoReset));
    }

    [Test]
    public void DefaultNoResetIsNotAddedWithoutAppId()
    {
        var capabilities = GetOptions(new Config()).ToCapabilities();

        Assert.That(capabilities.GetCapability("appium:noReset"), Is.Null);
    }

    private static AppiumOptions GetOptions(Config config)
    {
        var method = typeof(AppiumAndroidApp).GetMethod("GetOptions", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Android options factory not found.");
        return (AppiumOptions)method.Invoke(null, new object[] { config })!;
    }
}
