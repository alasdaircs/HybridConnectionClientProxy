using HybridConnectionClientProxy;
using HybridConnectionClientProxy.Settings;

using Microsoft.Extensions.Configuration;

Console.WriteLine( "Reading Configuration" );
var configurationManager = new ConfigurationManager();
configurationManager.AddJsonFile( "appSettings.json" );
configurationManager.AddUserSecrets( typeof(Program).Assembly );
var configurationSection = configurationManager.GetRequiredSection( AppSettings.Section );
var appSettings = new AppSettings();
ConfigurationBinder.Bind( configurationSection, appSettings );

Console.WriteLine( "Starting" );
var cts = new CancellationTokenSource();
var proxyTasks = new List<Task>();
if( appSettings.Proxies == null || appSettings.Proxies.Length == 0 )
{
	throw new Exception( "You must specify at least one proxy in the configuration." );
}
else
{
	foreach( var proxy in appSettings.Proxies )
	{
		if( proxy.HybridConnectionString == null )
		{
			throw new Exception( "Every proxy in configuration must have a HybridConnectionString." );
		}

		if( proxy.ListenPort == 0 )
		{
			throw new Exception( "Every proxy in configuration must have a ListenPort." );
		}

		proxyTasks.Add(
			ClientProxy.Create( 
				proxy.HybridConnectionString,
				proxy.ListenAddress,
				proxy.ListenPort,
				cts.Token
			)
		);
	}
}

Console.WriteLine( "Press a key to stop" );
Console.ReadKey( true );

Console.WriteLine( "Stopping" );
cts.Cancel();
await Task.WhenAll( proxyTasks );

Console.WriteLine( "Stopped" );
