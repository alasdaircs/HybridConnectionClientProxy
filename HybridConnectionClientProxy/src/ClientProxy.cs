using System.Net;
using System.Net.Sockets;

using Serilog;

namespace HybridConnectionClientProxy;

public class ClientProxy
{
	private ClientProxy() { }

	private async Task Run(
		IHybridConnectionProvider hycoProvider,
		TcpListener tcpListener,
		bool ownsListener,
		CancellationToken cancellation )
	{
		// CancellationTokenSource is created and disposed within Run, so no leak
		using var cts = CancellationTokenSource.CreateLinkedTokenSource( cancellation );

		if( ownsListener )
			tcpListener.Start();

		try
		{
			while( !cts.IsCancellationRequested )
			{
				var tcpClient = await tcpListener.AcceptTcpClientAsync( cts.Token );
				_ = ClientProxyConnection.Create( tcpClient, hycoProvider, cts.Token );
			}
		}
		catch( OperationCanceledException )
		{
			// quiet — expected on shutdown
		}
		catch( Exception ex )
		{
			Log.Error( ex, "ClientProxy encountered an unexpected error" );
		}
		finally
		{
			if( ownsListener )
				tcpListener.Stop();
		}
	}

	/// <summary>
	/// Creates and starts a proxy that listens on <paramref name="listenAddress"/>:<paramref name="listenPort"/>
	/// and forwards each accepted connection through the given Azure Hybrid Connection string.
	/// </summary>
	public static Task Create(
		string hycoConnectionString,
		IPAddress listenAddress,
		int listenPort,
		CancellationToken cancellation = default )
	{
		var proxy = new ClientProxy();
		var provider = new HybridConnectionClientProvider( hycoConnectionString );
		var listener = new TcpListener( listenAddress, listenPort );
		return proxy.Run( provider, listener, ownsListener: true, cancellation );
	}

	/// <summary>
	/// Internal overload used by tests: caller owns the <paramref name="tcpListener"/> lifecycle
	/// (Start/Stop), allowing the port to be inspected before passing it in.
	/// </summary>
	internal static Task Create(
		IHybridConnectionProvider hycoProvider,
		TcpListener tcpListener,
		CancellationToken cancellation = default )
	{
		var proxy = new ClientProxy();
		return proxy.Run( hycoProvider, tcpListener, ownsListener: false, cancellation );
	}
}
