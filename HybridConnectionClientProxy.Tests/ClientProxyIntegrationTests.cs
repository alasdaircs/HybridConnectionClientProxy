using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HybridConnectionClientProxy.Tests;

/// <summary>
/// End-to-end integration tests for the proxy pipeline using loopback TCP connections.
///
/// Topology:
///   [Test client] --TCP--> [ClientProxy listener] --[TcpHybridConnectionProvider]--> [Backend server]
///
/// No Azure credentials are required; the TcpHybridConnectionProvider forwards each connection
/// to a local TCP server that plays the role of the on-premises backend.
/// </summary>
public class ClientProxyIntegrationTests
{
	private const int TestTimeoutMs = 10_000;

	/// <summary>
	/// Binds to port 0, starts the listener, and returns it together with the OS-assigned port.
	/// </summary>
	private static (TcpListener listener, int port) StartListener()
	{
		var listener = new TcpListener( IPAddress.Loopback, 0 );
		listener.Start();
		var port = ( (IPEndPoint) listener.LocalEndpoint ).Port;
		return (listener, port);
	}

	[Fact]
	public async Task Proxy_ClientToBackend_DataForwardedCorrectly()
	{
		using var cts = new CancellationTokenSource( TestTimeoutMs );

		// Backend — simulates on-premises server
		var (backendListener, backendPort) = StartListener();

		// Proxy — listens for client connections, forwards via mock provider to backend
		var (proxyListener, proxyPort) = StartListener();
		var mockProvider = new TcpHybridConnectionProvider( backendPort );
		var proxyTask = ClientProxy.Create( mockProvider, proxyListener, cts.Token );

		// Connect client to proxy
		using var clientTcp = new TcpClient();
		await clientTcp.ConnectAsync( IPAddress.Loopback, proxyPort, cts.Token );
		var clientStream = clientTcp.GetStream();

		// Accept the corresponding backend connection
		using var backendTcp = await backendListener.AcceptTcpClientAsync( cts.Token );
		var backendStream = backendTcp.GetStream();

		// Send data client → proxy → backend
		var sent = "hello from client"u8.ToArray();
		await clientStream.WriteAsync( sent, cts.Token );
		await clientStream.FlushAsync( cts.Token );

		var received = new byte[sent.Length];
		await ReadExactAsync( backendStream, received, cts.Token );

		Assert.Equal( sent, received );

		await cts.CancelAsync();
		backendListener.Stop();
		proxyListener.Stop();
		await proxyTask;
	}

	[Fact]
	public async Task Proxy_BackendToClient_DataForwardedCorrectly()
	{
		using var cts = new CancellationTokenSource( TestTimeoutMs );

		var (backendListener, backendPort) = StartListener();
		var (proxyListener, proxyPort)   = StartListener();

		var mockProvider = new TcpHybridConnectionProvider( backendPort );
		var proxyTask = ClientProxy.Create( mockProvider, proxyListener, cts.Token );

		using var clientTcp = new TcpClient();
		await clientTcp.ConnectAsync( IPAddress.Loopback, proxyPort, cts.Token );
		var clientStream = clientTcp.GetStream();

		using var backendTcp = await backendListener.AcceptTcpClientAsync( cts.Token );
		var backendStream = backendTcp.GetStream();

		// Send data backend → proxy → client
		var sent = "hello from backend"u8.ToArray();
		await backendStream.WriteAsync( sent, cts.Token );
		await backendStream.FlushAsync( cts.Token );

		var received = new byte[sent.Length];
		await ReadExactAsync( clientStream, received, cts.Token );

		Assert.Equal( sent, received );

		await cts.CancelAsync();
		backendListener.Stop();
		proxyListener.Stop();
		await proxyTask;
	}

	[Fact]
	public async Task Proxy_Bidirectional_BothDirectionsWork()
	{
		using var cts = new CancellationTokenSource( TestTimeoutMs );

		var (backendListener, backendPort) = StartListener();
		var (proxyListener, proxyPort)   = StartListener();

		var mockProvider = new TcpHybridConnectionProvider( backendPort );
		var proxyTask = ClientProxy.Create( mockProvider, proxyListener, cts.Token );

		using var clientTcp = new TcpClient();
		await clientTcp.ConnectAsync( IPAddress.Loopback, proxyPort, cts.Token );
		var clientStream = clientTcp.GetStream();

		using var backendTcp = await backendListener.AcceptTcpClientAsync( cts.Token );
		var backendStream = backendTcp.GetStream();

		// Client → Backend
		var request = "REQUEST"u8.ToArray();
		await clientStream.WriteAsync( request, cts.Token );
		await clientStream.FlushAsync( cts.Token );

		var requestReceived = new byte[request.Length];
		await ReadExactAsync( backendStream, requestReceived, cts.Token );
		Assert.Equal( request, requestReceived );

		// Backend → Client
		var response = "RESPONSE"u8.ToArray();
		await backendStream.WriteAsync( response, cts.Token );
		await backendStream.FlushAsync( cts.Token );

		var responseReceived = new byte[response.Length];
		await ReadExactAsync( clientStream, responseReceived, cts.Token );
		Assert.Equal( response, responseReceived );

		await cts.CancelAsync();
		backendListener.Stop();
		proxyListener.Stop();
		await proxyTask;
	}

	[Fact]
	public async Task Proxy_MultipleSequentialConnections_AllForwardedCorrectly()
	{
		using var cts = new CancellationTokenSource( TestTimeoutMs );

		var (backendListener, backendPort) = StartListener();
		var (proxyListener, proxyPort)   = StartListener();

		var mockProvider = new TcpHybridConnectionProvider( backendPort );
		var proxyTask = ClientProxy.Create( mockProvider, proxyListener, cts.Token );

		for( int i = 0; i < 3; i++ )
		{
			using var clientTcp = new TcpClient();
			await clientTcp.ConnectAsync( IPAddress.Loopback, proxyPort, cts.Token );
			var clientStream = clientTcp.GetStream();

			using var backendTcp = await backendListener.AcceptTcpClientAsync( cts.Token );
			var backendStream = backendTcp.GetStream();

			var message = Encoding.UTF8.GetBytes( $"message-{i}" );
			await clientStream.WriteAsync( message, cts.Token );
			await clientStream.FlushAsync( cts.Token );

			var buffer = new byte[message.Length];
			await ReadExactAsync( backendStream, buffer, cts.Token );
			Assert.Equal( message, buffer );
		}

		await cts.CancelAsync();
		backendListener.Stop();
		proxyListener.Stop();
		await proxyTask;
	}

	[Fact]
	public async Task Proxy_WhenClientDisconnects_ConnectionCleanedUpGracefully()
	{
		using var cts = new CancellationTokenSource( TestTimeoutMs );

		var (backendListener, backendPort) = StartListener();
		var (proxyListener, proxyPort)   = StartListener();

		var mockProvider = new TcpHybridConnectionProvider( backendPort );
		var proxyTask = ClientProxy.Create( mockProvider, proxyListener, cts.Token );

		var clientTcp = new TcpClient();
		await clientTcp.ConnectAsync( IPAddress.Loopback, proxyPort, cts.Token );

		using var backendTcp = await backendListener.AcceptTcpClientAsync( cts.Token );
		var backendStream = backendTcp.GetStream();

		// Close client — backend should observe EOF without throwing
		clientTcp.Close();

		// Backend should receive EOF (ReadAsync returns 0) without hanging
		var readTask = backendStream.ReadAsync( new byte[1], cts.Token ).AsTask();
		var completed = await Task.WhenAny( readTask, Task.Delay( 3_000, cts.Token ) );
		Assert.Equal( readTask, completed );
		Assert.Equal( 0, await readTask );

		await cts.CancelAsync();
		backendListener.Stop();
		proxyListener.Stop();
		await proxyTask;
	}

	[Fact]
	public async Task Proxy_WhenCancelled_StopsAcceptingNewConnections()
	{
		using var cts = new CancellationTokenSource( TestTimeoutMs );

		var (proxyListener, proxyPort) = StartListener();

		// Backend is never used here — we just want to confirm the proxy stops
		var (backendListener, backendPort) = StartListener();
		var mockProvider = new TcpHybridConnectionProvider( backendPort );
		var proxyTask = ClientProxy.Create( mockProvider, proxyListener, cts.Token );

		// Cancel immediately
		await cts.CancelAsync();
		proxyListener.Stop();
		backendListener.Stop();

		// Proxy task should complete cleanly (not throw)
		await proxyTask;
	}

	/// <summary>
	/// Reads exactly <paramref name="buffer"/>.Length bytes, retrying partial reads as needed.
	/// This mirrors real network behaviour where data may arrive in multiple TCP segments.
	/// </summary>
	private static async Task ReadExactAsync( Stream stream, byte[] buffer, CancellationToken ct )
	{
		int totalRead = 0;
		while( totalRead < buffer.Length )
		{
			int n = await stream.ReadAsync( buffer.AsMemory( totalRead ), ct );
			if( n == 0 )
				throw new EndOfStreamException( $"Stream ended after {totalRead}/{buffer.Length} bytes." );
			totalRead += n;
		}
	}
}
