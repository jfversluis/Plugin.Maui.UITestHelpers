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
		if (options.TypeInfoResolver is not null || JsonSerializer.IsReflectionEnabledByDefault)
			return element.Deserialize(type, options);

		// Reflection-based serialization is disabled (trimmed app); build the value ourselves
		return ReadValue(element, type, options);
	}

	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only used when the app assembly isn't fully trimmed; with full trimming or NativeAOT a TypeInfoResolver is required, as documented on BackdoorOptions.JsonSerializerOptions.")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "See above.")]
	[UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "See above.")]
	[UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "See above.")]
	[UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "See above.")]
	[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "See above.")]
	[UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "See above.")]
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "See above.")]
	static object? ReadValue(JsonElement element, Type type, JsonSerializerOptions options)
	{
		// Arrays and collections: T[], List<T>, HashSet<T>, IEnumerable<T>, IList<T> and so on
		var elementType = type.IsArray ? type.GetElementType() : GetEnumerableElementType(type);
		if (elementType is not null && !IsDictionary(type))
		{
			if (element.ValueKind != JsonValueKind.Array)
				throw Mismatch(element, type);

			var items = element.EnumerateArray().Select(e => ConvertArgument(e, elementType, options)).ToList();

			// Read-only interfaces get an array, like System.Text.Json; other interfaces a List<T>
			if (type.IsArray || IsReadOnlyEnumerable(type))
			{
				var array = Array.CreateInstance(elementType, items.Count);
				for (var i = 0; i < items.Count; i++)
					array.SetValue(items[i], i);
				return array;
			}

			var collectionType = type.IsInterface || type.IsAbstract ? typeof(List<>).MakeGenericType(elementType) : type;
			if (!type.IsAssignableFrom(collectionType))
				throw new ArgumentException($"Collection type '{type.Name}' is not supported; use an array, List<T> or IList<T>.");

			var collection = Activator.CreateInstance(collectionType)
				?? throw new ArgumentException($"Cannot create collection type '{type.Name}'.");
			var add = collectionType.GetMethod("Add", new[] { elementType })
				?? throw new ArgumentException($"Collection type '{type.Name}' has no Add method.");
			foreach (var item in items)
				add.Invoke(collection, BindingFlags.DoNotWrapExceptions, null, new[] { item }, null);
			return collection;
		}

		if (element.ValueKind != JsonValueKind.Object)
			throw Mismatch(element, type);

		var lookup = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
		foreach (var property in element.EnumerateObject())
			lookup.TryAdd(property.Name, property.Value);

		// Dictionaries with string keys
		if (IsDictionary(type))
		{
			var arguments = type.GetGenericArguments();
			if (arguments.Length != 2 || arguments[0] != typeof(string))
				throw new ArgumentException($"Only dictionaries with string keys are supported, not '{type.Name}'.");

			var dictionaryType = type.IsInterface ? typeof(Dictionary<,>).MakeGenericType(arguments) : type;
			var dictionary = (IDictionary)Activator.CreateInstance(dictionaryType)!;
			foreach (var property in element.EnumerateObject())
				dictionary[property.Name] = ConvertArgument(property.Value, arguments[1], options);
			return dictionary;
		}

		if (type.IsInterface || type.IsAbstract)
			throw new ArgumentException($"Cannot create an instance of '{type.Name}' because it is an interface or abstract class.");

		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
			.Where(p => p.GetIndexParameters().Length == 0)
			.ToList();

		string JsonName(PropertyInfo property) =>
			property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name
			?? options.PropertyNamingPolicy?.ConvertName(property.Name)
			?? property.Name;

		static bool IsIgnored(PropertyInfo property) =>
			property.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>()?.Condition == System.Text.Json.Serialization.JsonIgnoreCondition.Always;

		JsonElement? Find(PropertyInfo property) =>
			!IsIgnored(property) && (lookup.TryGetValue(JsonName(property), out var value) || lookup.TryGetValue(property.Name, out value))
				? value
				: null;

		// Constructor selection like System.Text.Json: [JsonConstructor], else the public
		// parameterless constructor, else the only public constructor (records)
		var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
		var constructor = constructors.FirstOrDefault(c => c.GetCustomAttribute<System.Text.Json.Serialization.JsonConstructorAttribute>() is not null)
			?? constructors.FirstOrDefault(c => c.GetParameters().Length == 0)
			?? (constructors.Length == 1 ? constructors[0] : null);

		if (constructor is null && !type.IsValueType)
			throw new ArgumentException($"Cannot create '{type.Name}': it needs a public parameterless constructor, a single public constructor, or one marked with [JsonConstructor].");

		var populated = new HashSet<PropertyInfo>();
		object instance;
		if (constructor is null)
		{
			instance = Activator.CreateInstance(type)!;
		}
		else
		{
			var values = constructor.GetParameters().Select(parameter =>
			{
				// Constructor parameters bind to the property with the same name, and use its JSON name
				var property = properties.FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
				JsonElement? value = property is not null
					? Find(property)
					: lookup.TryGetValue(parameter.Name ?? string.Empty, out var raw) ? raw : null;

				if (value is null)
				{
					if (parameter.HasDefaultValue)
						return parameter.DefaultValue;
					return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
				}

				if (property is not null)
					populated.Add(property);
				return ConvertArgument(value.Value, parameter.ParameterType, options);
			}).ToArray();

			instance = constructor.Invoke(BindingFlags.DoNotWrapExceptions, null, values, null);
		}

		foreach (var property in properties)
		{
			if (populated.Contains(property) || property.GetSetMethod() is null)
				continue;

			if (Find(property) is JsonElement value)
			{
				property.SetValue(instance, ConvertArgument(value, property.PropertyType, options), BindingFlags.DoNotWrapExceptions, null, null, null);
				populated.Add(property);
			}
		}

		var missing = properties
			.Where(p => !populated.Contains(p) &&
				(p.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>() is not null ||
				 p.GetCustomAttribute<System.Text.Json.Serialization.JsonRequiredAttribute>() is not null))
			.Select(JsonName)
			.ToList();
		if (missing.Count > 0)
			throw new ArgumentException($"'{type.Name}' is missing required properties: {string.Join(", ", missing.Select(m => $"'{m}'"))}.");

		return instance;
	}

	static bool IsReadOnlyEnumerable(Type type) =>
		type.IsGenericType && type.GetGenericTypeDefinition() is var definition &&
		(definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyList<>) || definition == typeof(IReadOnlyCollection<>));

	[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Interfaces of argument types; see ReadValue.")]
	static Type? GetEnumerableElementType(Type type)
	{
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			return type.GetGenericArguments()[0];

		return type.GetInterfaces()
			.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			?.GetGenericArguments()[0];
	}

	[UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Interfaces of argument types; see ReadValue.")]
	static bool IsDictionary(Type type) =>
		typeof(IDictionary).IsAssignableFrom(type) ||
		(type.IsGenericType && type.GetGenericTypeDefinition() is var definition &&
			(definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>)));

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

			// Honor the System.Text.Json attributes, so properties hidden from JSON stay hidden
			var ignore = property.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>();
			if (ignore is not null && ignore.Condition == System.Text.Json.Serialization.JsonIgnoreCondition.Always)
				continue;

			var propertyValue = property.GetValue(value);
			if (propertyValue is null && ignore?.Condition is System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull or System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)
				continue;

			var name = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name
				?? options.PropertyNamingPolicy?.ConvertName(property.Name)
				?? property.Name;
			writer.WritePropertyName(name);
			WriteValue(writer, propertyValue, property.PropertyType, options, depth + 1);
		}
		writer.WriteEndObject();
	}
}
