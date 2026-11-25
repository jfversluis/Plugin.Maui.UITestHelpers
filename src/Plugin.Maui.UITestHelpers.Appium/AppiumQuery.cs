using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using Plugin.Maui.UITestHelpers.Core;

namespace Plugin.Maui.UITestHelpers.Appium
{
	public class AppiumQuery : IQuery
	{
		const string ClassToken = "class";
		const string IdToken = "id";
		const string NameToken = "name";
		const string AccessibilityToken = "accessibilityid";
		const string QuerySeparatorToken = "&";
		const string IdQuery = IdToken + "={0}";
		const string NameQuery = NameToken + "={0}";
		const string AccessibilityQuery = AccessibilityToken + "={0}";
		const string XPathToken = "xpath";
		const string ClassQuery = ClassToken + "={0}";
		const string XPathQuery = XPathToken + "={0}";
		readonly string _queryStr;

		public AppiumQuery(string queryStr)
		{
			_queryStr = queryStr;
		}

		/// <summary>
		/// Creates a combined query: the elements matching <paramref name="queryStr"/> within the elements found by
		/// <paramref name="query"/>.
		/// </summary>
		/// <param name="query">The query to refine.</param>
		/// <param name="queryStr">The query part to search within the elements found by <paramref name="query"/>.</param>
		public AppiumQuery(AppiumQuery query, string queryStr)
		{
			_queryStr = string.Join(QuerySeparatorToken, query._queryStr, queryStr);
		}

		IQuery IQuery.ByClass(string classQuery)
		{
			return new AppiumQuery(this, string.Format(ClassQuery, classQuery));
		}

		IQuery IQuery.ById(string id)
		{
			return new AppiumQuery(this, string.Format(IdQuery, id));
		}

		IQuery IQuery.ByAccessibilityId(string id)
		{
			return new AppiumQuery(this, string.Format(AccessibilityQuery, id));
		}

		IQuery IQuery.ByName(string nameQuery)
		{
			return new AppiumQuery(this, string.Format(NameQuery, nameQuery));
		}

		IQuery IQuery.ByXPath(string xpath)
		{
			return new AppiumQuery(this, string.Format(XPathQuery, Uri.EscapeDataString(xpath)));
		}

		public static AppiumQuery ById(string id)
		{
			return new AppiumQuery(string.Format(IdQuery, id));
		}

		public static AppiumQuery ByName(string nameQuery)
		{
			return new AppiumQuery(string.Format(NameQuery, nameQuery));
		}

		public static AppiumQuery ByXPath(string xpath)
		{
			return new AppiumQuery(string.Format(XPathQuery, Uri.EscapeDataString(xpath)));
		}

		/// <summary>
		/// Returns the XPath expression when this is a single XPath query (e.g. from <see cref="ByXPath"/> or
		/// <c>app.Marked()</c>), to build relational queries such as siblings from it.
		/// </summary>
		/// <param name="xpath">The XPath expression.</param>
		/// <returns><c>true</c> when this is a single XPath query.</returns>
		internal bool TryGetXPath([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? xpath)
		{
			string prefix = XPathToken + "=";

			// the XPath is URL-encoded, so a separator token always separates combined queries
			if (_queryStr.StartsWith(prefix, StringComparison.Ordinal) && !_queryStr.Contains(QuerySeparatorToken))
			{
				xpath = Uri.UnescapeDataString(_queryStr.Substring(prefix.Length));
				return true;
			}

			xpath = null;
			return false;
		}

		public static AppiumQuery ByAccessibilityId(string id)
		{
			return new AppiumQuery(string.Format(AccessibilityQuery, id));
		}

		public static AppiumQuery ByClass(string classQuery)
		{
			return new AppiumQuery(string.Format(ClassQuery, classQuery));
		}

		/// <summary>
		/// Returns the first element matching the query, or <c>null</c> when there's none.
		/// </summary>
		/// <param name="appiumApp">The app to search.</param>
		/// <returns>The element, or <c>null</c>.</returns>
#nullable disable
		public IUIElement FindElement(AppiumApp appiumApp)
		{
			var foundElement = FindAll(appiumApp)
				.FirstOrDefault();

			return foundElement is null ? null : new AppiumDriverElement(foundElement, appiumApp);
		}
#nullable enable

		/// <summary>
		/// Returns the elements matching the query.
		/// </summary>
		/// <param name="appiumApp">The app to search.</param>
		/// <returns>The elements, in the order they were found.</returns>
		public IReadOnlyCollection<IUIElement> FindElements(AppiumApp appiumApp)
		{
			return FindAll(appiumApp)
				.Select(e => new AppiumDriverElement(e, appiumApp))
				.ToList();
		}

		/// <summary>
		/// Returns the elements matching the query within the given element.
		/// </summary>
		/// <param name="element">The element to search within.</param>
		/// <param name="appiumApp">The app.</param>
		/// <returns>The elements, in the order they were found.</returns>
		public IReadOnlyCollection<IUIElement> FindElements(AppiumElement element, AppiumApp appiumApp)
		{
			return FindAll(appiumApp, element)
				.Select(e => new AppiumDriverElement(e, appiumApp))
				.ToList();
		}

		/// <summary>
		/// Returns the first element matching the query within the given element.
		/// </summary>
		/// <param name="element">The element to search within.</param>
		/// <param name="appiumApp">The app.</param>
		/// <returns>The element.</returns>
		/// <exception cref="NoSuchElementException">Thrown when no element matches the query.</exception>
		public IUIElement FindElement(AppiumElement element, AppiumApp appiumApp)
		{
			var foundElement = FindAll(appiumApp, element)
				.FirstOrDefault()
				?? throw new NoSuchElementException($"No element matches the query '{_queryStr}'");

			return new AppiumDriverElement(foundElement, appiumApp);
		}

		/// <summary>
		/// Runs the parts of the query one after another: the first part searches the app, or within the given
		/// element; each further part of a combined query searches within all the elements found by the previous
		/// part. Elements found several times are returned once.
		/// </summary>
		/// <param name="appiumApp">The app to search.</param>
		/// <param name="element">The element to search within, or <c>null</c> to search the whole app.</param>
		/// <returns>The elements found by the last part, in the order they were found.</returns>
		IReadOnlyList<AppiumElement> FindAll(AppiumApp appiumApp, AppiumElement? element = null)
		{
			string[] parts = _queryStr.Split(QuerySeparatorToken);
			ISearchContext context = element is null ? appiumApp.Driver : element;

			IReadOnlyList<AppiumElement> foundElements = context
				.FindElements(GetQueryBy(parts[0]))
				.Cast<AppiumElement>()
				.ToList();

			for (int i = 1; i < parts.Length && foundElements.Count > 0; i++)
			{
				By queryBy = GetQueryBy(parts[i]);

				// an element can be found within several elements, e.g. within nested layouts
				foundElements = foundElements
					.SelectMany(e => e.FindElements(queryBy))
					.Cast<AppiumElement>()
					.Distinct()
					.ToList();
			}

			return foundElements;
		}

		/// <summary>
		/// Returns the locator for a single query part, e.g. <c>id=MyButton</c>.
		/// </summary>
		static By GetQueryBy(string queryPart)
		{
			int separatorIndex = queryPart.IndexOf('=');
			if (separatorIndex <= 0)
			{
				throw new ArgumentException($"Invalid query part '{queryPart}'");
			}

			return GetQueryBy(queryPart.Substring(0, separatorIndex), queryPart.Substring(separatorIndex + 1));
		}

		private static By GetQueryBy(string token, string value)
		{
			return token.ToLowerInvariant() switch
			{
				ClassToken => MobileBy.ClassName(value),
				NameToken => MobileBy.Name(value),
				AccessibilityToken => MobileBy.AccessibilityId(value),
				IdToken => MobileBy.Id(value),
				XPathToken => MobileBy.XPath(Uri.UnescapeDataString(value)),
				_ => throw new ArgumentException("Unknown query type"),
			};
		}
	}
}
