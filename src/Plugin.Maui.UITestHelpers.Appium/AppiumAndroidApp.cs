using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	public class AppiumAndroidApp : AppiumApp, IAndroidApp
	{
		private AppiumAndroidApp(Uri remoteAddress, IConfig config)
			: base(new AndroidDriver(remoteAddress, GetOptions(config)), config)
		{
			_commandExecutor.AddCommandGroup(new AppiumAndroidVirtualKeyboardActions(this));
			_commandExecutor.AddCommandGroup(new AppiumAndroidAlertActions(this));
			_commandExecutor.AddCommandGroup(new AppiumAndroidSpecificActions(this));
			_commandExecutor.AddCommandGroup(new AppiumAndroidThemeChangeAction());
        }

		public static AppiumAndroidApp CreateAndroidApp(Uri remoteAddress, IConfig config)
		{
			var device = config.GetProperty<string>("EmulatorDeviceName");
			var apkPath = config.GetProperty<string>("AppPath");
			var pkgName = config.GetProperty<string>("AppId");
			var outDir = config.GetProperty<string>("ReportDirectory");
			var enableDebugPopup = config.GetProperty<bool>("EnableDebugPopup");
			var avdForce = config.GetProperty<bool>("AvdForceInstall");

			if (enableDebugPopup)
				Environment.SetEnvironmentVariable("SWIFTSHADER_DISABLE_DEBUGGER_WAIT_DIALOG", "0");
			else
				Environment.SetEnvironmentVariable("SWIFTSHADER_DISABLE_DEBUGGER_WAIT_DIALOG", "1");

			//// Will check if AVD with device name already exists first and not reinstall
			//AndroidEmulator.AvdCreate(device, force: avdForce);
			//// StartEmulator will return immediately if emulator is already running
			//AndroidEmulator.StartEmulator(device);
			//// TODO: Check for installed package first?
			//AndroidEmulator.InstallPackage(apkPath, pkgName, outDir);
			var androidApp = new AppiumAndroidApp(remoteAddress, config);
			androidApp.Driver.ActivateApp(pkgName);
			return androidApp;
		}

		public override IUIElementQueryable Query => new AppiumAndroidQueryable(this);

		public override ApplicationState AppState
		{
			get
			{
				var appId = Config.GetProperty<string>("AppId") ?? throw new InvalidOperationException($"{nameof(AppState)} could not get the appid property");
				var state = _driver?.ExecuteScript("mobile: queryAppState", new Dictionary<string, object>
						{
							{ "appId", appId },
						});

				// https://github.com/appium/appium-uiautomator2-driver#mobile-queryappstate
				if (state == null)
				{
					return ApplicationState.Unknown;
				}

				return Convert.ToInt32(state) switch
				{
					0 => ApplicationState.NotInstalled,
					1 => ApplicationState.NotRunning,
					3 or
					4 => ApplicationState.Running,
					_ => ApplicationState.Unknown,
				};
			}
		}

		private static AppiumOptions GetOptions(IConfig config)
		{
			config.SetProperty("PlatformName", "Android");
			config.SetProperty("AutomationName", "UIAutomator2");
			var appId = config.GetProperty<string>("AppId");

            var ignoreHiddenApiPolicyError = config.GetProperty<bool?>("IgnoreHiddenApiPolicyError");

            var systemPort = config.GetProperty<int?>("SystemPort");
            var skipServerInstallation = config.GetProperty<bool?>("SkipServerInstallation");
            var serverLaunchTimeout = config.GetProperty<int?>("UiAutomator2ServerLaunchTimeout");
            var serverInstallTimeout = config.GetProperty<int?>("UiAutomator2ServerInstallTimeout");
            var serverReadTimeout = config.GetProperty<int?>("UiAutomator2ServerReadTimeout");
            var disableWindowAnimation = config.GetProperty<bool?>("DisableWindowAnimation");
            var skipDeviceInitialization = config.GetProperty<bool?>("SkipDeviceInitialization");

            var options = new AppiumOptions();

			SetGeneralAppiumOptions(config, options);

            if (!string.IsNullOrWhiteSpace(appId))
			{
				options.AddAdditionalAppiumOption(MobileCapabilityType.NoReset, "true");
				options.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, appId);
				options.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, $"{appId}.MainActivity");
            }

            if (ignoreHiddenApiPolicyError != null)
                options.AddAdditionalAppiumOption("ignoreHiddenApiPolicyError", ignoreHiddenApiPolicyError);

            if (systemPort != null)
                options.AddAdditionalAppiumOption("systemPort", systemPort);

            if (skipServerInstallation != null)
                options.AddAdditionalAppiumOption("skipServerInstallation", skipServerInstallation);

            if (serverLaunchTimeout != null)
                options.AddAdditionalAppiumOption("uiAutomator2ServerLaunchTimeout", serverLaunchTimeout);

            if (serverInstallTimeout != null)
                options.AddAdditionalAppiumOption("uiAutomator2ServerInstallTimeout", serverInstallTimeout);

            if (serverReadTimeout != null)
                options.AddAdditionalAppiumOption("uiAutomator2ServerReadTimeout", serverReadTimeout);

            if (disableWindowAnimation != null)
                options.AddAdditionalAppiumOption("disableWindowAnimation", disableWindowAnimation);

            if (skipDeviceInitialization != null)
                options.AddAdditionalAppiumOption("skipDeviceInitialization", skipDeviceInitialization);


            return options;
		}
	}
}
