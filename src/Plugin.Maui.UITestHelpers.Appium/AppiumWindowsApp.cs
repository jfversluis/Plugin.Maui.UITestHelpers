﻿using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	public class AppiumWindowsApp : AppiumApp, IWindowsApp
	{
		public AppiumWindowsApp(Uri remoteAddress, IConfig config)
			: base(new WindowsDriver(remoteAddress, GetOptions(config)), config)
		{
			_commandExecutor.AddCommandGroup(new AppiumWindowsContextMenuActions(this));
		}

		public override ApplicationState AppState
		{
			get
			{
				try
				{
					_ = _driver.CurrentWindowHandle;
					return ApplicationState.Running;
				}
				catch (NoSuchWindowException)
				{
					return ApplicationState.NotRunning;
				}
			}
		}

#nullable disable
		public override IUIElement FindElement(string id)
		{
			return Query.ByAccessibilityId(id).FirstOrDefault();
		}
#nullable enable

		public override IReadOnlyCollection<IUIElement> FindElements(string id)
		{
			return Query.ByAccessibilityId(id);
		}

		private static AppiumOptions GetOptions(IConfig config)
		{
			config.SetProperty("PlatformName", "Windows");
			config.SetProperty("AutomationName", "Windows");
			config.SetProperty("DeviceName", "WindowsPC");

			var options = new AppiumOptions();
			SetGeneralAppiumOptions(config, options);
			return options;
		}
	}
}
