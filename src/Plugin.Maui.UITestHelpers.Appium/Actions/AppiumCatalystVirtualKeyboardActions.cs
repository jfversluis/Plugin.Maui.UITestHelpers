using OpenQA.Selenium;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	/// <summary>
	/// macOS Catalyst-specific implementation for virtual keyboard actions using Appium.
	/// </summary>
	public class AppiumCatalystVirtualKeyboardActions : AppiumVirtualKeyboardActions
	{
		readonly AppiumApp _appiumApp;

		/// <summary>
		/// Initializes a new instance of the <see cref="AppiumCatalystVirtualKeyboardActions"/> class.
		/// </summary>
		/// <param name="appiumApp">The Appium app instance.</param>
		public AppiumCatalystVirtualKeyboardActions(AppiumApp appiumApp)
			: base(appiumApp)
		{
			_appiumApp = appiumApp;
		}

		/// <summary>
		/// Presses the Enter key on the virtual keyboard using macOS keys API.
		/// </summary>
		/// <param name="parameters">The parameters for the command (not used).</param>
		/// <returns>A <see cref="CommandResponse"/> indicating the result of the operation.</returns>
		protected override CommandResponse PressEnter(IDictionary<string, object> parameters)
		{
			try
			{
				// https://developer.apple.com/documentation/xctest/xcuikeyboardkey?language=objc
				string[] keys = ["XCUIKeyboardKeyEnter"]; // Enter Key

				_appiumApp.Driver.ExecuteScript("macos: keys", new Dictionary<string, object>
 				{
 					{ "keys", keys },
 				});
			}
			catch (InvalidElementStateException)
			{
				return CommandResponse.FailedEmptyResponse;
			}

			return CommandResponse.SuccessEmptyResponse;
		}
	}
}