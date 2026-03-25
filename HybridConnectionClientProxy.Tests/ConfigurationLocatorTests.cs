namespace HybridConnectionClientProxy.Tests;

public class ConfigurationLocatorTests
{
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
	public void OverlayFilePath_EndsWithConfig()
	{
		var path = ConfigurationLocator.OverlayFilePath;
		var leaf = Path.GetFileName( path );
		Assert.Equal( "config", leaf );
	}

	[Fact]
	public void OverlayFile_CombinesOverlayFilePathAndOverlayFileName()
	{
		var expected = Path.Combine( ConfigurationLocator.OverlayFilePath, ConfigurationLocator.OverlayFileName );
		Assert.Equal( expected, ConfigurationLocator.OverlayFile );
	}
}
