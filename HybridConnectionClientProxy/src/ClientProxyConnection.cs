using System.Net.Sockets;

using Serilog;

namespace HybridConnectionClientProxy;

internal class ClientProxyConnection
{
	private ClientProxyConnection() { }

	private async Task Run( TcpClient tcpClient, IHybridConnectionProvider hycoProvider, string proxyLabel, int id, CancellationToken cancellation )
	{
		// Both the CancellationTokenSource and the TcpClient are disposed here,
		// eliminating the leaks present in the original design where Create() discarded the instance.
		using var cts = CancellationTokenSource.CreateLinkedTokenSource( cancellation );
		using var _ = tcpClient;

		var remote = tcpClient.Client.RemoteEndPoint;
		Log.Debug( "Proxy {Proxy}: connection {ConnectionId} accepted from {Remote}", proxyLabel, id, remote );

		try
		{
			using var tcpStream = tcpClient.GetStream();

			Stream hycoStream;
			try
			{
				hycoStream = await hycoProvider.CreateConnectionAsync( cts.Token );
			}
			catch( Exception ex ) when( !cts.IsCancellationRequested )
			{
				// The relay (or the on-prem listener behind it) is unavailable: drop this client, keep listening.
				// This includes cancellations the relay library raises itself, e.g. a timeout.
				Log.Warning(
					"Proxy {Proxy}: connection {ConnectionId} dropped, relay connection failed: {Error}",
					proxyLabel, id, SecretRedactor.Describe( ex )
				);
				return;
			}

			using var __ = hycoStream;
			Log.Debug( "Proxy {Proxy}: connection {ConnectionId} relayed", proxyLabel, id );

			var sendPump    = tcpStream.CopyToAsync( hycoStream,  cts.Token );
			var receivePump = hycoStream.CopyToAsync( tcpStream,  cts.Token );

			// Wait for whichever side closes first, then cancel the other pump
			// so it is not left running indefinitely (original bug: orphaned task).
			await Task.WhenAny( sendPump, receivePump );
			await cts.CancelAsync();

			// HybridConnectionStream.ReadAsync does not reliably unblock when its
			// CancellationToken is cancelled, so explicitly close both streams to
			// force any blocked reads to throw immediately rather than hanging.
			try { tcpStream.Close(); }  catch { }
			try { hycoStream.Close(); } catch { }

			// Await both pumps to ensure exceptions are observed and resources released.
			// Any exception here is expected - both streams have been explicitly closed.
			try { await sendPump; }    catch { }
			try { await receivePump; } catch { }

			Log.Debug( "Proxy {Proxy}: connection {ConnectionId} closed", proxyLabel, id );
		}
		catch( OperationCanceledException ) when( cts.IsCancellationRequested )
		{
			// quiet - expected on shutdown or when the remote side closes first
		}
		catch( Exception ex )
		{
			Log.Error( "Proxy {Proxy}: connection {ConnectionId} failed: {Error}", proxyLabel, id, SecretRedactor.Describe( ex ) );
		}
	}

	internal static Task Create( TcpClient tcpClient, IHybridConnectionProvider hycoProvider, string proxyLabel, int id, CancellationToken cancellation )
	{
		var connection = new ClientProxyConnection();
		return connection.Run( tcpClient, hycoProvider, proxyLabel, id, cancellation );
	}
}
