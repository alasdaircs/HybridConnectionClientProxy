# Hybrid Connection Client Proxy
*Provides the client side of a TCP relay via Azure Service Bus*

[Azure Hybrid Connections](https://learn.microsoft.com/en-us/azure/app-service/app-service-hybrid-connections) is a neat way to access TCP services on-prem or otherwise not exposed directly on the Internet. 

If you run your code on-prem, you can usually connect directly to your on-prem resources (e.g. database or web servers). If you move to Azure Web App, Hybrid Connections allow you to still connect to your on-prem resources. But what if you're a developer working from home, or you want to run that sweet firewall busting magic from elsewhere, like a container in AWS? Well, the official line is you're fresh out of luck.

## How does it work in Azure then?

Under the hood, Microsoft implement the client-side (running in your Azure Web App) of the Hybrid Connections by loading a process called workerforwarder.exe under the active website in IIS. You can see it in the Process Explorer in [Kudu](https://\<yoursite\>.scm.azurewebsites.net/ProcessExplorer) and if you look around the DebugConsole you'll see the binaries in `C:\Program Files\PortBridge`. It's a bit old and it's configuration comes from an environment variable called `WEBSITE_RELAYS` whose contents are Base64-encoded XML. It's written in old-schood .NET Framework 4.7. The clumsy configuration alone means it's not really usable as-is in your dev or production environment.

## Enter the Hybrid Connection Client Proxy
*Connect to on-prem resources from anywhere.*

Hybrid Connection Client Proxy is 
- Written as a console app in .NET 8.0
- Uses the official Microsoft.Azure.Relay to provide the Service Bus connections and implement the Hybrid Connection protocol
- Has simple JSON configuration (appSettings.json) and UserSecrets for development
- Runs from anywhere - tested on Windows but should run on Mac, Linux etc - anywhere .NET runs

## Configuration

If using appSettings.json the format is like this:
```
{
	"AppSettings": {
		"Proxies": [
			{
				"HybridConnectionString": "Endpoint=sb://xxx.servicebus.windows.net/;SharedAccessKeyName=defaultSender;SharedAccessKey=whatever;EntityPath=servername",
				"ListenIPAddress": "127.0.0.1",
				"ListenPort": 12345
			}
		]
	}
}
```

If you're using UserSecrets, it the usual transform:

```
{
	"AppSettings:Proxies:0:HybridConnectionString": "Endpoint=sb://xxx.servicebus.windows.net/;SharedAccessKeyName=defaultSender;SharedAccessKey=whatever;EntityPath=servername",
	"AppSettings:Proxies:0:ListenIPAddress": "127.0.0.1",
	"AppSettings:Proxies:0:ListenPort": 50080
}
```

The ListenIPAddress defaults to local loopback, so you usually won't need to specify it.

## Contributing

Yes please! This is a rough first cut, enough to get it working, but I haven't stress-tested it for memory leaks or other issues so be cautious using it in a production environment.

Fork me and send in your PR :-)
