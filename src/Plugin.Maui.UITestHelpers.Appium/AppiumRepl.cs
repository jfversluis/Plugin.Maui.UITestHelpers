using Plugin.Maui.UITestHelpers.Core;
using System.Text;
using System.Text.Json;

namespace Plugin.Maui.UITestHelpers.Appium
{
	/// <summary>
	/// Appium implementation of the REPL (Read-Eval-Print Loop) for interactive UI testing.
	/// </summary>
	public class AppiumRepl : IRepl
	{
		private readonly AppiumApp _app;
		private bool _isRunning;

		public AppiumRepl(AppiumApp app)
		{
			_app = app ?? throw new ArgumentNullException(nameof(app));
		}

		public void Start()
		{
			_isRunning = true;
			Console.WriteLine("Starting UI Test REPL for Appium...");
			Console.WriteLine("Type 'help' for available commands or 'exit' to quit.");
			Console.WriteLine(new string('=', 50));

			while (_isRunning)
			{
				Console.Write("uitest> ");
				var input = Console.ReadLine();

				if (string.IsNullOrWhiteSpace(input))
					continue;

				var result = ExecuteCommand(input.Trim());
				if (!string.IsNullOrEmpty(result))
				{
					Console.WriteLine(result);
				}
			}
		}

		public string ExecuteCommand(string command)
		{
			if (string.IsNullOrWhiteSpace(command))
				return "";

			var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var cmd = parts[0].ToLowerInvariant();

			try
			{
				return cmd switch
				{
					"help" or "?" => GetHelp(),
					"exit" or "quit" => HandleExit(),
					"tree" => GetElementTree(),
					"screenshot" or "ss" => TakeScreenshot(parts.Length > 1 ? parts[1] : null),
					"find" => FindElement(parts.Skip(1).ToArray()),
					"click" => ClickElement(parts.Skip(1).ToArray()),
					"text" => GetElementText(parts.Skip(1).ToArray()),
					"type" => TypeText(parts.Skip(1).ToArray()),
					"query" => QueryElements(parts.Skip(1).ToArray()),
					"logs" => GetLogs(parts.Length > 1 ? parts[1] : null),
					"clear" => ClearConsole(),
					"info" => GetAppInfo(),
					"xpath" => FindByXPath(parts.Skip(1).ToArray()),
					"id" => FindById(parts.Skip(1).ToArray()),
					"class" => FindByClass(parts.Skip(1).ToArray()),
					"name" => FindByName(parts.Skip(1).ToArray()),
					"accessibility" => FindByAccessibilityId(parts.Skip(1).ToArray()),
					_ => $"Unknown command: {cmd}. Type 'help' for available commands."
				};
			}
			catch (Exception ex)
			{
				return $"Error executing command: {ex.Message}";
			}
		}

		public void Stop()
		{
			_isRunning = false;
		}

		public string GetHelp()
		{
			var help = new StringBuilder();
			help.AppendLine("Available REPL commands:");
			help.AppendLine("");
			help.AppendLine("General Commands:");
			help.AppendLine("  help, ?                    - Show this help message");
			help.AppendLine("  exit, quit                 - Exit the REPL");
			help.AppendLine("  clear                      - Clear the console");
			help.AppendLine("  info                       - Show app information");
			help.AppendLine("");
			help.AppendLine("UI Inspection:");
			help.AppendLine("  tree                       - Show the current UI element tree");
			help.AppendLine("  screenshot [filename]      - Take a screenshot (alias: ss)");
			help.AppendLine("  logs [logtype]             - Show logs (optional logtype filter)");
			help.AppendLine("");
			help.AppendLine("Element Finding:");
			help.AppendLine("  find <selector>            - Find element using general selector");
			help.AppendLine("  id <id>                    - Find element by ID");
			help.AppendLine("  xpath <xpath>              - Find element by XPath");
			help.AppendLine("  class <classname>          - Find element by class name");
			help.AppendLine("  name <name>                - Find element by name");
			help.AppendLine("  accessibility <id>         - Find element by accessibility ID");
			help.AppendLine("  query <query>              - Execute a custom query");
			help.AppendLine("");
			help.AppendLine("Element Actions:");
			help.AppendLine("  click <selector>           - Click an element");
			help.AppendLine("  text <selector>            - Get text from an element");
			help.AppendLine("  type <selector> <text>     - Type text into an element");
			help.AppendLine("");
			help.AppendLine("Examples:");
			help.AppendLine("  id CounterBtn");
			help.AppendLine("  click CounterBtn");
			help.AppendLine("  text CounterBtn");
			help.AppendLine("  type MyEntry \"Hello World\"");
			help.AppendLine("  xpath //button[@text='Click me']");

			return help.ToString();
		}

		private string HandleExit()
		{
			Stop();
			return "Exiting REPL...";
		}

		private string GetElementTree()
		{
			try
			{
				return _app.ElementTree;
			}
			catch (Exception ex)
			{
				return $"Error getting element tree: {ex.Message}";
			}
		}

		private string TakeScreenshot(string? filename)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(filename))
				{
					filename = $"repl_screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
				}

				if (!filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
				{
					filename += ".png";
				}

				var file = _app.Screenshot(filename);
				return $"Screenshot saved to: {file.FullName}";
			}
			catch (Exception ex)
			{
				return $"Error taking screenshot: {ex.Message}";
			}
		}

		private string FindElement(string[] args)
		{
			if (args.Length == 0)
				return "Usage: find <selector>";

			try
			{
				var selector = string.Join(" ", args);
				var element = _app.FindElement(selector);
				
				if (element == null)
					return $"Element not found: {selector}";

				return FormatElementInfo(element);
			}
			catch (Exception ex)
			{
				return $"Error finding element: {ex.Message}";
			}
		}

		private string FindById(string[] args)
		{
			if (args.Length == 0)
				return "Usage: id <id>";

			try
			{
				var id = args[0];
				var element = _app.FindElement(id);
				
				if (element == null)
					return $"Element not found with ID: {id}";

				return FormatElementInfo(element);
			}
			catch (Exception ex)
			{
				return $"Error finding element by ID: {ex.Message}";
			}
		}

		private string FindByXPath(string[] args)
		{
			if (args.Length == 0)
				return "Usage: xpath <xpath_expression>";

			try
			{
				var xpath = string.Join(" ", args);
				var query = AppiumQuery.ByXPath(xpath);
				var element = query.FindElement(_app);
				
				if (element == null)
					return $"Element not found with XPath: {xpath}";

				return FormatElementInfo(element);
			}
			catch (Exception ex)
			{
				return $"Error finding element by XPath: {ex.Message}";
			}
		}

		private string FindByClass(string[] args)
		{
			if (args.Length == 0)
				return "Usage: class <classname>";

			try
			{
				var className = args[0];
				var query = AppiumQuery.ByClass(className);
				var element = query.FindElement(_app);
				
				if (element == null)
					return $"Element not found with class: {className}";

				return FormatElementInfo(element);
			}
			catch (Exception ex)
			{
				return $"Error finding element by class: {ex.Message}";
			}
		}

		private string FindByName(string[] args)
		{
			if (args.Length == 0)
				return "Usage: name <name>";

			try
			{
				var name = string.Join(" ", args);
				var query = AppiumQuery.ByName(name);
				var element = query.FindElement(_app);
				
				if (element == null)
					return $"Element not found with name: {name}";

				return FormatElementInfo(element);
			}
			catch (Exception ex)
			{
				return $"Error finding element by name: {ex.Message}";
			}
		}

		private string FindByAccessibilityId(string[] args)
		{
			if (args.Length == 0)
				return "Usage: accessibility <accessibility_id>";

			try
			{
				var accessibilityId = args[0];
				var query = AppiumQuery.ByAccessibilityId(accessibilityId);
				var element = query.FindElement(_app);
				
				if (element == null)
					return $"Element not found with accessibility ID: {accessibilityId}";

				return FormatElementInfo(element);
			}
			catch (Exception ex)
			{
				return $"Error finding element by accessibility ID: {ex.Message}";
			}
		}

		private string ClickElement(string[] args)
		{
			if (args.Length == 0)
				return "Usage: click <selector>";

			try
			{
				var selector = string.Join(" ", args);
				var element = _app.FindElement(selector);
				
				if (element == null)
					return $"Element not found: {selector}";

				element.Click();
				return $"Clicked element: {selector}";
			}
			catch (Exception ex)
			{
				return $"Error clicking element: {ex.Message}";
			}
		}

		private string GetElementText(string[] args)
		{
			if (args.Length == 0)
				return "Usage: text <selector>";

			try
			{
				var selector = string.Join(" ", args);
				var element = _app.FindElement(selector);
				
				if (element == null)
					return $"Element not found: {selector}";

				var text = element.GetText();
				return $"Text: \"{text}\"";
			}
			catch (Exception ex)
			{
				return $"Error getting element text: {ex.Message}";
			}
		}

		private string TypeText(string[] args)
		{
			if (args.Length < 2)
				return "Usage: type <selector> <text>";

			try
			{
				var selector = args[0];
				var text = string.Join(" ", args.Skip(1));
				
				// Remove quotes if present
				if (text.StartsWith("\"") && text.EndsWith("\"") && text.Length > 1)
				{
					text = text[1..^1];
				}

				var element = _app.FindElement(selector);
				
				if (element == null)
					return $"Element not found: {selector}";

				element.SendKeys(text);
				return $"Typed \"{text}\" into element: {selector}";
			}
			catch (Exception ex)
			{
				return $"Error typing text: {ex.Message}";
			}
		}

		private string QueryElements(string[] args)
		{
			if (args.Length == 0)
				return "Usage: query <query_string>";

			try
			{
				var queryString = string.Join(" ", args);
				var query = new AppiumQuery(queryString);
				var elements = query.FindElements(_app);
				
				if (!elements.Any())
					return $"No elements found for query: {queryString}";

				var result = new StringBuilder();
				result.AppendLine($"Found {elements.Count} element(s):");
				
				for (int i = 0; i < elements.Count; i++)
				{
					result.AppendLine($"[{i}] {FormatElementInfo(elements.ElementAt(i))}");
				}

				return result.ToString();
			}
			catch (Exception ex)
			{
				return $"Error executing query: {ex.Message}";
			}
		}

		private string GetLogs(string? logType)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(logType))
				{
					var logTypes = _app.GetLogTypes();
					return $"Available log types: {string.Join(", ", logTypes)}";
				}

				var logs = _app.GetLogEntries(logType);
				if (!logs.Any())
					return $"No logs found for type: {logType}";

				var result = new StringBuilder();
				result.AppendLine($"Logs for type '{logType}':");
				foreach (var log in logs)
				{
					result.AppendLine($"  {log}");
				}

				return result.ToString();
			}
			catch (Exception ex)
			{
				return $"Error getting logs: {ex.Message}";
			}
		}

		private string ClearConsole()
		{
			Console.Clear();
			return "";
		}

		private string GetAppInfo()
		{
			try
			{
				var info = new StringBuilder();
				info.AppendLine("App Information:");
				info.AppendLine($"  State: {_app.AppState}");
				info.AppendLine($"  Driver: {_app.Driver.GetType().Name}");
				
				// Get capabilities if available
				try
				{
					var capabilities = _app.Driver.Capabilities;
					info.AppendLine("  Capabilities:");
					
					// Try to get some common capabilities
					var platformName = capabilities.GetCapability("platformName");
					if (platformName != null)
						info.AppendLine($"    platformName: {platformName}");
						
					var deviceName = capabilities.GetCapability("deviceName");
					if (deviceName != null)
						info.AppendLine($"    deviceName: {deviceName}");
						
					var platformVersion = capabilities.GetCapability("platformVersion");
					if (platformVersion != null)
						info.AppendLine($"    platformVersion: {platformVersion}");
						
					var automationName = capabilities.GetCapability("automationName");
					if (automationName != null)
						info.AppendLine($"    automationName: {automationName}");
				}
				catch
				{
					info.AppendLine("  Capabilities: Not available");
				}

				return info.ToString();
			}
			catch (Exception ex)
			{
				return $"Error getting app info: {ex.Message}";
			}
		}

		private string FormatElementInfo(IUIElement element)
		{
			try
			{
				var info = new StringBuilder();
				
				var text = element.GetText();
				if (!string.IsNullOrEmpty(text))
					info.Append($"Text: \"{text}\" ");

				// Try to get additional properties if available
				if (element is AppiumDriverElement appiumElement)
				{
					try
					{
						var tagName = appiumElement.AppiumElement.TagName;
						if (!string.IsNullOrEmpty(tagName))
							info.Append($"Tag: {tagName} ");

						var enabled = appiumElement.AppiumElement.Enabled;
						info.Append($"Enabled: {enabled} ");

						var displayed = appiumElement.AppiumElement.Displayed;
						info.Append($"Displayed: {displayed} ");

						var location = appiumElement.AppiumElement.Location;
						var size = appiumElement.AppiumElement.Size;
						info.Append($"Location: ({location.X}, {location.Y}) Size: {size.Width}x{size.Height}");
					}
					catch
					{
						// Ignore errors getting additional properties
					}
				}

				return info.Length > 0 ? info.ToString().Trim() : "Element found (no additional info available)";
			}
			catch (Exception ex)
			{
				return $"Element found (error getting details: {ex.Message})";
			}
		}
	}
}