using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HybridConnectionClientProxy
{
	internal class ConfigurationLocator
	{
		public const String DefaultsFile
			= "appsettings.json";

		public static String OverlayFileName
			=> $"appsettings.{Environment.MachineName}.json";

		public static String OverlayFilePath
		{
			get
			{
				var segments = Environment.CurrentDirectory.Split( Path.DirectorySeparatorChar  );
				Int32 binpos = segments.Length - 1;

				for( int i = segments.Length - 1; i >= 0; i-- )
				{
					if( StringComparer.InvariantCultureIgnoreCase.Equals( segments[i], "bin" ) )
					{
						binpos = i;
						break;
					}
				}

				var parentpath = String.Join( Path.DirectorySeparatorChar, segments.Take( binpos ) );
				// var parentpath = string.Join( Path.DirectorySeparatorChar, Enumerable.Repeat( "..", segments.Length - binpos ) );

				return
					Path.Combine(
						parentpath,
						"config"
					);
			}
		}

		public static String OverlayFile
				= Path.Combine(
						OverlayFilePath,
						OverlayFileName
					);
	}
}
