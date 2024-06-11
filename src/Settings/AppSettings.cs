using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HybridConnectionClientProxy.Settings
{
	public class AppSettings
	{
		public const String Section = "AppSettings";

		public Proxy[]? Proxies { get; set; } = Array.Empty<Proxy>();
	}
}
