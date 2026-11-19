using System.Reflection;
using NUnit.Framework;
using OpenQA.Selenium.Appium;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace UITests.Android;

public class AppiumAndroidAdditionalCapabilitiesTests
{
    [Test]
    public void AdditionalCapabilitiesArePassedToAppium()
    {
        var config = new Config();
        config.SetProperty("AppId", "com.example.app");
        config.SetProperty("AdditionalCapabilities", new Dictionary<string, object>
        {
            ["systemPort"] = 8201,
            ["appium:disableWindowAnimation"] = true,
            ["goog:loggingPrefs"] = "browser",
            ["newCommandTimeout"] = 60,
        });

        var capabilities = GetOptions(config).ToCapabilities();

        Assert.That(capabilities.GetCapability("appium:systemPort"), Is.EqualTo(8201));
        Assert.That(capabilities.GetCapability("appium:disableWindowAnimation"), Is.EqualTo(true));
        Assert.That(capabilities.GetCapability("goog:loggingPrefs"), Is.EqualTo("browser"));
        Assert.That(capabilities.GetCapability("appium:newCommandTimeout"), Is.EqualTo(60), "should override the library default");
        Assert.That(capabilities.GetCapability("appium:appPackage"), Is.EqualTo("com.example.app"));
    }

    [Test]
    public void AdditionalCapabilitiesAcceptTypedDictionaries()
    {
        var config = new Config();
        config.SetProperty("AdditionalCapabilities", new Dictionary<string, int> { ["systemPort"] = 8202 });

        var capabilities = GetOptions(config).ToCapabilities();

        Assert.That(capabilities.GetCapability("appium:systemPort"), Is.EqualTo(8202));
    }

    [Test]
    public void AdditionalCapabilitiesMustBeADictionary()
    {
        var config = new Config();
        config.SetProperty("AdditionalCapabilities", "systemPort=8201");

        var exception = Assert.Throws<TargetInvocationException>(() => GetOptions(config));

        Assert.That(exception!.InnerException, Is.InstanceOf<InvalidOperationException>());
    }

    [TestCase("deviceName")]
    [TestCase("appium:app")]
    [TestCase("platformName")]
    [TestCase("browserName")]
    public void AdditionalCapabilitiesRejectUnsupportedCapabilities(string name)
    {
        var config = new Config();
        config.SetProperty("AdditionalCapabilities", new Dictionary<string, object> { [name] = "value" });

        var exception = Assert.Throws<TargetInvocationException>(() => GetOptions(config));

        Assert.That(exception!.InnerException, Is.InstanceOf<InvalidOperationException>());
        Assert.That(exception.InnerException!.Message, Does.Contain($"'{name}'"));
    }

    private static AppiumOptions GetOptions(Config config)
    {
        var method = typeof(AppiumAndroidApp).GetMethod("GetOptions", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Android options factory not found.");
        return (AppiumOptions)method.Invoke(null, new object[] { config })!;
    }
}
