# Aw.Kernel.Telemetry

Logging, spans and metrics for every C# host, with `Aw.Redaction`'s scrubber on every log and export path (spec §4b.1,
§4d "runtime hardening", §5 W7). Tier 0, sublevel 1 in `deps.yaml` (edges to `Kernel` and `Redaction`); its one
external dependency is `OpenTelemetry`. Its closure (see `src/Aw.Kernel.Telemetry/packages.lock.json`) also brings
`Microsoft.Extensions.Logging`, `.Logging.Configuration`, `.Configuration` (with `.Binder` and the
`.EnvironmentVariables` provider), `.Options` and `.DependencyInjection` into every host: nothing in this repo may use
`IConfiguration` (csharp.md DI rules), and no exporter, OTLP, gRPC or HTTP package is in it.

## Start it in the Host

```csharp
// Composition.Compose(services, environment, clock) — the signature every Host has (csharp.md DI rules):
services.AddKernelTelemetry(TelemetryOptions.FromEnvironment("aw-readiness", environment, clock));
// A host that exports picks the adapter here too: … with { TraceExporter = <adapter from Infrastructure> }.

// Program.cs, right after building the container (an http host: in LoopbackBinding.Build, right after builder.Build()):
// resolving the runtime hands it to the container, which then disposes (and flushes) it on exit.
using var provider = services.BuildServiceProvider();
_ = provider.GetRequiredService<TelemetryRuntime>();
```

`FromEnvironment` reads `AW_LOG_LEVEL` and takes the host's clock (a `FakeTimeProvider` in the DI validation test). It
never fails the host: an unknown level stays at information and sets `TelemetryOptions.Problem`
(`AW-TELEMETRY-001`, without the value, which could be anything pasted into the variable); the runtime writes it once
as its first line, a warning whose `eventName` is the code. Without DI, `using var telemetry =
TelemetryRuntime.Start(options)` and `telemetry.LoggerFactory`. `AddKernelTelemetry` starts the runtime right away (so
spans are recorded and the problem line is written while Compose runs, before anything resolves a logger) and
registers it as a singleton. The container disposes a singleton only once something has resolved it, hence the
resolve in Program.cs (an http host: in `LoopbackBinding.Build`, which Program.cs and its tests share); disposing the
runtime flushes it. A second `AddKernelTelemetry` call throws. Run **one runtime per process**: the withheld counter is process-wide.

OpenTelemetry's SDK reads some standard `OTEL_*` variables. The resource is exactly `service.name` from `ServiceName`
(`ResourceBuilder.CreateEmpty()`, so `OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` are ignored), and the sampler
is set in code (always on, so `OTEL_TRACES_SAMPLER` is ignored). `OTEL_SDK_DISABLED=true` still turns the providers
off: spans aren't recorded and log lines then carry null ids. None of them can turn exporting on: only an exporter
the host passes exports.

| Option | Default | Notes |
|---|---|---|
| `ServiceName` | required | On spans and metrics (the OpenTelemetry resource) |
| `MinimumLevel` | information | `AW_LOG_LEVEL`: trace, debug, information, warning, error, critical, none (any case); anything else is `AW-TELEMETRY-001` |
| `Sink` | standard error | An `ILogSink` (below) |
| `Redactor` | `Redactor.Default` | The scrubber on every path |
| `Clock` | `TimeProvider.System` | Log timestamps (`FakeTimeProvider` in tests) |
| `SourcePattern` | `Aw.*` | Activity sources and meters listened to (OpenTelemetry wildcard) |
| `TraceExporter`, `MetricExporter` | null | **Exporting is off by default.** An exporter is an adapter the host opts into (an OTLP exporter for a local collector is added with the first host that needs it); the runtime owns and disposes it |

## Log lines

One JSON object per line, written with `Utf8JsonWriter` (no reflection), relaxed escaping, '\n' terminated:

```json
{"timestamp":"2026-10-10T06:00:00.0000000Z","level":"warning","category":"Aw.Smoke","eventId":3,"eventName":"Call","message":"calling with Authorization: Bearer [REDACTED:bearer]","traceId":"…32 hex…","spanId":"…16 hex…","actor":"host:smoke","tenant":"personal","ring":0,"correlationId":"0f8fad5b-…","fields":{"Item":"item-1"}}
```

- `timestamp` in the AwJson `O` form (UTC, seven fractional digits, `Z`); `level` lowercase; `eventId` always,
  `eventName` and `exception` (the exception's `ToString()`) only when present.
- `traceId`/`spanId`: the current `Activity`, or `null` outside a span. The tracer provider records spans even with
  exporting off (unless `OTEL_SDK_DISABLED=true`), so log lines inside a span carry ids.
- `actor`, `tenant`, `ring`, `correlationId`, `causationId`: from the innermost `EventContext` scope
  (`logger.BeginScope(context)`). Other scope states are not written.
- `fields`: the structured values of a `[LoggerMessage]` call, as invariant text (a list as its elements joined with
  ", "). A name the template repeats gets a numbered key (`A`, `A_2`), so no JSON property repeats.
- Log through `[LoggerMessage]` source-generated methods (`CA1848`); hand finished text to `ILogger.Log` only when the
  text is built at run time.

## Standard error is diagnostic (the CLI stderr contract)

A CLI host's log lines share standard error with its own error output. So a program that calls one of these CLIs (a
`cli` edge in `deps.yaml`, such as sindri running `aw-readiness check --json`) reads:

- **the outcome from the exit code, as that CLI defines it.** The codes are per command, not one global map: for
  `aw-readiness check`, 0 the level holds, 1 it doesn't (a successful run with a negative result: the report is on
  standard output and standard error has no error line, only log lines if any), 2 a usage or tool error (`docs/readiness/README.md`); for
  `aw-packets`, 0 every check passed, 1 a check failed or the packet is malformed, 2 a usage or tool error
  (`docs/csharp/packets.md`). A package scaffolded by `/new-package` starts with 0 success, 1 an error `Result`,
  2 usage, and its own docs define the codes once its commands exist;
- **the result from standard output** (`--json` where the command offers it);
- **the error from standard error only to show it.** A program that must parse it passes `--json` where the CLI has it,
  then takes the one line with a `code` member and no `level` member: the error envelope (`{"code","kind","message"}`,
  the Kernel `Error`) never has `level`, and a log line always has `timestamp` and `level`. Error lines are written
  only on an error, so a nonzero exit doesn't by itself mean standard error has one (`aw-readiness` exit 1 has none).
  Without `--json`, the error is whatever the CLI documents (`aw-packets: <code> <message>`). A missing or unknown
  command is a `usage:` text line in every CLI; a bad argument to a command is that CLI's documented error
  (`aw-readiness`: the `AW-READINESS-001` envelope, with or without `--json`; `aw-packets`: a `usage:` line).

Standard error is otherwise diagnostic: its log lines change with `AW_LOG_LEVEL` and with what the code logs, so
nothing parses them as a contract. Golden tests and smokes that capture standard error pin a level that writes no log
lines, because every line carries a wall-clock timestamp (the AOT smokes set `AW_LOG_LEVEL=none`; the unknown-level
warning is checked by the scaffold e2e's native run instead).

`judge` is the exception on both points (`docs/csharp/judge.md`, Exit codes and streams). Its AOT smoke leaves
`AW_LOG_LEVEL` unset, to pin that the default level writes nothing to standard error. And its hook contract (spec §4c)
has one standard-error point a hook parses: `done-gate.sh` matches a line starting `no brief for` (`brief get`, exit 1).
The hook matches by line (`grep '^no brief for'`), so a JSON log line beside it can't break the match.

## Framework logs in http hosts

An http host clears ASP.NET's logging providers (`/new-package`'s `LoopbackBinding`), so Kernel's factory is the only
logger and ASP.NET's own lines (lifetime, hosting diagnostics, routing, Kestrel) are scrubbed JSON too. The factory
filters by `MinimumLevel` only, with no per-category rules, and the host reads no `Logging:LogLevel` configuration.
At the default level, information, each request therefore writes its hosting and routing lines, where a stock ASP.NET
app would hold `Microsoft.AspNetCore` at warning. This is deliberate for now: these hosts are local, loopback-only and
low-traffic, and the request lines carry the trace ids. A host that finds them too loud sets `AW_LOG_LEVEL=warning`;
per-category minimums (`Microsoft.*` and `System.*` at warning) are a `TelemetryOptions` change for when the first
http package needs them (none is planned in R2 since the bridge was retired; the scaffold's http shape stays).

## Scrubbing on every path

A name counts as a secret's when `IRedactor.IsSecretName` says so after a leading `@` or `$` (template destructuring
prefixes) is removed.

| Path | What happens |
|---|---|
| Log line | category, event name, message, exception (`ToString()`, inner exceptions included), every field key and value scrubbed; a field with a secret's **name** is withheld whole (`[REDACTED:secret-field]`), and so is a secret-named entry of a dictionary, a `KeyValuePair<string, T>` (alone or in a list) or a `(string, T)` tuple (`[password, [REDACTED:secret-field]]`), and an object's text as records and anonymous objects print it is cut at its first secret-named member (or a key-value pair's or tuple's secret key, as `[password, …` or `(password, …` prints inside it), nested objects included (`LoginRequest { User = alice, Password = [REDACTED:secret-field] }`: nothing after that point is written, because a value may itself hold `,` or `}`); a `char[]` is scrubbed as one string; nesting deeper than 8 levels (a list that contains itself) is written as `[REDACTED:too-deep]`. Any other object is written as its `ToString()` and only regex-scrubbed, so don't log objects that hold secrets in other shapes. The message is rendered again from its template when it holds such a value, as the formatter does (alignment and format applied), each hole taking the field of its own name: a source-generated `[LoggerMessage]` lists fields in parameter order, so position would put a value in the wrong hole |
| Span (only with a trace exporter) | the scrubbing processor runs before the export processor: display name, tag values and status description are rewritten in place. A tag with a secret's name is withheld whatever its type; numbers and flags are kept; a list is scrubbed element by element, with secret-named dictionary entries withheld (the tag becomes a string array); any other object is scrubbed as its text. A list holding anything but text, numbers and flags always becomes its rendered strings. The operation name, trace state, tag keys, a repeated tag key's values (`AddTag` allows repeats, `SetTag` rewrites only the first), the source's tags, events, and links (their tags and trace state) can't be rewritten, so a span with a secret in any of them is **dropped** (marked not recorded). Without an exporter nothing leaves the process, so spans aren't touched |
| Metric | the exporter is wrapped: an instrument whose meter tags or any point's tags carry a secret (in a key, a value, a list element, or as a secret's name) is **withheld** from every export for the life of the process (cumulative points come back each time) and counted once. Metric tags should be small fixed sets |
| Blocked scrub (a match timeout) | never passes raw text: a log line is written with `[REDACTED:blocked]` in place of the text; a span or metric instrument is dropped |
| Failure (the sink or a value's `ToString()` throws) | never thrown into the caller: the log line or span is dropped and counted as `failure`; `Flush` and `Dispose` never throw (a writer the host already closed is counted too) |

Withheld records are counted: `TelemetryRuntime.Withheld` (logs, spans, metrics, failures, for this runtime) and the
process-wide counter `aw.telemetry.scrub.withheld` with tag `path` (`log`, `span`, `metric`, `failure`), never with
the payload.

**Limit:** the wrapped metric exporter's `ParentProvider` stays unset (OpenTelemetry's setter is internal), so an
exporter that reads its resource from the provider sees none, and metric exports carry no `service.name`. The OTLP
adapter, when a host adds one, has to take the resource explicitly.

## The sink port

`ILogSink.WriteLine(line)` and `Flush()`; one call is one line, a line with a line break is refused
(`ArgumentException`), concurrent writers never interleave. `TextWriterLogSink` is the adapter over any `TextWriter`
(standard error, or a file the host opened; the host owns the writer). The in-memory fake (`InMemoryLogSink`) and every
real sink pass `LogSinkContract` in `tests/Aw.Kernel.Telemetry.Testing`; that project also has
`CollectingActivityExporter` and `CollectingMetricExporter` for tests that opt into exporting.

## NativeAOT

`src/Aw.Kernel.Telemetry.AotSmoke/run.sh` publishes a NativeAOT binary (0 IL2xxx/IL3xxx warnings required), runs it and
compares its output with `expected.txt`; the CI .NET job runs it with the other smokes. `stdin-mode` logs each
standard-input line and prints the JSON lines (for a hand test).
