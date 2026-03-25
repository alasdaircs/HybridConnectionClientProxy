using Microsoft.Azure.Relay;

namespace HybridConnectionClientProxy;

/// <summary>
/// Production implementation of <see cref="IHybridConnectionProvider"/> backed by <see cref="HybridConnectionClient"/>.
/// </summary>
internal sealed class HybridConnectionClientProvider : IHybridConnectionProvider
{
	private readonly HybridConnectionClient _client;

	public HybridConnectionClientProvider( string connectionString )
	{
		_client = new HybridConnectionClient( connectionString );
	}

	public async Task<Stream> CreateConnectionAsync( CancellationToken cancellationToken = default )
		=> await _client.CreateConnectionAsync( cancellationToken );
}
