using System.Net;

using HybridConnectionClientProxy.Settings;

namespace HybridConnectionClientProxy.Tests.Settings;

public class ProxySettingsTests
{
	[Fact]
	public void ListenAddress_WhenListenIPAddressIsNull_ReturnsLoopback()
	{
		var proxy = new Proxy { ListenIPAddress = null };
		Assert.Equal( IPAddress.Loopback, proxy.ListenAddress );
	}

	[Fact]
	public void ListenAddress_WhenListenIPAddressIsEmpty_ReturnsLoopback()
	{
		var proxy = new Proxy { ListenIPAddress = string.Empty };
		Assert.Equal( IPAddress.Loopback, proxy.ListenAddress );
	}

	[Fact]
	public void ListenAddress_WhenListenIPAddressIsInvalid_ReturnsLoopback()
	{
		var proxy = new Proxy { ListenIPAddress = "not-an-ip" };
		Assert.Equal( IPAddress.Loopback, proxy.ListenAddress );
	}

	[Fact]
	public void ListenAddress_WhenListenIPAddressIsLoopback_ReturnsLoopback()
	{
		var proxy = new Proxy { ListenIPAddress = "127.0.0.1" };
		Assert.Equal( IPAddress.Loopback, proxy.ListenAddress );
	}

	[Fact]
	public void ListenAddress_WhenListenIPAddressIsAny_ReturnsAny()
	{
		var proxy = new Proxy { ListenIPAddress = "0.0.0.0" };
		Assert.Equal( IPAddress.Any, proxy.ListenAddress );
	}

	[Fact]
	public void ListenAddress_WhenListenIPAddressIsIPv6Loopback_ReturnsIPv6Loopback()
	{
		var proxy = new Proxy { ListenIPAddress = "::1" };
		Assert.Equal( IPAddress.IPv6Loopback, proxy.ListenAddress );
	}

	[Fact]
	public void ListenAddress_WhenListenIPAddressIsArbitraryIPv4_ReturnsCorrectAddress()
	{
		var proxy = new Proxy { ListenIPAddress = "192.168.1.100" };
		Assert.Equal( IPAddress.Parse( "192.168.1.100" ), proxy.ListenAddress );
	}

	[Fact]
	public void Proxy_DefaultListenPort_IsZero()
	{
		var proxy = new Proxy();
		Assert.Equal( 0, proxy.ListenPort );
	}

	[Fact]
	public void Proxy_DefaultProxies_IsEmptyArray()
	{
		var settings = new AppSettings();
		Assert.Empty( settings.Proxies );
	}
}
