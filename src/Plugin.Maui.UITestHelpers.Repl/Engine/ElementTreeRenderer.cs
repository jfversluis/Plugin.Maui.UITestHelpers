using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Plugin.Maui.UITestHelpers.Repl.Engine
{
	internal sealed record TreeOptions(bool All = false, int? MaxDepth = null, string? XPath = null);

	/// <summary>
	/// Turns the Appium page source (the XML element tree of the app) into a readable tree.
	/// </summary>
	internal static partial class ElementTreeRenderer
	{
		const int MaxNodes = 1500;

		public static IRenderable Render(string pageSource, TreeOptions options, out int shownNodes)
		{
			XDocument document;
			try
			{
				document = XDocument.Parse(pageSource);
			}
			catch (Exception ex)
			{
				shownNodes = 0;
				return new Markup($"[red]Could not parse the page source:[/] {Markup.Escape(ex.Message)}");
			}

			int count = 0;
			var roots = new List<XElement>();

			if (!string.IsNullOrWhiteSpace(options.XPath))
			{
				try
				{
					roots.AddRange(document.XPathSelectElements(options.XPath));
				}
				catch (Exception ex)
				{
					shownNodes = 0;
					return new Markup($"[red]Invalid XPath:[/] {Markup.Escape(ex.Message)}");
				}

				if (roots.Count == 0)
				{
					shownNodes = 0;
					return new Markup($"[yellow]No elements match[/] {Markup.Escape(options.XPath)}");
				}
			}
			else if (document.Root is { } root)
			{
				// Appium wraps the app in a synthetic root ("hierarchy" on Android, "AppiumAUT" on iOS).
				if (root.Name.LocalName is "hierarchy" or "AppiumAUT")
				{
					roots.AddRange(root.Elements());
				}
				else
				{
					roots.Add(root);
				}
			}

			var tree = new Tree(new Markup(options.XPath is null ? "[bold]App[/]" : $"[bold]{Markup.Escape(options.XPath)}[/]"))
			{
				Style = new Style(Color.Grey),
				Guide = TreeGuide.Line,
			};

			foreach (var element in roots)
			{
				Add(tree, element, 0, options, ref count, force: true);
			}

			shownNodes = count;
			return tree;
		}

		static void Add(IHasTreeNodes parent, XElement element, int depth, TreeOptions options, ref int count, bool force = false)
		{
			if (count >= MaxNodes)
			{
				return;
			}

			var info = ElementDescription.From(element);

			if (!options.All && !force && info.IsHidden)
			{
				return;
			}

			bool keep = options.All || force || info.IsInteresting;
			IHasTreeNodes target = parent;
			int childDepth = depth;

			if (keep)
			{
				if (options.MaxDepth is int maxDepth && depth >= maxDepth)
				{
					return;
				}

				target = parent.AddNode(new Markup(info.ToMarkup()));
				count++;
				childDepth = depth + 1;
			}

			foreach (var child in element.Elements())
			{
				Add(target, child, childDepth, options, ref count);
			}
		}

		sealed record ElementDescription(
			string Tag,
			string? Id,
			string? Text,
			string? Description,
			string? Bounds,
			bool IsHidden,
			bool IsInteresting,
			string Flags)
		{
			public static ElementDescription From(XElement element)
			{
				var name = element.Name.LocalName;
				bool isApple = name.StartsWith("XCUIElementType", StringComparison.Ordinal);

				var tag = isApple ? name["XCUIElementType".Length..] : name[(name.LastIndexOf('.') + 1)..];

				string? id = Attr(element, "resource-id");
				if (id is not null)
				{
					var marker = id.IndexOf(":id/", StringComparison.Ordinal);
					if (marker >= 0)
					{
						id = id[(marker + 4)..];
					}
				}

				id ??= Attr(element, "AutomationId") ?? Attr(element, "identifier");

				var text = Attr(element, "text") ?? Attr(element, "label") ?? Attr(element, "value") ?? Attr(element, "title") ?? Attr(element, "Name");

				// On Apple platforms 'name' is the accessibility identifier, or the label when there isn't one.
				if (id is null && isApple && Attr(element, "name") is { } accessibilityName && accessibilityName != Attr(element, "label"))
				{
					id = accessibilityName;
				}

				if (text == id)
				{
					text = null;
				}

				var description = Attr(element, "content-desc");

				string? bounds = null;
				var androidBounds = Attr(element, "bounds");
				if (androidBounds is not null && BoundsRegex().Match(androidBounds) is { Success: true } m)
				{
					int l = int.Parse(m.Groups[1].Value), t = int.Parse(m.Groups[2].Value), r = int.Parse(m.Groups[3].Value), b = int.Parse(m.Groups[4].Value);
					bounds = $"{l},{t} {r - l}×{b - t}";
				}
				else if (Attr(element, "x") is { } x && Attr(element, "y") is { } y && Attr(element, "width") is { } w && Attr(element, "height") is { } h)
				{
					bounds = $"{x},{y} {w}×{h}";
				}

				// Zero-sized elements aren't on screen, like the items of closed menus on macOS.
				bool emptyBounds = Attr(element, "width") == "0" && Attr(element, "height") == "0";
				bool hidden = emptyBounds || Attr(element, "visible") == "false" || Attr(element, "displayed") == "false";
				bool clickable = Attr(element, "clickable") == "true";
				bool disabled = Attr(element, "enabled") == "false";
				bool isChecked = Attr(element, "checked") == "true";

				var flags = new List<string>();
				if (clickable)
				{
					flags.Add("clickable");
				}

				if (isChecked)
				{
					flags.Add("checked");
				}

				if (disabled)
				{
					flags.Add("disabled");
				}

				bool interesting = id is not null || text is not null || description is not null || clickable;

				return new ElementDescription(tag, id, text, description, bounds, hidden, interesting, string.Join(' ', flags));
			}

			public string ToMarkup()
			{
				var parts = new List<string> { $"[cyan]{Markup.Escape(Tag)}[/]" };

				if (Id is not null)
				{
					parts.Add($"[yellow]#{Markup.Escape(Id)}[/]");
				}

				if (Text is not null)
				{
					parts.Add($"[green]\"{Markup.Escape(Truncate(Text, 60))}\"[/]");
				}

				if (Description is not null)
				{
					parts.Add($"[magenta]({Markup.Escape(Truncate(Description, 40))})[/]");
				}

				if (Bounds is not null)
				{
					parts.Add($"[grey]{Markup.Escape(Bounds)}[/]");
				}

				if (Flags.Length > 0)
				{
					parts.Add($"[dim]{Markup.Escape(Flags)}[/]");
				}

				return string.Join(' ', parts);
			}

			static string Truncate(string value, int length)
			{
				value = value.ReplaceLineEndings(" ");
				return value.Length <= length ? value : value[..(length - 1)] + "…";
			}

			static string? Attr(XElement element, string name)
			{
				var value = element.Attribute(name)?.Value;
				return string.IsNullOrEmpty(value) ? null : value;
			}
		}

		[GeneratedRegex(@"^\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\]$")]
		private static partial Regex BoundsRegex();
	}
}
