using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenQA.Selenium;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace UITests.Shared;

public class WebViewTests : BaseTest
{
    public WebViewTests(TestDevice testDevice) : base(testDevice)
    {
    }

    // The WebView's AutomationId isn't exposed on Android, so the page is identified by its title
    const string WebViewPageTitle = "WebView sample";

    [Test]
    public void WebViewHelpers_InteractWithWebContent()
    {
        if (App is not AppiumAndroidApp && App is not AppiumIOSApp)
        {
            Assert.Ignore("Webview contexts are only supported on Android and iOS");
        }

        var driver = ((AppiumApp)App).Driver;

        // Toolbar items expose their AutomationId as content description (Android) or name (iOS), so use Marked()
        App.Tap(App.Marked("OpenWebViewBtn"));

        try
        {
            App.WaitForElement(WebViewPageTitle, "The WebView page did not open");
            App.Screenshot($"{nameof(WebViewHelpers_InteractWithWebContent)}.png");

            Assert.That(App.SwitchContextToWebview(TimeSpan.FromSeconds(30)), Is.True, "should switch to the webview context");
            WaitForPageLoaded(driver);

            Assert.That(App.GetCurrentUri(), Is.Not.Null);

            Assert.That(App.TapWebviewButtonByText("Say \"hi\" to O'Brien"), Is.True, "text with quotes should be matched");
            Assert.That(driver.FindElement(By.Id("result")).Text, Is.EqualTo("Quote button tapped"));

            Assert.That(App.TapWebviewButtonByText("Tap me"), Is.True);
            Assert.That(driver.FindElement(By.Id("result")).Text, Is.EqualTo("Plain button tapped"));

            Assert.That(App.TapWebviewButtonByText("Does not exist"), Is.False);

            Assert.That(App.ScrollWebview(down: true), Is.True);
            Assert.That(GetScrollPosition(driver).Y, Is.GreaterThan(0), "should have scrolled down");

            Assert.That(App.ScrollWebview(down: false), Is.True);
            Assert.That(GetScrollPosition(driver), Is.EqualTo((0L, 0L)), "should have scrolled back to the top");

            Assert.That(App.SwitchContextToNative(), Is.True, "should switch back to the native context");
        }
        finally
        {
            App.SwitchContextToNative();

            // Only navigate back when the WebView page is open, otherwise Back closes the app
            if (App.Exists(WebViewPageTitle))
            {
                App.Back();
            }

            App.WaitForElement("CounterBtn");
        }
    }

    [Test]
    public void WebView_FindsNativeWebView()
    {
        OnWebViewPage(() =>
        {
            var webView = App.WaitForElement(App.WebView());
            var rect = webView.GetRect();

            Assert.That(rect.Width, Is.GreaterThan(0), "webview must have a width");
            Assert.That(rect.Height, Is.GreaterThan(0), "webview must have a height");
        });
    }

    [Test]
    public void QueryWebViewCss_ReturnsElementWithTextAndBounds()
    {
        OnWebViewPage(() =>
        {
            var webViewRect = App.FindElement(App.WebView()).GetRect();

            var button = App.QueryWebViewCss("#plainButton").Single();

            Assert.That(button.Id, Is.EqualTo("plainButton"));
            Assert.That(button.Text, Is.EqualTo("Tap me"));
            Assert.That(button.Rect.Width, Is.GreaterThan(0), "button must have a width");
            Assert.That(button.Rect.Height, Is.GreaterThan(0), "button must have a height");
            Assert.That(webViewRect.Contains((int)button.CenterX, (int)button.CenterY), Is.True,
                $"button center ({button.CenterX}, {button.CenterY}) must be inside the webview {webViewRect}");
        });
    }

    [Test]
    public void QueryWebViewXPath_ReturnsSameElementAsCss()
    {
        OnWebViewPage(() =>
        {
            var byCss = App.QueryWebViewCss("#plainButton").Single();
            var byXPath = App.QueryWebViewXPath("//button[@id='plainButton']").Single();

            Assert.That(byXPath, Is.EqualTo(byCss));
        });
    }

    [Test]
    public void QueryWebViewCss_TapAtCenter_TapsElement()
    {
        OnWebViewPage(() =>
        {
            var button = App.QueryWebViewCss("#plainButton").Single();

            // a native tap at the calculated device coordinates must hit the HTML button
            App.TapCoordinates(button.CenterX, button.CenterY);

            WaitForResultText("Plain button tapped");
        });
    }

    [Test]
    public void QueryWebViewCss_SkipsHiddenElements()
    {
        OnWebViewPage(() =>
        {
            var renderedButtons = App.QueryWebViewCss("button").Select(b => b.Id).ToList();
            var allButtons = App.QueryWebViewCss("button", includeHidden: true);

            Assert.That(renderedButtons, Does.Contain("plainButton"));
            Assert.That(renderedButtons, Does.Not.Contain("hiddenButton"), "hidden button must be skipped");

            var hiddenButton = allButtons.SingleOrDefault(b => b.Id == "hiddenButton");
            Assert.That(hiddenButton, Is.Not.Null, "hidden button must be returned with includeHidden");
            Assert.That(hiddenButton!.Rect.Width, Is.Zero, "hidden button must have empty bounds");
        });
    }

    [Test]
    public void QueryWebViewXPath_TextNodes_AreSkipped()
    {
        OnWebViewPage(() =>
        {
            var result = App.QueryWebViewXPath("//p[@id='top']/text()");

            Assert.That(result, Is.Empty, "text nodes aren't elements and must be skipped");
        });
    }

    [Test]
    public void QueryWebViewCss_NoMatch_ReturnsEmptyList()
    {
        OnWebViewPage(() =>
        {
            Assert.That(App.QueryWebViewCss("#doesNotExist"), Is.Empty);
        });
    }

    [Test]
    public void QueryWebViewCss_RestoresPreviousContext()
    {
        OnWebViewPage(() =>
        {
            var driver = ((AppiumApp)App).Driver;

            // called from the native context
            App.QueryWebViewCss("#plainButton");
            Assert.That(driver.Context, Is.EqualTo("NATIVE_APP"), "native context must be restored");

            // called from the webview context
            Assert.That(App.SwitchContextToWebview(), Is.True, "should switch to the webview context");
            string webviewContext = driver.Context;

            Assert.That(App.QueryWebViewCss("#plainButton"), Has.Count.EqualTo(1));
            Assert.That(driver.Context, Is.EqualTo(webviewContext), "webview context must be restored");
        });
    }

    /// <summary>
    /// Opens the WebView sample page, waits until its content is loaded, runs the test and
    /// navigates back to the main page.
    /// </summary>
    void OnWebViewPage(Action test, [CallerMemberName] string testName = "")
    {
        if (App is not AppiumAndroidApp && App is not AppiumIOSApp)
        {
            Assert.Ignore("Webview contexts are only supported on Android and iOS");
        }

        App.Tap(App.Marked("OpenWebViewBtn"));

        try
        {
            App.WaitForElement(WebViewPageTitle, "The WebView page did not open");
            App.WaitForElement(App.WebView(), "The webview was not found");

            var deadline = DateTime.Now.AddSeconds(30);
            while (App.QueryWebViewCss("#plainButton").Count == 0)
            {
                if (DateTime.Now > deadline)
                {
                    Assert.Fail("Timed out waiting for the webview content");
                }

                Thread.Sleep(250);
            }

            App.Screenshot($"{testName}.png");

            test();
        }
        finally
        {
            App.SwitchContextToNative();

            // Only navigate back when the WebView page is open, otherwise Back closes the app
            if (App.Exists(WebViewPageTitle))
            {
                App.Back();
            }

            App.WaitForElement("CounterBtn");
        }
    }

    void WaitForResultText(string expectedText)
    {
        var deadline = DateTime.Now.AddSeconds(5);
        string? text = null;
        while (DateTime.Now < deadline)
        {
            text = App.QueryWebViewCss("#result").SingleOrDefault()?.Text;
            if (text == expectedText)
            {
                return;
            }

            Thread.Sleep(250);
        }

        Assert.That(text, Is.EqualTo(expectedText));
    }

    static void WaitForPageLoaded(IJavaScriptExecutor driver)
    {
        var deadline = DateTime.Now.AddSeconds(15);
        while (driver.ExecuteScript("return document.readyState === 'complete' && document.getElementById('quoteButton') !== null;") is not true)
        {
            if (DateTime.Now > deadline)
            {
                Assert.Fail("Timed out waiting for the webview page to load");
            }

            Thread.Sleep(250);
        }
    }

    static (long X, long Y) GetScrollPosition(IJavaScriptExecutor driver)
    {
        var position = (IReadOnlyCollection<object>)driver.ExecuteScript("return [Math.round(window.scrollX), Math.round(window.scrollY)];")!;
        return (Convert.ToInt64(position.ElementAt(0)), Convert.ToInt64(position.ElementAt(1)));
    }
}
