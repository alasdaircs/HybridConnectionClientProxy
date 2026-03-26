using System.Net.Sockets;

using Serilog;

namespace HybridConnectionClientProxy;

internal class ClientProxyConnection
{
	private ClientProxyConnection() { }

	private async Task Run( TcpClient tcpClient, IHybridConnectionProvider hycoProvider, CancellationToken cancellation )
	{
		// Both the CancellationTokenSource and the TcpClient are disposed here,
		// eliminating the leaks present in the original design where Create() discarded the instance.
		using var cts = CancellationTokenSource.CreateLinkedTokenSource( cancellation );
		using var _ = tcpClient;

		try
		{
			using var tcpStream = tcpClient.GetStream();
			using var hycoStream = await hycoProvider.CreateConnectionAsync( cts.Token );

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
		}
		catch( OperationCanceledException )
		{
			// quiet - expected on shutdown or when the remote side closes first
		}
		catch( Exception ex )
		{
			Log.Error( ex, "ClientProxyConnection encountered an unexpected error" );
		}
	}

	internal static Task Create( TcpClient tcpClient, IHybridConnectionProvider hycoProvider, CancellationToken cancellation )
	{
		var connection = new ClientProxyConnection();
		return connection.Run( tcpClient, hycoProvider, cancellation );
	}
}
