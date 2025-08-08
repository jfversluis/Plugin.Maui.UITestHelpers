using System.Drawing;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Interactions;
using OpenQA.Selenium.Interactions;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	/// <summary>
	/// Apple platform (iOS/macOS Catalyst) specific implementation for context menu actions using Appium.
	/// </summary>
	public class AppiumAppleContextMenuActions : ICommandExecutionGroup
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
		/// Initializes a new instance of the <see cref="AppiumAppleContextMenuActions"/> class.
		/// </summary>
		/// <param name="app">The Appium app instance.</param>
		public AppiumAppleContextMenuActions(AppiumApp app)
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
		/// Activates the context menu on the specified element using a swipe gesture.
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
				var target = _app.WaitForElement(element.Id);
				var rect = target.GetRect();
				var rootViewRect = GetRootViewRect(_app);

				var centerX = rect.Width / 2;
				var centerY = rect.Height / 2;
				var width = Math.Max(250, rect.Width);

				if ((rect.X + width) > rootViewRect.Width)
				{
					width = rootViewRect.Width - rect.X;
				}

				int fromX = (int)(rect.X + (0.95f * width));
				int fromY = centerY;
				int toX = (int)(rect.X + (0.05f * width));
				int toY = centerY;

				OpenQA.Selenium.Appium.Interactions.PointerInputDevice touchDevice = new OpenQA.Selenium.Appium.Interactions.PointerInputDevice(PointerKind.Touch);
				var dragSequence = new ActionSequence(touchDevice, 0);
				dragSequence.AddAction(touchDevice.CreatePointerMove(CoordinateOrigin.Viewport, fromX, fromY, TimeSpan.Zero));
				dragSequence.AddAction(touchDevice.CreatePointerDown(PointerButton.TouchContact));
				dragSequence.AddAction(touchDevice.CreatePointerMove(CoordinateOrigin.Viewport, toX, toY, TimeSpan.FromMilliseconds(250)));
				dragSequence.AddAction(touchDevice.CreatePointerUp(PointerButton.TouchContact));
				_app.Driver.PerformActions([dragSequence]);

				return CommandResponse.SuccessEmptyResponse;
			}

			return CommandResponse.FailedEmptyResponse;
		}

		/// <summary>
		/// Dismisses the context menu by tapping in the center of the screen.
		/// </summary>
		/// <param name="parameters">The parameters for the command (not used).</param>
		/// <returns>A <see cref="CommandResponse"/> indicating the result of the operation.</returns>
		protected CommandResponse DismissContextMenu(IDictionary<string, object> parameters)
		{
			try
			{
				var rootViewRect = GetRootViewRect(_app);

				var centerX = rootViewRect.Width / 2;
				var centerY = rootViewRect.Height / 2;

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