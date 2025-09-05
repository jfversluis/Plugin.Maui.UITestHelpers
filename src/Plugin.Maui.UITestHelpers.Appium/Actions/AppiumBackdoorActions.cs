using OpenQA.Selenium.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	/// <summary>
	/// Provides backdoor method invocation capabilities for Appium-based UI tests.
	/// Backdoors allow tests to invoke app methods directly without going through the UI,
	/// similar to Xamarin.UITest's backdoor functionality.
	/// </summary>
	public class AppiumBackdoorActions : ICommandExecutionGroup
	{
		const string InvokeCommand = "invoke";

		readonly AppiumApp _appiumApp;

		readonly List<string> _commands = new()
		{
			InvokeCommand
		};

		public AppiumBackdoorActions(AppiumApp appiumApp)
		{
			_appiumApp = appiumApp ?? throw new ArgumentNullException(nameof(appiumApp));
		}

		public bool IsCommandSupported(string commandName)
		{
			return _commands.Contains(commandName, StringComparer.OrdinalIgnoreCase);
		}

		public CommandResponse Execute(string commandName, IDictionary<string, object> parameters)
		{
			return commandName switch
			{
				InvokeCommand => Invoke(parameters),
				_ => CommandResponse.FailedEmptyResponse,
			};
		}

		CommandResponse Invoke(IDictionary<string, object> parameters)
		{
			try
			{
				var methodName = parameters.TryGetValue("methodName", out var methodNameObj) ? methodNameObj as string : null;
				var args = parameters.TryGetValue("args", out var argsObj) ? argsObj as object[] ?? Array.Empty<object>() : Array.Empty<object>();

				if (string.IsNullOrEmpty(methodName))
				{
					return CommandResponse.FailedEmptyResponse;
				}

				// Create the script parameters for the backdoor invocation
				var scriptParams = new Dictionary<string, object>
				{
					["command"] = "backdoor",
					["methodName"] = methodName,
					["args"] = args
				};

				// Execute the script to invoke the backdoor method
				// This uses Appium's executeScript capability which can be customized 
				// by the app under test to handle backdoor method invocations
				var result = _appiumApp.Driver.ExecuteScript("mobile: backdoor", scriptParams);

				return new CommandResponse(result, CommandResponseResult.Success);
			}
			catch (Exception ex)
			{
				// Log the exception for debugging purposes
				System.Diagnostics.Debug.WriteLine($"Backdoor invocation failed: {ex.Message}");
				return CommandResponse.FailedEmptyResponse;
			}
		}
	}
}