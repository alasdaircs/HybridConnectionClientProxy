namespace HybridConnectionClientProxy;

/// <summary>
/// Process exit codes. Part of the contract with service managers: a systemd unit can use
/// <c>RestartPreventExitStatus=2</c> so that a broken configuration is not restarted in a loop.
/// </summary>
internal static class ExitCodes
{
	/// <summary>Stopped cleanly (signal, key press), or <c>--version</c> / <c>--help</c>.</summary>
	public const int Success = 0;

	/// <summary>Unexpected fatal error.</summary>
	public const int UnexpectedError = 1;

	/// <summary>Invalid or missing configuration. Restarting will not help until the configuration changes.</summary>
	public const int ConfigurationError = 2;

	/// <summary>A listen port could not be bound (in use, or not permitted). May succeed on retry.</summary>
	public const int ListenError = 3;
}
