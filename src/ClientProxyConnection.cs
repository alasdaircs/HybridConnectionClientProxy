using System.Net.Sockets;

using Microsoft.Azure.Relay;

namespace HybridConnectionClientProxy
{
	public class ClientProxyConnection
		: IDisposable
	{
		private bool disposedValue;
		protected CancellationTokenSource? _cts;

		protected ClientProxyConnection()
		{
		}

		protected async Task Run( TcpClient tcpClient, HybridConnectionClient hycoClient, CancellationToken cancellation )
		{
			try
			{
				_cts = CancellationTokenSource.CreateLinkedTokenSource( cancellation );
				using var tcpStream = tcpClient.GetStream();
				using var hycoStream = await hycoClient.CreateConnectionAsync();

				var sendPump = tcpStream.CopyToAsync( hycoStream, _cts.Token );
				var receivePump = hycoStream.CopyToAsync( tcpStream, _cts.Token );
				await Task.WhenAny( sendPump, receivePump );
				await Task.WhenAll(
					tcpStream.FlushAsync(),
					hycoStream.FlushAsync() 
				);
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

		public static Task Create( TcpClient tcpClient, HybridConnectionClient hycoClient, CancellationToken cancellation )
		{
			var connection = new ClientProxyConnection();
			return connection.Run( tcpClient, hycoClient, cancellation );
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
		// ~ClientProxyConnection()
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
