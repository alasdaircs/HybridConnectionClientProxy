namespace HybridConnectionClientProxy;

internal static class ConfigurationLocator
{
	/// <summary>
	/// Lowercase convention matches ASP.NET Core and is case-safe on Linux.
	/// Previously "appSettings.json" (capital S) would silently fail on case-sensitive file systems.
	/// </summary>
	public const string DefaultsFile = "appsettings.json";

	public static string OverlayFileName
		=> $"appsettings.{Environment.MachineName}.json";

	public static string OverlayFilePath
	{
		get
		{
			var segments = Environment.CurrentDirectory.Split( Path.DirectorySeparatorChar );
			int binpos = segments.Length - 1;

			for( int i = segments.Length - 1; i >= 0; i-- )
			{
				if( StringComparer.InvariantCultureIgnoreCase.Equals( segments[i], "bin" ) )
				{
					binpos = i;
					break;
				}
			}

			var parentPath = string.Join( Path.DirectorySeparatorChar, segments.Take( binpos ) );
			return Path.Combine( parentPath, "config" );
		}
	}

	public static string OverlayFile
		=> Path.Combine( OverlayFilePath, OverlayFileName );
}
