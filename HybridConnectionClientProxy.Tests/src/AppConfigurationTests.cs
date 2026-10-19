using Microsoft.Extensions.Configuration;

using Xunit;

namespace HybridConnectionClientProxy.Tests;

public class AppConfigurationTests
{
	[Fact]
	public void BindAppSettings_FlatKeysOnly_BindsProxies()
	{
		// The same keys environment variables produce (AppSettings__Proxies__0__Name → AppSettings:Proxies:0:Name),
		// with no settings file at all
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection( new Dictionary<string, string?>
			{
				[ "AppSettings:Proxies:0:Name" ] = "lonsql07dev-sql",
				[ "AppSettings:Proxies:0:HybridConnectionString" ] = "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=cody;SharedAccessKey=k;EntityPath=lonsql07dev-sql",
				[ "AppSettings:Proxies:0:ListenPort" ] = "14331",
				[ "AppSettings:Proxies:1:Name" ] = "loniridium02",
				[ "AppSettings:Proxies:1:HybridConnectionString" ] = "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=cody;SharedAccessKey=k;EntityPath=loniridium02",
				[ "AppSettings:Proxies:1:ListenPort" ] = "14332",
			} )
			.Build();

		var settings = AppConfiguration.BindAppSettings( configuration );

		Assert.Equal( 2, settings.Proxies.Length );
		Assert.Equal( "lonsql07dev-sql", settings.Proxies[0].Name );
		Assert.Equal( 14331, settings.Proxies[0].ListenPort );
		Assert.Equal( 14332, settings.Proxies[1].ListenPort );
	}

	[Fact]
	public void BindAppSettings_NoAppSettingsSection_HasNoProxies()
	{
		var settings = AppConfiguration.BindAppSettings( new ConfigurationBuilder().Build() );
		Assert.Empty( settings.Proxies );
	}

	[Fact]
	public void BindAppSettings_InvalidPort_IsConfigurationError()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection( new Dictionary<string, string?> { [ "AppSettings:Proxies:0:ListenPort" ] = "not-a-port" } )
			.Build();

		Assert.Throws<ConfigurationException>( () => AppConfiguration.BindAppSettings( configuration ) );
	}

	[Fact]
	public void BindAppSettings_InvalidPortInSecondProxy_IsConfigurationErrorNamingIt()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection( new Dictionary<string, string?>
			{
				[ "AppSettings:Proxies:0:ListenPort" ] = "14331",
				[ "AppSettings:Proxies:1:ListenPort" ] = "14332x",
			} )
			.Build();

		var ex = Assert.Throws<ConfigurationException>( () => AppConfiguration.BindAppSettings( configuration ) );
		Assert.Contains( "#1", ex.Message );
	}

	[Fact]
	public void BindAppSettings_MisspeltProxyKey_IsConfigurationError()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection( new Dictionary<string, string?> { [ "AppSettings:Proxies:0:ListenPrt" ] = "14331" } )
			.Build();

		var ex = Assert.Throws<ConfigurationException>( () => AppConfiguration.BindAppSettings( configuration ) );
		Assert.Contains( "ListenPrt", ex.Message );
	}

	[Fact]
	public void Build_ExplicitDirectoryMissing_IsConfigurationError()
	{
		var missing = Path.Combine( Path.GetTempPath(), $"hccp-missing-{Guid.NewGuid():N}" );
		var overlay = new OverlayLocation( missing, OverlaySource.CommandLine );

		var ex = Assert.Throws<ConfigurationException>( () => AppConfiguration.Build( [], overlay ) );
		Assert.Contains( missing, ex.Message );
	}

	[Fact]
	public void Build_ExplicitDirectory_ReadsMachineSettingsFile()
	{
		var dir = Directory.CreateTempSubdirectory( "hccp-config-" );
		try
		{
			File.WriteAllText(
				Path.Combine( dir.FullName, ConfigurationLocator.OverlayFileName ),
				"""{ "AppSettings": { "Proxies": [ { "Name": "from-file", "ListenPort": 1 } ] } }"""
			);

			var configuration = AppConfiguration.Build( [], new OverlayLocation( dir.FullName, OverlaySource.EnvironmentVariable ) );

			Assert.Equal( "from-file", configuration[ "AppSettings:Proxies:0:Name" ] );
		}
		finally
		{
			dir.Delete( recursive: true );
		}
	}

	[Fact]
	public void Build_CommandLine_OverridesMachineSettingsFile()
	{
		var dir = Directory.CreateTempSubdirectory( "hccp-config-" );
		try
		{
			File.WriteAllText(
				Path.Combine( dir.FullName, ConfigurationLocator.OverlayFileName ),
				"""{ "AppSettings": { "Proxies": [ { "Name": "from-file", "ListenPort": 1 } ] } }"""
			);

			var configuration = AppConfiguration.Build(
				[ "--AppSettings:Proxies:0:ListenPort=2" ],
				new OverlayLocation( dir.FullName, OverlaySource.EnvironmentVariable )
			);

			Assert.Equal( "2", configuration[ "AppSettings:Proxies:0:ListenPort" ] );
			Assert.Equal( "from-file", configuration[ "AppSettings:Proxies:0:Name" ] );
		}
		finally
		{
			dir.Delete( recursive: true );
		}
	}

	[Fact]
	public void DefaultsFile_IsConsoleOnly_AndWindowsDefaultsAddFileAndDebug()
	{
		// File logging under %TEMP% is Windows-only, and so is the Debug sink: on Linux, .NET routes Debug
		// output to syslog, duplicating every line in the journal. Elsewhere the default is console only.
		var defaults = new ConfigurationBuilder()
			.SetBasePath( AppContext.BaseDirectory )
			.AddJsonFile( ConfigurationLocator.DefaultsFile )
			.Build();
		Assert.Equal(
			[ "Console" ],
			defaults.GetSection( "Serilog:WriteTo" ).GetChildren().Select( sink => sink[ "Name" ] )
		);

		var windows = new ConfigurationBuilder()
			.SetBasePath( AppContext.BaseDirectory )
			.AddJsonFile( ConfigurationLocator.DefaultsFile )
			.AddJsonFile( ConfigurationLocator.WindowsDefaultsFile )
			.Build();
		Assert.Contains(
			windows.GetSection( "Serilog:WriteTo" ).GetChildren(),
			sink => sink[ "Name" ] == "File" && sink[ "Args:path" ]!.StartsWith( "%TEMP%" )
		);
		Assert.Contains( windows.GetSection( "Serilog:WriteTo" ).GetChildren(), sink => sink[ "Name" ] == "Debug" );
	}
}
