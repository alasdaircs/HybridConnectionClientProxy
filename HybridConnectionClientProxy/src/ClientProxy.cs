using System.Collections.Concurrent;
using System.Net.Sockets;

using Serilog;

namespace HybridConnectionClientProxy;

public class ClientProxy
{
	private int _connectionCount;
	private readonly ConcurrentDictionary<int, Task> _connections = new();

	private ClientProxy() { }

	private async Task Run(
		IHybridConnectionProvider hycoProvider,
		TcpListener tcpListener,
		string proxyLabel,
		CancellationToken cancellation )
	{
		try
		{
			while( !cancellation.IsCancellationRequested )
			{
				TcpClient tcpClient;
				try
				{
					tcpClient = await tcpListener.AcceptTcpClientAsync( cancellation );
				}
				catch( SocketException ex ) when( !cancellation.IsCancellationRequested && IsTransient( ex ) )
				{
					// e.g. the client reset before the accept completed: keep listening
					Log.Warning( "Proxy {Proxy}: accept failed, continuing: {Error}", proxyLabel, ex.SocketErrorCode );
					await Task.Delay( 100, cancellation );
					continue;
				}

				var id = Interlocked.Increment( ref _connectionCount );
				var connection = ClientProxyConnection.Create( tcpClient, hycoProvider, proxyLabel, id, cancellation );
				_connections[id] = connection;
				_ = connection.ContinueWith( completed => _connections.TryRemove( id, out _ ), TaskScheduler.Default );
			}
		}
		catch( Exception ex ) when( cancellation.IsCancellationRequested && ex is OperationCanceledException or ObjectDisposedException or SocketException )
		{
			// quiet — expected on shutdown
		}
		catch( Exception ex )
		{
			Log.Error( "Proxy {Proxy}: stopped accepting connections: {Error}", proxyLabel, SecretRedactor.Describe( ex ) );
		}
		finally
		{
			// Each connection observes the same cancellation and handles its own errors
			await Task.WhenAll( _connections.Values );
		}
	}

	private static bool IsTransient( SocketException ex )
		=> ex.SocketErrorCode is not ( SocketError.OperationAborted or SocketError.Interrupted or SocketError.NotSocket );

	/// <summary>
	/// Accepts connections on an already started <paramref name="tcpListener"/> and forwards each one through
	/// <paramref name="hycoProvider"/> until <paramref name="cancellation"/> is signalled. The caller owns the
	/// listener lifecycle (Start/Stop). The returned task completes once every forwarded connection has closed.
	/// </summary>
	internal static Task Create(
		IHybridConnectionProvider hycoProvider,
		TcpListener tcpListener,
		CancellationToken cancellation = default,
		string? proxyLabel = null )
	{
		var proxy = new ClientProxy();
		return proxy.Run( hycoProvider, tcpListener, proxyLabel ?? tcpListener.LocalEndpoint.ToString() ?? "?", cancellation );
	}
}
