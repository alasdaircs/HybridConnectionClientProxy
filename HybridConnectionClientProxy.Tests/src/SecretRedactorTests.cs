using Microsoft.Extensions.Configuration;

using Xunit;

namespace HybridConnectionClientProxy.Tests;

public class SecretRedactorTests
{
	private const string Key = "c2VjcmV0LWtleS10aGF0LW11c3Qtbm90LWJlLWxvZ2dlZA==";

	private const string ConnectionString =
		"Endpoint=sb://clientportal.servicebus.windows.net/;SharedAccessKeyName=cody;SharedAccessKey=" + Key + ";EntityPath=lonsql07dev-sql";

	[Fact]
	public void RedactSetting_ConnectionString_HidesKeyButKeepsIdentifiers()
	{
		var redacted = SecretRedactor.RedactSetting( "AppSettings:Proxies:0:HybridConnectionString", ConnectionString );

		Assert.DoesNotContain( Key, redacted );
		Assert.Equal(
			"Endpoint=sb://clientportal.servicebus.windows.net/;SharedAccessKeyName=cody;SharedAccessKey=***;EntityPath=lonsql07dev-sql",
			redacted
		);
	}

	[Fact]
	public void RedactSetting_ConnectionStringUnderUnexpectedKey_IsStillRedacted()
	{
		var redacted = SecretRedactor.RedactSetting( "AppSettings:Proxies:0:Name", ConnectionString );
		Assert.DoesNotContain( Key, redacted );
	}

	[Fact]
	public void RedactSetting_ConnectionStringKey_MasksUnknownParts()
	{
		var redacted = SecretRedactor.RedactSetting( "Some:ConnectionString", "Server=db;User Id=sa;Pwd=hunter2;junk" );

		Assert.DoesNotContain( "hunter2", redacted );
		Assert.DoesNotContain( "junk", redacted );
	}

	[Fact]
	public void RedactSetting_SharedAccessSignature_IsRedacted()
	{
		var value = "Endpoint=sb://ns.servicebus.windows.net/;SharedAccessSignature=SharedAccessSignature sr=x&sig=abcdef&se=1&skn=cody;EntityPath=e";
		var redacted = SecretRedactor.RedactSetting( "AppSettings:Proxies:0:HybridConnectionString", value );

		Assert.DoesNotContain( "abcdef", redacted );
		Assert.Contains( "EntityPath=e", redacted );
	}

	[Theory]
	[InlineData( "AppSettings:Proxies:0:SharedAccessKey" )]
	[InlineData( "AppSettings:ApiKey" )]
	[InlineData( "AppSettings:ClientSecret" )]
	[InlineData( "AppSettings:Password" )]
	[InlineData( "AppSettings:AccessToken" )]
	[InlineData( "secrets:anything" )]
	public void RedactSetting_SensitiveKeyNames_AreMasked( string key )
	{
		Assert.Equal( SecretRedactor.Mask, SecretRedactor.RedactSetting( key, "plain-value" ) );
	}

	[Theory]
	[InlineData( "AppSettings:Proxies:0:ListenPort", "14331" )]
	[InlineData( "AppSettings:Proxies:0:Name", "lonsql07dev-sql" )]
	[InlineData( "Serilog:MinimumLevel", "Debug" )]
	public void RedactSetting_OrdinarySettings_AreUnchanged( string key, string value )
	{
		Assert.Equal( value, SecretRedactor.RedactSetting( key, value ) );
	}

	[Fact]
	public void RedactSetting_NullValue_IsNull()
	{
		Assert.Null( SecretRedactor.RedactSetting( "AppSettings:Proxies:0:HybridConnectionString", null ) );
	}

	[Theory]
	[InlineData( "Failed: SharedAccessKey=" + Key + ";EntityPath=x" )]
	[InlineData( "GET wss://ns.servicebus.windows.net/$hc/e?sb-hc-action=connect&sb-hc-token=" + Key )]
	[InlineData( "token SharedAccessSignature sr=x&sig=" + Key + "&se=1" )]
	public void RedactText_HidesCredentialPairs( string text )
	{
		var redacted = SecretRedactor.RedactText( text );
		Assert.DoesNotContain( Key, redacted );
		Assert.Contains( SecretRedactor.Mask, redacted );
	}

	[Fact]
	public void Describe_RedactsInnerExceptionMessages()
	{
		var ex = new InvalidOperationException( "outer", new ArgumentException( "bad SharedAccessKey=" + Key ) );
		var description = SecretRedactor.Describe( ex );

		Assert.DoesNotContain( Key, description );
		Assert.Contains( "InvalidOperationException: outer", description );
		Assert.Contains( "ArgumentException", description );
	}

	[Fact]
	public void LoggableSettings_RedactsSecretsAndOmitsOtherSections()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection( new Dictionary<string, string?>
			{
				[ "AppSettings:Proxies:0:Name" ] = "lonsql07dev-sql",
				[ "AppSettings:Proxies:0:HybridConnectionString" ] = ConnectionString,
				[ "AppSettings:Proxies:0:ListenPort" ] = "14331",
				[ "Serilog:MinimumLevel" ] = "Debug",
				// Unrelated process environment must never be logged, even when it does not look secret
				[ "DATABASE_URL" ] = "postgres://user:hunter2@db/app",
				[ "GITHUB_TOKEN" ] = "ghp_unrelated",
			} )
			.Build();

		var logged = AppConfiguration.LoggableSettings( configuration ).ToList();
		var text = string.Join( "\n", logged.Select( pair => $"{pair.Key}={pair.Value}" ) );

		Assert.DoesNotContain( Key, text );
		Assert.DoesNotContain( "hunter2", text );
		Assert.DoesNotContain( "ghp_unrelated", text );
		Assert.Contains( "AppSettings:Proxies:0:ListenPort=14331", text );
		Assert.Contains( "SharedAccessKeyName=cody", text );
		Assert.Contains( "Serilog:MinimumLevel=Debug", text );
	}
}
