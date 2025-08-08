using System.Drawing;
using OpenQA.Selenium.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	/// <summary>
	/// Windows-specific implementation for context menu actions using Appium.
	/// </summary>
	public class AppiumWindowsContextMenuActions : ICommandExecutionGroup
	{
		const string ActivateContextMenuCommand = "activateContextMenu";
		const string DismissContextMenuCommand = "dismissContextMenu";

		protected readonly AppiumApp _app;
		readonly List<string> _commands = new()
		{
			ActivateContextMenuCommand,
			DismissContextMenuCommand,
		};

		/// <summary>
		/// Initializes a new instance of the <see cref="AppiumWindowsContextMenuActions"/> class.
		/// </summary>
		/// <param name="app">The Appium app instance.</param>
		public AppiumWindowsContextMenuActions(AppiumApp app)
		{
			_app = app;
		}

		/// <summary>
		/// Determines whether the specified command is supported by this action group.
		/// </summary>
		/// <param name="commandName">The name of the command to check.</param>
		/// <returns><see langword="true"/> if the command is supported; otherwise, <see langword="false"/>.</returns>
		public bool IsCommandSupported(string commandName)
		{
			return _commands.Contains(commandName, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Executes the specified command with the given parameters.
		/// </summary>
		/// <param name="commandName">The name of the command to execute.</param>
		/// <param name="parameters">The parameters for the command.</param>
		/// <returns>A <see cref="CommandResponse"/> indicating the result of the command execution.</returns>
		public CommandResponse Execute(string commandName, IDictionary<string, object> parameters)
		{
			return commandName switch
			{
				ActivateContextMenuCommand => ActivateContextMenu(parameters),
				DismissContextMenuCommand => DismissContextMenu(parameters),
				_ => CommandResponse.FailedEmptyResponse,
			};
		}

		/// <summary>
		/// Activates the context menu on the specified element using a right-click.
		/// </summary>
		/// <param name="parameters">The parameters containing the element to activate the context menu on.</param>
		/// <returns>A <see cref="CommandResponse"/> indicating the result of the operation.</returns>
		protected CommandResponse ActivateContextMenu(IDictionary<string, object> parameters)
		{
			parameters.TryGetValue("element", out var value);

			if (value is null)
				return CommandResponse.FailedEmptyResponse;

			string elementString = (string)value;
			var element = GetAppiumElement(elementString);

			// If cannot find an element by Id, just try to find using the text.
			if (element is null)
				element = _app.Driver.FindElement(OpenQA.Selenium.By.XPath("//*[@text='" + elementString + "']"));

			if (element is not null)
			{
				_app.Driver.ExecuteScript("windows: click", new Dictionary<string, object>
				{
					{ "elementId", element.Id },
					{ "button", "right" },
				});

				return CommandResponse.SuccessEmptyResponse;
			}

			return CommandResponse.FailedEmptyResponse;
		}

		/// <summary>
		/// Dismisses the context menu by clicking in the center of the screen.
		/// </summary>
		/// <param name="parameters">The parameters for the command (not used).</param>
		/// <returns>A <see cref="CommandResponse"/> indicating the result of the operation.</returns>
		protected CommandResponse DismissContextMenu(IDictionary<string, object> parameters)
		{
			try
			{
				var screenbounds = GetRootViewRect(_app);

				var centerX = screenbounds.Width / 2;
				var centerY = screenbounds.Height / 2;

				_app.TapCoordinates(centerX, centerY);

				return CommandResponse.SuccessEmptyResponse;
			}
			catch
			{
				return CommandResponse.FailedEmptyResponse;
			}
		}

		/// <summary>
		/// Gets an AppiumElement from the provided element object.
		/// </summary>
		/// <param name="element">The element object to convert.</param>
		/// <returns>An <see cref="AppiumElement"/> if conversion is successful; otherwise, <see langword="null"/>.</returns>
		static AppiumElement? GetAppiumElement(object element)
		{
			if (element is AppiumElement appiumElement)
			{
				return appiumElement;
			}
			else if (element is AppiumDriverElement driverElement)
			{
				return driverElement.AppiumElement;
			}

			return null;
		}

		/// <summary>
		/// Gets the rectangle bounds of the root view element.
		/// </summary>
		/// <param name="app">The Appium app instance.</param>
		/// <returns>A <see cref="Rectangle"/> representing the root view bounds.</returns>
		static Rectangle GetRootViewRect(AppiumApp app)
		{
			var rootElement = app.FindElement(AppiumQuery.ByXPath("/*"));
			var rootViewRect = rootElement.GetRect();

			return rootViewRect;
		}
	}
}