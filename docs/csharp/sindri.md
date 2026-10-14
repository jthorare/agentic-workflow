# Sindri (C#) — the app shell

Sindri is built new in C# as one application (factory addendum §17.29, decision Q7), not ported from the TS
`sindri/`, which stays the reference for *what* a part does and keeps running until the switch. This page is the C#
contract for stage S1, the app shell: the `sindri` binary, the control-plane process, the host lease, the ledger, the
profile, the command layer and its API, the scheduler and `doctor`. The workflow engine (S2), context retrieval (S3),
blocks and workflows come later and plug into it. The Avalonia UI is its own stage (S1b).

Tier 3 in `deps.yaml` (`src/Sindri`, full shape: Core · Features · Infrastructure · Host). Nothing installs it before
the switch: `setup.sh`, the providers and launchd don't point at it.

## The binary

One binary, `sindri`, is both the control plane and its CLI. It publishes framework-dependent, not NativeAOT: the Temporal .NET SDK its worker runs has no trimming support (temporalio/sdk-dotnet#286; `.agents/rules/csharp.md` "AOT scope").

| Command | What it does | Exit |
|---|---|---|
| `sindri` | Prints the banner, starts the control plane if it isn't running (detached in its own session, so a Ctrl-C or a kill of the terminal's process group doesn't reach it; `sindri serve`, output in `<home>/logs/serve.log`), then prints the startup summary. It starts one only when nothing runs (`SND-API-001`), and never from an agent session (Callers and auth): there it prints `SND-API-001 Sindri isn't running; ask the human to run 'sindri'` and launches nothing; one that answers late (`SND-API-002`) is reported as it is. A started child that exits with `SND-LEASE-001` lost the lease to a concurrent start, so it waits up to 10 s for the winner to answer; past that the error ends with the line the child exited with. Only lines written to the log since this start count, not an earlier run's | 0; 1 when the profile needs approval, or the control plane went away before its status answered (`SND-API-001`); 2 on any other error (e.g. it can't start `SND-CLI-002`, the running one answers late `SND-API-002`, or the status call answers an error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri --headless` | The same without the UI line (the UI is S1b) | as above |
| `sindri serve` | Runs the control plane in the foreground: what bare `sindri` starts, and what launchd will keep alive later. Refused in an agent session; `AW_SINDRI_PROVER=1` skips only the parent-process check, never the variables. After it takes the lease it brings up the Temporal dev server (engine spec rev 8 §0 "Operations"; P4a, the Temporal foundation): it adopts the server already running on `<home>/temporal.db` when that answers, kills and replaces one that doesn't, else starts the pinned `temporal server start-dev` on loopback, waits for the `sindri` namespace and registers the search attributes. A failure gives the lease back and exits 2 with `SND-CLI-002` | 0 on a clean stop; 1 when it is refused in an agent session (`SND-API-001`), or when another process holds the lease (`SND-LEASE-001`) or the lease was lost (`SND-LEASE-002`); 2 when the port is taken or refused, or the host fails to start listening (`SND-API-003`; the stop is recorded and the lease released), or `AW_SINDRI_PORT` isn't a port number 0..65535 (`SND-CLI-003`, checked before the lease is taken), or the ledger can't be opened, or the human token file can't be written (`SND-LEDGER-001/002`), or the Temporal dev server didn't come up (`SND-CLI-002`; the lease is given back) |
| `sindri status [--json]` | The running control plane's status, through the API | 0; 1 when it isn't running (`SND-API-001`) or the profile needs approval (`profile.needsApproval` in the response); 2 on any other error (e.g. it didn't answer in time `SND-API-002`, answered an error without a code `SND-API-004`, failed inside `SND-API-005`, or the ledger or the saved approval can't be read `SND-LEDGER-002`/`SND-PROFILE-006`) |
| `sindri stop` | Stops it: records the stop, removes `control.json`, stops the Temporal worker (waiting at most 5 s for it) and then the dev server (the CLI waits 10 s for the answer; a dev server slow to exit can push past that, and the stop still completes), releases the lease, shuts down | 0; 1 when it isn't running (`SND-API-001`); 2 on any other error (e.g. the API refused this caller `SND-AUTH-002`, it didn't answer in time `SND-API-002`, the stop couldn't be recorded, or `control.json` couldn't be removed (the process still stops) `SND-LEDGER-002`, the dev server couldn't be stopped `SND-CLI-002`, answered an error without a code `SND-API-004`, or failed inside `SND-API-005`) |
| `sindri profile validate [--json]` | Checks the live profile file and prints its hash | 0 valid and approved (it is the saved approval and the latest recorded `profile.approved` event names it, the rule every surface uses); 1 valid, not approved; 2 on any error: invalid (`SND-PROFILE-001/002`), the saved approval can't be read (`SND-PROFILE-006`) or the ledger can't be read (`SND-LEDGER-002`), neither of which is ever "not approved" |
| `sindri profile approve` | `[--no-start]`: shows the live hash and the key paths changed since the latest approval; changes nothing | 0 when it already is approved; 1 when not approved yet, or when it isn't running and wasn't started (`SND-API-001`: `--no-start`, an agent session, no terminal); 2 on any other error (e.g. the API refused this caller `SND-AUTH-002`, which a running control plane does for an agent session or a CLI with no terminal because the preview is the protected ApproveProfile command; an invalid profile `SND-PROFILE-001/002`, a ledger it can't read `SND-LEDGER-002`, no answer in time `SND-API-002`, an API error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri profile approve <hash>` | Approves: the hash must be the live one, and you type its first 6 characters at an interactive terminal. Starts the control plane first if it isn't running, unless `--no-start` (`sindri profile approve [--no-start] <hash>`) | 0; 1 when it isn't running and wasn't started (`SND-API-001`); 2 on any other error (e.g. the API refused this caller `SND-AUTH-002`, it couldn't start `SND-CLI-002`, a malformed hash `SND-CLI-003`, a stale hash `SND-PROFILE-003`, no terminal or a wrong confirmation `SND-PROFILE-004`, approvals that can't be written `SND-PROFILE-006`, a ledger it can't read or write `SND-LEDGER-002`, no answer in time `SND-API-002`, an API error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri doctor [--json]` | One line per check, in order: `lease`, `ledger`, `profile`, `api`, `temporal` (the dev server on this home's `temporal.db`: reachable, its CLI version equal to the `binaries.lock` pin, namespace `sindri` present; `fail` with a fix otherwise), each `ok` / `warn` / `fail` with a fix. `profile` is `ok` only when approved by the one rule (see Profile); changed since the saved approval, or saved but not the latest recorded one, is a `warn` | 0 all ok, 1 any warn, 2 any fail |
| `sindri recipe validate <file> [--json]` | Validates a recipe in this process against the build's block catalog, never contacting the control plane: one line per finding (errors first; information and skipped findings labelled), then the recipe's hash. `--json` adds each finding's fields and the normalized long form. Reference: `docs/csharp/sindri-workflow.md` | 0 no errors; 1 any `RCP-*` error; 2 a usage error (no file, `SND-CLI-003`; an unknown `recipe` verb or extra arguments, `SND-CLI-001`), an unreadable file (`SND-RECIPE-010`) or a bug inside sindri (`SND-API-005`) |
| `sindri recipe eval <file> [--json]` · `--scaffold <file>` · `--record <address> --out <file>` | Runs the recipe's eval cases in this process over the pure engine (P3): one line per case, `<case> pass` or `<case> fail at <file>:<line>: <detail>`, or `no evals declared`. `--scaffold` prints a passing happy-path case; `--record` needs the server (P4). Reference: `docs/csharp/sindri-workflow.md` | 0 every case passes or none declared; 1 a case fails; 2 a usage error (`SND-CLI-003`, `SND-CLI-001`, `--record` until P4), an unreadable or unbound recipe (`SND-RECIPE-010`, `SND-RECIPE-001`), a missing or unreadable eval file (`SND-RECIPE-008`) or a bug inside sindri (`SND-API-005`) |
| `sindri workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--temporal <host:port>] [--no-wait]` | P4a (engine spec rev 8.1 "Phase 4a"; P4 moves it behind the API's `CompleteStep`): completes the open desk run `<token>` of the instance with the outcome and output files (each stored 0600 under `<home>/workflow/facts/` by its SHA-256), talking to the Temporal frontend that `control.json`'s `temporal` field records (`--temporal` overrides it). It prints `<address> run <token>: <outcome> -> <next state>`, or `-> sent; the instance moves on when a worker runs` with `--no-wait`, when no worker answers within 10 s, or when the instance is still on the run then (P4a: a report no transition takes reopens the run and also prints this; P4's Update validator refuses it instead) | 0; 1 when no dev server is recorded or it doesn't answer, the server has no `sindri` namespace (the run's task token is kept), or Temporal doesn't confirm the call (`SND-API-001`; the report may have landed); 2 on any other error (e.g. a bad address or option `SND-CLI-003`, a run that isn't open `SND-WF-005`, an output file missing or over 1 MiB `SND-WF-008`) |
| `sindri workflow list [--query <visibility query>] [--temporal <host:port>]` | P4a: the open `RecipeWorkflow` instances, by a visibility query on their search attributes (`Item`, `Recipe`, `State`, `NeedsYou`, …), one a line: `<address>  <state>[  needs-you]` | 0; 1 when no dev server is recorded or it doesn't answer (`SND-API-001`); 2 on any other error (e.g. a query Temporal refuses or a bad option, `SND-CLI-003`) |

Output: state is always a word, never colour; there is no colour at all, so `NO_COLOR` and redirected output need
nothing special. Errors go to stderr as `SND-<AREA>-NNN message`, with `(fix: …)` where there is one, or `(usage: …)` for a
usage error. `status --json` prints the API's response body exactly as received, then a newline (API spec §9, C5); the
local verbs' `--json` (`profile validate`, `doctor`, `recipe validate`) print their response record through AwJson.

**Which verbs are API clients.** `status`, `stop` and `profile approve` call the running process's API (the CLI is a
client of the same handlers the UI will call, §17.27.1). `doctor` and `profile validate` run the same handlers in the
calling process, read-only, because they must work while the control plane is down (`doctor` reports exactly that).
`recipe validate` is local too: it reads only the recipe file and its child recipes beside it.
`status` and `stop` never start the process: "not running" is an answer. `profile approve` needs it running, because
the approval is a ledger event fenced by the lease, so from a human at a terminal it starts it first (`--no-start` opts
out); no verb starts it from an agent session (Callers and auth below).

## Files

`AW_SINDRI_HOME`, default `$AW_STATE_DIR/sindri-app` (`$AW_STATE_DIR` defaults to `~/.agentic-workflow`). It is apart from
the TS sindri's `$AW_STATE_DIR/sindri` until the switch; no TS state is read. Each directory Sindri asks for (the home,
`approved/…`, `logs`) is made 0700 when Sindri creates it; parents created along the way keep the umask, and an existing
directory keeps its mode; files are 0600.

| Path | What |
|---|---|
| `ledger.db` | The ledger (SQLite, WAL, through `Aw.Kernel.Data`): `events`, `lease`, `outbox_commands` |
| `approved/<hash>/profile.json`, `approved/latest` | Approved profile snapshots (canonical JSON) and the latest hash. Runtime reads only these, never the live file |
| `auth/human.token` | This start's human token (0600, in a 0700 `auth/` folder): minted by `serve` after it takes the lease, removed on stop (see Callers and auth) |
| `control.json` | The running control plane's endpoint: `url`, `pid`, `epoch`, and `temporal` (the dev server's `pid`, `port` and `uiPort`). Written once it listens, removed on stop before the lease is released. A client checks the pid and epoch against `/sindri/ping`, so a stale record reads as "not running" |
| `logs/serve.log` | The detached control plane's output (0600: written under umask 077, and an older log is narrowed after each start) |
| `temporal.db` | The Temporal dev server's SQLite file (`--db-filename`); the server is found by it, so one home has one server |
| `logs/temporal.log` | The dev server's output (`--log-level warn`, appended under umask 077). The server runs detached in its own session, never as serve's child, so it outlives a serve that dies and the next start adopts it |
| `recipes/<id>.yaml` | P4a: the recipes the worker's `LoadRecipe` reads by id (P4 reads approved recipes from the registry instead) |
| `workflow/tasks/<address with / as ~>/<run>` | P4a: one file per open desk run, its Temporal task token (0600), written by `RunStep` and removed by `workflow complete` |
| `workflow/facts/<sha256>` | P4a: the output files `workflow complete` reported, stored 0600 by their SHA-256 |
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
context: { ... }          # optional: the context engine's section, checked by the engine (docs/sindri/context.md)
```

The approval hash is SHA-256 over the canonical JSON (the checked record, repos and services sorted, and the
`context:` section's keys sorted at every level), so comments and key order don't change it. A profile without
`context:` leaves it out of the JSON entirely. An approval snapshot whose content no longer matches its hash isn't trusted.

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
  records `process.stopped`, removes `control.json` while it still holds the grant, clears the grant, requests the
  shutdown, and only then releases the lease. So `/sindri/ping` answers epoch 0 (after the clear, until the port
  closes) only once `control.json` is gone, and no client finds `control.json` once the lease is free for another
  process (unless removing it failed: the stop then answers that error, and serve removes it again on its way out). A renewal that finishes afterwards doesn't hold the grant again, and one that lost to the release ends
  quietly (a stop, not a loss: exit 0).
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
approval is recorded: approving A, B, A, B, A records five events), `service.heartbeat` (`service.heartbeat:<epoch>:<slot>`), and from API P1
`command.handled` (`command.handled:<commandId>`; the command chain's audit row, see Command chain). It is flagged
non-public in `EventCatalog`, but `GET /sindri/events` still streams it, like every ledger row, until P5 filters the
stream by that flag (API spec, Phases: P5's signal "`command.handled` never appears"). P4a's Temporal mirror adds
`workflow.started` and `workflow.transition` (both keyed `workflow:<workflow id>:<run id>:<activity id>`, the RecordEvent
activity id; engine spec rev 8.1 §5.6). A service's
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
| `GET /sindri/commands` | `ListCommands`: the command catalog (Command catalog, below) |
| `POST /sindri/stop` | `StopProcess` |
| `POST /sindri/profile/approve` | `ApproveProfile` (`{hash, confirmed}`) |
| `GET /sindri/events?after=N` | `StreamEvents` as server-sent events: one event per ledger row, `id` = `seq`, `event` = type; resumes from `after` or `Last-Event-ID`; polls the ledger once a second; ends when the client goes away or a stop is requested, so a stop doesn't wait out Kestrel's 30 s graceful shutdown |

A service's error is ProblemDetails with the `SND-*` code as `title`. The API's own failures carry a code too: an
unhandled exception in a handler is logged and answered 500 with `SND-API-005`, and a body or query value the request
binding refuses (JSON that doesn't read as the request, a malformed hash or id) is a 400 with `SND-CLI-003` and fixed
text naming the method and path (a framework's message is logged, never answered). The loopback guard's 400 and 403, an
unknown route's 404, a wrong method's 405 and a wrong `Content-Type`'s 415 carry no ProblemDetails body; the CLI
reports an error answer without a code as `SND-API-004`, never as "not running". The approve route's `confirmed` is the CLI's claim that the human typed the
confirmation. Since API P1 every request is classed from its caller headers and every command runs through the command
chain (Callers and auth, Command chain, below): approving is a `protected` action, so only a `human` caller at a
terminal with this start's token gets past the chain. The threat model guards honest mistakes (`docs/csharp/bugfix.md`).
Device-bound approvals for the UI (passkeys, §17.21 D6) come with the UI's action forms.

## Callers and auth

Every request is classed from its headers before any handler runs (API spec §6). The actor on the events it records is
composed by the host from the class and the client label, never taken from a header.

| Header | Values | Effect |
|---|---|---|
| `X-Sindri-Caller` | `human`; `session` or missing; anything else | `human` or `session` (the default). Any other value, or two values, is 400 `SND-API-007` (logged) |
| `X-Sindri-Terminal` | `1` | Honoured only with `human` |
| `X-Sindri-Client` | `cli`, `ui`, `prover`, `mcp` | Names the actor: `human:cli`, `human:ui`, `human:prover`, else `human:http`; `session:cli`, `session:mcp`, else `session:http`. An unknown label counts as none (logged) |
| `X-Sindri-Repo` | a repo name | Scopes the caller (enforced from P3); a malformed name is 400 `SND-API-007` |
| `X-Sindri-Token` | the per-start token | Required with `human`, on queries too: missing or stale is 403 `SND-AUTH-001` ("restart the app or rerun the command"), never a silent downgrade to `session` |

- **The human token.** `sindri serve` mints `auth/human.token` under the home (32 random bytes, base64url; the file
  0600 in a 0700 `auth/` folder) right after it takes the lease, so a refused second `serve` never replaces the running
  one's token. It removes the file on its way out, after the stop (`control.json` goes first, before the lease is
  released), and only while the file still holds this start's token. The CLI and the app read the file and send it.
- **The prover label.** `X-Sindri-Client: prover` from a `human` caller is honoured only when the process's home, with
  every symbolic link resolved, lies inside the temp directory (`TMPDIR`, else the platform's) or inside a proof folder
  `<state root>/<repo slug>/proof/…` (the state root is `AW_STATE_DIR`, else `~/.agentic-workflow`). Elsewhere, such
  as the owner's real home, it is 403 `SND-AUTH-001`. The label is the only way the server sees the prover marker.
- **`Origin` on a command.** Every command route (the `commands` route group, later the issue routes) refuses any
  `Origin` header with 403 `SND-API-008` (logged), before the body is bound, so a malformed body still gets the 403.
  Queries keep S1's loopback guard: a loopback `Origin` is answered, any other is the guard's bare 403.
- **Not audited.** `SND-API-007`, `SND-AUTH-001` and `SND-API-008` are refused before the command chain, so they are
  logged, never written as `command.handled`.
- **`Sindri-Api-Version: 1`** is on every answer past the loopback guard, problems included, and `GET /sindri/status`
  reports `apiVersion: 1`.
- **In-process callers.** `serve` makes every call it runs itself (start, the stop on Ctrl-C, SIGTERM or a failed
  listen) as `service:host`, the only place that caller is set; the scheduler runs each service as `service:<name>`
  (`service:heartbeat`). Events written with no caller keep `host:sindri`. Each command attempt under a service caller
  gets its own command id from the chain (`AuditingService`), replacing the run's; the run's correlation is kept.

### How the CLI classes itself

The CLI calls as `human` only when stdin and stdout are a terminal **and** it sees no agent signal; otherwise it calls
as `session` (API spec §6). An agent signal is either:

- one of the agent-session variables, set to anything non-empty: `CLAUDECODE` (Claude Code), `AW_SINDRI_CHILD`
  (Sindri's own children), `CODEX_SANDBOX` (Codex's sandbox), `CURSOR_AGENT` (Cursor's agent); or
- an agent harness among the CLI's ancestors, nearest first, up to 32 levels: `claude`, `codex` or `cursor-agent`. Each
  ancestor is named by the basename of its `argv[0]`, as `ps` shows it (macOS: `proc_pidinfo` for the parent pid and
  `sysctl KERN_PROCARGS2` for `argv`; Linux: `/proc/<pid>/stat` and `/proc/<pid>/cmdline`), because the kernel's own
  name is the executable's file: Claude Code runs as `~/.local/share/claude/versions/<version>`, invoked as `claude`.
  An ancestry that can't be read (a sandbox refuses process info) is ignored; the variables still apply.

Both lists are provisional until the engine's harness probe pins them in a golden (engine §6.2, P2).
`AW_SINDRI_PROVER=1` (set by `scripts/sindri-prove.sh human`) skips the parent-process check, never the variables, and
labels a human CLI `X-Sindri-Client: prover`.

What the CLI sends on every API call:

| | `X-Sindri-Caller` | `X-Sindri-Terminal` | `X-Sindri-Client` | `X-Sindri-Token` | `X-Sindri-Repo` |
|---|---|---|---|---|---|
| human | `human` | `1` | `cli` (`prover` under the marker) | `<home>/auth/human.token`, read at each call | the repo, when found |
| session | `session` | — | `cli` | — (the token file is never read) | the repo, when found |

The repo (API spec §3.1): the parent of `git rev-parse --path-format=absolute --git-common-dir` from the working
directory (so a worktree resolves to its main checkout), as a real path, matched against the **approved** profile's
repo entries (real paths, `~` expanded) by whole path segments; the longest containing entry wins. Outside git or
outside every entry the header is omitted. A missing token file means no token is sent, which the API refuses with
`SND-AUTH-001`.

A command the API refuses with `SND-AUTH-002` names why the CLI ran as a session: `(this CLI ran as a session:
CLAUDECODE is set | parent process claude | no terminal)`. It exits 2.

**Who starts the control plane** (API spec §7):

| Verb | When it isn't running |
|---|---|
| `sindri`, `sindri --headless` | Starts it, except in an agent session: `SND-API-001 Sindri isn't running; ask the human to run 'sindri' (an agent session never starts it: <signal>)`, exit 1, nothing launched. When it runs, the summary prints as usual |
| `sindri serve` | Refused in an agent session (an agent variable is set, or a parent process is an agent harness without `AW_SINDRI_PROVER=1`): `SND-API-001 an agent session never starts Sindri (<signal>); ask the human to run 'sindri'`, exit 1 (P1 plan G6) |
| `sindri profile approve [--no-start] [<hash>]` | A human at a terminal starts it, then approves; `--no-start` prints `SND-API-001` instead. A malformed hash and a hash without a terminal are refused first, before anything starts. In an agent session or without a terminal it never starts it (P1 plan G8) |
| `sindri status`, `sindri stop` | Never start it: `not running`, exit 1 |
| `sindri doctor`, `sindri profile validate` | Run locally |

A CLI without a terminal and without an agent signal (a script, launchd) still starts it with bare `sindri`, as in S1.

**Checked against real runs (2026-10-11).**

| Harness | Mode | Signal the CLI saw | How it was checked |
|---|---|---|---|
| Claude Code 2.1.296 | its Bash tool | `CLAUDECODE is set` (`CLAUDECODE=1`); with it unset, `parent process claude` | the built `sindri --headless` and `serve` with a temp home, from this session: both refused, nothing launched |
| Codex CLI 0.162.1 | `codex exec -s read-only` | `CODEX_SANDBOX is set` (`CODEX_SANDBOX=seatbelt`; `ps` is refused in the sandbox) | `codex exec` running the built `sindri --headless` with a temp home |
| Codex CLI 0.162.1 | `codex exec -s workspace-write` | `CODEX_SANDBOX is set` (`seatbelt`) | as above |
| Codex CLI 0.162.1 | `codex exec -s danger-full-access` | `parent process codex` (`CODEX_SANDBOX` is unset in this mode, as the spec expected) | as above |
| Cursor's agent (`cursor-agent`) | — | **unverified** (not installed on this Mac): `CURSOR_AGENT` and the `cursor-agent` name are unconfirmed | owner step |

Codex also sets `CODEX_THREAD_ID`, `CODEX_SESSION_ID` and `CODEX_CI=1` in every mode, full access included: a candidate
for the engine probe's list (not read in P1).

## Command chain

Every command (`ICommand<T>`) and issue (`IIssue<T>`) is registered through `AddCommand`/`AddIssue`
(`Features/Commands/SindriCommandServiceCollectionExtensions.cs`), so none can skip the chain. Outermost first:

| # | Decorator | What it does |
|---|---|---|
| 1 | `CommandGate` | One command at a time, process-wide. A command that waits 5 s for the gate fails with `SND-API-009` (503) and writes nothing. Records `sindri.command.gate_wait` (seconds) |
| 2 | `AuditingService` | After the rest of the chain ran, appends one `command.handled {commandId, command, class, caller, outcome, code?}` keyed `command.handled:<commandId>`, under the lease, with the caller's actor and correlation. Outcome: `accepted`; `refused` (`SND-AUTH-*`); `invalid` (an `invalid`-kind error); `failed` (any other error, with its code). A `sindri.command <action>` span and the `sindri.commands` counter carry class, caller and outcome |
| 3 | `AuthorizingService` | Classifies the attempt (`ActionClassifier`) and checks the caller against the authorization table (`Authorization`, below) before validation. A refusal is `SND-AUTH-002 <Command> is a human decision. Ask the human to run it in a terminal or the Sindri app: <cli>` (protected rows: "in a terminal: <cli>") |
| 4 | `ValidatingService` (Kernel) | The slice's `IValidator` (`AcceptAllValidator<T>` when it has no rule) |

Queries (`IQuery<T>`, `AddQuery`) get a `sindri.query <name>` span around validation, and nothing else.

- **Self-audited:** `StopProcess`'s accepted path writes no `command.handled`: it releases the lease, and its
  `process.stopped` is the audit. A refused stop is audited like any other command.
- **No caller context** (nothing set it) is refused and audited as caller `none`. `ServeCommand` calls `StopProcess` as
  `service:host`, the one service the catalog grants a desk command.
- **The audit can't be written:** after an accepted command the result stands and the host logs `SND-LEDGER-002`; on any
  other outcome the caller gets `SND-LEDGER-002` (503), whose message starts with the attempt's own code and reason and
  then says why its row wasn't written. Without a held lease no row can be written, so a refusal while the process holds
  no lease answers `SND-LEDGER-002`.
- **Not audited:** requests the host can't bind (400/415), and the refusals before the chain (`SND-AUTH-001`,
  `SND-API-007`, `SND-API-008`); they are logged.

**Classification (fail-closed, UI spec E-12):** a plain command runs as its one catalog row. A command implementing
`IClassifiedCommand` runs as `<Command>:<Variant>`; an unknown variant, protected fields on a row that isn't protected,
or a command with no row of its own run as `protected` (a human at a terminal only).

**Authorization table (API spec §4.2):**

| Class | human + terminal | human (app) | session | service |
|---|---|---|---|---|
| `request`, `pause` | yes | yes | yes | yes |
| `job` | yes | yes | no | yes |
| `decision` | yes | yes | no | no |
| `desk` | yes | yes | no | only a service the row grants (`service:host` → `StopProcess`) |
| `protected` | yes | no | no | no |
| `issue` | yes | yes | yes | no |
| `step-report` | no (the launched session only, once P10's launch proof exists) | no | no | no |
| `local` | never through the API | | | |

A row with `NeedsTerminal` narrows any class to a human at a terminal.

## Command catalog

`ICommandCatalog` (Core) is implemented by the Host (`Host/Catalog/CommandCatalog.cs`), because routes and CLI forms are
Host facts: `ShellRows` (S1's shipped rows) and `PendingRows` (API P2–P10a's actions, registered before their phase so
the app and doctor can list them; each phase binds its command type and flips its rows to `ready`). The engine and
context lanes add their own rows (the engine's `ApplyCommand:<variant>` rows land with the engine, not P1).

One row per **action id**: a command (`StopProcess`) or a variant (`CloseItem:cancelled`). A row carries its command
type and variant, action class, the services it grants (desk rows), `NeedsTerminal`, `SelfAudited`, `Destructive` (the
app confirms first), `ReadsViews` (disabled while the views rebuild), an optional `Shortcut`, the method and route, the
CLI form and the UI status: `ready`, `pending:<phase>` or `cli-only:<reason>`.

| Action | Class | Callers | UI |
|---|---|---|---|
| `StopProcess` | desk (destructive, self-audited) | human+terminal, human, service:host | `ready` |
| `ApproveProfile` | protected (terminal) | human+terminal | `cli-only:typed-confirmation` |
| `RunDoctor`, `ValidateProfile` | local | none (CLI only) | `cli-only:local` |
| P2–P10a actions | per API spec §3.2 | per the table above | `pending:<phase>` |

`GET /sindri/commands` (a query: any caller) answers `{rows: [{action, class, allowedCallers, terminal, method, route,
cli, ui, destructive, readsViews, shortcut}]}` sorted by action id. `allowedCallers` comes from the authorization table
(`human+terminal`, `human`, `session`, `service`, `service:<name>`); a terminal-bound row lists only `human+terminal`, and
`local` and `step-report` rows list none.

**CLI forms (UI spec E-16):** `CliForm.Parse` accepts `sindri` followed by lower-case words, fixed `--flags`,
`<placeholders>` and `[optional parts]` made of those, and refuses any control, bidi or invisible character (named by
code point, never echoed). `CliForm.Fill(placeholder, id)` substitutes a typed id over text, and only one whose text is
letters, digits, `.`, `_`, `:` and `-`, starting with a letter or digit.

**Checks** (`tests/Aw.Sindri.Tests/Catalog/`, `Cqrs/`): every row's CLI parses; no `shortcut` uses a reserved key
(`G J K O A T B Y C R / ?`); `cli-only` only on terminal-bound or `local` rows; no `pending:<phase>` names a shipped
phase (`CatalogRules.ShippedPhases`, which each phase extends); every non-pending row's route is mapped; every event type
is in `EventCatalog`, and `command.handled` is flagged non-public there (the stream filters on it from P5). The architecture rules (API spec §2.5) run in `Aw.Sindri.Tests`
because they need Host internals: `ARCH-CQRS-001` (every GET serves an `IQuery`, every POST an `ICommand`, or an `IIssue`
on the four carve-out routes with `issue` rows; over the endpoint data source), `ARCH-CQRS-002` (no query handler takes
`ILedgerStore`, `ILease` or `IApprovals`, also through a handler's own Features helpers, followed one level; `GetStatusService` and `StreamEventsService` are allowlisted until P2's
`ILedgerReader`; the read-only approvals port is `IApprovedProfile`, which the context query handlers already take) and `ARCH-CQRS-003` (every command and issue has its row; a classified command has variant rows).

## Proving it

Proving a phase isn't a user feature, so its recipe isn't here: it is in API spec §12 and the phase's plan. The
wrapper's modes are in the header of `scripts/sindri-prove.sh`, and the API golden harness is in
`tests/Aw.Sindri.Tests/ApiGoldens/`.

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
| `SND-LEDGER-002` | The ledger can't be opened or written, or the human token file can't be written |
| `SND-API-001` | The control plane isn't running (no endpoint record, or nothing answers at it) |
| `SND-API-002` | It didn't answer the ping within 2 s, or the call itself within 10 s |
| `SND-API-003` | `sindri serve` can't listen on the API port: another program has it, it is refused, or the host failed to start listening (the stop is recorded and the lease released) |
| `SND-API-004` | The API answered an error without a catalogue code (an unknown route, a wrong method or `Content-Type`; the HTTP status is named) |
| `SND-API-005` | A handler failed with an unexpected exception; the control plane logged it |
| `SND-API-007` | An unknown `X-Sindri-Caller` value, or a malformed `X-Sindri-Repo` (400; logged, not audited) |
| `SND-API-008` | A command carried an `Origin` header (403; logged, not audited) |
| `SND-API-009` | The command gate wasn't acquired within 5 s: another command is running (503) |
| `SND-API-010` | A command that reads views was refused while the views rebuild; it wasn't run, retry (503) |
| `SND-API-011` | The views didn't reach the sequence in time: an `X-Sindri-After` query waited 2 s, or a command waited for the head; at once while the projector is stalled (503) |
| `SND-API-012` | Every event stream the caller may open is open: 16 in all, 4 held for the human (429); re-query every 5 s instead |
| `SND-AUTH-001` | A `human` caller without the valid per-start token: missing or stale (403; restart the app or rerun the command), or a `prover` label outside a temp or proof home |
| `SND-AUTH-002` | The caller may not run the command's action class (403; only the `command.handled` audit row is written) |
| `SND-CLI-001` | Unknown command |
| `SND-CLI-002` | `sindri serve` didn't come up after starting it, or serve couldn't bring up the Temporal dev server (the pinned CLI is missing, it exited, or it didn't answer within 30 s), or `sindri stop` couldn't stop it |
| `SND-CLI-003` | Bad input: a CLI argument or setting that is malformed (a profile hash that isn't 64 hex digits, an `AW_SINDRI_PORT` that isn't 0..65535), or an API request body or value the control plane refused (a 400) |
| `SND-SCHED-001` | A background service run failed |
| `SND-VIEW-001` | The projector stalled on an event (logged with its sequence and type); `sindri views rebuild` clears it |
| `SND-ITEM-404` | No item has that id or alias, or a `session` caller asked for an item outside its repo (404) |
| `SND-ITEM-409` | The item changed since the caller looked (a stale `itemVersion`, or another close won the fact key), or it is already done or cancelled (409) |

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
| `SND-WF-002` | — | Retired (engine spec rev 8): Updates serialize per workflow, so there is no version conflict |
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
0 pass, 1 fail, 2 usage or tool error. The recipe reference, the eval format, the `RCP-*` rule table and how to add
a block are in `docs/csharp/sindri-workflow.md`.

## Hook contract

None in S1. The `Hooks → Sindri` cli edge in `deps.yaml` (sindri-nudge) is the TS sindri's until the switch; the C#
app must provide what that hook reads before it replaces the TS one.

## Checks

```bash
dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=unit"          # domain, slices, CLI text
dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=integration"   # SQLite contracts, YAML files, the API on loopback, the scheduler
scripts/binaries-lock.sh fetch temporal && scripts/binaries-lock.sh fetch temporal-test-server   # the Temporal tests and the smoke run these pins
bash src/Sindri/aot-smoke/run.sh                                                # publish framework-dependent and drive the published apphost (expected.txt)
```
