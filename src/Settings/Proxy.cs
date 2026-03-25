using System.Net;

namespace HybridConnectionClientProxy.Settings;

public class Proxy
{
	public string? Name { get; set; } // Purely decorative
	public string? HybridConnectionString { get; set; }
	public string? ListenIPAddress { get; set; }
	public int ListenPort { get; set; }

	public IPAddress ListenAddress
		=> IPAddress.TryParse( ListenIPAddress, out var ip ) ? ip : IPAddress.Loopback;
}
