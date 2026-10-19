using HybridConnectionClientProxy.Settings;

using Microsoft.Azure.Relay;

namespace HybridConnectionClientProxy;

/// <summary>
/// Builds the relay connection provider for a proxy, choosing how it authenticates to Azure Relay.
/// </summary>
/// <remarks>
/// Only shared access keys carried in <see cref="Proxy.HybridConnectionString"/> are supported today.
/// An <c>AuthMode</c> setting (<c>SharedAccessKey</c> | <c>EntraId</c>) belongs here: Entra ID would build the
/// client from the hybrid connection address and <c>TokenProvider.CreateAzureActiveDirectoryTokenProvider</c>
/// instead of a connection string, and <see cref="HybridConnectionClientProvider"/> would not change.
/// </remarks>
internal static class HybridConnectionProviderFactory
{
	public static IHybridConnectionProvider Create( Proxy proxy, string proxyLabel )
		=> new HybridConnectionClientProvider( CreateSharedAccessKeyClient( proxy, proxyLabel ) );

	private static HybridConnectionClient CreateSharedAccessKeyClient( Proxy proxy, string proxyLabel )
	{
		if( string.IsNullOrWhiteSpace( proxy.HybridConnectionString ) )
			throw new ConfigurationException( $"Proxy {proxyLabel}: HybridConnectionString is required." );

		RelayConnectionStringBuilder builder;
		try
		{
			builder = new RelayConnectionStringBuilder( proxy.HybridConnectionString );
		}
		catch( Exception ex ) when( ex is ArgumentException or FormatException or UriFormatException )
		{
			throw new ConfigurationException(
				$"Proxy {proxyLabel}: HybridConnectionString is not a valid Azure Relay connection string ({SecretRedactor.Describe( ex )})."
			);
		}

		var missing = new List<string>();
		if( builder.Endpoint is null )
			missing.Add( "Endpoint" );
		if( string.IsNullOrEmpty( builder.EntityPath ) )
			missing.Add( "EntityPath" );
		if( string.IsNullOrEmpty( builder.SharedAccessSignature ) )
		{
			if( string.IsNullOrEmpty( builder.SharedAccessKeyName ) )
				missing.Add( "SharedAccessKeyName" );
			if( string.IsNullOrEmpty( builder.SharedAccessKey ) )
				missing.Add( "SharedAccessKey" );
		}

		if( missing.Count > 0 )
			throw new ConfigurationException(
				$"Proxy {proxyLabel}: HybridConnectionString is missing {string.Join( ", ", missing )}."
			);

		try
		{
			return new HybridConnectionClient( proxy.HybridConnectionString );
		}
		catch( Exception ex ) when( ex is ArgumentException or FormatException or UriFormatException )
		{
			throw new ConfigurationException(
				$"Proxy {proxyLabel}: HybridConnectionString was rejected ({SecretRedactor.Describe( ex )})."
			);
		}
	}
}
