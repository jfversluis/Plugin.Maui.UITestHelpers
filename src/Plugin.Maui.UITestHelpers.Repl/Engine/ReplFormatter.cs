using System.Collections;
using OpenQA.Selenium.Appium;
using Plugin.Maui.UITestHelpers.Appium;
using Plugin.Maui.UITestHelpers.Core;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Plugin.Maui.UITestHelpers.Repl.Engine
{
	/// <summary>
	/// Renders the result of a REPL submission.
	/// </summary>
	internal static class ReplFormatter
	{
		const int MaxListItems = 30;
		const int MaxElementRows = 12;

		public static IRenderable Format(object? value, IApp app)
		{
			switch (value)
			{
				case null:
					return new Markup("[grey]null[/]");
				case IUIElement element:
					return ElementTable(new[] { element }, app, 1);
				case string text:
					return FormatString(text);
				case byte[] bytes:
					return new Markup($"[grey]byte[[{bytes.Length}]][/]");
				case IEnumerable enumerable when value is not IDictionary:
					return FormatEnumerable(enumerable, app);
				case IDictionary dictionary:
					return FormatDictionary(dictionary);
			}

			return new Markup(Inline(value));
		}

		static IRenderable FormatString(string text)
		{
			if (text.Contains('\n') || text.Length > 160)
			{
				return new Panel(new Text(text))
				{
					Border = BoxBorder.Rounded,
					BorderStyle = new Style(Color.Green),
					Header = new PanelHeader($"[grey]string · {text.Length} chars[/]"),
					Expand = false,
				};
			}

			return new Markup($"[green]\"{Markup.Escape(text)}\"[/]");
		}

		static IRenderable FormatEnumerable(IEnumerable enumerable, IApp app)
		{
			var items = new List<object?>();
			int total = 0;
			foreach (var item in enumerable)
			{
				total++;
				if (items.Count < 500)
				{
					items.Add(item);
				}
			}

			if (total == 0)
			{
				return new Markup("[grey]empty collection (0 items)[/]");
			}

			if (items.All(i => i is IUIElement))
			{
				return ElementTable(items.Cast<IUIElement>().ToList(), app, total);
			}

			var rows = new List<IRenderable> { new Markup($"[grey]{total} item{(total == 1 ? "" : "s")}[/]") };
			foreach (var item in items.Take(MaxListItems))
			{
				rows.Add(new Markup("[grey]•[/] " + Inline(item)));
			}

			if (total > MaxListItems)
			{
				rows.Add(new Markup($"[grey]… and {total - MaxListItems} more[/]"));
			}

			return new Rows(rows);
		}

		static IRenderable FormatDictionary(IDictionary dictionary)
		{
			var table = new Table { Border = TableBorder.Rounded, BorderStyle = new Style(Color.Grey) };
			table.AddColumn("[bold]Key[/]");
			table.AddColumn("[bold]Value[/]");

			int shown = 0;
			foreach (DictionaryEntry entry in dictionary)
			{
				if (shown++ >= MaxListItems)
				{
					break;
				}

				table.AddRow(new Markup(Inline(entry.Key)), new Markup(Inline(entry.Value)));
			}

			if (dictionary.Count > MaxListItems)
			{
				table.Caption(new TableTitle($"[grey]… and {dictionary.Count - MaxListItems} more[/]"));
			}

			return table;
		}

		/// <summary>
		/// A single-line markup representation of any value.
		/// </summary>
		public static string Inline(object? value)
		{
			switch (value)
			{
				case null:
					return "[grey]null[/]";
				case string s:
					return $"[green]\"{Markup.Escape(s.ReplaceLineEndings(" "))}\"[/]";
				case bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
					return $"[blue]{Markup.Escape(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!)}[/]";
				case char c:
					return $"[green]'{Markup.Escape(c.ToString())}'[/]";
				case Enum e:
					return $"[yellow]{Markup.Escape(e.ToString())}[/]";
				case FileInfo file:
					return $"[underline]{Markup.Escape(file.FullName)}[/]";
				case System.Drawing.Rectangle or System.Drawing.Point or System.Drawing.Size or DateTime or DateTimeOffset or TimeSpan or Guid or Uri:
					return Markup.Escape(value.ToString()!);
				case Exception ex:
					return $"[red]{Markup.Escape(ex.GetType().Name)}[/]: {Markup.Escape(FirstLine(ex.Message))}";
				case IUIElement element:
					return Markup.Escape(DescribeElementShort(element));
				case byte[] bytes:
					return $"[grey]byte[[{bytes.Length}]][/]";
			}

			var type = value.GetType();
			if (type.GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType != typeof(object))
			{
				return Markup.Escape(value.ToString() ?? string.Empty);
			}

			if (value is IEnumerable enumerable)
			{
				var items = enumerable.Cast<object?>().Take(6).Select(Inline).ToList();
				return "[[" + string.Join(", ", items) + (enumerable.Cast<object?>().Skip(6).Any() ? ", …" : "") + "]]";
			}

			return DescribeObject(value, type);
		}

		// Only simple properties are read: complex ones (a driver, the page source...) can be slow or have side effects.
		static string DescribeObject(object value, Type type)
		{
			if (value is IApp)
			{
				return $"[cyan]{Markup.Escape(type.Name)}[/]";
			}

			var parts = new List<string>();
			foreach (var property in type.GetProperties().Where(p => p.GetIndexParameters().Length == 0 && p.CanRead).Take(40))
			{
				if (!(property.PropertyType.IsPrimitive || property.PropertyType.IsEnum || property.PropertyType == typeof(string) || property.PropertyType == typeof(decimal)))
				{
					continue;
				}

				try
				{
					parts.Add($"{Markup.Escape(property.Name)} = {Inline(property.GetValue(value))}");
				}
				catch
				{
				}

				if (parts.Count >= 10)
				{
					break;
				}
			}

			return parts.Count == 0
				? $"[cyan]{Markup.Escape(type.Name)}[/]"
				: $"[cyan]{Markup.Escape(type.Name)}[/] {{ {string.Join(", ", parts)} }}";
		}

		static string FirstLine(string text) => text.ReplaceLineEndings("\n").Split('\n')[0];

		static string DescribeElementShort(IUIElement element) =>
			element is AppiumDriverElement driverElement ? "element " + SafeTag(driverElement.AppiumElement) : "element";

		static string SafeTag(AppiumElement element)
		{
			try
			{
				return element.TagName;
			}
			catch
			{
				return string.Empty;
			}
		}

		static IRenderable ElementTable(IReadOnlyList<IUIElement> elements, IApp app, int total)
		{
			var device = SafeDevice(app);
			bool withRect = elements.Count <= 8;

			var table = new Table { Border = TableBorder.Rounded, BorderStyle = new Style(Color.Grey) };
			table.AddColumn("[bold]#[/]");
			table.AddColumn("[bold]Type[/]");
			table.AddColumn("[bold]Id[/]");
			table.AddColumn("[bold]Text[/]");
			if (withRect)
			{
				table.AddColumn("[bold]Bounds[/]");
				table.AddColumn("[bold]State[/]");
			}

			int index = 0;
			foreach (var element in elements.Take(MaxElementRows))
			{
				var details = ElementDetails.Read(element, device, withRect);
				var row = new List<string>
				{
					$"[grey]{index++}[/]",
					$"[cyan]{Markup.Escape(details.Type ?? "?")}[/]",
					details.Id is null ? "[grey]-[/]" : $"[yellow]{Markup.Escape(details.Id)}[/]",
					details.Text is null ? "[grey]-[/]" : $"[green]{Markup.Escape(Truncate(details.Text, 50))}[/]",
				};

				if (withRect)
				{
					row.Add(details.Bounds is null ? "[grey]-[/]" : $"[grey]{Markup.Escape(details.Bounds)}[/]");
					row.Add(details.State);
				}

				table.AddRow(row.Select(r => new Markup(r)));
			}

			table.Title = new TableTitle($"[bold]{total}[/] element{(total == 1 ? "" : "s")}");

			if (total > MaxElementRows)
			{
				table.Caption(new TableTitle($"[grey]… and {total - MaxElementRows} more[/]"));
			}

			return table;
		}

		static TestDevice? SafeDevice(IApp app)
		{
			try
			{
				return app.Config.GetProperty<TestDevice>("TestDevice");
			}
			catch
			{
				return null;
			}
		}

		static string Truncate(string value, int length)
		{
			value = value.ReplaceLineEndings(" ");
			return value.Length <= length ? value : value[..(length - 1)] + "…";
		}

		sealed record ElementDetails(string? Type, string? Id, string? Text, string? Bounds, string State)
		{
			public static ElementDetails Read(IUIElement element, TestDevice? device, bool withRect)
			{
				if (element is not AppiumDriverElement { AppiumElement: { } e })
				{
					return new ElementDetails(element.GetType().Name, null, null, null, string.Empty);
				}

				var idAttribute = device switch
				{
					TestDevice.Android => "resource-id",
					TestDevice.iOS => "name",
					TestDevice.Mac => "identifier",
					TestDevice.Windows => "AutomationId",
					_ => "name",
				};

				var typeAttribute = device switch
				{
					TestDevice.Android => "class",
					TestDevice.iOS => "type",
					TestDevice.Windows => "ClassName",
					_ => null,
				};

				var type = (typeAttribute is null ? null : Try(() => e.GetAttribute(typeAttribute))) ?? Try(() => e.TagName);
				if (!string.IsNullOrEmpty(type))
				{
					type = type.StartsWith("XCUIElementType", StringComparison.Ordinal) ? type["XCUIElementType".Length..] : type[(type.LastIndexOf('.') + 1)..];
				}

				var id = Try(() => e.GetAttribute(idAttribute));
				if (id is not null)
				{
					var marker = id.IndexOf(":id/", StringComparison.Ordinal);
					id = marker >= 0 ? id[(marker + 4)..] : id;
				}

				var text = Try(() => e.Text);

				// The accessibility id falls back to the label on iOS, which isn't an id.
				if (id is not null && id == text)
				{
					id = null;
				}

				string? bounds = null;
				string state = string.Empty;

				if (withRect)
				{
					var location = Try(() => e.Location);
					var size = Try(() => e.Size);
					if (location is { } l && size is { } s)
					{
						bounds = $"{l.X},{l.Y} {s.Width}×{s.Height}";
					}

					var displayed = Try(() => e.Displayed);
					var enabled = Try(() => e.Enabled);
					state = string.Join(' ', new[]
					{
						displayed == false ? "[red]hidden[/]" : displayed == true ? "[green]visible[/]" : null,
						enabled == false ? "[red]disabled[/]" : null,
					}.Where(s => s is not null));
				}

				return new ElementDetails(type, string.IsNullOrEmpty(id) ? null : id, string.IsNullOrEmpty(text) ? null : text, bounds, state);
			}

			static T? Try<T>(Func<T> read)
			{
				try
				{
					return read();
				}
				catch
				{
					return default;
				}
			}
		}
	}
}
