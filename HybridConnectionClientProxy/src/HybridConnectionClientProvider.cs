using Microsoft.Azure.Relay;

namespace HybridConnectionClientProxy;

/// <summary>
/// Production implementation of <see cref="IHybridConnectionProvider"/> backed by <see cref="HybridConnectionClient"/>.
/// Authentication is decided by whoever builds the client; see <see cref="HybridConnectionProviderFactory"/>.
/// </summary>
internal sealed class HybridConnectionClientProvider : IHybridConnectionProvider
{
	private readonly HybridConnectionClient _client;

	public HybridConnectionClientProvider( HybridConnectionClient client )
	{
		_client = client;
	}

	public Uri Address
		=> _client.Address;

	public async Task<Stream> CreateConnectionAsync( CancellationToken cancellationToken = default )
	{
		// HybridConnectionClient.CreateConnectionAsync cannot be cancelled, so on cancellation stop waiting for it
		// and dispose the stream if the attempt completes later, rather than holding up shutdown.
		var connect = _client.CreateConnectionAsync();
		try
		{
			return await connect.WaitAsync( cancellationToken );
		}
		catch( OperationCanceledException )
		{
			_ = connect.ContinueWith(
				attempt =>
				{
					if( attempt.IsCompletedSuccessfully )
						attempt.Result.Dispose();
					else
						_ = attempt.Exception; // observe it
				},
				TaskScheduler.Default
			);
			throw;
		}
	}
}
