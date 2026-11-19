using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Appium.iOS;
using OpenQA.Selenium.Appium.Mac;
using OpenQA.Selenium.Appium.Windows;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	public abstract class AppiumApp : IApp, IScreenshotSupportedApp, ILogsSupportedApp, IBackdoorSupportedApp
	{
		protected readonly AppiumDriver _driver;
		protected readonly IConfig _config;
		protected readonly AppiumCommandExecutor _commandExecutor;

		public AppiumApp(AppiumDriver driver, IConfig config)
		{
			_driver = driver ?? throw new ArgumentNullException(nameof(driver));
			_config = config ?? throw new ArgumentNullException(nameof(config));

			_commandExecutor = new AppiumCommandExecutor();
			_commandExecutor.AddCommandGroup(new AppiumDeviceActions(this));
			_commandExecutor.AddCommandGroup(new AppiumMouseActions(this));
			_commandExecutor.AddCommandGroup(new AppiumTouchActions(this));
			_commandExecutor.AddCommandGroup(new AppiumTextActions());
			_commandExecutor.AddCommandGroup(new AppiumGeneralActions());
			_commandExecutor.AddCommandGroup(new AppiumClipboardActions(this));
			_commandExecutor.AddCommandGroup(new AppiumVirtualKeyboardActions(this));
			_commandExecutor.AddCommandGroup(new AppiumPinchToZoomActions(this));
			_commandExecutor.AddCommandGroup(new AppiumSliderActions(this));
			_commandExecutor.AddCommandGroup(new AppiumSwipeActions(this));
			_commandExecutor.AddCommandGroup(new AppiumScrollActions(this));
			_commandExecutor.AddCommandGroup(new AppiumOrientationActions(this));
			_commandExecutor.AddCommandGroup(new AppiumLifecycleActions(this));
			_commandExecutor.AddCommandGroup(new AppiumBackdoorActions(this));
		}

		public abstract ApplicationState AppState { get; }
		public virtual IUIElementQueryable Query => new AppiumQueryable(this);
		public IConfig Config => _config;
		public AppiumDriver Driver => _driver;
		public ICommandExecution CommandExecutor => _commandExecutor;
		public string ElementTree => _driver.PageSource;

		public FileInfo Screenshot(string fileName)
		{
			Screenshot screenshot = _driver.GetScreenshot();
			screenshot.SaveAsFile(fileName);
			var file = new FileInfo(fileName);
			return file;
		}

		public byte[] Screenshot()
		{
			Screenshot screenshot = _driver.GetScreenshot();
			return screenshot.AsByteArray;
		}

		public IEnumerable<string> GetLogTypes()
		{
			return _driver.Manage().Logs.AvailableLogTypes;
		}

		public IEnumerable<string> GetLogEntries(string logType)
		{
			var entries = _driver.Manage().Logs.GetLog(logType);
			foreach (var entry in entries)
			{
				yield return entry.Message;
			}
		}

		public object? Invoke(string methodName, params object[] args)
		{
			var response = CommandExecutor.Execute("invoke", new Dictionary<string, object>
			{
				{ "methodName", methodName },
				{ "args", args }
			});

			return response.Result == CommandResponseResult.Success ? response.Value : null;
		}

		public T? Invoke<T>(string methodName, params object[] args)
		{
			var result = Invoke(methodName, args);
			
			if (result == null)
				return default(T);

			try
			{
				// Handle simple type conversions
				if (result is T directResult)
					return directResult;

				// Handle JSON string results that need deserialization
				if (result is string jsonString && typeof(T) != typeof(string))
				{
					return System.Text.Json.JsonSerializer.Deserialize<T>(jsonString);
				}

				// Attempt conversion for value types
				return (T)Convert.ChangeType(result, typeof(T));
			}
			catch
			{
				// If conversion fails, return default
				return default(T);
			}
		}

#nullable disable
		public virtual IUIElement FindElement(string id)
		{
			return Query.ById(id).FirstOrDefault();
		}
#nullable enable

		public virtual IUIElement FindElement(IQuery query)
		{
			AppiumQuery? appiumQuery = query as AppiumQuery;
			if (appiumQuery is not null)
			{
				return appiumQuery.FindElement(this);
			}

			var queryString = query.ToString() ?? throw new InvalidOperationException($"{nameof(FindElement)} could not get query string");
			var q = new AppiumQuery(queryString);
			return q.FindElement(this);
		}

#nullable disable
        public virtual IUIElement FindElementByText(string text)
        {
            return AppiumQuery.ByXPath(GetTextXPath(text)).FindElement(this);
        }
#nullable enable

        public virtual IReadOnlyCollection<IUIElement> FindElements(string id)
		{
			return Query.ById(id);
		}

        public virtual IReadOnlyCollection<IUIElement> FindElements(IQuery query)
		{
			AppiumQuery? appiumQuery = query as AppiumQuery;
			if (appiumQuery is not null)
			{
				return appiumQuery.FindElements(this);
			}

			var queryString = query.ToString() ?? throw new InvalidOperationException($"{nameof(FindElement)} could not get query string");
			var q = new AppiumQuery(queryString);
			return q.FindElements(this);
		}

        public virtual IReadOnlyCollection<IUIElement> FindElementsByText(string text)
        {
            return AppiumQuery.ByXPath(GetTextXPath(text)).FindElements(this);
        }

        /// <summary>
        /// Returns the XPath that finds elements by their displayed text. The attribute that
        /// contains the text differs per platform: Android (UiAutomator2) uses "text", iOS
        /// (XCUITest) uses "label" and "value", Windows (UI Automation) uses "Name".
        /// </summary>
        /// <param name="text">Text to search for.</param>
        /// <returns>XPath expression.</returns>
        protected virtual string GetTextXPath(string text)
        {
            string literal = ToXPathLiteral(text);

            return _driver switch
            {
                IOSDriver => $"//*[@label={literal} or @value={literal}]",
                WindowsDriver => $"//*[@Name={literal}]",
                _ => $"//*[@text={literal}]",
            };
        }

        /// <summary>
        /// Returns the XPath that finds elements "marked" with the given value, like
        /// Xamarin.UITest's Marked(): elements whose AutomationId, accessibility label or
        /// content description, or displayed text equals the value.
        /// </summary>
        /// <remarks>
        /// The attributes searched depend on the platform:
        /// <list type="bullet">
        /// <item><description>Android (UiAutomator2): "resource-id" (as
        /// "&lt;AppId&gt;:id/&lt;AutomationId&gt;", only when the AppId is configured),
        /// "content-desc" and "text"</description></item>
        /// <item><description>iOS (XCUITest): "name", "label" and "value"</description></item>
        /// <item><description>Mac Catalyst (Mac2): "identifier", "label", "title" and "value"</description></item>
        /// <item><description>Windows (UI Automation): "AutomationId" and "Name"</description></item>
        /// <item><description>Other platforms: the displayed text, see <see cref="GetTextXPath"/></description></item>
        /// </list>
        /// The value is escaped, so it may contain single and double quotes. Override this
        /// method to support further platforms or attributes.
        /// </remarks>
        /// <param name="marked">AutomationId or text to search for.</param>
        /// <returns>XPath expression that searches the whole element tree.</returns>
        protected internal virtual string GetMarkedXPath(string marked)
        {
            string literal = ToXPathLiteral(marked);

            switch (_driver)
            {
                case AndroidDriver:
                    // .NET MAUI exposes the AutomationId as resource-id "<package>:id/<AutomationId>"
                    string? appId = Config.GetProperty<string>("AppId");
                    string resourceId = string.IsNullOrWhiteSpace(appId)
                        ? string.Empty
                        : $"@resource-id={ToXPathLiteral($"{appId}:id/{marked}")} or ";
                    return $"//*[{resourceId}@content-desc={literal} or @text={literal}]";
                case IOSDriver:
                    return $"//*[@name={literal} or @label={literal} or @value={literal}]";
                case MacDriver:
                    return $"//*[@identifier={literal} or @label={literal} or @title={literal} or @value={literal}]";
                case WindowsDriver:
                    return $"//*[@AutomationId={literal} or @Name={literal}]";
                default:
                    return GetTextXPath(marked);
            }
        }

        /// <summary>
        /// Converts a string to an XPath 1.0 string literal; texts containing both kinds of
        /// quotes are combined with concat().
        /// </summary>
        /// <param name="value">String value.</param>
        /// <returns>XPath string literal.</returns>
        protected static string ToXPathLiteral(string value)
        {
            if (!value.Contains('\''))
            {
                return $"'{value}'";
            }

            if (!value.Contains('"'))
            {
                return $"\"{value}\"";
            }

            return "concat('" + value.Replace("'", "', \"'\", '") + "')";
        }

        protected static void SetGeneralAppiumOptions(IConfig config, AppiumOptions appiumOptions)
		{
			appiumOptions.AddAdditionalAppiumOption("reportDirectory", config.GetProperty<string>("ReportDirectory"));
			appiumOptions.AddAdditionalAppiumOption("reportFormat", config.GetProperty<string>("ReportFormat"));

			var testName = config.GetProperty<string>("TestName");
			if (!string.IsNullOrWhiteSpace(testName))
				appiumOptions.AddAdditionalAppiumOption("testName", testName);

			if (config.GetProperty<bool>("FullReset"))
				appiumOptions.AddAdditionalAppiumOption(MobileCapabilityType.FullReset, "true");

			if (config.GetProperty<bool>("NoReset"))
				appiumOptions.AddAdditionalAppiumOption(MobileCapabilityType.NoReset, "true");

			var appPath = config.GetProperty<string>("AppPath");
			if (!string.IsNullOrEmpty(appPath))
				appiumOptions.App = appPath;

			appiumOptions.PlatformName = config.GetProperty<string>("PlatformName");
			appiumOptions.AutomationName = config.GetProperty<string>("AutomationName");

			var deviceName = config.GetProperty<string>("DeviceName");
			if (!string.IsNullOrEmpty(deviceName))
				appiumOptions.DeviceName = deviceName;

			var platformVersion = config.GetProperty<string>("PlatformVersion");
			if (!string.IsNullOrEmpty(platformVersion))
				appiumOptions.PlatformVersion = platformVersion;

			appiumOptions.AddAdditionalAppiumOption(MobileCapabilityType.NewCommandTimeout, 3000);
		}

		public void Dispose()
		{
			_driver?.Dispose();
		}
	}
}
