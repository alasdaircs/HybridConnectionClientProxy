using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HybridConnectionClientProxy.Settings
{
	public class Proxy
	{
		public String? Name {  get; set; } // Purely decorative
		public String? HybridConnectionString { get; set; }
		public String? ListenIPAddress { get; set; }
		public int ListenPort { get; set; }

		public IPAddress ListenAddress
		{
			get
			{
				IPAddress? ip;
				if( !IPAddress.TryParse( ListenIPAddress, out ip ) )
				{
					ip = IPAddress.Loopback;
				}

				return ip;
			}
		}
	}
}
