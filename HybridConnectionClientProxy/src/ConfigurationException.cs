namespace HybridConnectionClientProxy;

/// <summary>
/// The configuration is invalid. Messages must never contain secrets.
/// </summary>
internal sealed class ConfigurationException( string message ) : Exception( message );
