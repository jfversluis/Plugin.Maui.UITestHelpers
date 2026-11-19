namespace Plugin.Maui.UITestHelpers.Backdoor;

/// <summary>
/// Marks a method as a backdoor that UI tests can call with <c>app.Invoke("MethodName", ...)</c>.
/// Methods with this attribute are found on the types registered with
/// <see cref="BackdoorOptions.AddMethods{T}()"/> or <see cref="BackdoorOptions.AddMethods{T}(T)"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class BackdoorMethodAttribute : Attribute
{
	/// <summary>
	/// Exposes the method under its own name.
	/// </summary>
	public BackdoorMethodAttribute()
	{
	}

	/// <summary>
	/// Exposes the method under the given name.
	/// </summary>
	/// <param name="name">Name that UI tests use to invoke the method.</param>
	public BackdoorMethodAttribute(string name)
	{
		Name = name;
	}

	/// <summary>
	/// Name that UI tests use to invoke the method, or <see langword="null"/> to use the method name.
	/// </summary>
	public string? Name { get; }
}
