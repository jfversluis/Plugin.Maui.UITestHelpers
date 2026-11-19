using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Microsoft.Maui;

namespace Plugin.Maui.UITestHelpers.Backdoor;

/// <summary>
/// Configures the backdoor that UI tests call with <c>app.Invoke(...)</c>.
/// </summary>
public sealed class BackdoorOptions
{
	/// <summary>
	/// Default TCP port of the backdoor. The UI tests use the same default.
	/// </summary>
	public const int DefaultPort = 37265;

	const DynamicallyAccessedMemberTypes MethodMembers =
		DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods;

	readonly List<BackdoorMethod> _methods = new();

	/// <summary>
	/// TCP port that the backdoor listens on. It only accepts connections from the device
	/// itself (loopback). Set the "BackdoorPort" property of the UI test configuration to
	/// the same value when you change it. Defaults to <see cref="DefaultPort"/>.
	/// </summary>
	public int Port { get; set; } = DefaultPort;

	/// <summary>
	/// Minimum length of <see cref="AccessKey"/> when network access is enabled.
	/// </summary>
	public const int MinimumNetworkAccessKeyLength = 16;

	/// <summary>
	/// Optional secret that UI tests must send with every request (the "BackdoorAccessKey"
	/// property of the UI test configuration). Without it, any app on the same device can call
	/// the backdoor. Required when <see cref="NetworkAccessEnabled"/> is <see langword="true"/>.
	/// </summary>
	public string? AccessKey { get; set; }

	/// <summary>
	/// Whether the backdoor listens on all network interfaces instead of only on the device
	/// itself. See <see cref="EnableNetworkAccess(string)"/>.
	/// </summary>
	public bool NetworkAccessEnabled { get; private set; }

	/// <summary>
	/// <para>
	/// LESS SECURE: lets the backdoor accept connections from other machines on the network,
	/// for example to run UI tests against a physical iOS device over Wi-Fi (like
	/// <c>DeviceIp</c> in Xamarin.UITest). By default the backdoor only accepts connections
	/// from the device itself.
	/// </para>
	/// <para>
	/// Anyone on the same network who knows the access key can then invoke the backdoor
	/// methods. Traffic is not encrypted, so the key and all arguments and results can be read
	/// by anyone who can see the network traffic. Only use this on trusted networks, with a
	/// random key that you don't commit to source control, and never in a build that you
	/// distribute.
	/// </para>
	/// </summary>
	/// <param name="accessKey">Secret of at least <see cref="MinimumNetworkAccessKeyLength"/>
	/// characters that the UI tests send with every request.</param>
	/// <returns>The options, to chain calls.</returns>
	public BackdoorOptions EnableNetworkAccess(string accessKey)
	{
		if (string.IsNullOrWhiteSpace(accessKey) || accessKey.Length < MinimumNetworkAccessKeyLength)
			throw new ArgumentException($"Network access requires an access key of at least {MinimumNetworkAccessKeyLength} characters.", nameof(accessKey));

		AccessKey = accessKey;
		NetworkAccessEnabled = true;
		return this;
	}

	internal void Validate()
	{
		if (NetworkAccessEnabled && (string.IsNullOrWhiteSpace(AccessKey) || AccessKey.Length < MinimumNetworkAccessKeyLength))
			throw new InvalidOperationException($"Network access requires an {nameof(AccessKey)} of at least {MinimumNetworkAccessKeyLength} characters.");
	}

	/// <summary>
	/// Options used to convert arguments and return values that aren't simple values
	/// (strings, numbers, booleans, enums and dates). When reflection-based serialization is
	/// disabled (for example with NativeAOT), objects fall back to their public constructors and
	/// properties, which works as long as the app assembly itself isn't fully trimmed (the
	/// .NET MAUI default). With <c>TrimMode=full</c> or NativeAOT, set a
	/// <see cref="JsonSerializerOptions.TypeInfoResolver"/> (for example a source-generated
	/// <c>JsonSerializerContext</c>).
	/// </summary>
	public JsonSerializerOptions JsonSerializerOptions { get; set; } = CreateDefaultJsonSerializerOptions();

	// Enums are sent and returned as their names, also inside objects, like the UI test side does
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Only used with reflection-based serialization; with a TypeInfoResolver its own converters apply.")]
	static JsonSerializerOptions CreateDefaultJsonSerializerOptions() => new(JsonSerializerDefaults.Web)
	{
		Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
	};

	internal IReadOnlyList<BackdoorMethod> Methods => _methods;

	/// <summary>
	/// Exposes a delegate as a backdoor method.
	/// </summary>
	/// <param name="name">Name that UI tests use to invoke the method.</param>
	/// <param name="handler">Method to run. Its parameters are converted from the arguments
	/// of the UI test, its return value (or the result of a returned <see cref="Task{T}"/>)
	/// is sent back to the UI test.</param>
	/// <returns>The options, to chain calls.</returns>
	public BackdoorOptions AddMethod(string name, Delegate handler)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(handler);

		var target = handler.Target;
		_methods.Add(new BackdoorMethod(name, handler.Method, () => target));
		return this;
	}

	/// <summary>
	/// Exposes all methods of <typeparamref name="T"/> marked with
	/// <see cref="BackdoorMethodAttribute"/>. For instance methods the target is resolved at the
	/// moment of invocation: the running <c>Application</c> when it is a <typeparamref name="T"/>
	/// (for example <c>AddMethods&lt;App&gt;()</c>), otherwise <typeparamref name="T"/> is
	/// resolved from the app's services.
	/// </summary>
	/// <typeparam name="T">Type that contains the backdoor methods.</typeparam>
	/// <returns>The options, to chain calls.</returns>
	public BackdoorOptions AddMethods<[DynamicallyAccessedMembers(MethodMembers)] T>()
		where T : class
	{
		AddAttributedMethods(typeof(T), ResolveTarget<T>);
		return this;
	}

	/// <summary>
	/// Exposes all methods of <paramref name="target"/> marked with
	/// <see cref="BackdoorMethodAttribute"/>.
	/// </summary>
	/// <typeparam name="T">Type that contains the backdoor methods.</typeparam>
	/// <param name="target">Object that the methods are invoked on.</param>
	/// <returns>The options, to chain calls.</returns>
	public BackdoorOptions AddMethods<[DynamicallyAccessedMembers(MethodMembers)] T>(T target)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(target);

		AddAttributedMethods(typeof(T), () => target);
		return this;
	}

	void AddAttributedMethods([DynamicallyAccessedMembers(MethodMembers)] Type type, Func<object?> targetProvider)
	{
		const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

		var found = false;
		foreach (var method in type.GetMethods(flags))
		{
			var attribute = method.GetCustomAttribute<BackdoorMethodAttribute>();
			if (attribute is null)
				continue;

			var name = string.IsNullOrWhiteSpace(attribute.Name) ? method.Name : attribute.Name;
			_methods.Add(new BackdoorMethod(name, method, method.IsStatic ? () => null : targetProvider));
			found = true;
		}

		if (!found)
			throw new ArgumentException($"Type '{type.FullName}' has no methods marked with [{nameof(BackdoorMethodAttribute)}].");
	}

	static object ResolveTarget<T>() where T : class
	{
		var platformApplication = IPlatformApplication.Current;

		if (platformApplication?.Application is T application)
			return application;

		if (platformApplication?.Services.GetService(typeof(T)) is T service)
			return service;

		throw new InvalidOperationException(
			$"Could not find an instance of '{typeof(T).FullName}' to invoke the backdoor method on. " +
			$"It must be the running Application or be registered in the app's services; " +
			$"otherwise pass the instance to {nameof(AddMethods)}.");
	}
}

internal sealed record BackdoorMethod(string Name, MethodInfo Method, Func<object?> TargetProvider);
