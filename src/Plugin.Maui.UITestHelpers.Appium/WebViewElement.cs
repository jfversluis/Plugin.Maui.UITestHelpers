using System.Drawing;

namespace Plugin.Maui.UITestHelpers.Appium
{
    /// <summary>
    /// An HTML element in a webview, as returned by <see cref="HelperExtensions.QueryWebViewCss"/>
    /// and <see cref="HelperExtensions.QueryWebViewXPath"/>.
    /// </summary>
    /// <param name="Id">The element's <c>id</c> attribute, or an empty string.</param>
    /// <param name="Text">The element's <c>textContent</c>, including the text of its descendants.</param>
    /// <param name="Rect">The element's bounds in device coordinates, as used by
    /// <see cref="HelperExtensions.TapCoordinates"/>.</param>
    public sealed record WebViewElement(string Id, string Text, Rectangle Rect)
    {
        /// <summary>
        /// The X coordinate of the element's center, in device coordinates.
        /// </summary>
        public float CenterX => Rect.X + (Rect.Width / 2f);

        /// <summary>
        /// The Y coordinate of the element's center, in device coordinates.
        /// </summary>
        public float CenterY => Rect.Y + (Rect.Height / 2f);
    }
}
