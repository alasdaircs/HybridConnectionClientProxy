using Microsoft.Extensions.Configuration;

using Xunit;

namespace HybridConnectionClientProxy.Tests;

public class LoggingTests
{
	[Theory]
	[InlineData( "10:24194", "socket:[24194]", true )]
	[InlineData( "10:24194", "socket:[99999]", false )] // inherited from a parent service, output goes elsewhere
	[InlineData( "10:24194", "/tmp/output.log", false )]
	[InlineData( "10:24194", "/dev/pts/3", false )]
	[InlineData( null, "socket:[24194]", false )]
	[InlineData( "", "socket:[24194]", false )]
	[InlineData( "garbage", "socket:[garbage]", false )]
	[InlineData( "10:24194", null, false )]
	public void IsJournalStream_RequiresStdoutToBeTheJournalSocket( string? journalStream, string? stdoutTarget, bool expected )
	{
		Assert.Equal( expected, Logging.IsJournalStream( journalStream, stdoutTarget ) );
	}

	[Theory]
	[InlineData( "Serilog:MinimumLevel", "Loud" )]
	[InlineData( "Serilog:WriteTo:Console:Args:theme", "NoSuchTheme" )]
	public void CreateLogger_InvalidSerilogSection_IsConfigurationError( string key, string value )
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection( new Dictionary<string, string?>
			{
				[ "Serilog:WriteTo:Console:Name" ] = "Console",
				[ key ] = value,
			} )
			.Build();

		var ex = Assert.Throws<ConfigurationException>( () => Logging.CreateLogger( configuration ) );
		Assert.StartsWith( "Invalid Serilog configuration", ex.Message );
	}

	[Fact]
	public void CreateLogger_NoSerilogSection_FallsBackToConsole()
	{
		using var logger = Logging.CreateLogger( new ConfigurationBuilder().Build() );
		Assert.NotNull( logger );
	}
}
