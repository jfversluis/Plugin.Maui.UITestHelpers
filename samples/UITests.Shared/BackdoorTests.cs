using NUnit.Framework;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace UITests.Shared;

// Backdoors invoke methods in the app directly, see App.xaml.cs and MauiProgram.cs in the sample app
public class BackdoorTests : BaseTest
{
    const string CounterButton = "CounterBtn";

    public BackdoorTests(TestDevice testDevice) : base(testDevice)
    {
    }

    record AppInfo(string Name, string PackageName, int Counter, string CurrentPage);

    // The test project doesn't reference the app, so it uses its own types with the same shape
    record OrderLine(string Product, int Quantity, decimal Price);
    record Order(string Customer, List<OrderLine> Lines, DateTime OrderedAt, string? Note = null, DayOfWeek? DeliveryDay = null);
    record OrderSummary(string Customer, int Orders, int Items, decimal Total, DateTime? Latest, List<string> Notes, List<DayOfWeek> DeliveryDays);

    [SetUp]
    public void ResetAppState()
    {
        // Get the app in a known state without going through the UI
        App.Invoke("GoToMainPageAsync");
        App.Invoke("ResetCounter");
        App.WaitForElement(CounterButton);
    }

    [OneTimeTearDown]
    public void LeaveCounterReset()
    {
        // Other test fixtures share the same app session and expect the counter to be at 0. If the
        // backdoor doesn't work, restart the app instead, so these tests can't affect the others.
        try
        {
            App.Invoke("ResetCounter");
        }
        catch (BackdoorException)
        {
            App.ResetApp();
        }
    }

    [Test]
    public void Invoke_SetsAppState()
    {
        // Act
        App.Invoke("SetCounter", 41);
        App.FindElement(CounterButton).Click();

        // Assert
        App.Screenshot($"{nameof(Invoke_SetsAppState)}.png");
        Assert.That(App.FindElement(CounterButton).GetText(), Is.EqualTo("Clicked 42 times"));
    }

    [Test]
    public void Invoke_ReadsAppState()
    {
        // Arrange
        var button = App.FindElement(CounterButton);

        // Act
        button.Click();
        button.Click();

        // Assert
        Assert.That(App.Invoke("GetCounter"), Is.TypeOf<long>().And.EqualTo(2L), "whole numbers are returned as long without a type argument");
        Assert.That(App.Invoke<int>("GetCounter"), Is.EqualTo(2));
    }

    [Test]
    public void Invoke_WithOptionalAndMultipleArguments()
    {
        Assert.That(App.Invoke("Greet", "Maui"), Is.EqualTo("Hello, Maui!"));
        Assert.That(App.Invoke("Greet", new object[] { "Maui", "Hi" }), Is.EqualTo("Hi, Maui!"));
        Assert.That(App.Invoke<string>("Greet", "\"Quotes\" & ünïcödé ✓", "Hey"), Is.EqualTo("Hey, \"Quotes\" & ünïcödé ✓!"));
    }

    [Test]
    public void Invoke_ReturnsComplexObject()
    {
        // Arrange
        App.Invoke("SetCounter", 7);

        // Act
        var info = App.Invoke<AppInfo>("GetAppInfo");

        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.Counter, Is.EqualTo(7));
        Assert.That(info.CurrentPage, Is.EqualTo("MainPage"));
        Assert.That(info.Name, Is.Not.Empty);
        Assert.That(info.PackageName, Is.Not.Empty);
    }

    [Test]
    public void Invoke_AwaitsAsyncMethods()
    {
        // Act
        var count = App.Invoke<int>("IncrementCounterAsync", 3);

        // Assert
        Assert.That(count, Is.EqualTo(3));
        Assert.That(App.FindElement(CounterButton).GetText(), Is.EqualTo("Clicked 3 times"));
        Assert.That(App.Invoke<string>("GoToMainPageAsync"), Is.EqualTo("MainPage"));
    }

    [Test]
    public void Invoke_ObjectArgumentsOnServiceFromDependencyInjection()
    {
        App.Invoke("ClearOrders");
        var orders = new[]
        {
            new Order("Ann", new() { new("Coffee", 2, 3.5m), new("Cake", 1, 4.25m) }, new DateTime(2026, 11, 18, 9, 30, 0, DateTimeKind.Utc)),
            new Order("Ann", new() { new("Tea", 3, 2m) }, new DateTime(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc), "Extra hot", DayOfWeek.Friday),
            new Order("Bob", new() { new("Water", 1, 1m) }, new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc)),
        };

        // The array is one argument, so wrap it
        var count = App.Invoke<int>("SeedOrders", new object[] { orders });
        var summary = App.Invoke<OrderSummary>("GetOrderSummary", "Ann");

        Assert.That(count, Is.EqualTo(3));
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary!.Orders, Is.EqualTo(2));
        Assert.That(summary.Items, Is.EqualTo(6));
        Assert.That(summary.Total, Is.EqualTo(17.25m));
        Assert.That(summary.Latest, Is.EqualTo(new DateTime(2026, 11, 19, 8, 0, 0, DateTimeKind.Utc)));
        Assert.That(summary.Notes, Is.EqualTo(new[] { "Extra hot" }));
        Assert.That(summary.DeliveryDays, Is.EqualTo(new[] { DayOfWeek.Friday }), "enums inside objects round-trip as names");

        App.Invoke("ClearOrders");
    }

    [Test]
    public void Invoke_DelegateMethod()
    {
        var expectedPlatform = App switch
        {
            AppiumAndroidApp => "Android",
            AppiumIOSApp => "iOS",
            AppiumCatalystApp => "MacCatalyst",
            AppiumWindowsApp => "WinUI",
            _ => throw new NotSupportedException(),
        };

        Assert.That(App.Invoke("GetPlatform"), Is.EqualTo(expectedPlatform));
    }

    [Test]
    public void Invoke_XamarinUITestStyleNames()
    {
        // Tests migrated from Xamarin.UITest may use Objective-C selectors or camelCase Java names
        App.Invoke("setCounter:", 3);

        Assert.That(App.Invoke<int>("getCounter"), Is.EqualTo(3));
    }

    [Test]
    public void Invoke_EnumRoundTrip()
    {
        Assert.That(App.Invoke<DayOfWeek>("NextDay", DayOfWeek.Saturday), Is.EqualTo(DayOfWeek.Sunday));
        Assert.That(App.Invoke("NextDay", DayOfWeek.Monday), Is.EqualTo("Tuesday"), "enums are returned as their names without a type argument");
    }

    [Test]
    public void Invoke_ArrayArgument()
    {
        var tags = new[] { "a", "b", "c" };

        // Like in Xamarin.UITest, an object[] (and, through array covariance, a string[]) is the
        // list of arguments. To pass an array as one argument, wrap it in the arguments array.
        Assert.That(App.Invoke("JoinTags", new object[] { tags }), Is.EqualTo("a,b,c"));
        Assert.That(App.Invoke<string>("JoinTags", new object[] { tags }), Is.EqualTo("a,b,c"));

        var exception = Assert.Throws<BackdoorException>(() => App.Invoke("JoinTags", tags));
        Assert.That(exception!.Message, Does.Contain("accepts 3 argument(s)"));
    }

    [Test]
    public void Invoke_UnknownMethod_Throws()
    {
        var exception = Assert.Throws<BackdoorException>(() => App.Invoke("DoesNotExist"));

        Assert.That(exception!.Message, Does.Contain("No backdoor method named 'DoesNotExist'"));
        Assert.That(exception.Message, Does.Contain("SetCounter"), "the message lists the available methods");
    }

    [Test]
    public void Invoke_InvalidArgument_Throws()
    {
        var exception = Assert.Throws<BackdoorException>(() => App.Invoke("SetCounter", "not a number"));

        Assert.That(exception!.Message, Does.Contain("could not be converted"));

        // Values are never changed silently, for example by rounding
        exception = Assert.Throws<BackdoorException>(() => App.Invoke("SetCounter", 2.5));
        Assert.That(exception!.Message, Does.Contain("not a whole number"));
        Assert.That(App.Invoke<int>("GetCounter"), Is.EqualTo(0));
    }

    [Test]
    public void Invoke_MethodThrows_ThrowsWithAppException()
    {
        var exception = Assert.Throws<BackdoorException>(() => App.Invoke("ThrowError", "Something went wrong"));

        Assert.That(exception!.Message, Does.Contain("Something went wrong"));
        Assert.That(exception.AppExceptionType, Is.EqualTo(typeof(InvalidOperationException).FullName));
        Assert.That(exception.AppStackTrace, Does.Contain("ThrowError"));

        // The app keeps running
        Assert.That(App.Invoke("Greet", "again"), Is.EqualTo("Hello, again!"));
    }
}
