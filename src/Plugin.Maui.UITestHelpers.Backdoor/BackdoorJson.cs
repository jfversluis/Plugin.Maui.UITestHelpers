using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Plugin.Maui.UITestHelpers.Backdoor;

/// <summary>
/// Converts backdoor arguments and return values from and to JSON. Simple values are
/// converted without reflection, so they also work in trimmed apps where reflection-based
/// <see cref="JsonSerializer"/> support is disabled.
/// </summary>
internal static class BackdoorJson
{
	const int MaxDepth = 32;

	public static object? ConvertArgument(JsonElement element, Type type, JsonSerializerOptions options)
	{
		if (type == typeof(JsonElement))
			return element.Clone();

		if (type == typeof(JsonNode))
			return JsonNode.Parse(element.GetRawText());

		var underlying = Nullable.GetUnderlyingType(type);
		if (element.ValueKind == JsonValueKind.Null)
		{
			if (type.IsValueType && underlying is null)
				throw new ArgumentException($"null cannot be converted to '{type.Name}'.");
			return null;
		}

		type = underlying ?? type;

		if (type == typeof(object))
			return ToPlainObject(element);
		// Conversions are strict, so the right overload is chosen and values are never changed silently
		if (type == typeof(string))
			return element.ValueKind == JsonValueKind.String ? element.GetString() : throw Mismatch(element, type);
		if (type == typeof(bool))
			return element.ValueKind is JsonValueKind.True or JsonValueKind.False ? element.GetBoolean() : throw Mismatch(element, type);
		if (type.IsEnum)
			return element.ValueKind == JsonValueKind.String
				? Enum.Parse(type, element.GetString()!, ignoreCase: true)
				: Enum.ToObject(type, element.GetInt64());
		if (type == typeof(char))
			return element.GetString() is { Length: 1 } s ? s[0] : throw new ArgumentException("Expected a string with one character.");
		if (type == typeof(Guid))
			return element.GetGuid();
		if (type == typeof(DateTime))
			return element.GetDateTime();
		if (type == typeof(DateTimeOffset))
			return element.GetDateTimeOffset();
		if (type == typeof(TimeSpan))
			return TimeSpan.Parse(element.GetString()!, CultureInfo.InvariantCulture);
		if (type == typeof(Uri))
			return new Uri(element.GetString()!, UriKind.RelativeOrAbsolute);
		if (IsNumber(type))
		{
			if (element.ValueKind != JsonValueKind.Number)
				throw Mismatch(element, type);

			if (type == typeof(float))
				return element.GetSingle();
			if (type == typeof(double))
				return element.GetDouble();
			if (type == typeof(decimal))
				return element.GetDecimal();

			// Integer types: reject fractions instead of rounding, Convert throws when out of range
			if (!element.TryGetDecimal(out var number) || decimal.Truncate(number) != number)
				throw new ArgumentException($"{element.GetRawText()} is not a whole number.");
			return Convert.ChangeType(number, type, CultureInfo.InvariantCulture);
		}

		return Deserialize(element, type, options);
	}

	public static void WriteValue(Utf8JsonWriter writer, object? value, Type declaredType, JsonSerializerOptions options, int depth = 0)
	{
		if (depth > MaxDepth)
			throw new InvalidOperationException("The return value is nested too deeply or contains a cycle.");

		switch (value)
		{
			case null:
				writer.WriteNullValue();
				return;
			case string s:
				writer.WriteStringValue(s);
				return;
			case bool b:
				writer.WriteBooleanValue(b);
				return;
			case char c:
				writer.WriteStringValue(c.ToString());
				return;
			case Enum e:
				writer.WriteStringValue(e.ToString());
				return;
			case byte or sbyte or short or ushort or int or uint or long:
				writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
				return;
			case ulong ul:
				writer.WriteNumberValue(ul);
				return;
			case float f:
				writer.WriteNumberValue(f);
				return;
			case double d:
				writer.WriteNumberValue(d);
				return;
			case decimal m:
				writer.WriteNumberValue(m);
				return;
			case Guid g:
				writer.WriteStringValue(g);
				return;
			case DateTime dt:
				writer.WriteStringValue(dt);
				return;
			case DateTimeOffset dto:
				writer.WriteStringValue(dto);
				return;
			case TimeSpan ts:
				writer.WriteStringValue(ts.ToString("c", CultureInfo.InvariantCulture));
				return;
			case Uri uri:
				writer.WriteStringValue(uri.OriginalString);
				return;
			case JsonElement element:
				element.WriteTo(writer);
				return;
			case JsonNode node:
				node.WriteTo(writer);
				return;
			case IDictionary dictionary:
				writer.WriteStartObject();
				foreach (DictionaryEntry entry in dictionary)
				{
					writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
					WriteValue(writer, entry.Value, typeof(object), options, depth + 1);
				}
				writer.WriteEndObject();
				return;
			case IEnumerable enumerable:
				writer.WriteStartArray();
				foreach (var item in enumerable)
					WriteValue(writer, item, typeof(object), options, depth + 1);
				writer.WriteEndArray();
				return;
		}

		WriteObject(writer, value, options, depth);
	}

	static object? ToPlainObject(JsonElement element) => element.ValueKind switch
	{
		JsonValueKind.String => element.GetString(),
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		JsonValueKind.Null or JsonValueKind.Undefined => null,
		JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
		_ => element.Clone(),
	};

	static ArgumentException Mismatch(JsonElement element, Type type) =>
		new($"A JSON {element.ValueKind.ToString().ToLowerInvariant()} cannot be converted to '{type.Name}'.");

	static bool IsNumber(Type type) =>
		type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
		type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
		type == typeof(float) || type == typeof(double) || type == typeof(decimal);

	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Falls back to reflection only when no TypeInfoResolver is configured; documented on BackdoorOptions.JsonSerializerOptions.")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "See above.")]
	static object? Deserialize(JsonElement element, Type type, JsonSerializerOptions options)
	{
		try
		{
			return element.Deserialize(type, options);
		}
		catch (InvalidOperationException ex) when (!JsonSerializer.IsReflectionEnabledByDefault && options.TypeInfoResolver is null)
		{
			throw new InvalidOperationException(
				$"Cannot convert the argument to '{type.FullName}' because reflection-based JSON serialization is disabled in this app. " +
				$"Use simple parameter types, or set a TypeInfoResolver on {nameof(BackdoorOptions)}.{nameof(BackdoorOptions.JsonSerializerOptions)}.", ex);
		}
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Falls back to reflection; documented on BackdoorOptions.JsonSerializerOptions.")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "See above.")]
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Only used when the app assembly isn't fully trimmed; with full trimming or NativeAOT a TypeInfoResolver is required, as documented on BackdoorOptions.JsonSerializerOptions.")]
	static void WriteObject(Utf8JsonWriter writer, object value, JsonSerializerOptions options, int depth)
	{
		var type = value.GetType();

		if (options.TypeInfoResolver is not null || JsonSerializer.IsReflectionEnabledByDefault)
		{
			JsonSerializer.Serialize(writer, value, type, options);
			return;
		}

		// Reflection-based serialization is disabled (trimmed app); write the public properties ourselves
		writer.WriteStartObject();
		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
		{
			if (!property.CanRead || property.GetIndexParameters().Length > 0)
				continue;

			var name = options.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
			writer.WritePropertyName(name);
			WriteValue(writer, property.GetValue(value), property.PropertyType, options, depth + 1);
		}
		writer.WriteEndObject();
	}
}
