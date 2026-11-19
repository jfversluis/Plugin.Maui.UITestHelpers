using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using NUnit.Framework;

namespace Plugin.Maui.UITestHelpers.Backdoor.Tests;

// A problem with the backdoor must never keep the app from starting; it only turns the backdoor off
[NonParallelizable]
public class BackdoorStartupTests
{
	class NoBackdoorMethods
	{
		public void NotMarked()
		{
		}
	}

	[TearDown]
	public void StopServer() => BackdoorInitializeService.Server?.Dispose();

	static int FreePort()
	{
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}

	static void BuildApp(Action<BackdoorOptions> configure)
	{
		var builder = MauiApp.CreateBuilder(useDefaults: false);
		builder.UseUITestBackdoor(configure);
		var app = builder.Build();

		// MAUI runs the initialize services when the app starts
		foreach (var service in app.Services.GetServices<IMauiInitializeService>())
			service.Initialize(app.Services);
	}

	static bool IsListening(int port)
	{
		try
		{
			using var client = new TcpClient();
			client.Connect(IPAddress.Loopback, port);
			return true;
		}
		catch (SocketException)
		{
			return false;
		}
	}

	[Test]
	public void ValidConfiguration_StartsBackdoor()
	{
		var port = FreePort();

		BuildApp(b => b.AddMethod("Ping", () => "pong").Port = port);

		Assert.That(IsListening(port), Is.True);
	}

	[Test]
	public void ConfigurationThatThrows_AppStillStarts()
	{
		var port = FreePort();

		Assert.DoesNotThrow(() => BuildApp(b =>
		{
			b.Port = port;
			b.AddMethods<NoBackdoorMethods>();
		}));
		Assert.That(IsListening(port), Is.False, "the backdoor must stay off");
	}

	[Test]
	public void InvalidNetworkConfiguration_AppStillStartsWithoutListening()
	{
		var port = FreePort();

		Assert.DoesNotThrow(() => BuildApp(b =>
		{
			b.Port = port;
			b.AddMethod("Ping", () => "pong");
			b.EnableNetworkAccess("too short");
		}));
		Assert.That(IsListening(port), Is.False, "the backdoor must stay off instead of listening without a valid key");
	}

	[Test]
	public void PortInUse_AppStillStarts()
	{
		using var blocker = new TcpListener(IPAddress.Loopback, 0);
		blocker.Start();
		var port = ((IPEndPoint)blocker.LocalEndpoint).Port;

		Assert.DoesNotThrow(() => BuildApp(b => b.AddMethod("Ping", () => "pong").Port = port));
	}
}
