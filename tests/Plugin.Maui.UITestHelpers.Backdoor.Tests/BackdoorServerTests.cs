using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;

namespace Plugin.Maui.UITestHelpers.Backdoor.Tests;

// Talks to the backdoor server over real sockets with raw HTTP, so the security checks are tested
// the way clients (and attackers) actually reach them. Reflection-based JSON is disabled in this
// project, like in trimmed .NET MAUI apps.
public class BackdoorServerTests
{
	const string Key = "0123456789abcdef0123456789abcdef";

	BackdoorServer? _server;

	[TearDown]
	public void StopServer() => _server?.Dispose();

	class Dto
	{
		public string Name { get; set; } = "visible";

		[JsonIgnore]
		public string Secret { get; set; } = "hidden";

		[JsonPropertyName("renamed")]
		public int Value { get; set; } = 42;
	}

	BackdoorServer Start(Action<BackdoorOptions>? configure = null, TimeSpan? readTimeout = null)
	{
		var options = new BackdoorOptions { Port = 0 };
		options.AddMethod("Echo", (string text) => text);
		options.AddMethod("Half", (int value) => value / 2);
		options.AddMethod("Pick", (string text) => "string").AddMethod("Pick", (int value) => "int");
		options.AddMethod("Amb", (int value) => "int").AddMethod("Amb", (long value) => "long");
		options.AddMethod("Optional", (string name, string greeting = "Hello") => $"{greeting}, {name}");
		options.AddMethod("Big", () => 9007199254740993L);
		options.AddMethod("Async", async () => { await Task.Delay(10); return 7; });
		options.AddMethod("Day", (DayOfWeek day) => day);
		options.AddMethod("Dto", () => new Dto());
		options.AddMethod("Anonymous", () => new { Name = "x", Items = new[] { 1, 2 } });
		options.AddMethod("Throw", new Action(() => throw new InvalidOperationException("boom")));
		configure?.Invoke(options);

		_server = new BackdoorServer(options, invoke => invoke()) { RequestReadTimeout = readTimeout ?? TimeSpan.FromSeconds(15) };
		_server.Start();
		return _server;
	}

	static async Task<(int Status, JsonElement Body)> SendAsync(BackdoorServer server, string method, string path, string? body = null, Dictionary<string, string>? headers = null)
	{
		headers ??= new();
		headers.TryAdd("Host", $"127.0.0.1:{server.Port}");
		if (body is not null)
			headers.TryAdd("Content-Type", "application/json");

		var bodyBytes = body is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
		var request = new StringBuilder($"{method} {path} HTTP/1.1\r\n");
		foreach (var (name, value) in headers)
			request.Append($"{name}: {value}\r\n");
		request.Append($"Content-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n");

		return await SendRawAsync(server, Encoding.ASCII.GetBytes(request.ToString()).Concat(bodyBytes).ToArray());
	}

	static async Task<(int Status, JsonElement Body)> SendRawAsync(BackdoorServer server, byte[] request)
	{
		using var client = new TcpClient();
		await client.ConnectAsync("127.0.0.1", server.Port);
		var stream = client.GetStream();
		await stream.WriteAsync(request);

		using var response = new MemoryStream();
		await stream.CopyToAsync(response).WaitAsync(TimeSpan.FromSeconds(10));
		var text = Encoding.UTF8.GetString(response.ToArray());
		var status = int.Parse(text.Split(' ')[1]);
		var json = text[(text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..];
		return (status, JsonDocument.Parse(json).RootElement.Clone());
	}

	static Task<(int Status, JsonElement Body)> InvokeAsync(BackdoorServer server, string json, Dictionary<string, string>? headers = null) =>
		SendAsync(server, "POST", "/backdoor", json, headers);

	static string Reason(JsonElement body) => body.GetProperty("reason").GetString()!;

	[Test]
	public async Task Ping_ListsMethods()
	{
		var server = Start();

		var (status, body) = await SendAsync(server, "GET", "/ping");

		Assert.That(status, Is.EqualTo(200));
		var methods = body.GetProperty("result").GetProperty("methods").EnumerateArray().Select(e => e.GetString());
		Assert.That(methods, Does.Contain("Echo").And.Contain("Pick"));
	}

	[TestCase("{\"method\":\"Echo\",\"arguments\":[\"hi\"]}", "\"hi\"")]
	[TestCase("{\"method\":\"Optional\",\"arguments\":[\"Maui\"]}", "\"Hello, Maui\"")]
	[TestCase("{\"method\":\"Optional\",\"arguments\":[\"Maui\",\"Hi\"]}", "\"Hi, Maui\"")]
	[TestCase("{\"method\":\"Pick\",\"arguments\":[42]}", "\"int\"")]
	[TestCase("{\"method\":\"Pick\",\"arguments\":[\"42\"]}", "\"string\"")]
	[TestCase("{\"method\":\"Big\"}", "9007199254740993")]
	[TestCase("{\"method\":\"Async\"}", "7")]
	[TestCase("{\"method\":\"Day\",\"arguments\":[\"Friday\"]}", "\"Friday\"")]
	[TestCase("{\"method\":\"echo:\",\"arguments\":[\"xamarin\"]}", "\"xamarin\"")]
	[TestCase("{\"method\":\"Anonymous\"}", "{\"name\":\"x\",\"items\":[1,2]}")]
	public async Task Invoke_ReturnsResult(string request, string expectedResult)
	{
		var server = Start();

		var (status, body) = await InvokeAsync(server, request);

		Assert.That(status, Is.EqualTo(200), body.ToString());
		Assert.That(body.GetProperty("result").GetRawText(), Is.EqualTo(expectedResult));
	}

	[Test]
	public async Task Invoke_TrimmedFallbackHonorsJsonAttributes()
	{
		Assert.That(JsonSerializer.IsReflectionEnabledByDefault, Is.False, "this test needs reflection-based JSON to be disabled");
		var server = Start();

		var (status, body) = await InvokeAsync(server, "{\"method\":\"Dto\"}");

		Assert.That(status, Is.EqualTo(200));
		Assert.That(body.GetProperty("result").GetRawText(), Is.EqualTo("{\"name\":\"visible\",\"renamed\":42}"));
	}

	[TestCase("{\"method\":\"Echo\",\"arguments\":[5]}", 400, "cannot be converted")]
	[TestCase("{\"method\":\"Half\",\"arguments\":[2.5]}", 400, "not a whole number")]
	[TestCase("{\"method\":\"Half\",\"arguments\":[99999999999]}", 400, "cannot be converted")]
	[TestCase("{\"method\":\"Amb\",\"arguments\":[1]}", 409, "match 2 overloads")]
	[TestCase("{\"method\":\"Echo\"}", 400, "accepts 0 argument")]
	[TestCase("{\"method\":\"Nope\"}", 404, "No backdoor method named 'Nope'")]
	[TestCase("{\"arguments\":[]}", 400, "'method' is required")]
	[TestCase("not json", 400, "Invalid request")]
	public async Task Invoke_InvalidRequests_AreRejected(string request, int expectedStatus, string expectedReason)
	{
		var server = Start();

		var (status, body) = await InvokeAsync(server, request);

		Assert.That(status, Is.EqualTo(expectedStatus));
		Assert.That(Reason(body), Does.Contain(expectedReason));
	}

	[Test]
	public async Task Invoke_MethodThrows_ReturnsAppException()
	{
		var server = Start();

		var (status, body) = await InvokeAsync(server, "{\"method\":\"Throw\"}");

		Assert.That(status, Is.EqualTo(500));
		Assert.That(body.GetProperty("exceptionType").GetString(), Is.EqualTo(typeof(InvalidOperationException).FullName));
		Assert.That(Reason(body), Does.Contain("boom"));
	}

	[Test]
	public async Task Security_WebPageRequests_AreRejected()
	{
		var server = Start();

		var withOrigin = await InvokeAsync(server, "{\"method\":\"Echo\",\"arguments\":[\"x\"]}", new() { ["Origin"] = "https://evil.example" });
		var plainText = await InvokeAsync(server, "{\"method\":\"Echo\",\"arguments\":[\"x\"]}", new() { ["Content-Type"] = "text/plain" });
		var rebinding = await SendAsync(server, "GET", "/ping", headers: new() { ["Host"] = $"evil.example:{server.Port}" });

		Assert.That(withOrigin.Status, Is.EqualTo(403));
		Assert.That(plainText.Status, Is.EqualTo(415));
		Assert.That(rebinding.Status, Is.EqualTo(403), "DNS rebinding: a host name other than localhost must be rejected");
	}

	[TestCase("localhost")]
	[TestCase("127.0.0.1")]
	[TestCase("[::1]")]
	public async Task Security_LoopbackHostNames_AreAllowed(string host)
	{
		var server = Start();

		var (status, _) = await SendAsync(server, "GET", "/ping", headers: new() { ["Host"] = $"{host}:{server.Port}" });

		Assert.That(status, Is.EqualTo(200));
	}

	[TestCase(null)]
	[TestCase("wrong")]
	[TestCase("0123456789abcdef0123456789abcde")]
	[TestCase("0123456789abcdef0123456789abcdefX")]
	public async Task Security_AccessKey_IsRequired(string? key)
	{
		var server = Start(o => o.AccessKey = Key);
		var headers = key is null ? new Dictionary<string, string>() : new() { ["X-Backdoor-Key"] = key };

		var ping = await SendAsync(server, "GET", "/ping", headers: new(headers));
		var invoke = await InvokeAsync(server, "{\"method\":\"Echo\",\"arguments\":[\"x\"]}", new(headers));

		Assert.That(ping.Status, Is.EqualTo(401), "the method list must not be visible without the key");
		Assert.That(invoke.Status, Is.EqualTo(401));
	}

	[Test]
	public async Task Security_ConflictingAccessKeyHeaders_AreRejected()
	{
		var server = Start(o => o.AccessKey = Key);
		var request = Encoding.ASCII.GetBytes(
			$"GET /ping HTTP/1.1\r\nHost: 127.0.0.1\r\nX-Backdoor-Key: wrong\r\nX-Backdoor-Key: {Key}\r\nConnection: close\r\n\r\n");

		using var client = new TcpClient();
		await client.ConnectAsync("127.0.0.1", server.Port);
		await client.GetStream().WriteAsync(request);
		using var response = new MemoryStream();
		await client.GetStream().CopyToAsync(response).WaitAsync(TimeSpan.FromSeconds(10));

		Assert.That(Encoding.ASCII.GetString(response.ToArray()), Does.Not.Contain("SUCCESS"));
	}

	[Test]
	public async Task Security_ValidAccessKey_IsAccepted()
	{
		var server = Start(o => o.AccessKey = Key);

		var (status, body) = await InvokeAsync(server, "{\"method\":\"Echo\",\"arguments\":[\"x\"]}", new() { ["X-Backdoor-Key"] = Key });

		Assert.That(status, Is.EqualTo(200));
		Assert.That(body.GetProperty("result").GetString(), Is.EqualTo("x"));
	}

	[Test]
	public async Task Security_UnauthenticatedLargeBody_IsRejectedBeforeReadingIt()
	{
		var server = Start(o => o.AccessKey = Key);

		// Claims an allowed body size but never sends it: the server must answer from the headers
		// alone, without allocating or waiting for the body
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var (status, _) = await SendRawAsync(server, Encoding.ASCII.GetBytes(
			"POST /backdoor HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Type: application/json\r\nContent-Length: 1000000\r\n\r\n"));

		Assert.That(status, Is.EqualTo(401));
		Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "the server must not wait for the body of an unauthenticated request");
	}

	[TestCase("Content-Length: 100000000", 413)]
	[TestCase("Content-Length: -1", 413)]
	[TestCase("Content-Length: abc", 413)]
	[TestCase("Transfer-Encoding: chunked", 400)]
	public async Task Security_InvalidBodyFraming_IsRejected(string header, int expectedStatus)
	{
		var server = Start();

		var (status, _) = await SendRawAsync(server, Encoding.ASCII.GetBytes(
			$"POST /backdoor HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Type: application/json\r\n{header}\r\n\r\n"));

		Assert.That(status, Is.EqualTo(expectedStatus));
	}

	[Test]
	public async Task Security_SlowClients_AreLimitedAndTimedOut()
	{
		var server = Start(readTimeout: TimeSpan.FromSeconds(6));

		// Fill all connection slots with clients that never finish their request
		var idle = new List<TcpClient>();
		for (var i = 0; i < 8; i++)
		{
			var c = new TcpClient();
			await c.ConnectAsync("127.0.0.1", server.Port);
			await c.GetStream().WriteAsync("GET /ping HTTP/1.1\r\n"u8.ToArray());
			idle.Add(c);
		}

		await Task.Delay(300);

		// A further connection is dropped instead of queued
		using (var extra = new TcpClient())
		{
			await extra.ConnectAsync("127.0.0.1", server.Port);
			// Well before the read timeout, so this isn't the timeout closing it
			var read = await extra.GetStream().ReadAsync(new byte[16]).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
			Assert.That(read, Is.EqualTo(0), "the connection over the limit must be closed");
		}

		// After the read timeout the slow clients are disconnected and the backdoor works again
		await Task.Delay(TimeSpan.FromSeconds(6));
		var (status, _) = await SendAsync(server, "GET", "/ping");
		Assert.That(status, Is.EqualTo(200));

		foreach (var c in idle)
			c.Dispose();
	}

	[Test]
	public void NetworkAccess_RequiresLongKey()
	{
		var options = new BackdoorOptions();

		Assert.Throws<ArgumentException>(() => options.EnableNetworkAccess("short"));
		Assert.That(options.NetworkAccessEnabled, Is.False);

		options.EnableNetworkAccess(Key);
		Assert.That(options.NetworkAccessEnabled, Is.True);
		Assert.That(options.AccessKey, Is.EqualTo(Key));
	}

	[Test]
	public void NetworkAccess_ClearingKeyAfterwards_PreventsStart()
	{
		var options = new BackdoorOptions { Port = 0 }.EnableNetworkAccess(Key);
		options.AccessKey = null;
		options.AddMethod("Echo", (string text) => text);

		using var server = new BackdoorServer(options, invoke => invoke());

		Assert.Throws<InvalidOperationException>(server.Start);
	}

	[Test]
	public async Task NetworkAccess_ListensOnAllInterfacesAndRequiresKey()
	{
		var server = Start(o => o.EnableNetworkAccess(Key));

		var noKey = await SendAsync(server, "GET", "/ping", headers: new() { ["Host"] = $"192.168.1.10:{server.Port}" });
		var withKey = await SendAsync(server, "GET", "/ping", headers: new() { ["Host"] = $"192.168.1.10:{server.Port}", ["X-Backdoor-Key"] = Key });

		Assert.That(noKey.Status, Is.EqualTo(401));
		Assert.That(withKey.Status, Is.EqualTo(200), "in network mode the device's IP address is a valid host");
	}
}
