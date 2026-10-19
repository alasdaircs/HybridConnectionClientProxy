namespace HybridConnectionClientProxy;

/// <summary>
/// Where the machine-specific settings file came from.
/// </summary>
internal enum OverlaySource
{
	CommandLine,
	EnvironmentVariable,
	BinaryLocation,
	WorkingDirectory,
}

/// <summary>
/// The resolved location of the machine-specific settings file.
/// </summary>
internal sealed record OverlayLocation( string Directory, OverlaySource Source )
{
	public string File
		=> Path.Combine( Directory, ConfigurationLocator.OverlayFileName );

	public bool IsExplicit
		=> Source is OverlaySource.CommandLine or OverlaySource.EnvironmentVariable;
}

internal static class ConfigurationLocator
{
	/// <summary>
	/// Lowercase convention matches ASP.NET Core and is case-safe on Linux.
	/// Previously "appSettings.json" (capital S) would silently fail on case-sensitive file systems.
	/// </summary>
	public const string DefaultsFile = "appsettings.json";

	/// <summary>
	/// Defaults applied only on Windows, after <see cref="DefaultsFile"/> (file logging under %TEMP%).
	/// </summary>
	public const string WindowsDefaultsFile = "appsettings.Windows.json";

	/// <summary>
	/// Command-line switch naming the directory that holds the machine-specific settings file.
	/// Accepts both <c>--config &lt;dir&gt;</c> and <c>--config=&lt;dir&gt;</c>.
	/// </summary>
	public const string ConfigDirectorySwitch = "--config";

	/// <summary>
	/// Environment variable naming the directory that holds the machine-specific settings file.
	/// </summary>
	public const string ConfigDirectoryVariable = "HCCP_CONFIG_DIR";

	public static string OverlayFileName
		=> $"appsettings.{Environment.MachineName}.json";

	/// <summary>
	/// Resolves the directory holding <see cref="OverlayFileName"/>.
	/// An explicit directory (<c>--config</c>, then <see cref="ConfigDirectoryVariable"/>) always wins.
	/// Otherwise the conventional <c>config</c> directory is looked for relative to the binary and then,
	/// for backward compatibility, relative to the working directory; the first that contains the file is used.
	/// </summary>
	public static OverlayLocation Resolve(
		IReadOnlyList<string> args,
		string? environmentValue,
		string baseDirectory,
		string currentDirectory,
		Func<string, bool> fileExists )
	{
		var commandLineValue = GetCommandLineValue( args );
		if( !string.IsNullOrWhiteSpace( commandLineValue ) )
			return new OverlayLocation( Path.GetFullPath( commandLineValue ), OverlaySource.CommandLine );

		if( !string.IsNullOrWhiteSpace( environmentValue ) )
			return new OverlayLocation( Path.GetFullPath( environmentValue ), OverlaySource.EnvironmentVariable );

		var fromBinary = new OverlayLocation( ConventionalConfigDirectory( baseDirectory ), OverlaySource.BinaryLocation );
		if( fileExists( fromBinary.File ) )
			return fromBinary;

		var fromWorkingDirectory = new OverlayLocation( ConventionalConfigDirectory( currentDirectory ), OverlaySource.WorkingDirectory );
		if( fileExists( fromWorkingDirectory.File ) )
			return fromWorkingDirectory;

		return fromBinary;
	}

	public static OverlayLocation Resolve( IReadOnlyList<string> args )
		=> Resolve(
			args,
			Environment.GetEnvironmentVariable( ConfigDirectoryVariable ),
			AppContext.BaseDirectory,
			Environment.CurrentDirectory,
			System.IO.File.Exists
		);

	/// <summary>
	/// The <c>config</c> sibling of the nearest enclosing <c>bin</c> directory, so that
	/// <c>project/bin/Debug/net10.0</c> and <c>install/bin</c> both map to <c>…/config</c>.
	/// Without a <c>bin</c> ancestor, the <c>config</c> sibling of <paramref name="directory"/> itself.
	/// </summary>
	public static string ConventionalConfigDirectory( string directory )
	{
		var start = Path.TrimEndingDirectorySeparator( Path.GetFullPath( directory ) );

		for( var dir = start; dir is not null; dir = Path.GetDirectoryName( dir ) )
		{
			if( StringComparer.OrdinalIgnoreCase.Equals( Path.GetFileName( dir ), "bin" ) )
				return Path.Combine( Path.GetDirectoryName( dir ) ?? dir, "config" );
		}

		return Path.Combine( Path.GetDirectoryName( start ) ?? start, "config" );
	}

	internal static string? GetCommandLineValue( IReadOnlyList<string> args )
	{
		for( int i = 0; i < args.Count; i++ )
		{
			if( args[i] == ConfigDirectorySwitch )
				return i + 1 < args.Count ? args[i + 1] : null;

			if( args[i].StartsWith( ConfigDirectorySwitch + "=", StringComparison.Ordinal ) )
				return args[i][( ConfigDirectorySwitch.Length + 1 )..];
		}

		return null;
	}
}
