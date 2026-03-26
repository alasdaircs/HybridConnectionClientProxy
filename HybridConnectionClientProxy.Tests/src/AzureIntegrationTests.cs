using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Xunit;

namespace HybridConnectionClientProxy.Tests;

/// <summary>
/// Integration tests that run against a real Azure Hybrid Connection endpoint.
///
/// These tests require <c>appsettings.Test.json</c> to be present alongside the test
/// assembly, containing valid Azure Relay credentials in the same format as
/// <c>appsettings.json</c>.  When the file is absent (fork PRs, local runs without
/// credentials) every test is skipped automatically.
///
/// The endpoint referenced by the first proxy in the config must have a listener
/// registered and running for data-flow assertions to pass.
/// </summary>
public class AzureIntegrationTests
{
	private const int TimeoutMs = 30_000;

	// -------------------------------------------------------------------------
	// Tests
	// -------------------------------------------------------------------------

	[Fact]
	public async Task AzureProxy_WhenClientConnects_ConnectionEstablishedWithoutError()
	{
		var connStr = GetConnectionString();
		Skip.If( connStr is null, "No Azure credentials in appsettings.Test.json" );

		using var cts = new CancellationTokenSource( TimeoutMs );

		var provider = new HybridConnectionClientProvider( connStr! );
		var (proxyListener, proxyPort) = StartListener();
		var proxyTask = ClientProxy.Create( provider, proxyListener, cts.Token );

		using var client = new TcpClient();
		await client.ConnectAsync( IPAddress.Loopback, proxyPort, cts.Token );

		// Give the relay a moment to confirm the connection is live
		await Task.Delay( 1_000, cts.Token );

		await cts.CancelAsync();
		proxyListener.Stop();
		await proxyTask;
	}

	[Fact]
	public async Task AzureProxy_WhenClientDisconnects_CleansUpPromptly()
	{
		var connStr = GetConnectionString();
		Skip.If( connStr is null, "No Azure credentials in appsettings.Test.json" );

		using var cts = new CancellationTokenSource( TimeoutMs );

		var provider = new HybridConnectionClientProvider( connStr! );
		var (proxyListener, proxyPort) = StartListener();
		var proxyTask = ClientProxy.Create( provider, proxyListener, cts.Token );

		var client = new TcpClient();
		await client.ConnectAsync( IPAddress.Loopback, proxyPort, cts.Token );
		await Task.Delay( 500, cts.Token ); // let the relay connection settle

		// Disconnect the client — this is the scenario that used to hang
		client.Close();

		// The proxy should clean up within a few seconds; if it hangs the test
		// times out and fails, confirming the regression is present.
		await Task.Delay( 3_000, cts.Token );

		await cts.CancelAsync();
		proxyListener.Stop();
		await proxyTask;
	}

	[Fact]
	public async Task AzureProxy_DataFlow_ClientToBackend()
	{
		var connStr = GetConnectionString();
		Skip.If( connStr is null, "No Azure credentials in appsettings.Test.json" );

		using var cts = new CancellationTokenSource( TimeoutMs );

		var provider = new HybridConnectionClientProvider( connStr! );
		var (proxyListener, proxyPort) = StartListener();
		_ = ClientProxy.Create( provider, proxyListener, cts.Token );

		using var client = new TcpClient();
		await client.ConnectAsync( IPAddress.Loopback, proxyPort, cts.Token );
		var stream = client.GetStream();

		// Send a recognisable payload — the backend is expected to echo it back
		var sent = Encoding.UTF8.GetBytes( "PING" );
		await stream.WriteAsync( sent, cts.Token );
		await stream.FlushAsync( cts.Token );

		var received = new byte[4];
		await ReadExactAsync( stream, received, cts.Token );
		Assert.Equal( sent, received );

		await cts.CancelAsync();
		proxyListener.Stop();
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	private static string? GetConnectionString()
	{
		var path = Path.Combine( AppContext.BaseDirectory, "appsettings.Test.json" );
		if( !File.Exists( path ) ) return null;

		try
		{
			using var doc = JsonDocument.Parse( File.ReadAllText( path ) );
			var proxies = doc.RootElement
				.GetProperty( "AppSettings" )
				.GetProperty( "Proxies" );
			if( proxies.GetArrayLength() == 0 ) return null;
			return proxies[0].GetProperty( "HybridConnectionString" ).GetString();
		}
		catch { return null; }
	}

	private static (TcpListener listener, int port) StartListener()
	{
		var listener = new TcpListener( IPAddress.Loopback, 0 );
		listener.Start();
		return (listener, ((IPEndPoint) listener.LocalEndpoint).Port);
	}

	private static async Task ReadExactAsync( Stream stream, byte[] buffer, CancellationToken ct )
	{
		int total = 0;
		while( total < buffer.Length )
		{
			int n = await stream.ReadAsync( buffer.AsMemory( total ), ct );
			if( n == 0 ) throw new EndOfStreamException( $"Stream ended after {total}/{buffer.Length} bytes." );
			total += n;
		}
	}
}
