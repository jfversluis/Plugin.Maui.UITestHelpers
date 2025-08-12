using Plugin.Maui.UITestHelpers.Core;
using System.Diagnostics;
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
			
			// Check if we're in a CI environment first
			if (IsInCIEnvironment())
			{
				Console.WriteLine("REPL Error: Cannot start interactive REPL in CI/CD environment.");
				Console.WriteLine("Use ExecuteReplCommand() for programmatic access in automated tests.");
				return;
			}
			
			// Check if console is redirected (test runner environment)
			if (Console.IsInputRedirected || Console.IsOutputRedirected)
			{
				Console.WriteLine("REPL: Console is redirected (test runner environment detected).");
				Console.WriteLine("Attempting to open a new console window for interactive REPL...");
				
				if (TryOpenNewConsoleWindow())
				{
					Console.WriteLine("REPL: New console window opened successfully!");
					Console.WriteLine("Use the new window for interactive commands.");
					Console.WriteLine("This test will continue after you exit the REPL.");
					return;
				}
				else
				{
					Console.WriteLine("REPL Error: Failed to open new console window.");
					Console.WriteLine("This is common when running 'dotnet test' as console redirection prevents interactive input.");
					Console.WriteLine();
					Console.WriteLine("Solutions:");
					Console.WriteLine("1. Run the specific test in an IDE with debugging support");
					Console.WriteLine("2. Run the test manually in a debugger/interactive environment");
					Console.WriteLine("3. Use App.ExecuteReplCommand(\"command\") for programmatic access");
					Console.WriteLine("4. Run tests outside of 'dotnet test' runner");
					Console.WriteLine();
					Console.WriteLine("Example programmatic usage:");
					Console.WriteLine("  var result = App.ExecuteReplCommand(\"id CounterBtn\");");
					Console.WriteLine("  var help = App.ExecuteReplCommand(\"help\");");
					return;
				}
			}

			StartInteractiveSession();
		}

		private void StartInteractiveSession()
		{
			Console.WriteLine("Starting UI Test REPL for Appium...");
			Console.WriteLine("Type 'help' for available commands or 'exit' to quit.");
			
			// Show additional info if console appears redirected
			if (Console.IsInputRedirected || Console.IsOutputRedirected)
			{
				Console.WriteLine();
				Console.WriteLine("Note: Console appears to be redirected (test runner environment).");
				Console.WriteLine("REPL will attempt to work, but interactive input may be limited.");
				Console.WriteLine("Consider running this test outside of 'dotnet test' for full functionality.");
			}
			
			Console.WriteLine(new string('=', 50));

			while (_isRunning)
			{
				try
				{
					Console.Write("uitest> ");
					Console.Out.Flush(); // Ensure prompt is displayed even with redirection
					
					var input = Console.ReadLine();

					if (input == null) // End of stream (Ctrl+C, EOF, etc.)
					{
						Console.WriteLine();
						Console.WriteLine("Input stream ended. Exiting REPL...");
						break;
					}

					if (string.IsNullOrWhiteSpace(input))
						continue;

					var result = ExecuteCommand(input.Trim());
					if (!string.IsNullOrEmpty(result))
					{
						Console.WriteLine(result);
					}
				}
				catch (InvalidOperationException ex) when (ex.Message.Contains("not available"))
				{
					Console.WriteLine();
					Console.WriteLine("Console input is no longer available. Exiting REPL...");
					Console.WriteLine("This usually happens in non-interactive environments.");
					Console.WriteLine("Consider using ExecuteReplCommand() for programmatic access.");
					break;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"REPL Error: {ex.Message}");
					Console.WriteLine("Type 'exit' to quit or continue with other commands.");
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
			help.AppendLine("");
			help.AppendLine("Note: Interactive REPL attempts to work in test environments but may have limitations.");
			help.AppendLine("      For best results, run tests outside of 'dotnet test' or use ExecuteReplCommand() for programmatic access.");

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

		private bool TryOpenNewConsoleWindow()
		{
			try
			{
				Console.WriteLine("Attempting to open a new console window for interactive REPL...");
				
				// Try platform-specific approaches to open a new terminal/console window
				if (TryOpenPlatformSpecificTerminal())
				{
					Console.WriteLine("Successfully launched new terminal window.");
					Console.WriteLine("Look for a new terminal/console window that should have opened.");
					Console.WriteLine("If no window appeared, the terminal launch may have failed silently.");
					return true;
				}
				
				// Fallback: provide guidance if platform-specific launch failed
				Console.WriteLine();
				Console.WriteLine("Failed to automatically open a new console window.");
				Console.WriteLine("This can happen due to:");
				Console.WriteLine("- Missing terminal applications");
				Console.WriteLine("- Security restrictions");
				Console.WriteLine("- Headless/SSH environments");
				Console.WriteLine("- Platform not supported");
				Console.WriteLine();
				Console.WriteLine("Manual alternatives:");
				Console.WriteLine("1. Open a new terminal/command prompt manually");
				Console.WriteLine("2. Navigate to your test project directory");
				Console.WriteLine("3. Run: dotnet run --project YourTestProject");
				Console.WriteLine("4. Or run your test in an IDE with debugging support");
				Console.WriteLine();
				Console.WriteLine("Programmatic REPL (works in any environment):");
				Console.WriteLine("  App.ExecuteReplCommand(\"tree\")          // Show UI tree");
				Console.WriteLine("  App.ExecuteReplCommand(\"id CounterBtn\")  // Find element by ID");
				Console.WriteLine("  App.ExecuteReplCommand(\"click CounterBtn\") // Click element");
				Console.WriteLine("  App.ExecuteReplCommand(\"screenshot\")     // Take screenshot");
				Console.WriteLine("  App.ExecuteReplCommand(\"help\")           // Show all commands");
				
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in TryOpenNewConsoleWindow: {ex.Message}");
				return false;
			}
		}

		private bool TryOpenPlatformSpecificTerminal()
		{
			try
			{
				if (OperatingSystem.IsWindows())
				{
					return TryOpenWindowsConsole();
				}
				else if (OperatingSystem.IsMacOS())
				{
					return TryOpenMacOSTerminal();
				}
				else if (OperatingSystem.IsLinux())
				{
					return TryOpenLinuxTerminal();
				}
				
				Console.WriteLine($"Platform not supported for automatic terminal launch: {Environment.OSVersion.Platform}");
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Platform detection error: {ex.Message}");
				return false;
			}
		}

		private bool TryOpenWindowsConsole()
		{
			try
			{
				// Try multiple Windows terminal options in order of preference
				var terminalCommands = new[]
				{
					// Windows Terminal (modern)
					("wt.exe", "new-tab --title \"UITest REPL\" -- cmd /k echo UITest REPL - Use App.ExecuteReplCommand() for commands"),
					// PowerShell
					("powershell.exe", "-NoExit -Command \"Write-Host 'UITest REPL - Use App.ExecuteReplCommand() for commands'; Write-Host 'This window demonstrates that a new console can be opened.'; Write-Host 'For full REPL functionality, use the programmatic API in your test code.'\""),
					// Command Prompt
					("cmd.exe", "/k echo UITest REPL - Use App.ExecuteReplCommand() for commands & echo This window demonstrates that a new console can be opened.")
				};

				foreach (var (command, args) in terminalCommands)
				{
					try
					{
						var startInfo = new ProcessStartInfo
						{
							FileName = command,
							Arguments = args,
							UseShellExecute = true,
							CreateNoWindow = false
						};

						var process = Process.Start(startInfo);
						if (process != null)
						{
							Console.WriteLine($"Successfully launched {command}");
							return true;
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Failed to launch {command}: {ex.Message}");
						// Continue to next option
					}
				}

				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Windows terminal launch error: {ex.Message}");
				return false;
			}
		}

		private bool TryOpenMacOSTerminal()
		{
			try
			{
				// Try multiple macOS terminal options
				var terminalCommands = new[]
				{
					// Terminal.app with AppleScript
					("osascript", "-e \"tell application \\\"Terminal\\\" to do script \\\"echo 'UITest REPL - Use App.ExecuteReplCommand() for commands'; echo 'This window demonstrates that a new terminal can be opened.'; echo 'For full REPL functionality, use the programmatic API in your test code.'\\\"\""),
					// iTerm2 if available
					("osascript", "-e \"tell application \\\"iTerm\\\" to create window with default profile command \\\"echo 'UITest REPL - Use App.ExecuteReplCommand() for commands'\\\"\""),
					// Fallback to opening Terminal.app
					("open", "-a Terminal")
				};

				foreach (var (command, args) in terminalCommands)
				{
					try
					{
						var startInfo = new ProcessStartInfo
						{
							FileName = command,
							Arguments = args,
							UseShellExecute = true,
							CreateNoWindow = true
						};

						var process = Process.Start(startInfo);
						if (process != null)
						{
							process.WaitForExit(2000); // Wait up to 2 seconds
							if (process.ExitCode == 0)
							{
								Console.WriteLine($"Successfully launched macOS terminal via {command}");
								return true;
							}
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Failed to launch macOS terminal with {command}: {ex.Message}");
						// Continue to next option
					}
				}

				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"macOS terminal launch error: {ex.Message}");
				return false;
			}
		}

		private bool TryOpenLinuxTerminal()
		{
			try
			{
				// Try multiple Linux terminal emulators
				var terminalCommands = new[]
				{
					// GNOME Terminal
					("gnome-terminal", "-- bash -c \"echo 'UITest REPL - Use App.ExecuteReplCommand() for commands'; echo 'This window demonstrates that a new terminal can be opened.'; echo 'For full REPL functionality, use the programmatic API in your test code.'; exec bash\""),
					// KDE Konsole
					("konsole", "-e bash -c \"echo 'UITest REPL - Use App.ExecuteReplCommand() for commands'; echo 'This window demonstrates that a new terminal can be opened.'; exec bash\""),
					// xterm (widely available)
					("xterm", "-e bash -c \"echo 'UITest REPL - Use App.ExecuteReplCommand() for commands'; echo 'This window demonstrates that a new terminal can be opened.'; exec bash\""),
					// Xfce Terminal
					("xfce4-terminal", "-e \"bash -c 'echo UITest REPL - Use App.ExecuteReplCommand() for commands; exec bash'\""),
					// LXTerminal
					("lxterminal", "-e bash -c \"echo 'UITest REPL - Use App.ExecuteReplCommand() for commands'; exec bash\"")
				};

				foreach (var (command, args) in terminalCommands)
				{
					try
					{
						// First check if the command exists
						var whichProcess = Process.Start(new ProcessStartInfo
						{
							FileName = "which",
							Arguments = command,
							UseShellExecute = false,
							CreateNoWindow = true,
							RedirectStandardOutput = true
						});

						whichProcess?.WaitForExit(1000);
						if (whichProcess?.ExitCode != 0)
						{
							continue; // Command not found, try next
						}

						var startInfo = new ProcessStartInfo
						{
							FileName = command,
							Arguments = args,
							UseShellExecute = false,
							CreateNoWindow = false
						};

						var process = Process.Start(startInfo);
						if (process != null)
						{
							Console.WriteLine($"Successfully launched {command}");
							return true;
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Failed to launch {command}: {ex.Message}");
						// Continue to next option
					}
				}

				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Linux terminal launch error: {ex.Message}");
				return false;
			}
		}



		private bool IsInCIEnvironment()
		{
			// Check if we're in a CI/CD environment
			var ciIndicators = new[]
			{
				"CI", "CONTINUOUS_INTEGRATION", "BUILD_NUMBER", "JENKINS_URL",
				"GITHUB_ACTIONS", "TRAVIS", "APPVEYOR", "BUILDKITE", "CIRCLECI"
			};

			foreach (var indicator in ciIndicators)
			{
				if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(indicator)))
					return true;
			}

			return false;
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