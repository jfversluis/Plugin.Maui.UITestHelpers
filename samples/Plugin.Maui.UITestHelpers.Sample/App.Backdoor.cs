#if UITEST_BACKDOOR
using Plugin.Maui.UITestHelpers.Backdoor;

namespace Plugin.Maui.UITestHelpers.Sample;

public partial class App
{
	// Backdoor methods, invoked from the UI tests with app.Invoke(...). They run on the UI thread.
	// See MauiProgram.cs for how they are registered and BackdoorTests.cs for how they are used.

	[BackdoorMethod]
	public void SetCounter(int count) => GetMainPage().Count = count;

	[BackdoorMethod]
	public int GetCounter() => GetMainPage().Count;

	[BackdoorMethod]
	public void ResetCounter() => GetMainPage().Count = 0;

	[BackdoorMethod]
	public string Greet(string name, string greeting = "Hello") => $"{greeting}, {name}!";

	[BackdoorMethod]
	public object GetAppInfo() => new
	{
		Name = AppInfo.Current.Name,
		PackageName = AppInfo.Current.PackageName,
		Counter = GetMainPage().Count,
		CurrentPage = Shell.Current.CurrentPage.GetType().Name,
	};

	// Async methods are awaited before the result is returned to the UI test
	[BackdoorMethod]
	public async Task<int> IncrementCounterAsync(int times)
	{
		var page = GetMainPage();
		for (var i = 0; i < times; i++)
		{
			await Task.Delay(50);
			page.Count++;
		}

		return page.Count;
	}

	[BackdoorMethod]
	public async Task<string> GoToMainPageAsync()
	{
		await Shell.Current.Navigation.PopToRootAsync();
		return Shell.Current.CurrentPage.GetType().Name;
	}

	// Enums are sent as their names
	[BackdoorMethod]
	public DayOfWeek NextDay(DayOfWeek day) => (DayOfWeek)(((int)day + 1) % 7);

	// Takes one array argument; see BackdoorTests.Invoke_ArrayArgument for how to pass it
	[BackdoorMethod]
	public string JoinTags(string[] tags) => string.Join(",", tags);

	[BackdoorMethod]
	public void ThrowError(string message) => throw new InvalidOperationException(message);

	static MainPage GetMainPage() =>
		(Shell.Current?.CurrentItem?.CurrentItem?.CurrentItem as IShellContentController)?.Page as MainPage
		?? throw new InvalidOperationException("The main page is not available.");
}
#endif
