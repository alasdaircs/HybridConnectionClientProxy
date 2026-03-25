namespace HybridConnectionClientProxy.Settings;

public class AppSettings
{
	public const string Section = "AppSettings";

	public Proxy[] Proxies { get; set; } = [];
}
