using HybridConnectionClientProxy;
using HybridConnectionClientProxy.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

using Serilog;

// Bootstrap logger — replaced after full configuration is loaded
Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console()
	.WriteTo.Debug()
	.CreateLogger();

Log.Information( "Reading Configuration" );
var configurationManager = new ConfigurationManager();
configurationManager.AddJsonFile( ConfigurationLocator.DefaultsFile );
configurationManager.AddJsonFile( ConfigurationLocator.OverlayFile, optional: true );
#if DEBUG
configurationManager.AddUserSecrets( typeof( Program ).Assembly, optional: true );
#endif
configurationManager.AddEnvironmentVariables();
configurationManager.AddCommandLine( args );

Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration( configurationManager )
	.CreateLogger();

Log.Information( "Started in {WorkingDirectory}", Environment.CurrentDirectory );

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
	Log.Verbose( "{Config} = {Value}", entry.Key, entry.Value );
}

var configurationSection = configurationManager.GetRequiredSection( AppSettings.Section );
var appSettings = new AppSettings();
ConfigurationBinder.Bind( configurationSection, appSettings );

Log.Information( "Starting" );

if( appSettings.Proxies.Length == 0 )
	throw new InvalidOperationException( "You must specify at least one proxy in the configuration." );

var cts = new CancellationTokenSource();
var proxyTasks = new List<Task>();

foreach( var proxy in appSettings.Proxies )
{
	if( proxy.HybridConnectionString is null )
		throw new InvalidOperationException( "Every proxy in configuration must have a HybridConnectionString." );

	if( proxy.ListenPort == 0 )
		throw new InvalidOperationException( "Every proxy in configuration must have a ListenPort." );

	Log.Debug( "Adding proxy {ProxyName}", proxy.Name );
	proxyTasks.Add(
		ClientProxy.Create(
			proxy.HybridConnectionString,
			proxy.ListenAddress,
			proxy.ListenPort,
			cts.Token
		)
	);
}

Log.Information( "Running" );
Console.WriteLine( "Press a key to stop" );
Console.ReadKey( true );

Log.Information( "Stopping" );
await cts.CancelAsync();
await Task.WhenAll( proxyTasks );

Log.Information( "Stopped" );
await Log.CloseAndFlushAsync();
