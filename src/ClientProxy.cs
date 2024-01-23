using System.Net;
using System.Net.Sockets;

using Microsoft.Azure.Relay;

namespace HybridConnectionClientProxy
{
	public class ClientProxy
		: IDisposable
	{
		private bool disposedValue;

		protected CancellationTokenSource? _cts;

		protected ClientProxy()
		{
		}

		protected async Task Run( String hycoConnectionString, IPAddress listenAddress, int listenPort, CancellationToken cancellation = default )
		{
			_cts = CancellationTokenSource.CreateLinkedTokenSource( cancellation );
			using var tcpListener = new TcpListener( listenAddress, listenPort );
			var hycoClient = new HybridConnectionClient( hycoConnectionString );
			tcpListener.Start();

			try
			{
				while( !_cts.IsCancellationRequested )
				{
					var tcpClient = await tcpListener.AcceptTcpClientAsync( _cts.Token );
					var _ = ClientProxyConnection.Create( tcpClient, hycoClient, _cts.Token );
				}
			}
			catch( OperationCanceledException )
			{
				// quiet
			}
			catch( Exception ex )
			{
				Console.WriteLine( ex.ToString() );
			}
		}

		public static Task Create( String hycoConnectionString, IPAddress listenAddress, int listenPort, CancellationToken cancellation = default )
		{
			var clientProxy = new ClientProxy();
			return clientProxy.Run( hycoConnectionString, listenAddress, listenPort, cancellation );
		}

		protected virtual void Dispose( bool disposing )
		{
			if( !disposedValue )
			{
				if( disposing )
				{
					// TODO: dispose managed state (managed objects)
					if( _cts != null && _cts.Token.CanBeCanceled )
					{
						_cts.Cancel();
					}
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~ClientProxy()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose( disposing: true );
			GC.SuppressFinalize( this );
		}
	}
}
