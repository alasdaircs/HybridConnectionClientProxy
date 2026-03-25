namespace HybridConnectionClientProxy;

/// <summary>
/// Abstracts the creation of a Hybrid Connection stream, enabling unit testing without live Azure credentials.
/// </summary>
internal interface IHybridConnectionProvider
{
	Task<Stream> CreateConnectionAsync( CancellationToken cancellationToken = default );
}
