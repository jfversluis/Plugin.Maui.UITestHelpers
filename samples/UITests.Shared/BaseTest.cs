using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Core;
using UITest.Appium.NUnit;

namespace UITests.Shared;

[TestFixture(TestDevice.Android)]
public abstract class BaseTest : UITestBase
{
    public BaseTest(TestDevice testDevice) : base(testDevice)
    {
        InitialSetup();
    }

    public override IConfig GetTestConfig()
    {
        var config = new Config();
        config.SetProperty("AppId", "com.companyname.uitesthelperssample");
        config.SetProperty("DeviceName", "pixel_5_-_api_33");

        return config;
    }
}