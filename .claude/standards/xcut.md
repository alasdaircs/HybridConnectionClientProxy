# Cross-cutting concerns: house rules

Status: DRAFT from `/roles:xcut-review survey` of the code at 680adf0, describing what the code already does.
The owner still has to resolve the "Open questions" at the end; until then, reviews treat those points as undecided.

These rules override the generic xcut-standards. Reviews cite the `H-…` IDs.

Context: a single-process .NET 10 console app / systemd service. No DI container, no ASP.NET Core,
no hosted-service framework. Static Serilog `Log` throughout.

---

## Logging (LOG)

**H-LOG-1 Logger.** Use the static Serilog `Log` class. No `ILogger<T>`, no DI. `Program.cs` installs
`Logging.CreateBootstrapLogger()` first, replaces it with `Logging.CreateLogger(configuration)` once
configuration is built, and calls `Log.CloseAndFlushAsync()` in the top-level `finally`.
Sinks and levels come from the `Serilog` configuration section; sink assemblies must be named in
`ConfigurationReaderOptions` in `Logging.CreateLogger` (single-file publish has no deps manifest).
Exemplars: `src/Logging.cs`, `src/Program.cs`.

**H-LOG-2 Templates and property names.** Always use message templates with PascalCase named properties,
never interpolation. Use these names for these things:

| Thing | Property |
|---|---|
| Proxy label (`#<index>` or `#<index> (<Name>)`) | `{Proxy}` |
| Per-proxy connection sequence number | `{ConnectionId}` |
| Client remote endpoint | `{Remote}` |
| Bound local endpoint | `{ListenEndpoint}` |
| Redacted exception description | `{Error}` |
| Configuration key / redacted value | `{Config}` / `{Value}` |
| POSIX signal | `{Signal}` |
| App name / version | `{Application}` / `{Version}` |

Exemplars: `src/ClientProxyConnection.cs`, `src/ClientProxy.cs`.

**H-LOG-3 Context by message prefix, not scopes.** Logs about a proxy or connection start with
`"Proxy {Proxy}: "` and, for a connection, `"Proxy {Proxy}: connection {ConnectionId} "`, followed by the
event (`accepted from`, `relayed`, `closed`, `dropped, <reason>`, `failed`). `LogContext` / scopes are
not used. The proxy label is computed once in `ProxyHost.Start` and passed down.
Exemplars: `src/ClientProxyConnection.cs`, `src/ProxyHost.cs:87`.

**H-LOG-4 Level policy.**
- **Debug**: per-connection lifecycle (accepted, relayed, closed); the redacted configuration dump;
  file/directory diagnostics at startup.
- **Information**: process lifecycle (`Reading Configuration`, `<App> <Version> starting …`, `Starting`,
  `Running`, `Received {Signal}, stopping`, `Stopping`, `Stopped`) and each proxy's
  `listening on {ListenEndpoint}`.
- **Warning**: a handled per-connection or per-accept failure where the proxy carries on (relay
  connection failed → client dropped; transient accept failure → continuing), and a shutdown that
  ran out of time.
- **Error**: an unexpected failure of a connection, or a proxy's accept loop stopping.
- **Fatal**: only in `Program.cs` top-level catches, immediately before returning a non-zero exit code.

**H-LOG-5 Exceptions are never passed to the logger.** Do not use `Log.X(ex, …)`. Serilog would render
`ex.ToString()` unredacted. Instead log `{Error}` with `SecretRedactor.Describe(ex)` (one line:
`Type: message ---> Type: message`, redacted). The sole exception is the last-resort catch in
`Program.cs`, which logs `SecretRedactor.RedactText(ex.ToString())` so the stack trace is kept.
Exception messages already guaranteed secret-free (`ConfigurationException`, `ListenException`) may be
logged as `ex.Message`. The exception is logged once, by the handler that decides what happens next.
Exemplars: `src/ClientProxyConnection.cs:33-36`, `src/Program.cs:33-47`.

**H-LOG-6 Redaction.** Anything that can contain a credential goes through `SecretRedactor` before it
reaches a log or an exception message:
- configuration values: `SecretRedactor.RedactSetting(key, value)` (connection strings keep only
  `Endpoint`, `EntityPath`, `SharedAccessKeyName`, `OperationTimeout`, `Authentication`);
- free text and third-party exception messages: `SecretRedactor.RedactText`;
- exceptions: `SecretRedactor.Describe`.
Only the `AppSettings` and `Serilog` sections are ever dumped (`AppConfiguration.LoggableSettings`);
the process environment is never logged. New credential-bearing settings or token formats must be added
to `SecretRedactor` with a test in `SecretRedactorTests`.
Exemplars: `src/SecretRedactor.cs`, `src/AppConfiguration.cs:90-95`.

**H-LOG-7 Volume.** Nothing is logged inside the byte-copy pumps. Per-connection events are Debug only;
Warning/Error per connection only on failure.

**H-LOG-8 Actionable warnings and errors.** A Warning/Error states the proxy (and connection), what
failed, and what happens next, e.g. `"…dropped, relay connection failed: {Error}"`,
`"…accept failed, continuing: {Error}"`, `"…stopped accepting connections: {Error}"`,
`"Connections still open after {ShutdownTimeout}; exiting anyway"`.

**H-LOG-9 Startup and shutdown.** Startup logs name, version, pid, runtime and RID at Information, then the
redacted configuration at Debug. Shutdown logs the signal, `Stopping` and `Stopped`. `Application` is
set as a global property from `Serilog:Properties` in `appsettings.json`.

**H-LOG-10 Output formats.** Console is the default sink on every platform. Under systemd
(`JOURNAL_STREAM` set) the console template is `Logging.JournaldOutputTemplate`: syslog priority prefix
from `SyslogPriorityEnricher`, no timestamp. On Windows, `appsettings.Windows.json` adds a rolling file
under `%TEMP%` and the Debug sink. `Console.WriteLine` is reserved for `--version`, `--help` and the
interactive "Press a key" prompt; everything else is logged.

## Telemetry (TEL)

**H-TEL-1** Logs are currently the only telemetry: no metrics, tracing or health endpoint exist.
Whether this is deliberate is an open question (see below). Until it is decided, do not flag their absence.

## Error handling (ERR)

**H-ERR-1 Exit-code contract.** The process reports failure only through `ExitCodes`:
`0` stopped cleanly / `--version` / `--help`, `1` unexpected error, `2` configuration error
(a systemd unit can set `RestartPreventExitStatus=2` so it is not restarted in a loop), `3` listen port could not be bound. Each non-zero code maps to
one typed exception caught in `Program.cs`. A new failure category needs a typed exception, an
`ExitCodes` constant, a catch in `Program.cs`, and updates to the exit-code text in `Usage()` and the README.
Exemplars: `src/ExitCodes.cs`, `src/Program.cs:29-51`.

**H-ERR-2 Typed startup exceptions.** Invalid configuration throws `ConfigurationException`; a bind failure
throws `ListenException` wrapping the `SocketException`. Messages start with `Proxy <label>: ` when about
a proxy, name the setting key, include the offending value unless it is sensitive, and must never contain
secrets (wrap third-party messages with `SecretRedactor.RedactText`/`Describe`).
Exemplars: `src/ProxyHost.cs:39-56`, `src/HybridConnectionProviderFactory.cs`.

**H-ERR-3 Narrow catches, `when` filters.** Catch specific types, using `when` filters to narrow further.
Broad `catch (Exception)` is allowed only at these boundaries, and must log:
the top level (`Program.cs`), a proxy's accept loop (`ClientProxy.Run`), a connection
(`ClientProxyConnection.Run`), and the relay-connect call, where any non-cancellation failure means
"relay unavailable: drop this client".

**H-ERR-4 Empty catches.** Allowed only for best-effort cleanup after the outcome is already decided
(closing streams to unblock reads, awaiting pumps after closing, stopping listeners) and for expected
conditions. Each carries a comment saying why.
Exemplar: `src/ClientProxyConnection.cs:51-60`.

**H-ERR-5 Cancellation.** Shutdown-time `OperationCanceledException`, `ObjectDisposedException` and
`SocketException` are swallowed silently, but only when the shutdown token is actually cancelled
(`when( cancellation.IsCancellationRequested && … )`).
Exemplar: `src/ClientProxy.cs:44-47`.

**H-ERR-6 Background tasks.** Every task started without being awaited either catches all of its own
exceptions (connection tasks), or is tracked and awaited at shutdown (`ClientProxy._connections`,
`ProxyHost._proxies`), or has its fault observed in a continuation (abandoned relay connect in
`HybridConnectionClientProvider`). Discards with `_ =` are only for tasks that cannot fault or whose fault
is observed.

## Configuration (CFG)

**H-CFG-1 Sources and precedence.** `ConfigurationManager` layers, later winning: `appsettings.json` →
`appsettings.Windows.json` (Windows) → journald defaults (in-memory) → `appsettings.<MachineName>.json`
in the directory from `ConfigurationLocator` → user secrets (Debug only) → environment variables →
command line. Exemplar: `src/AppConfiguration.cs:20-52`.

**H-CFG-2 Typed binding, strict per proxy.** Bind to `Settings.AppSettings` / `Settings.Proxy`. Each proxy is
bound individually with `ErrorOnUnknownConfiguration = true` so misspelt keys and mistyped values fail
instead of silently dropping the proxy. Binder errors become `ConfigurationException` (redacted).

**H-CFG-3 Validate everything before side effects.** All proxies are validated (port range, required port,
IP address, connection string parseable and complete) before any listener is bound. Then all listeners are
bound before any proxy starts; a failure stops the ones already bound.
Exemplar: `src/ProxyHost.cs:35-92`.

**H-CFG-4 Required vs defaulted.** Required: `HybridConnectionString`, `ListenPort` (0 means missing).
Optional: `Name` (labels only), `ListenIPAddress` (default `127.0.0.1`). An explicit `--config` /
`HCCP_CONFIG_DIR` directory must exist, but the file in it is optional. New settings are documented in the
README's Proxies table and state their default.

**H-CFG-5 No committed secrets.** Committed JSON holds only placeholders
(`config/appsettings.template.json`). Real connection strings come from environment variables
(systemd-creds in production), user secrets (development) or an untracked machine settings file. CI
writes `appsettings.Test.json` from the `APPSETTINGS` secret at build time.

## Resilience and resources (RES)

**H-RES-1 Relay failures are not retried.** If the relay connection fails, that client connection is dropped
with a Warning and the proxy keeps listening; the client application retries. A transient accept
`SocketException` waits 100 ms and continues.

**H-RES-2 Relay connect timeout.** `HybridConnectionClient.CreateConnectionAsync` cannot be cancelled. It is
bounded by the library's operation timeout (configurable via `OperationTimeout` in the connection string)
and abandoned on shutdown through `WaitAsync(token)`, which disposes any late-arriving stream.
Exemplar: `src/HybridConnectionClientProvider.cs`.

**H-RES-3 Disposal.** Use `using var` for `TcpClient`, streams and linked `CancellationTokenSource`s. When one
pump finishes, cancel the other and explicitly `Close()` both streams, because `HybridConnectionStream`
reads do not honour cancellation. Then await both pumps.
Exemplar: `src/ClientProxyConnection.cs:11-72`.

**H-RES-4 Graceful shutdown.** SIGTERM/SIGINT (`PosixSignalRegistration`) or a key press cancels one
`CancellationTokenSource`; a second signal falls through to default termination. `ProxyHost.StopAsync`
cancels, stops listeners, and waits up to 5 s for connections, logging a Warning on timeout. Logs are flushed
in `Program.cs`'s `finally`. The systemd unit's `TimeoutStopSec` must exceed the drain timeout.

---

## Open questions (for the owner)

1. **Gitignore does not cover the real config locations (H-CFG-5).** `/config/*.json` and
   `!/config/appsettings.template.json` are anchored to the repository root, but the config directory is
   `HybridConnectionClientProxy/config/` (where the template is). That is also where a Debug run looks for
   its machine settings file. `HybridConnectionClientProxy.Tests/appsettings.Test.json` (Azure credentials)
   is not ignored either. Proposed fix: `**/config/*.json`, `!**/config/appsettings.template.json`,
   `**/appsettings.Test.json`.
2. **Cancellation swallowed without checking the token (H-ERR-5).** `ClientProxyConnection.cs:64` swallows
   any `OperationCanceledException`, and the relay catch at line 30 skips them. A cancellation the relay
   library raises itself (for example a timeout surfaced as `TaskCanceledException`) would drop the client
   with no log at all. Should it be `catch( OperationCanceledException ) when( cts.IsCancellationRequested )`,
   matching `ClientProxy.cs`?
3. **Telemetry (H-TEL-1).** Should "logs are the only telemetry; no metrics, tracing or health endpoint" be
   declared deliberate? Should the service tell systemd it is ready or still alive (`Type=notify`,
   watchdog), or is `Type=exec` enough?
4. **A proxy whose accept loop dies.** `ClientProxy.cs:48-51` logs an Error and that proxy stops, while the
   process keeps running with the other proxies, so systemd never restarts it. Is a degraded-but-running
   process intended, or should a dead accept loop end the process with a non-zero exit code?
5. **Serilog misconfiguration exits with code 1.** Errors from `Logging.CreateLogger` (bad sink or bad
   `Serilog` section) reach the generic catch and exit 1, so systemd will restart in a loop. Should they
   become `ConfigurationException` (exit 2)?
6. **Global identity and properties (TEL4/LOG3).** `Application` is set only when a `Serilog` section exists
   (`Logging.CreateLogger` fallback adds nothing), and `Version` appears only in the startup line. Should
   both be global properties? Should `{Proxy}` be split into `ProxyIndex`/`ProxyName` for structured
   filtering? The bootstrap logger enables `Enrich.FromLogContext()` but the main logger does not; nothing
   uses `LogContext` today, so pick one.
7. **Stack traces.** Errors use `SecretRedactor.Describe` (no stack trace); only the last-resort Fatal keeps
   the stack. Is that the intended policy for `Error` level (H-LOG-5)?
8. **Remote endpoint as personal data.** `{Remote}` (client IP) is logged at Debug. Acceptable?
9. **Uncommented empty catches.** `ProxyHost.cs:118` (`try { listener.Stop(); } catch { }`) and
   `Program.cs:116-118` have no explanatory comment as H-ERR-4 requires. Add comments, or exempt
   obviously-expected cases?
10. **Process-wide safety net.** No `AppDomain.UnhandledException` or `TaskScheduler.UnobservedTaskException`
    handler is registered. Add one that logs Fatal (redacted) and flushes, or rely on H-ERR-6 alone?
11. **Hard-coded timings.** The 5 s shutdown drain and the 100 ms accept back-off are constants. Should the
    drain timeout be a setting, so it can be kept below `TimeoutStopSec`?
12. **Unused packages.** `Azure.Identity` and the JWT packages are referenced for future Entra ID auth. When
    that lands, token acquisition will need its own timeout, redaction and retry rules here.
