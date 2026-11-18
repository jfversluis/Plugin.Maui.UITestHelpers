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

    [Test]
    public void WebViewHelpers_InteractWithWebContent()
    {
        if (App is not AppiumAndroidApp && App is not AppiumIOSApp)
        {
            Assert.Ignore("Webview contexts are only supported on Android and iOS");
        }

        var driver = ((AppiumApp)App).Driver;

        // The button is at the bottom of the page and can be off screen on small devices
        App.ScrollDownTo("OpenWebViewBtn", "MainScrollView");
        App.Tap("OpenWebViewBtn");

        try
        {
            Assert.That(App.SwitchContextToWebview(), Is.True, "should switch to the webview context");
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
            App.Back();
            App.WaitForElement("MainScrollView");
            App.ScrollUpTo("CounterBtn", "MainScrollView");
        }
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
