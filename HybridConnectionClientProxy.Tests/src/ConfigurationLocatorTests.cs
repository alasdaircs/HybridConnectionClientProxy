using Xunit;

namespace HybridConnectionClientProxy.Tests;

public class ConfigurationLocatorTests
{
	// A fully qualified root on every OS; nothing is created on disk
	private static readonly string Root = Path.Combine( Path.GetTempPath(), "hccp-locator-tests" );

	private static string At( params string[] segments )
		=> Path.Combine( [ Root, .. segments ] );

	private static OverlayLocation Resolve(
		string[]? args = null,
		string? environmentValue = null,
		string? baseDirectory = null,
		string? currentDirectory = null,
		params string[] existingFiles )
		=> ConfigurationLocator.Resolve(
			args ?? [],
			environmentValue,
			baseDirectory ?? At( "install", "bin" ),
			currentDirectory ?? At( "work" ),
			path => existingFiles.Contains( path )
		);

	[Fact]
	public void DefaultsFile_IsLowercaseAppsettingsJson()
	{
		// Verifies the case-safe lowercase filename convention (capital S caused failures on Linux)
		Assert.Equal( "appsettings.json", ConfigurationLocator.DefaultsFile );
	}

	[Fact]
	public void OverlayFileName_ContainsMachineName()
	{
		var fileName = ConfigurationLocator.OverlayFileName;
		Assert.Contains( Environment.MachineName, fileName );
	}

	[Fact]
	public void OverlayFileName_HasJsonExtension()
	{
		Assert.EndsWith( ".json", ConfigurationLocator.OverlayFileName );
	}

	[Fact]
	public void OverlayFileName_StartsWithAppsettingsDot()
	{
		Assert.StartsWith( "appsettings.", ConfigurationLocator.OverlayFileName );
	}

	[Fact]
	public void OverlayLocation_File_CombinesDirectoryAndOverlayFileName()
	{
		var location = new OverlayLocation( At( "config" ), OverlaySource.BinaryLocation );
		Assert.Equal( Path.Combine( At( "config" ), ConfigurationLocator.OverlayFileName ), location.File );
	}

	// -------------------------------------------------------------------------
	// Conventional config directory
	// -------------------------------------------------------------------------

	[Fact]
	public void ConventionalConfigDirectory_DevelopmentOutput_IsProjectConfig()
	{
		var dir = ConfigurationLocator.ConventionalConfigDirectory( At( "project", "bin", "Debug", "net10.0" ) );
		Assert.Equal( At( "project", "config" ), dir );
	}

	[Fact]
	public void ConventionalConfigDirectory_IgnoresTrailingSeparator()
	{
		// AppContext.BaseDirectory always ends with a separator
		var dir = ConfigurationLocator.ConventionalConfigDirectory( At( "project", "bin", "Release", "net10.0" ) + Path.DirectorySeparatorChar );
		Assert.Equal( At( "project", "config" ), dir );
	}

	[Fact]
	public void ConventionalConfigDirectory_UsesNearestBin()
	{
		var dir = ConfigurationLocator.ConventionalConfigDirectory( At( "bin", "tools", "proxy", "bin" ) );
		Assert.Equal( At( "bin", "tools", "proxy", "config" ), dir );
	}

	[Fact]
	public void ConventionalConfigDirectory_BinIsCaseInsensitive()
	{
		var dir = ConfigurationLocator.ConventionalConfigDirectory( At( "proxy", "Bin" ) );
		Assert.Equal( At( "proxy", "config" ), dir );
	}

	[Fact]
	public void ConventionalConfigDirectory_WithoutBin_IsSiblingConfig()
	{
		var dir = ConfigurationLocator.ConventionalConfigDirectory( At( "tools", "proxy" ) );
		Assert.Equal( At( "tools", "config" ), dir );
	}

	// -------------------------------------------------------------------------
	// Resolution order
	// -------------------------------------------------------------------------

	[Fact]
	public void Resolve_CommandLineSwitch_WinsOverEnvironment()
	{
		var location = Resolve( args: [ "--config", At( "from-args" ) ], environmentValue: At( "from-env" ) );

		Assert.Equal( At( "from-args" ), location.Directory );
		Assert.Equal( OverlaySource.CommandLine, location.Source );
		Assert.True( location.IsExplicit );
	}

	[Fact]
	public void Resolve_CommandLineSwitchWithEquals_IsAccepted()
	{
		var location = Resolve( args: [ "--Other:Key=1", $"--config={At( "from-args" )}" ] );

		Assert.Equal( At( "from-args" ), location.Directory );
		Assert.Equal( OverlaySource.CommandLine, location.Source );
	}

	[Fact]
	public void Resolve_CommandLineSwitchWithoutValue_IsIgnored()
	{
		var location = Resolve( args: [ "--config" ], environmentValue: At( "from-env" ) );
		Assert.Equal( OverlaySource.EnvironmentVariable, location.Source );
	}

	[Fact]
	public void Resolve_EnvironmentVariable_WinsOverConvention()
	{
		var binaryConfig = Path.Combine( At( "install", "config" ), ConfigurationLocator.OverlayFileName );
		var location = Resolve( environmentValue: At( "from-env" ), existingFiles: binaryConfig );

		Assert.Equal( At( "from-env" ), location.Directory );
		Assert.Equal( OverlaySource.EnvironmentVariable, location.Source );
		Assert.True( location.IsExplicit );
	}

	[Fact]
	public void Resolve_ExplicitRelativeDirectory_IsMadeAbsolute()
	{
		var location = Resolve( args: [ "--config", "relative-config" ] );

		Assert.True( Path.IsPathFullyQualified( location.Directory ) );
		Assert.Equal( Path.GetFullPath( "relative-config" ), location.Directory );
	}

	[Fact]
	public void Resolve_FileBesideBinary_IsPreferredOverWorkingDirectory()
	{
		var binaryConfig = Path.Combine( At( "install", "config" ), ConfigurationLocator.OverlayFileName );
		var workConfig = Path.Combine( At( "config" ), ConfigurationLocator.OverlayFileName );

		var location = Resolve( existingFiles: [ binaryConfig, workConfig ] );

		Assert.Equal( At( "install", "config" ), location.Directory );
		Assert.Equal( OverlaySource.BinaryLocation, location.Source );
		Assert.False( location.IsExplicit );
	}

	[Fact]
	public void Resolve_FileOnlyRelativeToWorkingDirectory_IsStillFound()
	{
		// Backward compatibility: earlier versions only looked relative to the working directory
		var workConfig = Path.Combine( At( "config" ), ConfigurationLocator.OverlayFileName );

		var location = Resolve( existingFiles: workConfig );

		Assert.Equal( At( "config" ), location.Directory );
		Assert.Equal( OverlaySource.WorkingDirectory, location.Source );
	}

	[Fact]
	public void Resolve_NoFileAnywhere_DefaultsToBesideBinary()
	{
		var location = Resolve();

		Assert.Equal( At( "install", "config" ), location.Directory );
		Assert.Equal( OverlaySource.BinaryLocation, location.Source );
	}
}
