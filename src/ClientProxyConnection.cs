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

			// Await both pumps so exceptions are observed and resources released cleanly.
			// Errors here are diagnostic only — the connection is already closing.
			try { await sendPump; }
			catch( Exception ex ) when( ex is not OperationCanceledException )
			{ Log.Debug( ex, "Send pump closed with error" ); }

			try { await receivePump; }
			catch( Exception ex ) when( ex is not OperationCanceledException )
			{ Log.Debug( ex, "Receive pump closed with error" ); }
		}
		catch( OperationCanceledException )
		{
			// quiet — expected on shutdown or when the remote side closes first
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
