using System.Reflection;

namespace HybridConnectionClientProxy;

internal static class AppInfo
{
	public const string Name = "HybridConnectionClientProxy";

	/// <summary>
	/// The release version (the tag without its leading "v"), or 0.0.0-dev for local builds.
	/// </summary>
	public static string Version { get; } =
		typeof( AppInfo ).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
		?? typeof( AppInfo ).Assembly.GetName().Version?.ToString()
		?? "unknown";
}
