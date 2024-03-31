using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Core;
using UITest.Appium.NUnit;

namespace UITests.Shared;

#if ANDROID
[TestFixture(TestDevice.Android)]
#elif IOS
[TestFixture(TestDevice.iOS)]
#endif
public abstract class BaseTest : UITestBase
{
    public BaseTest(TestDevice testDevice) : base(testDevice)
    {
    }

    public override IConfig GetTestConfig()
    {
        var config = new Config();

        // Note: an app with this ID has to be deployed to the emulator/device you want to run it on
        config.SetProperty("AppId", "com.companyname.uitesthelperssample");
        config.SetProperty("noReset", "true");

        if (_testDevice == TestDevice.iOS)
        {
            // Note: this is passed down from the GitHub Action. If nothing is set, fall back to a default value below
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SIMID"))
                && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SIMNAME")))
            {
                config.SetProperty("DeviceName", Environment.GetEnvironmentVariable("SIMNAME"));
                config.SetProperty("udid", Environment.GetEnvironmentVariable("SIMID"));
                config.SetProperty("PlatformVersion", "16.4");
            }
            else
            {
                config.SetProperty("DeviceName", "iPhone 15 Pro");
                config.SetProperty("PlatformVersion", "17.2");
            }
        }

        return config;
    }
}