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

        if (_testDevice == TestDevice.iOS)
        {
            config. SetProperty("DeviceName", "iPhone 15 Pro");
            config.SetProperty("PlatformVersion", "17.2");
        }

        return config;
    }
}