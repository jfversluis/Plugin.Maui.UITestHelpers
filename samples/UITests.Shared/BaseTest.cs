using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Core;
using System.Diagnostics;
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

        var appIdentifierKey = "AppId";

        // Note: an app with this ID has to be deployed to the emulator/device you want to run it on
        var appIdentifier = "com.companyname.uitesthelperssample";

        if (_testDevice == TestDevice.Windows)
        {
            appIdentifierKey = "AppPath";

            // Note: a release build has to be done and the path to this .exe file should exist. Tweak this path if necessary
            var absolutePath = Path.GetFullPath("..\\..\\..\\..\\Plugin.Maui.UITestHelpers.Sample\\bin\\Release\\net8.0-windows10.0.19041.0\\win10-x64\\Plugin.Maui.UITestHelpers.Sample.exe");
            appIdentifier = absolutePath;
        }

        // If the app ID is provided through an environment variable, like through CI, use that instead
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPID")))
        {
            appIdentifier = Environment.GetEnvironmentVariable("APPID");
        }

        config.SetProperty(appIdentifierKey, appIdentifier);

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