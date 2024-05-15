using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Core;
using UITest.Appium.NUnit;

namespace UITests.Shared;

#if ANDROID
[TestFixture(TestDevice.Android)]
#elif IOS
[TestFixture(TestDevice.iOS)]
#elif MACOS
[TestFixture(TestDevice.Mac)]
#elif WINDOWS
[TestFixture(TestDevice.Windows)]
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
        if (_testDevice == TestDevice.Windows)
        {
            config.SetProperty("AppPath", "com.companyname.uitesthelperssample_9zz4h110yvjzm!App");
        }
        else
        {
            config.SetProperty("AppId", "com.companyname.uitesthelperssample");
        }

        // If the app ID is provided through an environment variable, like through CI, use that instead
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPID")))
        {
            config.SetProperty("AppId", Environment.GetEnvironmentVariable("APPID"));
        }

        if (_testDevice == TestDevice.iOS)
        {
            // Note: this is passed down from the GitHub Action. If nothing is set, fall back to a default value below
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SIMID")))
            {
                config.SetProperty("Udid", Environment.GetEnvironmentVariable("SIMID"));
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