using System.Net;
using System.Net.Sockets;

using HybridConnectionClientProxy.Settings;

using Serilog;

namespace HybridConnectionClientProxy;

/// <summary>
/// A local listen port could not be bound.
/// </summary>
internal sealed class ListenException( string message, Exception inner ) : Exception( message, inner );

/// <summary>
/// A proxy stopped accepting connections while the process was running. The cause has already been logged.
/// </summary>
internal sealed class AcceptLoopFailedException() : Exception( "A proxy stopped accepting connections." );

/// <summary>
/// Runs every configured proxy: validates the configuration, binds all listen ports up front
/// (so that configuration and bind errors fail the process at startup), and stops them together.
/// </summary>
internal sealed class ProxyHost
{
	/// <summary>
	/// How long <see cref="StopAsync"/> waits for open connections. A service manager's stop timeout
	/// (systemd TimeoutStopSec) must be longer.
	/// </summary>
	public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds( 5 );

	private readonly CancellationTokenSource _stopping = new();
	private readonly TaskCompletionSource _acceptLoopFailed = new( TaskCreationOptions.RunContinuationsAsynchronously );
	private readonly List<TcpListener> _listeners = [];
	private readonly List<Task> _proxies = [];

	private ProxyHost() { }

	/// <summary>
	/// Completes if any proxy stops accepting connections other than through <see cref="StopAsync"/>.
	/// </summary>
	public Task AcceptLoopFailed
		=> _acceptLoopFailed.Task;

	public IReadOnlyList<IPEndPoint> ListenEndpoints
		=> _listeners.Select( listener => (IPEndPoint) listener.LocalEndpoint ).ToList();

	/// <summary>
	/// Validates <paramref name="settings"/>, binds every listen port and starts accepting connections.
	/// </summary>
	/// <exception cref="ConfigurationException">The configuration is invalid.</exception>
	/// <exception cref="ListenException">A listen port could not be bound.</exception>
	public static ProxyHost Start(
		AppSettings settings,
		Func<Proxy, string, IHybridConnectionProvider> providerFactory )
	{
		if( settings.Proxies.Length == 0 )
			throw new ConfigurationException( "You must specify at least one proxy in the configuration (AppSettings:Proxies)." );

		// Validate everything before binding anything
		var planned = new List<(Proxy Proxy, string Label, IHybridConnectionProvider Provider)>();
		for( int i = 0; i < settings.Proxies.Length; i++ )
		{
			var proxy = settings.Proxies[i];
			var label = string.IsNullOrWhiteSpace( proxy.Name ) ? $"#{i}" : $"#{i} ({proxy.Name})";

			if( proxy.ListenPort is < 0 or > IPEndPoint.MaxPort )
				throw new ConfigurationException( $"Proxy {label}: ListenPort {proxy.ListenPort} is out of range." );

			if( proxy.ListenPort == 0 )
				throw new ConfigurationException( $"Proxy {label}: ListenPort is required." );

			if( !string.IsNullOrWhiteSpace( proxy.ListenIPAddress ) && !IPAddress.TryParse( proxy.ListenIPAddress, out _ ) )
				throw new ConfigurationException( $"Proxy {label}: ListenIPAddress '{proxy.ListenIPAddress}' is not an IP address." );

			planned.Add( (proxy, label, providerFactory( proxy, label )) );
		}

		var host = new ProxyHost();
		try
		{
			foreach( var (proxy, label, _) in planned )
			{
				var listener = new TcpListener( proxy.ListenAddress, proxy.ListenPort );
				try
				{
					listener.Start();
				}
				catch( SocketException ex )
				{
					throw new ListenException( $"Proxy {label}: cannot listen on {proxy.ListenAddress}:{proxy.ListenPort} ({ex.SocketErrorCode}).", ex );
				}
				host._listeners.Add( listener );
			}
		}
		catch
		{
			host.StopListeners();
			throw;
		}

		for( int i = 0; i < planned.Count; i++ )
		{
			var (_, label, provider) = planned[i];
			Log.Information( "Proxy {Proxy}: listening on {ListenEndpoint}", label, host._listeners[i].LocalEndpoint );
			host._proxies.Add( ClientProxy.Create(
				provider, host._listeners[i], host._stopping.Token, label, () => host._acceptLoopFailed.TrySetResult()
			) );
		}

		return host;
	}

	/// <summary>
	/// Stops listening, closes open connections and waits up to <paramref name="timeout"/> for them to finish.
	/// </summary>
	/// <returns><c>true</c> if everything stopped within the timeout.</returns>
	public async Task<bool> StopAsync( TimeSpan timeout )
	{
		await _stopping.CancelAsync();
		StopListeners();

		try
		{
			await Task.WhenAll( _proxies ).WaitAsync( timeout );
			return true;
		}
		catch( TimeoutException )
		{
			return false;
		}
	}

	private void StopListeners()
	{
		foreach( var listener in _listeners )
		{
			// Best effort: the listener may already be stopped, and shutdown carries on regardless
			try { listener.Stop(); } catch { }
		}
	}
}
