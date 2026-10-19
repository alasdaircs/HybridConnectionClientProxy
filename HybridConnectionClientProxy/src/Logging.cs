using Microsoft.Extensions.Configuration;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Settings.Configuration;

namespace HybridConnectionClientProxy;

internal static class Logging
{
	/// <summary>
	/// Console output template when stdout is connected to the systemd journal: no timestamp (journald adds one),
	/// prefixed with the syslog priority so that <c>journalctl -p</c> works.
	/// </summary>
	public const string JournaldOutputTemplate = "{SyslogPriority:l}[{Level:u3}] {Message:lj}{NewLine}{Exception}";

	/// <summary>
	/// systemd sets JOURNAL_STREAM when a service's stdout or stderr is connected to the journal.
	/// </summary>
	public static bool IsJournald
		=> !string.IsNullOrEmpty( Environment.GetEnvironmentVariable( "JOURNAL_STREAM" ) );

	/// <summary>
	/// Configuration applied over the defaults when running under journald; machine settings,
	/// environment variables and the command line can still override it.
	/// </summary>
	public static IEnumerable<KeyValuePair<string, string?>> JournaldDefaults
		=> [ new( "Serilog:WriteTo:Console:Args:outputTemplate", JournaldOutputTemplate ) ];

	public static Logger CreateBootstrapLogger()
	{
		var logger = new LoggerConfiguration()
			.Enrich.FromLogContext()
			.Enrich.With<SyslogPriorityEnricher>();

		// On Linux, .NET sends Debug output to syslog, which would duplicate every line in the journal
		if( OperatingSystem.IsWindows() )
			logger.WriteTo.Debug();

		return ( IsJournald
			? logger.WriteTo.Console( outputTemplate: JournaldOutputTemplate )
			: logger.WriteTo.Console()
		).CreateLogger();
	}

	public static Logger CreateLogger( IConfiguration configuration )
	{
		var logger = new LoggerConfiguration().Enrich.With<SyslogPriorityEnricher>();

		if( !configuration.GetSection( "Serilog" ).Exists() )
			return logger.WriteTo.Console().CreateLogger();

		// Name the sink assemblies explicitly: single-file publishes have no dependency
		// manifest for Serilog.Settings.Configuration to discover them from.
		var options = new ConfigurationReaderOptions(
			typeof( ConsoleLoggerConfigurationExtensions ).Assembly,
			typeof( FileLoggerConfigurationExtensions ).Assembly,
			typeof( LoggerSinkConfigurationDebugExtensions ).Assembly
		);

		return logger.ReadFrom.Configuration( configuration, options ).CreateLogger();
	}
}

/// <summary>
/// Adds <c>SyslogPriority</c> ("&lt;6&gt;" etc.), which journald strips from the start of a line and uses as its priority.
/// </summary>
internal sealed class SyslogPriorityEnricher : ILogEventEnricher
{
	private static readonly LogEventProperty[] Priorities =
	[
		new( "SyslogPriority", new ScalarValue( "<7>" ) ), // Verbose → debug
		new( "SyslogPriority", new ScalarValue( "<7>" ) ), // Debug → debug
		new( "SyslogPriority", new ScalarValue( "<6>" ) ), // Information → info
		new( "SyslogPriority", new ScalarValue( "<4>" ) ), // Warning → warning
		new( "SyslogPriority", new ScalarValue( "<3>" ) ), // Error → err
		new( "SyslogPriority", new ScalarValue( "<2>" ) ), // Fatal → crit
	];

	public void Enrich( LogEvent logEvent, ILogEventPropertyFactory propertyFactory )
		=> logEvent.AddPropertyIfAbsent( Priorities[(int) logEvent.Level] );
}
