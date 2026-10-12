# Sindri (C#) — the app shell

Sindri is built new in C# as one application (factory addendum §17.29, decision Q7), not ported from the TS
`sindri/`, which stays the reference for *what* a part does and keeps running until the switch. This page is the C#
contract for stage S1, the app shell: the `sindri` binary, the control-plane process, the host lease, the ledger, the
profile, the command layer and its API, the scheduler and `doctor`. The workflow engine (S2), context retrieval (S3),
blocks and workflows come later and plug into it. The Avalonia UI is its own stage (S1b).

Tier 3 in `deps.yaml` (`src/Sindri`, full shape: Core · Features · Infrastructure · Host). Nothing installs it before
the switch: `setup.sh`, the providers and launchd don't point at it.

## The binary

One NativeAOT binary, `sindri`, is both the control plane and its CLI.

| Command | What it does | Exit |
|---|---|---|
| `sindri` | Prints the banner, starts the control plane if it isn't running (detached in its own session, so a Ctrl-C or a kill of the terminal's process group doesn't reach it; `sindri serve`, output in `<home>/logs/serve.log`), then prints the startup summary. It starts one only when nothing runs (`SND-API-001`); one that answers late (`SND-API-002`) is reported as it is. A started child that exits with `SND-LEASE-001` lost the lease to a concurrent start, so it waits up to 10 s for the winner to answer; past that the error ends with the line the child exited with. Only lines written to the log since this start count, not an earlier run's | 0; 1 when the profile needs approval, or the control plane went away before its status answered (`SND-API-001`); 2 on any other error (e.g. it can't start `SND-CLI-002`, the running one answers late `SND-API-002`, or the status call answers an error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri --headless` | The same without the UI line (the UI is S1b) | as above |
| `sindri serve` | Runs the control plane in the foreground: what bare `sindri` starts, and what launchd will keep alive later | 0 on a clean stop; 1 when another process holds the lease (`SND-LEASE-001`) or the lease was lost (`SND-LEASE-002`); 2 when the port is taken or refused, or the host fails to start listening (`SND-API-003`; the stop is recorded and the lease released), or `AW_SINDRI_PORT` isn't a port number 0..65535 (`SND-CLI-003`, checked before the lease is taken), or the ledger can't be opened (`SND-LEDGER-001/002`) |
| `sindri status [--json]` | The running control plane's status, through the API | 0; 1 when it isn't running (`SND-API-001`) or the profile needs approval (`profile.needsApproval` in the response); 2 on any other error (e.g. it didn't answer in time `SND-API-002`, answered an error without a code `SND-API-004`, failed inside `SND-API-005`, or the ledger or the saved approval can't be read `SND-LEDGER-002`/`SND-PROFILE-006`) |
| `sindri stop` | Stops it: records the stop, releases the lease, shuts down | 0; 1 when it isn't running (`SND-API-001`); 2 on any other error (e.g. it didn't answer in time `SND-API-002`, the stop couldn't be recorded `SND-LEDGER-002`, answered an error without a code `SND-API-004`, or failed inside `SND-API-005`) |
| `sindri profile validate [--json]` | Checks the live profile file and prints its hash | 0 valid and approved (it is the saved approval and the latest recorded `profile.approved` event names it, the rule every surface uses); 1 valid, not approved; 2 on any error: invalid (`SND-PROFILE-001/002`), the saved approval can't be read (`SND-PROFILE-006`) or the ledger can't be read (`SND-LEDGER-002`), neither of which is ever "not approved" |
| `sindri profile approve` | Shows the live hash and the key paths changed since the latest approval; changes nothing | 0 when it already is approved; 1 when not approved yet, or when it isn't running (`SND-API-001`); 2 on any other error (e.g. an invalid profile `SND-PROFILE-001/002`, a ledger it can't read `SND-LEDGER-002`, no answer in time `SND-API-002`, an API error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri profile approve <hash>` | Approves: the hash must be the live one, and you type its first 6 characters at an interactive terminal | 0; 1 when it isn't running (`SND-API-001`); 2 on any other error (e.g. a malformed hash `SND-CLI-003`, a stale hash `SND-PROFILE-003`, no terminal or a wrong confirmation `SND-PROFILE-004`, approvals that can't be written `SND-PROFILE-006`, a ledger it can't read or write `SND-LEDGER-002`, no answer in time `SND-API-002`, an API error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri doctor [--json]` | One line per check, in order: `lease`, `ledger`, `profile`, `api`, each `ok` / `warn` / `fail` with a fix. `profile` is `ok` only when approved by the one rule (see Profile); changed since the saved approval, or saved but not the latest recorded one, is a `warn` | 0 all ok, 1 any warn, 2 any fail |
| `sindri recipe validate <file> [--json]` | Validates a recipe in this process against the build's block catalog, never contacting the control plane: one line per finding (errors first; information and skipped findings labelled), then the recipe's hash. `--json` adds each finding's fields and the normalized long form. Reference: `docs/csharp/sindri-workflow.md` | 0 no errors; 1 any `RCP-*` error; 2 a usage error (no file, `SND-CLI-003`; an unknown `recipe` verb or extra arguments, `SND-CLI-001`), an unreadable file (`SND-RECIPE-010`) or a bug inside sindri (`SND-API-005`) |
| `sindri recipe eval <file>` | Arrives with the engine's evals (P3); until then it refuses | 2 (`SND-CLI-001`) |

Output: state is always a word, never colour; there is no colour at all, so `NO_COLOR` and redirected output need
nothing special. Errors go to stderr as `SND-<AREA>-NNN message`, with `(fix: …)` where there is one, or `(usage: …)` for a usage error. `--json` prints
the response record through AwJson.

**Which verbs are API clients.** `status`, `stop` and `profile approve` call the running process's API (the CLI is a
client of the same handlers the UI will call, §17.27.1). `doctor` and `profile validate` run the same handlers in the
calling process, read-only, because they must work while the control plane is down (`doctor` reports exactly that).
`recipe validate` is local too: it reads only the recipe file and its child recipes beside it.
`status` and `stop` never start the process: "not running" is an answer. `profile approve` needs it running, because
the approval is a ledger event fenced by the lease.

## Files

`AW_SINDRI_HOME`, default `$AW_STATE_DIR/sindri-app` (`$AW_STATE_DIR` defaults to `~/.agentic-workflow`). It is apart from
the TS sindri's `$AW_STATE_DIR/sindri` until the switch; no TS state is read. Each directory Sindri asks for (the home,
`approved/…`, `logs`) is made 0700 when Sindri creates it; parents created along the way keep the umask, and an existing
directory keeps its mode; files are 0600.

| Path | What |
|---|---|
| `ledger.db` | The ledger (SQLite, WAL, through `Aw.Kernel.Data`): `events`, `lease`, `outbox_commands` |
| `approved/<hash>/profile.json`, `approved/latest` | Approved profile snapshots (canonical JSON) and the latest hash. Runtime reads only these, never the live file |
| `control.json` | The running control plane's endpoint: `url`, `pid`, `epoch`. Written once it listens, removed on stop. A client checks the pid and epoch against `/sindri/ping`, so a stale record reads as "not running" |
| `logs/serve.log` | The detached control plane's output (0600: written under umask 077, and an older log is narrowed after each start) |
| `profile/profile.yaml` | The live profile (`AW_SINDRI_PROFILE` overrides the path) |

## Profile

YAML, typed through AwJson (owner decision 2026-10-11): the YamlDotNet parser and representation model (no reflection)
turn the file into JSON, which AwJson reads into `SindriProfile`. Scalars follow a subset of the YAML 1.2 core schema
(null, booleans, decimal integers and decimals; everything else is a string, so `yes` and `no` stay strings);
anchors, aliases, tags and duplicate keys are refused; unknown keys are errors.

```yaml
schemaVersion: 1          # required; this build reads 1
user: thor                # required
tenant: local             # optional, default local (the tenant key on every row until team mode)
repos:                    # optional
  - { name: agentic-workflow, path: /Users/thor/repos/agentic-workflow }
services:                 # optional: background services by name, each with a cadence
  heartbeat: { every: 30s }   # <n>s|m|h, 5s to 24h
api: { port: 7410 }       # optional, default 7410; 0 (a free port) or 1024..65535; AW_SINDRI_PORT overrides it
```

The approval hash is SHA-256 over the canonical JSON (the checked record, repos and services sorted), so comments and
key order don't change it. An approval snapshot whose content no longer matches its hash isn't trusted.

Approving appends the `profile.approved` event first and saves the snapshot second. A failed append changes nothing;
a retry after a failed save finds the latest recorded approval already naming that hash, appends nothing and only saves. "Approved" means saved
and named by the latest recorded `profile.approved` event, so a saved approval whose event is missing (or was followed by a later one)
is not approved, and approving again records it. Every surface judges by this one rule (`ProfileRules.IsApproved`): `profile
validate`, the `profile approve` preview, `status` (`profile.needsApproval`) and the startup summary ("approval not recorded in
the ledger as the latest one"), and `doctor`'s profile check (a `warn` with the fix `sindri profile approve`). A ledger or a
saved approval that can't be read is an error on each of them, never "not approved": `profile validate`, `status` and bare
`sindri` exit 2 with `SND-LEDGER-002` or `SND-PROFILE-006`, the preview refuses (approving a hash still works, and it repairs
an unreadable snapshot), and `doctor` fails its profile check. `status`'s `profile.problem` names only a live file that doesn't load.

## Host lease

One control plane per host (design spec §9.1, factory addendum §17.22 D2). The lease is one row in `ledger.db`: holder
(host, pid, pid start time to the second, boot id), epoch, acquired and expiry times, released flag. TTL 60 s, renewed
every 20 s.

- **Acquire** (in one `BEGIN IMMEDIATE`): free or released → taken with epoch + 1. A holder that is provably dead on
  this machine (another boot, no such pid, or the pid reused by a later process) → taken at once, so `kill -9` doesn't
  cost a 60 s wait. A holder proven alive on this machine → refused with `SND-LEASE-001 held by <host>/<pid> since
  <time> (epoch N)`, even past expiry (its renewals are only late). A holder on another machine, whose liveness can't be
  proven → refused until the lease expires, then taken. Start times that differ by at most 2 s count as the same
  process (Linux derives them from boot time and clock ticks, so they jitter).
- **Renew** succeeds while the row still names this holder at this epoch, even past expiry (a process that slept, when
  nobody took over, lost nothing to fencing). Once another process took the lease, renew fails with `SND-LEASE-002` and
  the process stops (exit 1): it must not write under a stale epoch. Any other renewal error (a busy or unreadable
  ledger) is logged and retried at the next tick, until the grant expires; past expiry the process stops too. A stop
  clears the grant and requests the shutdown before releasing the lease; a renewal that finishes afterwards doesn't
  hold the grant again, and one that lost to the release ends quietly (a stop, not a loss: exit 0).
- **Fencing:** every ledger append checks, in its own transaction, that the writer's grant is the current epoch;
  otherwise `SND-LEASE-003` and nothing is written.

## Ledger

`ILedgerStore` (Core port; SQLite adapter in Infrastructure; contract suite `LedgerStoreContract`, passed by the
in-memory store and the SQLite adapter). Append-only, idempotent and fenced.

| Column | |
|---|---|
| `seq` | position, every consumer's cursor |
| `id`, `type`, `schema_version`, `occurred_at`, `actor`, `tenant`, `ring`, `correlation_id`, `causation_id`, `trace_id` | the §17.21 envelope (`Aw.Kernel.Events.EventEnvelope`) |
| `item` | the work item, when there is one (S2+) |
| `epoch` | the writer's lease epoch |
| `idempotency_key` | unique: a second append with the same key writes nothing and returns the first event's position |
| `payload_json` | the payload record, through AwJson |

S1 events and their keys: `process.started` (`process.started:<epoch>`), `process.stopped` (`process.stopped:<epoch>`),
`profile.approved` (`profile.approved:<hash>:after:<seq of the latest profile.approved event, or 0>`, so each
approval is recorded: approving A, B, A, B, A records five events), `service.heartbeat` (`service.heartbeat:<epoch>:<slot>`). A service's
run event is `service.<name>`. `outbox_commands` is Kernel.Data's outbox table (migration 3), empty in S1: outward
actions join it in later stages.

## Command layer and API

Every action is a typed request handled by one `IService<TReq, TRes>` in `Features` (§17.27.1). The API, on
`127.0.0.1:<port>` only (`LoopbackBinding`: no argument or environment can widen it, and the host environment is always Production, so an inherited `ASPNETCORE_ENVIRONMENT=Development` can't turn bad input into a 500; a foreign `Host` is a 400, a
foreign browser `Origin` a 403):

| Route | Service |
|---|---|
| `GET /sindri/status` | `GetStatus`: running, pid, epoch, lease expiry, approved and live profile hashes, each service with its cadence and last run |
| `GET /sindri/ping` | `PingProcess`: `{pid, epoch}` |
| `POST /sindri/stop` | `StopProcess` |
| `POST /sindri/profile/approve` | `ApproveProfile` (`{hash, confirmed}`) |
| `GET /sindri/events?after=N` | `StreamEvents` as server-sent events: one event per ledger row, `id` = `seq`, `event` = type; resumes from `after` or `Last-Event-ID`; polls the ledger once a second; ends when the client goes away or a stop is requested, so a stop doesn't wait out Kestrel's 30 s graceful shutdown |

A service's error is ProblemDetails with the `SND-*` code as `title`. The API's own failures carry a code too: an
unhandled exception in a handler is logged and answered 500 with `SND-API-005`, and a body or query value the request
binding refuses (JSON that doesn't read as the request, a malformed hash or id) is a 400 with `SND-CLI-003` and fixed
text naming the method and path (a framework's message is logged, never answered). The loopback guard's 400 and 403, an
unknown route's 404, a wrong method's 405 and a wrong `Content-Type`'s 415 carry no ProblemDetails body; the CLI
reports an error answer without a code as `SND-API-004`, never as "not running". The approve route's `confirmed` is the CLI's claim that the human typed the
confirmation; in S1 that terminal check in the CLI is the only gate. There is no API token in S1: the threat model guards honest
mistakes (`docs/csharp/bugfix.md`), and approval stays terminal-bound in the CLI. Device-bound approvals for the UI
(passkeys, §17.21 D6) come with the UI's action forms.

## Scheduler

The control plane runs the services the **approved** profile names, on their cadence. It re-reads the approval once a
second, so an approval takes effect without a restart. Missed slots after a sleep are skipped, not replayed in a burst. A
failed run (an error or an exception) is logged (`SND-SCHED-001`) and the next slot still runs; an approval it can't read
is logged once (`SND-SCHED-001`) and the current schedule keeps running. Nothing runs while the process holds no lease. S1
ships one service, `heartbeat`, which records `service.heartbeat` per slot; the index, intake and scorer join later as
blocks.

## Errors

| Code | When |
|---|---|
| `SND-LEASE-001` | Another live process holds the lease |
| `SND-LEASE-002` | This process lost the lease (another took it after expiry) |
| `SND-LEASE-003` | A write carried a stale epoch |
| `SND-PROFILE-001` | No profile file, or it can't be read |
| `SND-PROFILE-002` | The profile doesn't parse or doesn't match the schema (file, key path, expected type, fix) |
| `SND-PROFILE-003` | The hash to approve isn't the live profile's |
| `SND-PROFILE-004` | Approving needs an interactive terminal and the typed confirmation |
| `SND-PROFILE-005` | No profile is approved, or the latest approval's snapshot is missing or changed |
| `SND-PROFILE-006` | The approved snapshots can't be read or written (permissions, a full disk) |
| `SND-LEDGER-001` | The ledger's schema doesn't match this build |
| `SND-LEDGER-002` | The ledger can't be opened or written |
| `SND-API-001` | The control plane isn't running (no endpoint record, or nothing answers at it) |
| `SND-API-002` | It didn't answer the ping within 2 s, or the call itself within 10 s |
| `SND-API-003` | `sindri serve` can't listen on the API port: another program has it, it is refused, or the host failed to start listening (the stop is recorded and the lease released) |
| `SND-API-004` | The API answered an error without a catalogue code (an unknown route, a wrong method or `Content-Type`; the HTTP status is named) |
| `SND-API-005` | A handler failed with an unexpected exception; the control plane logged it |
| `SND-CLI-001` | Unknown command |
| `SND-CLI-002` | `sindri serve` didn't come up after starting it |
| `SND-CLI-003` | Bad input: a CLI argument or setting that is malformed (a profile hash that isn't 64 hex digits, an `AW_SINDRI_PORT` that isn't 0..65535), or an API request body or value the control plane refused (a 400) |
| `SND-SCHED-001` | A background service run failed |

### Workflow errors (`WorkflowErrors`)

The workflow engine's catalogue (engine spec §8) is a second class, `Aw.Sindri.Core.Workflow.WorkflowErrors`, validated
together with `SindriErrors`. Retired codes stay reserved in `WorkflowErrors.Retired` and are never reused.

| Code | Kind | When |
|---|---|---|
| `SND-RECIPE-001` | invalid | Validation failed; fix each `RCP-*` line |
| `SND-RECIPE-002` | notFound | No recipe with that id or hash (`sindri recipe list`) |
| `SND-RECIPE-003` | conflict | Not approved (`sindri recipe approve`) |
| `SND-RECIPE-004` | — | Retired: a caller without a terminal gets the API's `SND-AUTH-002` |
| `SND-RECIPE-005` | conflict | Revoked; approve a new version |
| `SND-RECIPE-006` | conflict | A pinned block changed since approval; re-approve (`recipe list --stale`) |
| `SND-RECIPE-007` | conflict | Same id and version, other content; raise `version` |
| `SND-RECIPE-008` | conflict | Evals missing or failing (`sindri recipe eval <file>`) |
| `SND-RECIPE-009` | conflict | A recipe challenge expired, or its subject changed since the preview |
| `SND-RECIPE-010` | notFound | The recipe file can't be read |
| `SND-WF-001` | notFound | No such instance (`sindri workflow list`) |
| `SND-WF-002` | conflict | The instance changed; re-read and retry |
| `SND-WF-003` | conflict | The trigger isn't allowed here (`allowedCommands` in `status`) |
| `SND-WF-004` | conflict | A gate refused; act on the reasons |
| `SND-WF-005` | conflict | A stale run token; use the token in `status` |
| `SND-WF-006` | invalid | An ambiguous address; use a listed address |
| `SND-WF-007` | — | Retired: authorization is the API's `SND-AUTH-002` |
| `SND-WF-008` | invalid | A malformed desk report |
| `SND-WF-009` | conflict | The command's nonce was used, expired or is for another version |
| `SND-WF-010` | unavailable | No runtime for a named block or resource in this build |
| `SND-WF-011` | forbidden | The effect kind isn't on the profile's effect allowlist (`workflow.effects`) |
| `SND-WF-012` | conflict | An earlier attempt of this item is still live |
| `SND-WF-013` | conflict | A workflow challenge expired, or its subject changed |
| `SND-WF-014` | invalid | `--item` and `fact:item`'s `id` differ |

`sindri recipe validate <file> [--json]` and `sindri recipe eval <file>` are local checker verbs (engine spec W27): exit
0 pass, 1 fail, 2 usage or tool error; `eval` arrives with P3. The recipe reference, the `RCP-*` rule table and how to add
a block are in `docs/csharp/sindri-workflow.md`.

## Hook contract

None in S1. The `Hooks → Sindri` cli edge in `deps.yaml` (sindri-nudge) is the TS sindri's until the switch; the C#
app must provide what that hook reads before it replaces the TS one.

## Checks

```bash
dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=unit"          # domain, slices, CLI text
dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=integration"   # SQLite contracts, YAML files, the API on loopback, the scheduler
bash src/Sindri/aot-smoke/run.sh                                                # publish NativeAOT and drive the native binary (expected.txt)
```
