using HybridConnectionClientProxy;
using HybridConnectionClientProxy.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

using Serilog;
using Serilog.Events;

// Bootstrap logger
Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console()
	.WriteTo.Debug()
	.CreateLogger();

Log.Information( "Reading Configuration" );
var configurationManager = new ConfigurationManager();
configurationManager.AddJsonFile( ConfigurationLocator.DefaultsFile );
configurationManager.AddJsonFile( ConfigurationLocator.OverlayFile, true );
#if DEBUG
configurationManager.AddUserSecrets( typeof(Program).Assembly, true );
#endif
configurationManager.AddEnvironmentVariables();
configurationManager.AddCommandLine( args );

Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration( configurationManager )
	.CreateLogger();

Log.Information( "Started in {workingDirectory}", Environment.CurrentDirectory );

var files = configurationManager.GetFileProvider();
Log.Debug(
	"{AppSettingsDefaultsFile}: {AppSettingsDefaultsFileExists}",
	ConfigurationLocator.DefaultsFile,
	files.GetFileInfo( ConfigurationLocator.DefaultsFile ).Exists
);
Log.Debug(
	"{AppSettingsOverlayFile}: {AppSettingsOverlayFileExists}",
	ConfigurationLocator.OverlayFile,
	new PhysicalFileProvider( ConfigurationLocator.OverlayFilePath ).GetFileInfo( ConfigurationLocator.OverlayFileName ).Exists
);

foreach( var entry in configurationManager.AsEnumerable().OrderBy( pair => pair.Key ) )
{
	Log.Verbose( "{config} = {value}", entry.Key, entry.Value );
}

var configurationSection = configurationManager.GetRequiredSection( AppSettings.Section );
var appSettings = new AppSettings();
ConfigurationBinder.Bind( configurationSection, appSettings );

Log.Information( "Starting" );
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

		Log.Debug( "Adding proxy {proxyName}", proxy.Name );
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

Log.Information( "Running" );
Console.WriteLine( "Press a key to stop" );
Console.ReadKey( true );

Log.Information( "Stopping" );
cts.Cancel();
await Task.WhenAll( proxyTasks );

Log.Information( "Stopped" );
await Log.CloseAndFlushAsync();
