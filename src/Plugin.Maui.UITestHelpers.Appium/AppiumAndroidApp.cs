using System.Diagnostics;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	public class AppiumAndroidApp : AppiumApp, IAndroidApp
	{
		string? _backdoorForwardSerial;
		int? _backdoorForwardPort;

		private AppiumAndroidApp(Uri remoteAddress, IConfig config)
			: base(new AndroidDriver(remoteAddress, GetOptions(config)), config)
		{
			_commandExecutor.AddCommandGroup(new AppiumAndroidVirtualKeyboardActions(this));
			_commandExecutor.AddCommandGroup(new AppiumAndroidAlertActions(this));
			_commandExecutor.AddCommandGroup(new AppiumAndroidSpecificActions(this));
			_commandExecutor.AddCommandGroup(new AppiumAndroidContextMenuActions(this));
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

		/// <summary>
		/// Forwards a free port on the test machine to the backdoor port on the device with
		/// "adb forward", unless the "BackdoorHost" configuration property is set. adb is found
		/// through the "AdbPath" configuration property, ANDROID_HOME, ANDROID_SDK_ROOT or PATH.
		/// </summary>
		protected override Uri GetBackdoorAddress()
		{
			if (!string.IsNullOrWhiteSpace(Config.GetProperty<string>("BackdoorHost")))
				return base.GetBackdoorAddress();

			if (_backdoorForwardPort is int existingPort)
				return new UriBuilder(Uri.UriSchemeHttp, "127.0.0.1", existingPort).Uri;

			var serial = GetDeviceSerial();
			var output = RunAdb(serial, "forward", "tcp:0", $"tcp:{BackdoorPort}");
			if (!int.TryParse(output.Trim(), out var hostPort))
				throw new BackdoorException($"Could not forward the backdoor port with adb; 'adb forward' returned: {output}");

			_backdoorForwardSerial = serial;
			_backdoorForwardPort = hostPort;

			return new UriBuilder(Uri.UriSchemeHttp, "127.0.0.1", hostPort).Uri;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && _backdoorForwardPort is int port)
			{
				try
				{
					RunAdb(_backdoorForwardSerial, "forward", "--remove", $"tcp:{port}");
				}
				catch (Exception)
				{
					// The device may be gone already. Cleaning up the backdoor must never keep
					// the Appium session from closing.
				}

				_backdoorForwardPort = null;
			}

			base.Dispose(disposing);
		}

		string? GetDeviceSerial()
		{
			var serial = Config.GetProperty<string>("DeviceSerial");
			if (!string.IsNullOrWhiteSpace(serial))
				return serial;

			// UiAutomator2 reports the serial of the device it uses in the session capabilities
			return (_driver.Capabilities.GetCapability("deviceUDID") ?? _driver.Capabilities.GetCapability("udid"))?.ToString();
		}

		string RunAdb(string? serial, params string[] arguments)
		{
			var startInfo = new ProcessStartInfo(GetAdbPath())
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			if (!string.IsNullOrWhiteSpace(serial))
			{
				startInfo.ArgumentList.Add("-s");
				startInfo.ArgumentList.Add(serial);
			}

			foreach (var argument in arguments)
				startInfo.ArgumentList.Add(argument);

			try
			{
				using var process = Process.Start(startInfo) ?? throw new BackdoorException($"Could not start '{startInfo.FileName}'.");
				var stdout = process.StandardOutput.ReadToEndAsync();
				var stderr = process.StandardError.ReadToEndAsync();
				if (!process.WaitForExit(30_000))
				{
					process.Kill(true);
					process.WaitForExit();
					throw new BackdoorException($"'adb {string.Join(' ', startInfo.ArgumentList)}' did not finish in time.");
				}

				if (process.ExitCode != 0)
					throw new BackdoorException($"'adb {string.Join(' ', startInfo.ArgumentList)}' failed with exit code {process.ExitCode}: {stderr.Result}{stdout.Result}");

				return stdout.Result;
			}
			catch (System.ComponentModel.Win32Exception ex)
			{
				throw new BackdoorException($"Could not run adb ('{startInfo.FileName}'), which is needed to reach the backdoor on Android. Set ANDROID_HOME or the \"AdbPath\" configuration property.", ex);
			}
		}

		string GetAdbPath()
		{
			var configured = Config.GetProperty<string>("AdbPath");
			if (!string.IsNullOrWhiteSpace(configured))
				return configured;

			var executable = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
			foreach (var variable in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
			{
				var sdk = Environment.GetEnvironmentVariable(variable);
				if (string.IsNullOrWhiteSpace(sdk))
					continue;

				var path = Path.Combine(sdk, "platform-tools", executable);
				if (File.Exists(path))
					return path;
			}

			return executable;
		}

		private static AppiumOptions GetOptions(IConfig config)
		{
			config.SetProperty("PlatformName", "Android");
			config.SetProperty("AutomationName", "UIAutomator2");

			var appId = config.GetProperty<string>("AppId");
			var appActivity = config.GetProperty<string>("AppActivity");

			var options = new AppiumOptions();

			SetGeneralAppiumOptions(config, options);

			if (!string.IsNullOrWhiteSpace(appId))
			{
				if (!config.GetProperty<bool>("FullReset"))
					options.AddAdditionalAppiumOption(MobileCapabilityType.NoReset, "true");

				options.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppPackage, appId);
			}

			if (!string.IsNullOrWhiteSpace(appActivity))
			{
				options.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AppActivity, appActivity);
			}

			var deviceSerial = config.GetProperty<string>("DeviceSerial");
			if (!string.IsNullOrWhiteSpace(deviceSerial))
			{
				options.AddAdditionalAppiumOption(MobileCapabilityType.Udid, deviceSerial);
			}

			SetAdditionalAppiumOptions(config, options);

			return options;
		}
	}
}