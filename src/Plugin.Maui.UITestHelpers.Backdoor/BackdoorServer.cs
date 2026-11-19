using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Plugin.Maui.UITestHelpers.Backdoor;

/// <summary>
/// Small HTTP server inside the app under test that executes backdoor methods for UI tests.
/// It only listens on the loopback interface. Protocol:
/// <list type="bullet">
/// <item><description><c>GET /ping</c> returns the names of the available methods.</description></item>
/// <item><description><c>POST /backdoor</c> with <c>{"method":"Name","arguments":[...]}</c> invokes a method.</description></item>
/// </list>
/// Responses are <c>{"outcome":"SUCCESS","result":...}</c> or
/// <c>{"outcome":"ERROR","reason":"...","exceptionType":"...","stackTrace":"..."}</c>.
/// </summary>
internal sealed class BackdoorServer : IDisposable
{
	internal const string AccessKeyHeader = "X-Backdoor-Key";
	const int MaxHeaderBytes = 64 * 1024;
	const int MaxBodyBytes = 1024 * 1024;
	const int MaxConcurrentConnections = 8;
	internal TimeSpan RequestReadTimeout { get; init; } = TimeSpan.FromSeconds(15);

	static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();

	readonly BackdoorOptions _options;
	readonly Func<Func<Task<InvocationResult>>, Task<InvocationResult>> _dispatch;
	readonly ILogger? _logger;
	readonly CancellationTokenSource _cts = new();
	readonly SemaphoreSlim _connections = new(MaxConcurrentConnections, MaxConcurrentConnections);
	readonly object _listenerLock = new();
	TcpListener? _listener;

	public BackdoorServer(BackdoorOptions options, Func<Func<Task<InvocationResult>>, Task<InvocationResult>> dispatch, ILogger? logger = null)
	{
		_options = options;
		_dispatch = dispatch;
		_logger = logger;
	}

	public int Port => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? _options.Port;

	public void Start()
	{
		_options.Validate();

		_listener = CreateListener();
		_ = AcceptLoopAsync(_cts.Token);

		if (_options.NetworkAccessEnabled)
			Log(LogLevel.Warning, $"UI test backdoor listening on ALL network interfaces, port {Port}, with {_options.Methods.Count} method(s). Anyone on the network with the access key can invoke them; never enable this in a build you distribute.");
		else
			Log(LogLevel.Information, $"UI test backdoor listening on 127.0.0.1:{Port} with {_options.Methods.Count} method(s).");
	}

	public void Dispose()
	{
		lock (_listenerLock)
		{
			_cts.Cancel();
			_listener?.Stop();
			_listener = null;
		}
	}

	TcpListener CreateListener()
	{
		if (!_options.NetworkAccessEnabled)
		{
			var loopback = new TcpListener(IPAddress.Loopback, _options.Port);
			loopback.Start();
			return loopback;
		}

		if (Socket.OSSupportsIPv6)
		{
			// Dual mode accepts both IPv4 and IPv6 connections
			var dualMode = new TcpListener(IPAddress.IPv6Any, _options.Port);
			try
			{
				dualMode.Server.DualMode = true;
				dualMode.Start();
				return dualMode;
			}
			catch (SocketException)
			{
				dualMode.Stop();
			}
		}

		var any = new TcpListener(IPAddress.Any, _options.Port);
		any.Start();
		return any;
	}

	async Task AcceptLoopAsync(CancellationToken cancellationToken)
	{
		var failures = 0;
		while (!cancellationToken.IsCancellationRequested)
		{
			TcpClient client;
			try
			{
				var listener = _listener ?? throw new ObjectDisposedException(nameof(BackdoorServer));
				client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
				failures = 0;
			}
			catch (Exception ex) when (ex is OperationCanceledException || cancellationToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex) when (ex is SocketException or ObjectDisposedException or InvalidOperationException)
			{
				// The listening socket can become unusable, for example when iOS reclaims it while the
				// app is suspended. Back off instead of spinning, and recreate the listener.
				failures++;
				Log(LogLevel.Warning, $"UI test backdoor failed to accept a connection: {ex.Message}");

				try
				{
					await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(1000, 10 * Math.Pow(2, failures))), cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					return;
				}

				if (failures >= 3)
				{
					try
					{
						lock (_listenerLock)
						{
							// Don't publish a new listener after Dispose, it would keep the port in use
							if (cancellationToken.IsCancellationRequested)
								return;

							_listener?.Stop();
							_listener = CreateListener();
						}

						Log(LogLevel.Information, $"UI test backdoor recreated its listener on port {Port}.");
					}
					catch (SocketException restartError)
					{
						Log(LogLevel.Warning, $"UI test backdoor could not recreate its listener: {restartError.Message}");
					}
				}

				continue;
			}

			if (!_connections.Wait(0))
			{
				// Too many connections at once; drop this one instead of using more memory
				client.Dispose();
				continue;
			}

			_ = HandleClientAsync(client, cancellationToken);
		}
	}

	async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
	{
		using (client)
		{
			try
			{
				var stream = client.GetStream();
				(int Status, byte[] Body) response;

				// Reading must finish in time, so clients can't hold on to connections (and memory)
				using (var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					readTimeout.CancelAfter(RequestReadTimeout);
					var (method, path, headers, received) = await ReadHeadersAsync(stream, readTimeout.Token).ConfigureAwait(false);

					// Reject requests before reading their body, so unauthenticated clients can't make the app allocate memory
					var rejection = CheckRequest(path, headers, out var contentLength);
					if (rejection is not null)
					{
						response = rejection.Value;
					}
					else
					{
						var body = await ReadBodyAsync(stream, received, contentLength, readTimeout.Token).ConfigureAwait(false);
						response = await HandleRequestAsync(method, path, headers, body).ConfigureAwait(false);
					}
				}

				await WriteResponseAsync(stream, response.Status, response.Body, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				Log(LogLevel.Warning, $"UI test backdoor failed to handle a request: {ex.Message}");
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				_connections.Release();
			}
		}
	}

	(int Status, byte[] Body)? CheckRequest(string path, IReadOnlyDictionary<string, string> headers, out int contentLength)
	{
		contentLength = 0;

		// Web pages in a browser on the same machine or device can send requests to 127.0.0.1 too.
		// Browsers always send Origin for those and can't send a JSON content type without a
		// preflight request (which this server doesn't answer), so reject both.
		if (headers.ContainsKey("Origin"))
			return (403, Error("Requests from web pages are not allowed."));

		// Protects against DNS rebinding, where a web page uses its own host name for 127.0.0.1
		if (!_options.NetworkAccessEnabled && !IsLoopbackHost(headers))
			return (403, Error("Only requests to 127.0.0.1 or localhost are allowed."));

		if (!string.IsNullOrEmpty(_options.AccessKey) && !IsValidAccessKey(headers))
			return (401, Error("Missing or invalid access key. Set the \"BackdoorAccessKey\" property of the UI test configuration to the AccessKey of the app."));

		if (headers.ContainsKey("Transfer-Encoding"))
			return (400, Error("Transfer-Encoding is not supported; send a Content-Length."));

		if (headers.TryGetValue("Content-Length", out var length) &&
			(!int.TryParse(length, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out contentLength) || contentLength > MaxBodyBytes))
			return (413, Error($"The request body must be at most {MaxBodyBytes} bytes."));

		return null;
	}

	static bool IsLoopbackHost(IReadOnlyDictionary<string, string> headers)
	{
		if (!headers.TryGetValue("Host", out var host))
			return true;

		if (host.StartsWith('['))
		{
			var end = host.IndexOf(']');
			host = end > 0 ? host[1..end] : host;
		}
		else
		{
			var colon = host.IndexOf(':');
			host = colon >= 0 ? host[..colon] : host;
		}

		return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
			(IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));
	}

	internal async Task<(int Status, byte[] Body)> HandleRequestAsync(string method, string path, IReadOnlyDictionary<string, string> headers, byte[] body)
	{
		var route = path.Split('?')[0].TrimEnd('/');

		var rejection = CheckRequest(path, headers, out _);
		if (rejection is not null)
			return rejection.Value;

		if (method == "GET" && (route is "" or "/ping"))
		{
			var names = _options.Methods.Select(m => m.Name).Distinct().ToArray();
			var identity = new Dictionary<string, object?>
			{
				["appId"] = GetAppId(),
				["methods"] = names,
			};
			return (200, Success(identity, typeof(Dictionary<string, object?>)));
		}

		if (method == "POST" && route == "/backdoor")
		{
			if (!headers.TryGetValue("Content-Type", out var contentType) ||
				!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
				return (415, Error("The request must have Content-Type: application/json."));

			string? methodName;
			JsonElement[] arguments;
			try
			{
				using var document = JsonDocument.Parse(body);
				var root = document.RootElement;
				methodName = root.TryGetProperty("method", out var m) ? m.GetString() : null;
				arguments = root.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Array
					? a.EnumerateArray().Select(e => e.Clone()).ToArray()
					: Array.Empty<JsonElement>();
			}
			catch (JsonException ex)
			{
				return (400, Error($"Invalid request: {ex.Message}"));
			}

			if (string.IsNullOrWhiteSpace(methodName))
				return (400, Error("Invalid request: 'method' is required."));

			return await InvokeAsync(methodName, arguments).ConfigureAwait(false);
		}

		return (404, Error($"Unknown endpoint '{method} {path}'."));
	}

	static string? GetAppId()
	{
		try
		{
			return Microsoft.Maui.ApplicationModel.AppInfo.Current.PackageName;
		}
		catch (Exception)
		{
			// AppInfo isn't available outside an app, for example in unit tests
			return null;
		}
	}

	bool IsValidAccessKey(IReadOnlyDictionary<string, string> headers)
	{
		if (!headers.TryGetValue(AccessKeyHeader, out var provided))
			return false;

		// Constant-time comparison, so the key can't be guessed from response times
		return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
			Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(_options.AccessKey!));
	}

	async Task<(int Status, byte[] Body)> InvokeAsync(string methodName, JsonElement[] arguments)
	{
		var candidates = FindMethods(methodName);
		if (candidates.Count == 0)
		{
			var available = string.Join(", ", _options.Methods.Select(m => m.Name).Distinct());
			return (404, Error($"No backdoor method named '{methodName}' was found. Available methods: {(available.Length == 0 ? "(none)" : available)}."));
		}

		var matching = candidates.Where(c => AcceptsArgumentCount(c.Method, arguments.Length)).ToList();
		if (matching.Count == 0)
			return (400, Error($"No backdoor method named '{methodName}' accepts {arguments.Length} argument(s)."));

		// Conversions are strict, so normally only one overload accepts the arguments
		Exception? conversionError = null;
		var converted = new List<(BackdoorMethod Method, object?[] Arguments)>();
		foreach (var candidate in matching)
		{
			try
			{
				converted.Add((candidate, ConvertArguments(candidate.Method, arguments)));
			}
			catch (Exception ex)
			{
				conversionError = ex;
			}
		}

		if (converted.Count > 1)
			return (409, Error($"The arguments match {converted.Count} overloads of backdoor method '{methodName}'. Give the overloads different names."));

		foreach (var (candidate, convertedArguments) in converted)
		{

			InvocationResult result;
			try
			{
				result = await _dispatch(() => InvokeMethodAsync(candidate, convertedArguments)).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				var inner = ex is TargetInvocationException { InnerException: { } innerException } ? innerException : ex;
				Log(LogLevel.Warning, $"UI test backdoor method '{methodName}' threw {inner.GetType().FullName}: {inner.Message}");
				return (500, Error($"{inner.GetType().FullName}: {inner.Message}", inner));
			}

			try
			{
				return (200, Success(result.Value, result.Type));
			}
			catch (Exception ex)
			{
				return (500, Error($"The return value of backdoor method '{methodName}' could not be converted to JSON: {ex.Message}", ex));
			}
		}

		return (400, Error($"The arguments for backdoor method '{methodName}' could not be converted: {conversionError?.Message}", conversionError));
	}

	List<BackdoorMethod> FindMethods(string methodName)
	{
		var exact = _options.Methods.Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal)).ToList();
		if (exact.Count > 0)
			return exact;

		// Be forgiving for tests migrated from Xamarin.UITest, which used Objective-C selectors
		// like "myMethod:" on iOS and Java method names like "myMethod" on Android.
		var colon = methodName.IndexOf(':');
		var normalized = colon >= 0 ? methodName[..colon] : methodName;
		return _options.Methods.Where(m => string.Equals(m.Name, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
	}

	static bool AcceptsArgumentCount(MethodInfo method, int count)
	{
		var parameters = method.GetParameters();
		var required = parameters.Count(p => !p.IsOptional);
		return count >= required && count <= parameters.Length;
	}

	object?[] ConvertArguments(MethodInfo method, JsonElement[] arguments)
	{
		var parameters = method.GetParameters();
		var result = new object?[parameters.Length];

		for (var i = 0; i < parameters.Length; i++)
		{
			var parameter = parameters[i];
			if (i < arguments.Length)
			{
				try
				{
					result[i] = BackdoorJson.ConvertArgument(arguments[i], parameter.ParameterType, _options.JsonSerializerOptions);
				}
				catch (Exception ex)
				{
					throw new ArgumentException($"Argument {i + 1} ({arguments[i].GetRawText()}) cannot be converted to parameter '{parameter.Name}' of type '{parameter.ParameterType.Name}': {ex.Message}", ex);
				}
			}
			else
			{
				result[i] = parameter.HasDefaultValue ? parameter.DefaultValue : Type.Missing;
			}
		}

		return result;
	}

	[DynamicDependency(nameof(Task<object>.Result), typeof(Task<>))]
	[DynamicDependency(nameof(ValueTask<object>.AsTask), typeof(ValueTask<>))]
	[UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The Result property of Task<T> and ValueTask<T> is preserved with DynamicDependency.")]
	static async Task<InvocationResult> InvokeMethodAsync(BackdoorMethod backdoorMethod, object?[] arguments)
	{
		var method = backdoorMethod.Method;
		var target = method.IsStatic ? null : backdoorMethod.TargetProvider();
		var returnType = method.ReturnType;

		var value = method.Invoke(target, arguments);

		if (value is Task task)
		{
			await task;
			return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
				? new InvocationResult(returnType.GetProperty(nameof(Task<object>.Result))!.GetValue(task), returnType.GetGenericArguments()[0])
				: new InvocationResult(null, typeof(void));
		}

		if (value is ValueTask valueTask)
		{
			await valueTask;
			return new InvocationResult(null, typeof(void));
		}

		if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
		{
			var asTask = (Task)returnType.GetMethod(nameof(ValueTask<object>.AsTask))!.Invoke(value, null)!;
			await asTask;
			return new InvocationResult(asTask.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(asTask), returnType.GetGenericArguments()[0]);
		}

		return new InvocationResult(returnType == typeof(void) ? null : value, returnType);
	}

	byte[] Success(object? value, Type type)
	{
		using var buffer = new MemoryStream();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			writer.WriteStartObject();
			writer.WriteString("outcome", "SUCCESS");
			writer.WritePropertyName("result");
			BackdoorJson.WriteValue(writer, value, type, _options.JsonSerializerOptions);
			writer.WriteEndObject();
		}

		return buffer.ToArray();
	}

	static byte[] Error(string reason, Exception? exception = null)
	{
		using var buffer = new MemoryStream();
		using (var writer = new Utf8JsonWriter(buffer))
		{
			writer.WriteStartObject();
			writer.WriteString("outcome", "ERROR");
			writer.WriteString("reason", reason);
			if (exception is not null)
			{
				writer.WriteString("exceptionType", exception.GetType().FullName);
				writer.WriteString("stackTrace", exception.StackTrace);
			}
			writer.WriteEndObject();
		}

		return buffer.ToArray();
	}

	static async Task<(string Method, string Path, IReadOnlyDictionary<string, string> Headers, ArraySegment<byte> Received)> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
	{
		var buffer = new byte[8192];
		var received = new MemoryStream();
		int headerEnd;

		while (true)
		{
			var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
			if (read == 0)
				throw new IOException("Connection closed before the request headers were received.");

			received.Write(buffer, 0, read);
			headerEnd = received.GetBuffer().AsSpan(0, (int)received.Length).IndexOf(HeaderTerminator);
			if (headerEnd >= 0)
				break;

			if (received.Length > MaxHeaderBytes)
				throw new InvalidDataException("Request headers are too large.");
		}

		var data = received.GetBuffer();
		var lines = Encoding.ASCII.GetString(data, 0, headerEnd).Split("\r\n");
		var requestLine = lines[0].Split(' ');
		if (requestLine.Length < 2)
			throw new InvalidDataException($"Invalid request line '{lines[0]}'.");

		var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var line in lines.Skip(1))
		{
			var separator = line.IndexOf(':');
			if (separator <= 0)
				continue;

			var name = line[..separator].Trim();
			var value = line[(separator + 1)..].Trim();

			// Conflicting duplicates of these headers could make requests ambiguous
			if (headers.TryGetValue(name, out var existing) && existing != value &&
				(name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
				 name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
				 name.Equals(AccessKeyHeader, StringComparison.OrdinalIgnoreCase)))
				throw new InvalidDataException($"Conflicting '{name}' headers.");

			headers[name] = value;
		}

		var bodyStart = headerEnd + HeaderTerminator.Length;
		return (requestLine[0].ToUpperInvariant(), requestLine[1], headers, new ArraySegment<byte>(data, bodyStart, (int)received.Length - bodyStart));
	}

	static async Task<byte[]> ReadBodyAsync(Stream stream, ArraySegment<byte> received, int contentLength, CancellationToken cancellationToken)
	{
		var body = new byte[contentLength];
		var alreadyReceived = Math.Min(received.Count, contentLength);
		received.AsSpan(0, alreadyReceived).CopyTo(body);

		var offset = alreadyReceived;
		while (offset < contentLength)
		{
			var read = await stream.ReadAsync(body.AsMemory(offset), cancellationToken).ConfigureAwait(false);
			if (read == 0)
				throw new IOException("Connection closed before the request body was received.");
			offset += read;
		}

		return body;
	}

	static async Task WriteResponseAsync(Stream stream, int status, byte[] body, CancellationToken cancellationToken)
	{
		var reason = status switch
		{
			200 => "OK",
			400 => "Bad Request",
			401 => "Unauthorized",
			403 => "Forbidden",
			404 => "Not Found",
			409 => "Conflict",
			413 => "Content Too Large",
			415 => "Unsupported Media Type",
			_ => "Internal Server Error",
		};

		var header = Encoding.ASCII.GetBytes(
			$"HTTP/1.1 {status} {reason}\r\n" +
			"Content-Type: application/json; charset=utf-8\r\n" +
			$"Content-Length: {body.Length}\r\n" +
			"Connection: close\r\n\r\n");

		await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
		await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
		await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
	}

	void Log(LogLevel level, string message)
	{
		if (_logger is not null)
			_logger.Log(level, "{Message}", message);
		else
			System.Diagnostics.Debug.WriteLine(message);
	}
}

internal readonly record struct InvocationResult(object? Value, Type Type);
