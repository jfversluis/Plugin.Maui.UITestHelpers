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
	const int MaxHeaderBytes = 64 * 1024;
	const int MaxBodyBytes = 16 * 1024 * 1024;

	static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();

	readonly BackdoorOptions _options;
	readonly Func<Func<Task<InvocationResult>>, Task<InvocationResult>> _dispatch;
	readonly ILogger? _logger;
	readonly CancellationTokenSource _cts = new();
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
		_listener = new TcpListener(IPAddress.Loopback, _options.Port);
		_listener.Start();
		_ = AcceptLoopAsync(_listener, _cts.Token);

		Log(LogLevel.Information, $"UI test backdoor listening on 127.0.0.1:{Port} with {_options.Methods.Count} method(s).");
	}

	public void Dispose()
	{
		_cts.Cancel();
		_listener?.Stop();
		_listener = null;
	}

	async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested)
		{
			TcpClient client;
			try
			{
				client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
			{
				return;
			}
			catch (SocketException ex)
			{
				Log(LogLevel.Warning, $"UI test backdoor failed to accept a connection: {ex.Message}");
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
				var (method, path, headers, body) = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
				var (status, response) = await HandleRequestAsync(method, path, headers, body).ConfigureAwait(false);
				await WriteResponseAsync(stream, status, response, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Log(LogLevel.Warning, $"UI test backdoor failed to handle a request: {ex.Message}");
			}
		}
	}

	internal async Task<(int Status, byte[] Body)> HandleRequestAsync(string method, string path, IReadOnlyDictionary<string, string> headers, byte[] body)
	{
		var route = path.Split('?')[0].TrimEnd('/');

		// Web pages in a browser on the same machine or device can send requests to 127.0.0.1 too.
		// Browsers always send Origin for those and can't send a JSON content type without a
		// preflight request (which this server doesn't answer), so reject both.
		if (headers.ContainsKey("Origin"))
			return (403, Error("Requests from web pages are not allowed."));

		if (method == "GET" && (route is "" or "/ping"))
		{
			var names = _options.Methods.Select(m => m.Name).Distinct().ToArray();
			return (200, Success(names, typeof(string[])));
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

	static async Task<(string Method, string Path, IReadOnlyDictionary<string, string> Headers, byte[] Body)> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
	{
		var buffer = new byte[8192];
		using var received = new MemoryStream();
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
		var headerText = Encoding.ASCII.GetString(data, 0, headerEnd);
		var lines = headerText.Split("\r\n");
		var requestLine = lines[0].Split(' ');
		if (requestLine.Length < 2)
			throw new InvalidDataException($"Invalid request line '{lines[0]}'.");

		var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var line in lines.Skip(1))
		{
			var separator = line.IndexOf(':');
			if (separator > 0)
				headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
		}

		var contentLength = headers.TryGetValue("Content-Length", out var length)
			? int.Parse(length, System.Globalization.CultureInfo.InvariantCulture)
			: 0;

		if (contentLength < 0 || contentLength > MaxBodyBytes)
			throw new InvalidDataException("Request body is too large.");

		var body = new byte[contentLength];
		var bodyStart = headerEnd + HeaderTerminator.Length;
		var alreadyReceived = Math.Min((int)received.Length - bodyStart, contentLength);
		Array.Copy(data, bodyStart, body, 0, alreadyReceived);

		var offset = alreadyReceived;
		while (offset < contentLength)
		{
			var read = await stream.ReadAsync(body.AsMemory(offset), cancellationToken).ConfigureAwait(false);
			if (read == 0)
				throw new IOException("Connection closed before the request body was received.");
			offset += read;
		}

		return (requestLine[0].ToUpperInvariant(), requestLine[1], headers, body);
	}

	static async Task WriteResponseAsync(Stream stream, int status, byte[] body, CancellationToken cancellationToken)
	{
		var reason = status switch
		{
			200 => "OK",
			400 => "Bad Request",
			403 => "Forbidden",
			404 => "Not Found",
			409 => "Conflict",
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
