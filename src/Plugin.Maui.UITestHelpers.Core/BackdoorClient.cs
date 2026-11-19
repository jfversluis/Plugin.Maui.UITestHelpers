using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Plugin.Maui.UITestHelpers.Core
{
	/// <summary>
	/// Calls backdoor methods of an app that uses the Plugin.Maui.UITestHelpers.Backdoor package.
	/// </summary>
	public class BackdoorClient : IDisposable
	{
		/// <summary>
		/// Default TCP port of the backdoor in the app.
		/// </summary>
		public const int DefaultPort = 37265;

		// The app returns enums as their names
		internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
		{
			Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
		};

		/// <summary>
		/// HTTP header that carries the access key.
		/// </summary>
		public const string AccessKeyHeader = "X-Backdoor-Key";

		readonly HttpClient _httpClient;

		/// <summary>
		/// Creates a client for the backdoor at <paramref name="baseAddress"/>.
		/// </summary>
		/// <param name="baseAddress">Address of the backdoor, for example http://127.0.0.1:37265/.</param>
		/// <param name="timeout">Maximum time a backdoor call may take.</param>
		/// <param name="accessKey">Access key of the backdoor, if the app sets one.</param>
		public BackdoorClient(Uri baseAddress, TimeSpan timeout, string? accessKey = null)
		{
			BaseAddress = baseAddress ?? throw new ArgumentNullException(nameof(baseAddress));
			_httpClient = new HttpClient(new SocketsHttpHandler { UseProxy = false })
			{
				BaseAddress = baseAddress,
				Timeout = timeout,
			};

			if (!string.IsNullOrEmpty(accessKey))
				_httpClient.DefaultRequestHeaders.Add(AccessKeyHeader, accessKey);
		}

		public Uri BaseAddress { get; }

		/// <summary>
		/// Invokes a backdoor method in the app.
		/// </summary>
		/// <param name="methodName">Name of the backdoor method.</param>
		/// <param name="arguments">Arguments for the method; they are sent as JSON.</param>
		/// <returns>The JSON return value of the method.</returns>
		/// <exception cref="BackdoorException">The backdoor could not be reached or the method failed.</exception>
		public JsonElement Invoke(string methodName, object?[]? arguments)
		{
			if (string.IsNullOrWhiteSpace(methodName))
				throw new ArgumentNullException(nameof(methodName));

			var payload = JsonSerializer.Serialize(new { method = methodName, arguments = arguments ?? Array.Empty<object?>() }, SerializerOptions);
			using var request = new HttpRequestMessage(HttpMethod.Post, "backdoor")
			{
				Content = new StringContent(payload, Encoding.UTF8, "application/json"),
			};

			return Send(request, $"invoke backdoor method '{methodName}'");
		}

		/// <summary>
		/// Returns the names of the backdoor methods that the app exposes.
		/// </summary>
		/// <exception cref="BackdoorException">The backdoor could not be reached.</exception>
		public IReadOnlyList<string> GetMethodNames() =>
			Ping().GetProperty("methods").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();

		/// <summary>
		/// Returns the package name or bundle identifier of the app the backdoor runs in.
		/// </summary>
		/// <exception cref="BackdoorException">The backdoor could not be reached.</exception>
		public string? GetAppId() =>
			Ping().TryGetProperty("appId", out var appId) ? appId.GetString() : null;

		JsonElement Ping()
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, "ping");
			return Send(request, "reach the backdoor");
		}

		/// <summary>
		/// Converts a JSON return value to a simple .NET value: <see cref="string"/>,
		/// <see cref="bool"/>, <see cref="long"/>, <see cref="double"/> or <see langword="null"/>.
		/// Objects and arrays are returned as <see cref="JsonElement"/>.
		/// </summary>
		public static object? ToObject(JsonElement element) => element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null or JsonValueKind.Undefined => null,
			JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
			_ => element,
		};

		/// <summary>
		/// Converts a JSON return value to <typeparamref name="T"/>.
		/// </summary>
		public static T? ToObject<T>(JsonElement element) => element.Deserialize<T>(SerializerOptions);

		JsonElement Send(HttpRequestMessage request, string action)
		{
			HttpResponseMessage response;
			try
			{
				response = _httpClient.Send(request);
			}
			catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException)
			{
				throw new BackdoorException(
					$"Could not {action}: the backdoor at {BaseAddress} could not be reached ({ex.Message}). " +
					"Make sure the app is running and calls builder.UseUITestBackdoor(...) from the Plugin.Maui.UITestHelpers.Backdoor package, " +
					"and that the \"BackdoorPort\" configuration matches the port of the app. If it does, the app's log has a 'UI test backdoor' message with the reason.", ex);
			}
			catch (TaskCanceledException ex)
			{
				throw new BackdoorException($"Could not {action}: the backdoor at {BaseAddress} did not respond within {_httpClient.Timeout}.", ex);
			}

			using (response)
			{
				using var stream = response.Content.ReadAsStream();
				JsonDocument document;
				try
				{
					document = JsonDocument.Parse(stream);
				}
				catch (JsonException ex)
				{
					throw new BackdoorException($"Could not {action}: the backdoor at {BaseAddress} returned an invalid response (HTTP {(int)response.StatusCode}).", ex);
				}

				using (document)
				{
					var root = document.RootElement;
					var outcome = root.TryGetProperty("outcome", out var o) ? o.GetString() : null;
					if (!string.Equals(outcome, "SUCCESS", StringComparison.OrdinalIgnoreCase))
					{
						var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : $"HTTP {(int)response.StatusCode}";
						var exceptionType = root.TryGetProperty("exceptionType", out var t) ? t.GetString() : null;
						var stackTrace = root.TryGetProperty("stackTrace", out var s) ? s.GetString() : null;
						throw new BackdoorException($"Could not {action}: {reason}", exceptionType, stackTrace);
					}

					return root.TryGetProperty("result", out var result) ? result.Clone() : default;
				}
			}
		}

		public void Dispose()
		{
			_httpClient.Dispose();
			GC.SuppressFinalize(this);
		}
	}

	/// <summary>
	/// Thrown when a backdoor method could not be invoked or threw an exception in the app.
	/// </summary>
	public class BackdoorException : Exception
	{
		public BackdoorException(string message, Exception? innerException = null)
			: base(message, innerException)
		{
		}

		public BackdoorException(string message, string? appExceptionType, string? appStackTrace)
			: base(message)
		{
			AppExceptionType = appExceptionType;
			AppStackTrace = appStackTrace;
		}

		/// <summary>
		/// Full type name of the exception that the backdoor method threw in the app, if any.
		/// </summary>
		public string? AppExceptionType { get; }

		/// <summary>
		/// Stack trace of the exception in the app, if any.
		/// </summary>
		public string? AppStackTrace { get; }
	}
}
