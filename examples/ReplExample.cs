using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;

namespace Plugin.Maui.UITestHelpers.Examples
{
    /// <summary>
    /// Example console application demonstrating REPL usage
    /// </summary>
    public class ReplExample
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("UI Test Helpers REPL Example");
            Console.WriteLine("============================");
            
            // Note: This is a simplified example. In a real scenario, you would:
            // 1. Start an Appium server
            // 2. Have a real app running on device/emulator
            // 3. Configure proper capabilities
            
            try
            {
                var config = CreateExampleConfig();
                var driver = CreateExampleDriver(config);
                var app = AppiumAndroidApp.CreateAndroidApp(driver, config);
                
                Console.WriteLine("App created successfully!");
                Console.WriteLine("Starting REPL session...");
                Console.WriteLine();
                
                // Start the interactive REPL
                app.StartRepl();
                
                Console.WriteLine("REPL session ended.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("This example requires:");
                Console.WriteLine("1. Appium server running");
                Console.WriteLine("2. Android device/emulator with test app");
                Console.WriteLine("3. Proper configuration in CreateExampleConfig()");
                Console.WriteLine();
                Console.WriteLine("Demonstrating REPL commands without real app:");
                DemonstrateReplCommands();
            }
        }
        
        private static IConfig CreateExampleConfig()
        {
            var config = new Config();
            
            // Example configuration - adjust for your setup
            config.SetProperty("AppId", "com.companyname.uitesthelperssample");
            config.SetProperty("PlatformName", "Android");
            config.SetProperty("DeviceName", "emulator-5554");
            config.SetProperty("AutomationName", "UiAutomator2");
            config.SetProperty("PlatformVersion", "11.0");
            
            return config;
        }
        
        private static AppiumDriver CreateExampleDriver(IConfig config)
        {
            var options = new AndroidOptions();
            options.PlatformName = config.GetProperty<string>("PlatformName");
            options.DeviceName = config.GetProperty<string>("DeviceName");
            options.AutomationName = config.GetProperty<string>("AutomationName");
            options.PlatformVersion = config.GetProperty<string>("PlatformVersion");
            options.App = config.GetProperty<string>("AppId");
            
            // Connect to Appium server (adjust URL as needed)
            var driver = new AndroidDriver(new Uri("http://localhost:4723"), options);
            return driver;
        }
        
        private static void DemonstrateReplCommands()
        {
            Console.WriteLine("REPL Command Examples:");
            Console.WriteLine("=====================");
            Console.WriteLine();
            Console.WriteLine("# Get help");
            Console.WriteLine("uitest> help");
            Console.WriteLine();
            Console.WriteLine("# Find elements");
            Console.WriteLine("uitest> id CounterBtn");
            Console.WriteLine("uitest> xpath //button[@text='Click me']");
            Console.WriteLine("uitest> class android.widget.Button");
            Console.WriteLine();
            Console.WriteLine("# Interact with elements");
            Console.WriteLine("uitest> click CounterBtn");
            Console.WriteLine("uitest> text CounterBtn");
            Console.WriteLine("uitest> type MyEntry \"Hello World\"");
            Console.WriteLine();
            Console.WriteLine("# Inspect UI");
            Console.WriteLine("uitest> tree");
            Console.WriteLine("uitest> screenshot test.png");
            Console.WriteLine("uitest> info");
            Console.WriteLine();
            Console.WriteLine("# Exit REPL");
            Console.WriteLine("uitest> exit");
        }
    }
}