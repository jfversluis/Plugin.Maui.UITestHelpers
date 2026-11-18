namespace Plugin.Maui.UITestHelpers.Sample;

public class WebViewPage : ContentPage
{
	const string Html = """
		<!DOCTYPE html>
		<html>
		<head><meta name="viewport" content="width=device-width, initial-scale=1"></head>
		<body style="font-size: 20px">
			<p id="top">Top of the page</p>
			<button id="quoteButton" onclick="document.getElementById('result').textContent = 'Quote button tapped'">Say "hi" to O'Brien</button>
			<button id="plainButton" onclick="document.getElementById('result').textContent = 'Plain button tapped'">Tap me</button>
			<p id="result">Nothing tapped</p>
			<div style="height: 3000px"></div>
			<p id="bottom">Bottom of the page</p>
		</body>
		</html>
		""";

	public WebViewPage()
	{
		Title = "WebView";
		Content = new WebView
		{
			AutomationId = "SampleWebView",
			Source = new HtmlWebViewSource { Html = Html },
		};
	}
}
