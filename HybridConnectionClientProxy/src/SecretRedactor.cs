using System.Text.RegularExpressions;

namespace HybridConnectionClientProxy;

/// <summary>
/// Removes credentials from configuration values and messages before they are logged.
/// Errs on the side of hiding too much: a hidden setting is an inconvenience, a logged key is an incident.
/// </summary>
internal static partial class SecretRedactor
{
	public const string Mask = "***";

	/// <summary>
	/// Configuration keys containing any of these (case-insensitive) have their values hidden.
	/// </summary>
	private static readonly string[] SensitiveKeyFragments =
		[ "ConnectionString", "Key", "Secret", "Password", "Pwd", "Token", "Signature", "Credential" ];

	/// <summary>
	/// Connection string parts that identify a resource without granting access to it.
	/// Everything else in a connection string is masked.
	/// </summary>
	private static readonly HashSet<string> SafeConnectionStringParts = new( StringComparer.OrdinalIgnoreCase )
		{ "Endpoint", "EntityPath", "SharedAccessKeyName", "OperationTimeout", "Authentication" };

	// Credential-bearing name=value pairs as they appear in connection strings, SAS tokens and relay URIs.
	[GeneratedRegex( @"\b(?<name>SharedAccessKey|SharedAccessSignature|AccountKey|Password|Pwd|sig|sb-hc-token)(?<sep>\s*=\s*)(?<value>[^;&\s""']*)", RegexOptions.IgnoreCase )]
	private static partial Regex SecretPairPattern();

	/// <summary>
	/// Returns <paramref name="value"/> made safe to log under configuration key <paramref name="key"/>.
	/// </summary>
	public static string? RedactSetting( string key, string? value )
	{
		if( string.IsNullOrEmpty( value ) )
			return value;

		if( key.Contains( "ConnectionString", StringComparison.OrdinalIgnoreCase ) || LooksLikeConnectionString( value ) )
			return RedactConnectionString( value );

		if( SensitiveKeyFragments.Any( fragment => key.Contains( fragment, StringComparison.OrdinalIgnoreCase ) ) )
			return Mask;

		return value;
	}

	/// <summary>
	/// Keeps only the identifying parts of a connection string (endpoint, entity, key name); masks every other value.
	/// </summary>
	public static string RedactConnectionString( string connectionString )
	{
		var parts = connectionString.Split( ';' );
		for( int i = 0; i < parts.Length; i++ )
		{
			if( parts[i].Length == 0 )
				continue;

			var separator = parts[i].IndexOf( '=' );
			if( separator < 0 )
			{
				parts[i] = Mask;
				continue;
			}

			var name = parts[i][..separator].Trim();
			if( !SafeConnectionStringParts.Contains( name ) )
				parts[i] = $"{name}={Mask}";
		}

		return string.Join( ';', parts );
	}

	/// <summary>
	/// Masks credential-bearing name=value pairs in free text such as exception messages.
	/// </summary>
	public static string RedactText( string text )
		=> SecretPairPattern().Replace( text, match => $"{match.Groups["name"].Value}{match.Groups["sep"].Value}{Mask}" );

	/// <summary>
	/// A one-line, redacted description of an exception and its inner exceptions, for log messages.
	/// </summary>
	public static string Describe( Exception exception )
	{
		var messages = new List<string>();
		for( var ex = exception; ex is not null; ex = ex.InnerException )
			messages.Add( $"{ex.GetType().Name}: {ex.Message}" );

		return RedactText( string.Join( " ---> ", messages ) );
	}

	private static bool LooksLikeConnectionString( string value )
		=> value.Contains( "SharedAccessKey=", StringComparison.OrdinalIgnoreCase )
		|| value.Contains( "SharedAccessSignature=", StringComparison.OrdinalIgnoreCase )
		|| value.Contains( "AccountKey=", StringComparison.OrdinalIgnoreCase )
		|| value.Contains( "Password=", StringComparison.OrdinalIgnoreCase );
}
