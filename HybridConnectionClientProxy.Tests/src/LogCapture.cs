using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace HybridConnectionClientProxy.Tests;

/// <summary>
/// Captures events written through the static <see cref="Log"/> while in scope. Other tests running in
/// parallel may log too, so assertions should filter on something unique to the test.
/// </summary>
internal sealed class LogCapture : ILogEventSink, IDisposable
{
	private static readonly SemaphoreSlim Exclusive = new( 1, 1 );

	private readonly ILogger _previous;
	private readonly List<LogEvent> _events = [];

	private LogCapture()
	{
		_previous = Log.Logger;
		Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink( this ).CreateLogger();
	}

	public static LogCapture Start()
	{
		Exclusive.Wait();
		return new LogCapture();
	}

	public IReadOnlyList<LogEvent> Events
	{
		get { lock( _events ) return _events.ToList(); }
	}

	public void Emit( LogEvent logEvent )
	{
		lock( _events ) _events.Add( logEvent );
	}

	public void Dispose()
	{
		Log.Logger = _previous;
		Exclusive.Release();
	}
}
