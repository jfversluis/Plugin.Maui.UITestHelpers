namespace Plugin.Maui.UITestHelpers.Sample;

public partial class MainPage : ContentPage
{
	int count = 0;

	public MainPage()
	{
		InitializeComponent();
	}

	public int Count
	{
		get => count;
		set
		{
			count = value;
			UpdateCounterText();
		}
	}

	private void OnCounterClicked(object? sender, EventArgs e)
	{
		count++;
		UpdateCounterText();

		SemanticScreenReader.Announce(CounterBtn.Text);
	}

	private void UpdateCounterText()
	{
		CounterBtn.Text = count switch
		{
			0 => "Click me",
			1 => $"Clicked {count} time",
			_ => $"Clicked {count} times",
		};
	}

	private async void OnOpenWebViewClicked(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new WebViewPage());
	}
}
