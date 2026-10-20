# Sindri (C#) — the app shell

Sindri is built new in C# as one application (factory addendum §17.29, decision Q7), not ported from the TS
`sindri/`, which stays the reference for *what* a part does and keeps running until the switch. This page is the C#
contract for stage S1, the app shell: the `sindri` binary, the control-plane process, the host lease, the ledger, the
profile, the command layer and its API, the scheduler and `doctor`. The workflow engine (S2), context retrieval (S3),
blocks and workflows come later and plug into it. The Avalonia UI is its own package, `SindriUi` (stage S1b, `docs/csharp/sindri-ui.md`).

Tier 3 in `deps.yaml` (`src/Sindri`, full shape: Core · Features · Infrastructure · Host). Nothing installs it before
the switch: the providers and launchd don't point at it, and `setup.sh` only through `./setup.sh --with-sindri`, whose Onyx step
(`scripts/install-onyx.sh`; `--no-onyx` opts out) runs `onyx up` via `scripts/sindri-app.sh`.

## The binary

One binary, `sindri`, is both the control plane and its CLI. It publishes framework-dependent, not NativeAOT: the Temporal .NET SDK its worker runs has no trimming support (temporalio/sdk-dotnet#286; `.agents/rules/csharp.md` "AOT scope").

| Command | What it does | Exit |
|---|---|---|
| `sindri` | Prints the banner, starts the control plane if it isn't running (detached in its own session, so a Ctrl-C or a kill of the terminal's process group doesn't reach it; `sindri serve`, output in `<home>/logs/serve.log`), opens the Sindri app (`sindri-ui` next to the binary, or `AW_SINDRI_UI`; detached), then prints the startup summary, whose `ui` line says what opening the app did (`opened (sindri-ui, pid N)`, or `not found at <path>` / `not executable at <path>` with the fix); the app never changes the exit code. It starts one only when nothing runs (`SND-API-001`), and never from an agent session (Callers and auth): there it prints `SND-API-001 Sindri isn't running; ask the human to run 'sindri'` and launches nothing; one that answers late (`SND-API-002`) is reported as it is. A started child that exits with `SND-LEASE-001` lost the lease to a concurrent start, so it waits up to 10 s for the winner to answer; past that the error ends with the line the child exited with. Only lines written to the log since this start count, not an earlier run's. On a box where Onyx isn't set up, a human at a terminal (not an agent session, not headless) is first asked "Set up Onyx (Sindri's business-context search) now? [Y/n]" when the approved profile names no Onyx: yes runs `sindri onyx up` in that terminal (and Sindri starts afterwards whichever way it ends), no is remembered in `$AW_SINDRI_HOME/onyx/offer-declined` so it isn't asked again; a no (or a non-terminal or session run) leaves a "Set up Onyx" row on the needs-you list until `onyx up` has finished or the profile names an Onyx | 0; 1 when the profile needs approval, or the control plane went away before its status answered (`SND-API-001`); 2 on any other error (e.g. it can't start `SND-CLI-002`, the running one answers late `SND-API-002`, or the status call answers an error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri --headless` | The same without opening the app and without the `ui` line | as above |
| `sindri serve` | Runs the control plane in the foreground: what bare `sindri` starts, and what launchd will keep alive later. Refused in an agent session; `AW_SINDRI_PROVER=1` skips only the parent-process check, never the variables. After it takes the lease it brings up the Temporal dev server (engine spec rev 8 §0 "Operations"; P4a, the Temporal foundation): it adopts the server already running on `<home>/temporal.db` when that answers, kills and replaces one that doesn't, else starts the pinned `temporal server start-dev` on loopback, waits for the namespace and registers the search attributes. The namespace and the worker's task queue are settings (README X61): `AW_SINDRI_TEMPORAL_NAMESPACE` and `AW_SINDRI_TEMPORAL_TASK_QUEUE`, each `sindri` when unset; the worker, the clients, every instance start and its activities use them (a Step's activity runs on its instance's queue; `StepTaskQueue` is the seam for a per-Step queue). A failure gives the lease back and exits 2 with `SND-CLI-002`. When the approved profile says `context.sources.onyx.managed: true` and `onyx up` finished here, `serve` brings the managed Onyx up in the background once the control plane is listening (Docker answering, the files that are already there, `compose up -d`, up to 5 minutes for Onyx to answer; it never fetches, pulls, writes config or bootstraps, never blocks the API and never fails serve) and records `{project, url, state}` as `onyx` in `control.json` beside `temporal`; `state` is `running`, `needs-setup` or `failed`, and its last outcome is kept in `$AW_SINDRI_HOME/onyx/ensure.txt` for the needs-you list. An unmanaged Onyx is never touched | 0 on a clean stop; 1 when it is refused in an agent session (`SND-API-001`), or when another process holds the lease (`SND-LEASE-001`) or the lease was lost (`SND-LEASE-002`); 2 when the port is taken or refused, or the host fails to start listening (`SND-API-003`; the stop is recorded and the lease released), or `AW_SINDRI_PORT` isn't a port number 0..65535 (`SND-CLI-003`, checked before the lease is taken), or the ledger can't be opened, or the human token file can't be written (`SND-LEDGER-001/002`), or the Temporal dev server didn't come up (`SND-CLI-002`; the lease is given back) |
| `sindri status [--json]` | The running control plane's status, through the API | 0; 1 when it isn't running (`SND-API-001`), the profile needs approval (`profile.needsApproval` in the response) or `health.word` isn't `healthy`; the text adds `views: cursor N of M` (or `views: rebuilding (cursor N of M)`, `views: stalled at N (<type>): run sindri views rebuild`, or `views: can't be updated (<code>: <detail>): run sindri views rebuild` when the projector can't run, `sindri doctor` for a ledger cause) and `health: <word>` with one line per problem; 2 on any other error (e.g. it didn't answer in time `SND-API-002`, answered an error without a code `SND-API-004`, failed inside `SND-API-005`, or the ledger or the saved approval can't be read `SND-LEDGER-002`/`SND-PROFILE-006`) |
| `sindri status --follow [--json]` | API P5: prints the status, then again each time it changes. It opens `GET /events?after=<head>`, re-reads `GET /status` (with `X-Sindri-After`) on each event (events within 250 ms count once) and prints only when the summary differs; a `: ping` or an event that changes nothing prints nothing. `--json` prints one status body per line. A 429 (`SND-API-012`) or an `error` frame (also one inside the 250 ms window) falls back to re-reading every 5 s; when the views can't catch up to the event (`SND-API-011`: stalled or rebuilding) it re-reads without `X-Sindri-After`, so the summary that names the stall prints. A read verb: it never starts the process | 0 on Ctrl-C or SIGTERM; 1 when it isn't running at the start or stops while followed (`not running`); 2 when the first read fails for another reason |
| `sindri ledger [--after N] [--item X] [--type T] [--max N] [--json] [--no-start]` | API P5: pages the ledger through `GET /ledger`. One line per event, `<seq>  <time>  <type>  <actor>  [<item>]`, then `next: N` after a full page or while the read stopped short of the head (a `next` that leads to an empty page with no `next` is the end); `--json` is the raw body. A human sees the types outside the public ones too (such as `command.handled`); inside an agent session it runs as a session, public types of its repo only, and never starts the process | 0; 1 not running; 2 on an error (`--after` past the head or `--max` over 500 `SND-CLI-003`, no such item `SND-ITEM-404`) |
| `sindri stop` | Stops it: records the stop, removes `control.json`, stops the Temporal worker (waiting at most 5 s for it) and then the dev server (the CLI waits 10 s for the answer; a dev server slow to exit can push past that, and the stop still completes), releases the lease, shuts down. Before the record and the lease go, the serve process runs `docker compose stop` for a managed Onyx (containers only; volumes and files kept), bounded at about a minute (skipped when the lease was lost); a failure or a timeout is told on standard error, not fatal | 0; 1 when it isn't running (`SND-API-001`); 2 on any other error (e.g. the API refused this caller `SND-AUTH-002`, it didn't answer in time `SND-API-002`, the stop couldn't be recorded, or `control.json` couldn't be removed (the process still stops) `SND-LEDGER-002`, the dev server couldn't be stopped `SND-CLI-002`, answered an error without a code `SND-API-004`, or failed inside `SND-API-005`) |
| `sindri views rebuild [--no-start]` | Asks the running control plane to replay the ledger into `views.db` (the fix when the views stall; API spec `RebuildViews`): starts the control plane first unless `--no-start`, prints `views rebuild requested (sequence N)` and does not wait for the replay (watch `sindri status`) | 0 accepted; 1 when it isn't running and wasn't started (`SND-API-001`); 2 on any other error (e.g. the API refused this caller `SND-AUTH-002`, a session; no lease `SND-LEASE-002`; the request couldn't be recorded `SND-LEDGER-002`; no answer in time `SND-API-002`, an API error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri queue [--json] [--no-start]` | API P3: the queue as groups (`waitingOnYou`, `withYou`, `running`, `queued`), one line per item (`ITM-…  <title>  [<repo>]  <state>  since <UTC time>`) or the group's empty sentence; `--json` is the response body as received. A client of `GET /queue`; starts the control plane first unless `--no-start` (never from an agent session) | 0; 1 while the views rebuild or stall (`asOf.rebuilding` or `asOf.stalled`: the rows may be partial) or when it isn't running (`SND-API-001`); 2 on any other error |
| `sindri items [--state <s>] [--repo <r>] [--json] [--no-start]` | API P3: the item list (`GET /items`), the oldest `since` first, the same line per item; open items only unless `--state` names a state (so `done` and `cancelled` items need `--state done|cancelled`); with none, the `empty` sentence for the filters (`No open items.`, `No done items in demo.`) | 0; 1 while the views rebuild or stall (`asOf.rebuilding` or `asOf.stalled`: the rows may be partial) or when it isn't running; 2 on any other error (an unknown state is `SND-CLI-003`) |
| `sindri item add <title> [--repo <r>] [--no-start]` | API P3: `POST /items` as the human (`desk`: refused to a session `SND-AUTH-002`); prints `ITM-… created (sequence N)`. The repo is `--repo`, else the one the working directory sits in (`X-Sindri-Repo`); one the approved profile doesn't name is `SND-CLI-003` (for the working directory's repo the message says `this checkout, <name>, isn't a repo of the approved profile (fix: pass --repo <name>, or add it to the profile and approve it)`) | 0; 1 not running; 2 on any other error |
| `sindri item close <item> [--cancelled] [--version <n>] [--no-start]` | API P3: closes an item (an `ITM-…` id or a tracker key such as `ENG-142`) as `done`, or `cancelled` with `--cancelled`, through `POST /items/{item}/close`; prints `<item> closed: done (sequence N)`. Without `--version` it first reads `GET /items/{item}` and sends that version. Every view read (that pre-read, `items`, `queue`) first reads the ledger head from `GET /status` and sends it as `X-Sindri-After`, so a read right after a write never lags the projector (a view still behind after 2 s prints the server's `SND-API-011` and exits 2; while the status says the views stall or rebuild no header is sent, so the answer arrives with its `asOf` and sentence and `queue` and `items` exit 1) (the API itself always requires `itemVersion`, so a stale one is `SND-ITEM-409`) | 0; 1 not running; 2 on any other error (no such item `SND-ITEM-404`, already closed or stale `SND-ITEM-409`) |
| `sindri request <kind> [--item <item>] <text> [--json] [--no-start]` | API P4: `POST /requests` (`FileRequest`, class `request`: a session's verb, a human may file one too). `<kind>` is `rescope`, `add-task`, `note` or `handback`; `<item>` an `ITM-…` id or tracker key; the text is 1 to 2,000 characters (the words after the kind are joined). Prints `REQ-… filed; DEC-… waits for a human (sequence N)`; `--json` is the response body as received. A request about no item is scoped by the working directory's repo (`X-Sindri-Repo`) | 0; 1 not running; 2 on any other error (a bad kind, item or text `SND-CLI-003`/`SND-REQ-001`, no such item `SND-ITEM-404`, the item already closed `SND-ITEM-409`, too many open requests `SND-REQ-002`) |
| `sindri requests [--state <open\|accepted\|declined\|voided>] [--item <item>] [--json] [--no-start]` | API P4: `GET /requests`, newest first; per request a line `REQ-…  <kind>  <state>  [<repo>]  <item>  by <actor>  since <UTC time>  decision DEC-…` and its text indented; the list's sentence first when there is none or the views are stale; `--json` is the body as received. A session sees only its repo's requests and the repo-less ones | 0; 1 while the views rebuild or stall or when it isn't running; 2 on any other error |
| `sindri decisions [--pending \| --decided] [--item <item>] [--json] [--no-start]` | API P4: `GET /decisions` (`ListDecisions`); `--pending` is `?pending=true`, `--decided` `?pending=false` (giving both is `SND-CLI-003`), `--item` an `ITM-…` id or tracker key. Per decision a line `DEC-…  <label>  <state>  [<repo>]  <item>  by <filer>  since <UTC time>`, its question indented, for a decided one `decided <choice> by <who> at <time>[: note]`, the notice, and while it waits the command line that decides it; the list's sentence first when there is none or the views are stale; `--json` is the body as received. A session sees only its repo's decisions and the repo-less ones | 0; 1 while the views rebuild or stall or when it isn't running; 2 on any other error |
| `sindri decide <decisionId> <choice> [--note <note>] [--json] [--no-start]` | API P4: `POST /decisions/{decisionId}` (`Decide`, class `decision`: the human's verb). `<choice>` is one of the decision's options (`accept` or `decline` for an accept-request decision); prints `DEC-… decided: <choice> (sequence N)`. A session is refused `SND-AUTH-002` with this command line; a second decision of the same one is `SND-ITEM-409` ("decided elsewhere"), and one voided by closing its item is `SND-ITEM-409` ("the item was closed") | 0; 1 not running; 2 on any other error (an unknown decision, or one outside a session's repo, `SND-DEC-404`; a choice that isn't an option `SND-DEC-001`) |
| `sindri needs-you [--json] [--no-start]` | API P4: `GET /needs-you`, what needs a human: per row its label, title, `[repo]` and `since`, then the reason, the recorded-only notice when it has one, and the command line that does it; with none, `Nothing needs you. N running, M queued.` (or the views' sentence); `--json` is the body as received | 0; 1 while the views rebuild or stall or when it isn't running; 2 on any other error |
| `sindri agents [--provider P] [--state S] [--repo R] [--json]`, `sindri agents show <provider>/<id> [--json]` | Collector CO-3: `GET /sessions`, `GET /sessions/{provider}/{id}`. A head (`N live · M asking · K outside a workflow`), one line per session (`<ref>  RUNNING\|IDLE\|ASKING · approval · Bash  <repo>  <host label>`), the recently ended ones with their resume argv, then a line per problem. Never starts the control plane | 0 (an empty list too); 1 when Sindri isn't running (SND-API-001) or a `problems` row is present; 2 on usage or another error (SND-SESSION-404, SND-SESSION-002) |
| `sindri services [--json] [--no-start]` | API P6: `GET /services`, one line per service: `name  every <cadence>  [paused  ]last: <ok\|failed CODE\|never>  next: <slot\|paused\|->[  running JOB-…]` | 0; 1 while the views rebuild or stall; 2 on an error |
| `sindri services run <name> [--detach] [--json] [--no-start]` | API P6: starts the service as a job and follows it (`JobFollower`, see Jobs): `job JOB-… started`, each progress line, then `succeeded: <summary>`, `failed: <summary> <code>` or `cancelled`. Ctrl-C cancels the job (a second one leaves at once). A session is refused `SND-AUTH-002` | 0 succeeded; 1 failed, cancelled, left, or Sindri stopped; 2 refused or invalid |
| `sindri services pause <name>` · `sindri services resume <name>` | API P6: `service.paused` / `service.resumed` (`desk`: the human only); prints `<name> paused (sequence N)` | 0; 2 on an error |
| `sindri jobs [--state <s>] [--kind <k>] [--json] [--no-start]` | API P6 (provisional owner default): `GET /jobs`, newest start first (a job that ended just now stays where it started; a tie goes by id), one line per job (`JOB-…  <state>  <kind>  <service or key>  <started>  N lines  [summary] [code]`) | 0; 1 while the views rebuild or stall |
| `sindri jobs show <jobId> [--json] [--no-start]` · `sindri jobs cancel <jobId> [--no-start]` | API P6: one job with its lines, how it ended and `cancel: <cli>` while it runs; `cancel` prints `JOB-… cancel requested (sequence N)` (`SND-JOB-409` when it already ended) | 0; 2 on an error (`SND-JOB-404`, `SND-JOB-409`) |
| `sindri profile validate [--json]` | Checks the live profile file and prints its hash | 0 valid and approved (it is the saved approval and the latest recorded `profile.approved` event names it, the rule every surface uses); 1 valid, not approved; 2 on any error: invalid (`SND-PROFILE-001/002`), the saved approval can't be read (`SND-PROFILE-006`) or the ledger can't be read (`SND-LEDGER-002`), neither of which is ever "not approved" |
| `sindri profile approve` | `[--no-start]`: shows the live hash and the key paths changed since the latest approval; changes nothing | 0 when it already is approved; 1 when not approved yet, or when it isn't running and wasn't started (`SND-API-001`: `--no-start`, an agent session, no terminal); 2 on any other error (e.g. the API refused this caller `SND-AUTH-002`, which a running control plane does for an agent session or a CLI with no terminal because the preview is the protected ApproveProfile command; an invalid profile `SND-PROFILE-001/002`, a ledger it can't read `SND-LEDGER-002`, no answer in time `SND-API-002`, an API error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri profile approve <hash>` | Approves: the hash must be the live one; the CLI asks the server for an `approve profile <hash6>` challenge and you type that text back at an interactive terminal (engine spec §4.2, S1-D5). Starts the control plane first if it isn't running, unless `--no-start` (`sindri profile approve [--no-start] <hash>`) | 0; 1 when it isn't running and wasn't started (`SND-API-001`); 2 on any other error (e.g. the API refused this caller `SND-AUTH-002`, it couldn't start `SND-CLI-002`, a malformed hash `SND-CLI-003`, a stale hash `SND-PROFILE-003`, no terminal or a wrong confirmation `SND-PROFILE-004`, approvals that can't be written `SND-PROFILE-006`, a ledger it can't read or write `SND-LEDGER-002`, no answer in time `SND-API-002`, an API error without a code `SND-API-004` or from inside `SND-API-005`) |
| `sindri doctor [--json]` | One line per check, in order: `lease`, `ledger`, `views` (`ok` cursor N of head M; `warn` no views yet or rebuilding; `fail` stalled at N, fix `sindri views rebuild`), `profile`, `api`, `temporal` (the dev server on this home's `temporal.db`: reachable, its CLI version equal to the `binaries.lock` pin, namespace `sindri` present; `fail` with a fix otherwise), then `context:map` and `context:retention` (S-3a), then `context:onyx` (S-5A, docs/sindri/onyx.md), then `context:sources` and `context:mcp-scope` (docs/sindri/context.md, S-1M), then `onyx:stack` (Onyx P2, only once `sindri onyx up` has recorded its state: `warn` when Docker isn't answering (`SND-ONYX-001`), when it answers but the stack can't be read (`SND-ONYX-011` and its message), when `state.json` is unreadable ("state file unreadable; `sindri onyx up` rebuilds it") or the stack is stopped, with the fix; `fail` for a container that isn't running or reports unhealthy, a published port not bound to `127.0.0.1`, or an image the lock doesn't pin for this platform; `ok` otherwise), each `ok` / `warn` / `fail` with a fix, then one line per pinned harness (see "The harness probe"). `profile` is `ok` only when approved by the one rule (see Profile); changed since the saved approval, or saved but not the latest recorded one, is a `warn` | 0 all ok, 1 any warn, 2 any fail |
| `sindri onyx status [--json]` | Where Sindri's Onyx stands and whether this box can run the pinned one (`context/onyx.lock`, spec `docs/superpowers/specs/2026-10-16-sindri-onyx-managed-design.md`). Rows `onyx` (`not set up (run `sindri onyx up`)`; `running`, then rows `url` and `running` (the version the running Onyx reports, or `unreachable`); `stopped (run `sindri onyx up` to start it)`; or, set up but with a Docker this box can't use (another machine, no daemon, no CLI), `unknown (Docker isn't usable here: <the docker row's finding>)`, with no hint and no `url` or `running` rows: it doesn't guess stopped; JSON state `unknown`), `pinned` (version and the first 7 of the commit), then the preflight checks `docker`, `compose`, `memory`, `disk`, `port` (while running, `port` is `ok` and says "(Onyx's own)": the port is ours, not taken), each `ok` / `warn` / `fail` with what it found and, on a failure, `(SND-ONYX-0nn: the fix)`. Set up means `onyx up` finished its start step and the fetched compose file is still there; running means `docker compose ps` shows `nginx` and `api_server` running. Read-only (it runs `docker version`, `docker compose version --short`, `docker info --format '{{.MemTotal}}|{{.OperatingSystem}}|{{.DockerRootDir}}'` (the data is taken to live in a VM from `OperatingSystem`, Docker Desktop or OrbStack, not from whether the data-root directory exists here; `docker version` is retried once with 20 s when its first 5 s try times out, Docker Desktop waking) and `docker context inspect` (unless `DOCKER_HOST` is set), `docker ps -q` by the project label and `docker stats --no-stream` (the memory the containers outside Onyx's project already use; the `memory` row fails with `SND-ONYX-003` when Docker's `MemTotal` minus that use is under the pinned floor, naming how much the others use, and only warns when the read fails), 5 s each, and, once set up, `docker compose ps`; the `disk` row measures free space at `$AW_SINDRI_HOME` and, when Docker's data root is a directory on this host (a Linux engine), there too, and reports the smaller with the path it measured; when the data root isn't on this host (Docker Desktop keeps it in its VM) it keeps the host measurement and adds "Docker's data lives in a VM here; its disk image size is capped separately (Docker Desktop: Settings → Resources)"; and connects to and briefly binds loopback ports, 3000 first and 3001-3010 only when it is taken); runs in the calling process, so it works with the control plane down and for an agent session. `--json` prints `OnyxStatusResponse` | 0 every check ok, 1 a check needs attention or a step error, 2 usage |
| `sindri onyx up [--json]` | Takes this box from nothing to the pinned Onyx running on `127.0.0.1` (spec §4.1 steps 1-5), idempotent and resumable: (1) preflight (a failed row stops everything before anything is fetched; a `Docker` that points at another machine is `SND-ONYX-010`; Compose older than 2.24.0, the release that reads the override's `!override` tag, is `SND-ONYX-002` with an update fix, and an unreadable Compose version only warns), (2) `fetch`: each file of `context/onyx.lock` from `https://raw.githubusercontent.com/onyx-dot-app/onyx/<commit>/deployment/<path>`, https only, no redirect followed, 30 s and 1 MiB per file, sha256 checked before anything is written (a mismatch is `SND-ONYX-004` and leaves existing good files alone; every run asks for the files again, which hashes the ones already there and downloads only a missing or tampered one, so a deleted or edited pinned file is restored, and only a change of the pins reruns the later steps), (3) `config`: `.env` (the shipped template with `IMAGE_TAG`, `DISABLE_TELEMETRY=true`, `AUTH_TYPE=basic`, `USER_AUTH_SECRET`, `INDEXING_MODEL_SERVER_HOST` and `HOST_PORT` set; an existing auth key is always kept, a missing one generated once, the file `0600`) and `docker-compose.override.yml` (nginx on `127.0.0.1:<port>:80` only, every image by its pinned platform digest, one-user sizing, the Slack and Discord bots off), both rendered again on every run and written only when they differ (a hand-edited override or `.env` is put back, the generated key kept; a `.env` wider than `0600` is narrowed), beside the fetched compose file under `$AW_SINDRI_HOME/onyx/deploy/deployment/docker_compose/`, (4) `pull` (`docker compose pull`, then the `RepoDigests` of each image of an active service (no profile, or one `COMPOSE_PROFILES` enables, so minio too) must hold the pinned digest), (5) `start` (first `docker compose config --format json` must show nginx's port 80 on `127.0.0.1:<port>` as the only published port of any service, else `SND-ONYX-004` and nothing is started; then `docker compose up -d --no-build`, each call limited to what is left of the 15-minute window and retried while a dependency is still starting or an `up` ran out of its own 5 minutes, then `/api/health` and `/api/version` every 2 s for up to 15 minutes; another version or a timeout is `SND-ONYX-005`). Each completed step is recorded in `$AW_SINDRI_HOME/onyx/state.json` with the stamp of its inputs, so a rerun repeats nothing that is already right; one `up` runs at a time (`SND-API-009` for a second): each stage takes the workspace lock for itself: the stack stage (steps 1-5) and then the account stage (steps 6-9, held through the pauses for you at the terminal), so `onyx down` is refused while either runs (it can run in the gap between them, which only stops a stack the account stage then reports as down). The port is the preflight's (3000, or the first free of 3001-3010), and stays while our own nginx publishes it (decided from `docker compose ps`, whatever `state.json` says). The compose project is `sindri-onyx-<8 hex>`, one per Sindri home (see External executables), recorded in `state.json`. Progress goes to standard error (a line per step, a dot per poll or pull line); standard output carries `url`, `version`, the steps `ran` and `skipped`, and, once the account stage is done, a `key` line (the pointer, never the key), a note when the profile section is written but not approved yet, and the connector checklist (or `needs you: <step>` when a step waits for the human). The human's verb: a session or a CLI with no terminal is refused (`SND-AUTH-002`, exit 1, nothing called). Ctrl-C and SIGTERM cancel it (the running docker command is killed, temporary files are removed, the lock is released) and it prints `interrupted; run `sindri onyx up` again to resume` and exits 130. After the stack is up it continues with your account and Sindri's key (spec §4.1 steps 6-9, P3): (6) it opens Onyx's sign-up page and waits ("Create your Onyx account in the browser, then press Enter"; Sindri registers nobody, the first account is Onyx's admin); (7) it asks once, at the terminal, for **the owner's own Onyx admin login** (prompt: "Your own Onyx admin login (the account you signed up with; Sindri creates its own user from it)", then that account's password, not shown, never stored and not in any log, file or argument; the dedicated address or the retired one typed there is refused with SND-ONYX-008 before anything is sent), logs in, and with that owner session sets up Sindri's own Onyx user, `sindri@example.com` (`OnyxAccounts.DedicatedEmail`; the same address as Context's `OnyxSettings.DedicatedAccountEmail`, held together by a test; an example.com address because RFC 2606 reserves it and its null MX means no mail is delivered, and Onyx's pinned email-validator 2.2.0 accepts it): `PATCH /api/manage/admin/deactivate-user` for the retired `sindri@sindri.app` of an earlier build (404 means none; found, it is deactivated and a progress line says so), then `POST /api/password/reset_password` for the dedicated user, which makes a new random password that only this call holds and needs no invite, so a rerun works on an invite-only Onyx; only when Onyx answers 404 "User not found" is it registered with `POST /api/auth/register` and a random password (`IRandomHex`, 32 hex characters and `aA1!` so any password rule passes); then `PATCH /api/manage/admin/users/admin-access` makes it an admin. It logs out, logs in as that user, lists its tokens (`GET /api/user/pats`) and revokes every old one named `sindri-onyx` (`DELETE /api/user/pats/{id}`), makes the `sindri-onyx` personal access token (`POST /api/user/pats`, no expiry, no scopes, so it carries the user's full permissions: the one kind that reaches admin search), checks it with `GET /api/me` (the token belongs to that email and holds `admin`) and logs out; after a successful store exactly one Sindri token exists. Onyx Community Edition has no service-account keys (`/admin/api-key` and the user-group list are Business tier and answer 402), hence a user and a token; a login that isn't an admin is `SND-ONYX-007`, a refused registration (invite-only or a domain list: the message says to invite `sindri@example.com` once or allow example.com), a failed login or check `SND-ONYX-008`; (8) it stores the key as `keychain:onyx/sindri-onyx` (macOS `security -i` or Linux `secret-tool store`, the secret on standard input, never in an argument) or, when no store takes it, in an owner-only file under `$AW_SINDRI_HOME/secrets/` with a `file:` pointer (`SND-ONYX-009` when neither works); (9) it writes `context.sources.onyx` (`url`, `token`, `version`, `managed: true`) into the live profile keeping every other key and comment, then shows the profile approval inline (the preview, then the typed challenge, as `sindri profile approve` does, so like it this starts the control plane when it is not running: `SND-CLI-002` when it will not start, `SND-API-001` when it is down; with `--json` the approval text goes to standard error so standard output stays one document; declining leaves the section written and says how to approve later), opens the connectors page and prints the connector checklist. Each step is recorded in `state.json` and skipped on a rerun; a recorded key is used again only when it still resolves and Onyx still takes it as an admin. Without a terminal nothing is asked and the stage ends `needs you: signup` or `key`. These requests are a separate allowlist of ten in `HttpOnyxProvisioning`, refused before sending when off the list; the query adapter still sends exactly its five. **Community Edition, the dedicated user and the ten requests.** Every request goes through `HttpOnyxProvisioning`'s own allowlist, nine exact method and path pairs and one delete pattern, refused before sending when off the list: `POST /api/auth/login`, `POST /api/auth/logout`, `POST /api/auth/register`, `POST /api/password/reset_password`, `PATCH /api/manage/admin/users/admin-access`, `PATCH /api/manage/admin/deactivate-user`, `GET /api/user/pats`, `POST /api/user/pats`, `GET /api/me`, and `DELETE /api/user/pats/{id}` (the id one to nine ASCII numerals and nothing after it; `/api/user/pats/7/x`, `/api/user/pats/abc`, `/api/user/pats/` and a ten-numeral id are refused), each with its source and why in `docs/sindri/onyx.md` section 7. None is under a Business-tier prefix of `PATH_PREFIX_MIN_TIER`. The calls that change a user take no email (`RetireLegacyUser`, `ResetDedicatedPassword`, `RegisterDedicatedUser`, `MakeDedicatedAdmin`): the adapter names the dedicated address, or for the retire the old one, so no caller can aim them at the owner's account or anyone else's (a test pins the interface and the bodies sent). The query adapter still sends exactly its five. **Resume.** The sign-up step is recorded in `state.json` before the key step, and the key step is repeatable at any point: the reset comes first, so an existing user is found whatever the invite or domain settings are now (register, which an invite-only Onyx refuses on every try after the first, is only reached when the user doesn't exist), the admin step is idempotent, and a token is made only after the dedicated login worked and the old ones were revoked, so a rerun after any failure resumes at the key step with no second user, no second sign-up wait and no duplicate stack (the stack step is skipped under the same compose project). A token made and then lost (a timeout after Onyx made it, a failed check or store) is a live admin token that nobody can read back; every such error says so, and says the next run revokes it or that deactivating `sindri@example.com` in Onyx's Admin → Users stops it now. The dedicated user itself stays in Onyx after `onyx down` or when Sindri is no longer used; deactivate it there to retire it. Doctor's `SND-CONTEXT-053` passes for a `STANDARD` admin token whose email is `sindri@example.com` (compared case-insensitively) and for a `SERVICE_ACCOUNT` key, and warns for any other account without echoing its address. `--json` prints `{ up: OnyxUpResponse, account: { url, keyPointer, profileChanged, steps } | null }` (`OnyxUpResponse`: `{ port, url, version, warnings: [string], steps: [{ step, outcome }] }`, one entry per step in the order fetch, config, pull, start, `outcome` being `ran`, `skipped` or `needs-human`) | 0 running (the profile section written and approved), 1 a step failed or was refused (the `SND-ONYX-0nn` error names the fix), or the profile section is written but not approved yet, 2 usage or a tool error, including a profile that can't be edited or read (`SND-PROFILE-001`, `SND-PROFILE-002`) |
| `sindri onyx open` | Opens Onyx in the browser: the sign-up page until `onyx up` has recorded your account, then the connectors page (`/admin/add-connector`); prints the address when no browser opens (it asks the OS opener, `open` on macOS or `xdg-open` elsewhere, for the loopback address only). The human's verb: a session is refused (`SND-AUTH-002`, exit 1). Runs in the calling process; nothing is sent to Onyx | 0, 1 not set up or refused |
| `sindri onyx down [--json]` | Stops Sindri's Onyx with `docker compose stop` (containers only: no volume, image or file is removed; `sindri onyx up` starts it again). It stops whatever containers `docker compose ps` shows running for the project (also after a start that failed, or with an unreadable `state.json`) and says which; it does nothing when none runs; when any of the compose file, the override or the `.env` is gone it finds and stops them by the compose project label instead (`docker ps -q --filter label=com.docker.compose.project=<name>`, then `docker stop <ids>`: compose is never run without its files, so nothing depends on the caller's directory), says so, asks again and reports only what actually stopped, with a warning for any container still running; refused for a Docker that points at another machine (`SND-ONYX-010`). The stop is bounded at about a minute (skipped when the lease was lost); running out of it is `SND-ONYX-011` ("the stop was cut short", rerun `down`), not "Docker isn't answering". Ctrl-C ends it with exit 130. The human's verb like `onyx up`. `--json` prints `OnyxDownResponse` | 0, 1 refused or a docker error, 2 usage |
| `sindri recipe validate <file> [--json]` | Validates a recipe in this process against the build's block catalog, never contacting the control plane: one line per finding (errors first; information and skipped findings labelled), then the recipe's hash. `--json` adds each finding's fields and the normalized long form. Reference: `docs/csharp/sindri-workflow.md` | 0 no errors; 1 any `RCP-*` error; 2 a usage error (no file, `SND-CLI-003`; an unknown `recipe` verb or extra arguments, `SND-CLI-001`), an unreadable file (`SND-RECIPE-010`) or a bug inside sindri (`SND-API-005`) |
| `sindri recipe eval <file> [--json]` · `--scaffold <file> [--out <dir>]` · `--record <address> --out <file>` | Runs the recipe's eval cases in this process over the pure engine (P3): one line per case, `<case> pass` or `<case> fail at <file>:<line>: <detail>`, or `no evals declared`. `--scaffold` prints a passing happy-path case (`--scaffold <file> --out <dir>` writes one `scaffold-<branch>.jsonl` per path of a recipe with a route state, and refuses to overwrite); `--record <address> --out <file>` writes a served instance's history as an eval case, read from the ledger and the stored facts, with no server (P7). Reference: `docs/csharp/sindri-workflow.md` | 0 every case passes or none declared; 1 a case fails; 2 a usage error (`SND-CLI-003`, `SND-CLI-001`, `--record` of an address the ledger never saw: `SND-WF-001`; an `--out` file that exists: `SND-CLI-003`), an unreadable or unbound recipe (`SND-RECIPE-010`, `SND-RECIPE-001`), a missing or unreadable eval file (`SND-RECIPE-008`) or a bug inside sindri (`SND-API-005`) |
| `sindri recipe register <file>` | Registers the recipe by hash on the server (`desk`; starts the control plane if it may): `registered <id>@<hash12> (unapproved)`, or `already registered …`. Reference: `docs/csharp/sindri-workflow.md` | 0; 1 when it isn't running and wasn't started (`SND-API-001`); 2 on any other error (e.g. other content under a registered id and version `SND-RECIPE-007`, a failing rule `SND-RECIPE-001`, a session `SND-AUTH-002`, an unreadable file `SND-RECIPE-010`) |
| `sindri recipe show <id\|id@hash> [--json]` · `sindri recipe list [--stale] [--json]` | Queries. `show` prints the recipe's keys: `<id>@<hash12>`, `urgency:`, `name:` when it has one, an `input:` line per input, an `output:` line per output, the path order (`order:`, a `state:` line per state) and a `loop:` line per loop; a bare id is the approved version, `--json` is the served `RecipeView`. The standing, the approval and the approval preview aren't in `show`: `list` has the standing (one line per registered recipe with its standing, via and staleness; `--stale`: the approved ones that are stale; `--json`: the server's body) and `recipe approve` prints the preview before it asks. Never start the control plane | 0; 1 when it isn't running (`SND-API-001 not running: …`); 2 on any other error (e.g. an unknown id or hash `SND-RECIPE-002`, an id with no approved version `SND-RECIPE-003`, a malformed `<id>@<hash>` `SND-CLI-003`) |
| `sindri recipe approve <file\|id@hash>` · `sindri recipe revoke <id@hash>` | `protected`: the server's challenge, its preview, the action text typed at a terminal (`approve <id> <version>`, `revoke <id>@<hash12>`), then `approved …` or `revoked …; N instances parked`. Starts the control plane first if it may. Reference: `docs/csharp/sindri-workflow.md` | 0; 1 when it isn't running and wasn't started (`SND-API-001`); 2 on any other error (e.g. a session or no terminal `SND-AUTH-002`, a stale or mistyped challenge `SND-RECIPE-009`, revoked `SND-RECIPE-005`, missing evals `SND-RECIPE-008`, nothing approved to pin or revoke `SND-RECIPE-003`, an edited file at a registered version `SND-RECIPE-007`) |
| `sindri workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--nonce <n>] [--json]` | Engine P4: reports the open desk run `<token>` through the API's `CompleteStep` (the command chain classes the caller; an agent session is refused `SND-AUTH-002`). Output files go by content, never path (each at most 1 MiB). Without `--nonce` the instance's nonce route issues one; a repeated nonce prints `already applied (…)`. Full verb set: `docs/csharp/sindri-workflow.md` | 0 applied; 1 not running; 2 refused (`SND-WF-005` run not open, `SND-WF-008` bad output, `SND-WF-009` …) |
| `sindri workflow list [--needs-me \| --item <id> \| --state <s>] [--recipe <id>] [--urgency <u>] [--subject <s>] [--root true\|false] [--query <q>] [--json]` | A client of `GET /workflow/instances`: one line per instance (`<address>  <recipe>@<hash12>  <state>: <now>  since <time> (<age>)`) in the API order; bare `list` and `--needs-me` ask `open=true` (the unfinished ones), `--item` and `--state` list ended ones too. `--needs-me` groups the rows by their chip, Decisions (PARKED, ESCALATED, WITH YOU) then Tasks (DECISION), each section by address; a parent whose question mirrors a listed child is dropped by the server, so the question shows once, under the child. Empty, it prints the sentence the server served for the filter: `nothing needs you` (`--needs-me`, also when no row falls under either heading), `No workflows are open.` (bare list) or `No workflows match.`. A session lists its own repo's instances only. Never starts the control plane | 0, empty included; 1 not running; 2 on any other error |

Output: state is always a word, never colour; there is no colour at all, so `NO_COLOR` and redirected output need
nothing special. Errors go to stderr as `SND-<AREA>-NNN message`, with `(fix: …)` where there is one, or `(usage: …)` for a
usage error. `status --json` prints the API's response body exactly as received, then a newline (API spec §9, C5); the
local verbs' `--json` (`profile validate`, `doctor`, `recipe validate`) print their response record through AwJson.

**Which verbs are API clients.** `status`, `stop`, `profile approve`, `views rebuild`, `queue`, `items`, `item add`, `item close`, `request`, `requests`, `decisions`, `decide` and `needs-you` call the running process's API (the CLI is a
client of the same handlers the UI will call, §17.27.1). `doctor` and `profile validate` run the same handlers in the
calling process, read-only, because they must work while the control plane is down (`doctor` reports exactly that).
`recipe validate` is local too: it reads only the recipe file and its child recipes beside it.
`status` and `stop` never start the process: "not running" is an answer. `profile approve` needs it running, because
the approval is a ledger event fenced by the lease, so from a human at a terminal it starts it first (`--no-start` opts
out); no verb starts it from an agent session (Callers and auth below).

**How long a CLI call may take.** Connecting to the recorded port is bounded at 5 s (`couldn't connect to 127.0.0.1:<port> within 5 s`, `SND-API-002`), so a dead or wrong port fails fast. Once connected there is no call timeout by default: a slow but healthy answer (a map review over thousands of items can take 40 s) completes. Every call to the control plane, including the POST that starts a job (`services run`, `context sources sync`, `context map build`), ends on Ctrl-C: the CLI prints `interrupted` and exits 130 (a job the server already accepted keeps running; `sindri jobs` shows it); job-follow keeps its two presses once the job exists (first cancels the job, second leaves). `AW_SINDRI_CLI_TIMEOUT` (whole seconds, at most 2147483; unset, empty or `0` is no cap) sets a hard cap on one call; past it the CLI exits 2 with `SND-API-002 <host:port> didn't answer <route> within N s (AW_SINDRI_CLI_TIMEOUT)` (`didn't open <route>` for an event stream whose headers don't arrive in time). A value that isn't a whole number of seconds in range is `SND-CLI-003`, answered by any verb that calls the control plane; verbs that make no call (`recipe validate`, `help`, ...) ignore the variable. The cap does not touch the 2 s liveness ping or the 10 s wait for a freshly started `serve`, and an open event stream (`status --follow`, `workflow events --follow`, job follows) has no cap once its headers are in.

**External executables.** `deps.yaml` models edges between this repo's packages only, so the programs Sindri runs on the host are listed here instead. Managed Onyx (spec `docs/superpowers/specs/2026-10-16-sindri-onyx-managed-design.md` §9) runs `docker` (`version` (`--format {{.Server.Version}}` and `{{.Server.Os}}/{{.Server.Arch}}`), `compose version --short`, `info`, `context inspect`, `image inspect --format {{json .RepoDigests}}`, `ps -q|-a --filter label=com.docker.compose.project=<name>` and `stop <ids>` (a project whose files are gone), `stats --no-stream --format {{.ID}}|{{.MemUsage}}` (the preflight's count of what other containers use), and `compose -p sindri-onyx-<8 hex> -f <compose> -f <override> --env-file <env> pull|up -d --no-build|stop|ps -a --format json|config --format json` (the project name is per Sindri home: `sindri-onyx-` and the first 8 hex digits of the sha256 of the full `AW_SINDRI_HOME` path, computed in one place, recorded in `state.json` and used by status, doctor and down, so two homes on one Docker never touch each other's stack), with a 5 s timeout for the read-only facts, 30 s for `ps`, `config` and `image inspect`, 2 minutes for `stop`, 5 minutes for `up` and 45 for `pull`; every compose call runs with Compose's own `COMPOSE_*` variables and the `.env` keys Sindri writes unset in its environment, so the rendered `.env` is authoritative) and fetches the pinned deployment files over https from `raw.githubusercontent.com`, and, in the account stage (P3), `security -i` (macOS) or `secret-tool store` and `lookup` (Linux) with the key on standard input (and read back with `security find-generic-password` / `secret-tool lookup` to check it was stored), `open` (macOS) or `xdg-open` (elsewhere) for Onyx's loopback pages, and the ten setup-only HTTP requests to `127.0.0.1` (`login`, `logout`, `register`, `reset_password`, `admin-access`, `deactivate-user`, `user/pats` (list, create, delete by id), `me`); and, in later phases, the clipboards: `pbcopy`, `wl-copy`, `xclip` and `xsel`. `sindri onyx status` and `down` run only `docker`; `up` also fetches the files and, after the stack is up, runs the account stage above; the pins they read are `context/onyx.lock`, embedded into the build. `DOCKER_HOST` is read by the adapter (it says where the docker CLI sends its commands; a remote one is refused).

## Files

`AW_SINDRI_HOME`, default `$AW_STATE_DIR/sindri-app` (`$AW_STATE_DIR` defaults to `~/.agentic-workflow`). It is apart from
the TS sindri's `$AW_STATE_DIR/sindri` until the switch; no TS state is read. Each directory Sindri asks for (the home,
`approved/…`, `logs`) is made 0700 when Sindri creates it; parents created along the way keep the umask, and an existing
directory keeps its mode; files are 0600.

| Path | What |
|---|---|
| `ledger.db` | The ledger (SQLite, WAL, through `Aw.Kernel.Data`): `events`, `lease`, `outbox_commands` |
| `approved/<hash>/profile.json`, `approved/latest` | Approved profile snapshots (canonical JSON) and the latest hash. Runtime reads only these, never the live file |
| `views.db` | The projector's views (SQLite, WAL): `projector_state` (cursor, schema version, stall) and one `view_<name>(key, row_json, seq)` table per view (`services`, `profile`, and since API P3 `items`, `item-aliases`, `item-timeline`, since API P6 `jobs` (`view_jobs`, migration `0004_jobs.sql`); `view_items` also has a nullable `owner` column, reserved for multi-user, README X61). Schema version 5 (migrations `0002_items.sql`, `0003` for requests, decisions and needs-you, and `0004_jobs.sql`; the next free number, because the spec's per-phase ranges clash with the migration runner's no-gap rule). Derived state: deleting it, a schema change or `sindri views rebuild` rebuilds it from the ledger |
| `auth/human.token` | This start's human token (0600, in a 0700 `auth/` folder): minted by `serve` after it takes the lease, removed on stop (see Callers and auth). Also read, at each call, by the Sindri app (`SindriUi`, a declared file edge; `docs/csharp/sindri-ui.md`) |
| `control.json` | The running control plane's endpoint: `url`, `pid`, `epoch`, and `temporal` (the dev server's `pid`, `port` and `uiPort`). Written once it listens, removed on stop before the lease is released. A client checks the pid and epoch against `/sindri/ping`, so a stale record reads as "not running". Also read by the Sindri app (`SindriUi`, a declared file edge; `docs/csharp/sindri-ui.md`): a change to its fields is a contract change. When serve manages Onyx it also holds `onyx` (`project`, `url`, `state`: `running`, `needs-setup` or `failed`), no secret |
| `logs/serve.log` | The detached control plane's output (0600: written under umask 077, and an older log is narrowed after each start) |
| `temporal.db` | The Temporal dev server's SQLite file (`--db-filename`); the server is found by it, so one home has one server |
| `logs/temporal.log` | The dev server's output (`--log-level warn`, appended under umask 077). The server runs detached in its own session, never as serve's child, so it outlives a serve that dies and the next start adopts it |
| `recipes/<id>.yaml` | P4a: the recipes the worker's `LoadRecipe` reads by id (P4 reads approved recipes from the registry instead) |
| `workflow/nonces/<nonce>` | P4: the stand-in for the API's nonce table (0600 files in a 0700 directory, a 10-minute lifetime, not swept yet): a command's claim, so a retry with the same nonce is recognised |
| `workflow/events.cursor` | P4: the ledger-to-Temporal bridge's last delivered ledger sequence |
| `workflow/facts/<sha256>` | P4: the fact files `StartInstance` takes as inputs and `CompleteStep` takes as outputs, stored 0600 and named by their SHA-256 |
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
workflow:                 # optional (engine S1-D11); the times are durations <n>m|h|d
  deskTimeout: 2d         # default 2d: the floor of every desk Step's timeout
  eventHold: 24h          # default 24h: how long a buffered event waits for its wait
  effects: [effect.notify.desk]   # engine P6: the effect allowlist (effect.* ids); absent, RCP-EFFECT-001 is skipped and every recipe effect is refused (SND-WF-011)
  dispatchDelay: 10s      # engine P6: default 10s, the undo window before each effect runs (<n>s|m|h, 5s to 24h, like a cadence)
context: { ... }          # optional: the context engine's section, checked by the engine (docs/sindri/context.md)
```

The approval hash is SHA-256 over the canonical JSON (the checked record, repos and services sorted, and the
`context:` section's keys sorted at every level), so comments and key order don't change it. A profile without a
`workflow:` block or a `context:` section leaves it out of the JSON entirely, so its hash (and any approval of it) is
unchanged. An approval snapshot whose content no longer matches its hash isn't trusted.

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
| `temporal_event_id` | nullable (migration 0005, UI E-30): on a row the Temporal mirror (`RecordEvent`) wrote, the id of the Temporal activity it ran as, which is the third part of its `idempotency_key` (`workflow:<workflow id>:<run id>:<activity id>`), so the column and the key never disagree; null on every other row. Indexed with the payload's `instance` (`events_instance_temporal`, mirrored rows only), so `GET /engine/instances/{address}/history` joins a page of event ids to ledger sequences with one query. The join is on `idempotency_key`, which carries the run id (the row for (run, activity id) is the newest row whose key is `workflow:<workflow id>:<run id>:<activity id>` or starts with it and `:`, since a replayed activity that mirrored a different event keeps its own `…:<content hash>` row); `(instance, temporal_event_id)` alone is not unique across a reset (a new run reuses the activity ids), so nothing may treat it as a key |

S1 events and their keys: `process.started` (`process.started:<epoch>`), `process.stopped` (`process.stopped:<epoch>`),
`profile.approved` (`profile.approved:<hash>:after:<seq of the latest profile.approved event, or 0>`, so each
approval is recorded: approving A, B, A, B, A records five events), `service.heartbeat` (`service.heartbeat:<epoch>:<slot>`), and from API P1
`command.handled` (`command.handled:<commandId>`; the command chain's audit row, see Command chain). It is flagged
non-public in `EventCatalog`, so the event stream never sends it and only a human's `GET /sindri/ledger` shows it (see Event stream). P4a's Temporal mirror adds
six mirrored types: `workflow.started`, `workflow.transition`, `workflow.refused`, `workflow.expired`, `workflow.shifted` and
`workflow.notified`, each keyed `workflow:<workflow id>:<run id>:<activity id>[:<content hash>]` (the RecordEvent activity id;
engine spec rev 8.1 §5.6). The suffix appears only when a workflow-task retry replayed the same activity id with a different
event: the first row keeps the plain key and the different one is recorded under the key plus the first 12 hex digits of
SHA-256 over its type and serialized payload. The join rule, stated once: the row for (run, activity id) is the newest row
whose `idempotency_key` is that key, or starts with that key and `:`; a content-hash row supersedes the plain one. A service's
run event is `service.<name>`. `outbox_commands` is Kernel.Data's outbox table (migration 3), empty in S1: outward
actions join it in later stages.

## Command layer and API

Every action is a typed request handled by one `IService<TReq, TRes>` in `Features` (§17.27.1). The API, on
`127.0.0.1:<port>` only (`LoopbackBinding`: no argument or environment can widen it, and the host environment is always Production, so an inherited `ASPNETCORE_ENVIRONMENT=Development` can't turn bad input into a 500; a foreign `Host` is a 400, a
foreign browser `Origin` a 403):

| Route | Service |
|---|---|
| `GET /sindri/status` | `GetStatus`: running, pid, epoch, lease expiry, approved and live profile hashes, each service with its cadence and last run, and `sessions {live, asking, problems}` from the session collector's last pass (additive, collector CA-8; zeros before the first pass; box-wide for every caller, a `session` caller included, and deliberately public, through the MCP `sindri_status` tool too, because it is counts only and no rows; the list stays scoped to its repo) |
| `GET /sindri/sessions?provider=&state=&repo=&inWorkflow=` | `ListSessions` (a query, collector CA-1; reads the collector's in-memory snapshot, not `views.db`): `{sessions, recentlyEnded, problems, counts{live, asking, outsideWorkflow}, empty, asOf}`. A `session` caller sees only the rows of its `X-Sindri-Repo` (none without one) and `title`, `pid`, `host.session`, `host.pane` and `recentlyEnded[].resume` are null. `focus` is `{method: none, possible: false, why: not-built}` until CO-4a. `SND-SESSION-002` replaces the list. No catalog row (`ARCH-CQRS-003`) |
| `GET /sindri/sessions/{provider}/{id}` | `GetSession` (a query, CA-2): `{session: <row>, cwd, stateHistory, asOf}`; for a human `cwd`, `session.host.socket` and `stateHistory` (the last 20 state changes of this run, oldest first) are filled, for a `session` caller they are null. A ref that is unknown, ended or in another repo is the same `SND-SESSION-404` |
| `GET /sindri/services` | `ListServices` (a query): `{services: [{name, every, paused, lastRun, nextSlot, job}], empty, asOf}` (`job` is the running job of a manual run, else null; `nextSlot` is null while the service is paused, and otherwise counts from the last scheduled slot only: a manual run fills none) from the `services` view, for the approved profile's services and the build's built-ins (`workflow-events`, `context-sync-gaps`, ... at their own cadence unless the profile names them); `empty` is the sentence for no rows, rebuilding or stalled views |
| `GET /sindri/profile` | `GetProfile` (a query): `{approved, approvedAt, live, checkedAt, changed, changedPaths, asOf}` from the `profile` view |
| `GET /sindri/queue` | `GetQueue` (a query, API P3): `{groups: [{group, items, empty}], empty, asOf}`, one entry per group of `QueueGroups.Order` (`waitingOnYou`, `withYou`, `running`, `queued`; a parked item sits in `waitingOnYou`, owner decision 2026-10-12), each group's items the oldest `since` first; `empty` on the queue is its sentence when no group has an item (or the views are stale). A session sees only its repo's items |
| `GET /sindri/items?state=&repo=` | `ListItems` (a query): `{items: [ItemSummary], empty, asOf}`, the oldest `since` first, filtered by the kebab-case `state` and the `repo`; without `state` it lists open items only (`done` and `cancelled` appear only when `state` names them), and its empty sentence then says `No open items[ in <repo>].`; a malformed filter is 400 `SND-CLI-003` |
| `GET /sindri/items/{itemId}` | `GetItem` (a query): the `ItemSummary` fields flattened, then `version, createdBy, waitingOn, instance, createdAt, updatedAt, body, link, asOf`. `{itemId}` is an `ITM-…` id or a tracker key; an unknown one, or one outside a session's repo, is 404 `SND-ITEM-404`. `body` (scrubbed) and `link` appear only here |
| `GET /sindri/items/{itemId}/timeline?after=N` | `GetItemTimeline` (a query): `{entries: [{sequence, time, type, actor, line}], next, empty, asOf}`, a page of 100 after sequence N; `next` is the page's last sequence when more follow, else null |
| `GET /sindri/needs-you` | `GetNeedsYou` (a query, API P4): `{rows: [NeedsYouRow], empty, asOf}`, in `NeedsYouOrder` (urgency, then `since`, then id). A row is `{id, kind, label, subject: {kind, id}, item, title, reason, repo, urgency, since, recordedOnly, notice, answerKind, via?, primaryAction, actions, cli}` (`via` is `"child"` when `cli` points at a child that decides the answer, and omitted otherwise); `kind` is `decision` (`id` `decision:DEC-…`, subject a decision), `failure` (`failure:service:<name>`, subject a service) (its `cli` is `sindri services run <name>` since API P6, filled through `CliForm.Fill`; its `primaryAction` is `RunService`) or `profile` (`profile:drift`, subject `{profile, <live hash>}`). `recordedOnly` is true for a decision (its answer is recorded and not yet acted on) with `notice` the API's sentence for it. `empty` is `Nothing needs you. N running, M queued.` when there is no row, or the views' sentence when they are stale. A session sees its repo's rows and the repo-less ones |
| `GET /sindri/requests?state=&item=` | `ListRequests` (a query, API P4): `{requests: [{id, kind, item, repo, text, actor, state, decisionId, filedAt}], empty, asOf}`, newest first; `state` is `open`, `accepted`, `declined` or `voided`, `item` an id or alias (a malformed one is `SND-CLI-003`) |
| `GET /sindri/decisions?pending=&item=` | `ListDecisions` (a query, API P4): `{decisions: [DecisionView], empty, asOf}`, pending oldest first, decided newest first (at most 200). A `DecisionView` carries `id, kind, class, label, item, itemVersion, repo, question, options: [{id, label, rejectLike, cli}], recommended, state, filedBy, filedAt, decidedBy, decidedAt, choice, note, recordedOnly, notice, actions, cli, cleared`; `actions` is `[Decide]` while pending and empty after; `cleared` is the timeline line that settled or voided it. There is no `GET /decisions/{id}` |
| `POST /sindri/requests` | `FileRequest` (class `request`): body `{kind, item, text}`; answers `{accepted: {commandId, sequence}, requestId, decisionId}`; appends one `request.filed`, which opens the request and its pending decision |
| `POST /sindri/services/{name}/run` | `RunService` (API P6 contract; class `job`, a job started with `AddJob`/`MapJob`): answers `JobStartedResponse {accepted, jobId, links: {job, cancel, events}}` (`links` are paths: `/sindri/jobs/JOB-…`, its `/cancel`, and `/sindri/events?after=<sequence - 1>&types=job.*`, so the stream starts at the job's own `job.started`). The job runs the named service as `service:<name>`, records the run's manual `service.ran` and ends `ok: worked`, `ok: idle` or the service's own summary. A name that is neither in the approved profile nor a built-in is 404 `SND-SVC-404` (a built-in runs by name though the profile does not name it, so the `sindri services run <name>` of its failure row works; a built-in cannot be paused, so `services pause|resume` of one is 404); a second run of a service already running is 409 `SND-JOB-409` naming `runningJob`; a scheduled run in flight is `SND-JOB-409` too, with no `runningJob` (there is no job to name). Starting a ninth job while eight run is 429 `SND-JOB-002`. A manual run of a paused service runs: a pause stops the schedule only. A `session` is refused `SND-AUTH-002` (`cli`: `sindri services run <name>`) |
| `POST /sindri/services/{name}/pause`, `POST /sindri/services/{name}/resume` | `PauseService`, `ResumeService` (API P6 contract; class `desk`, a human only): append `service.paused` / `service.resumed`; the scheduler skips a paused service from its next tick (a slot due within milliseconds of the pause can still run once). Pausing a paused service is accepted again; a manual run of a paused service is allowed |
| `POST /sindri/jobs/{jobId}/cancel` | `CancelJob` (API P6 contract; class `job`, reads views): appends `job.cancelled` and trips the work's token; an ended job is 409 `SND-JOB-409` (no `runningJob`), an unknown one 404 `SND-JOB-404` |
| `GET /sindri/jobs/{jobId}`, `GET /sindri/jobs?state=&kind=` | `GetJob` (`JobView`) and `ListJobs` (`JobList`; the list is a provisional owner default, API P6 Q1, 2026-10-15). `JobView` is `{id, kind, key, item, repo, service, state, startedBy, startedAt, endedAt, progress: [{round, line, at}], rounds, summary, code, result, cancelledBy, actions, cli, asOf}`; a session sees the jobs of its own repo and the jobs of no repo (every `RunService` job started from the app is repo-less). A list a filter left empty says which filter (`No running RunService jobs.`; unfiltered it is `No jobs have run yet…`); a `kind` no registered job has, or a `state` other than running, succeeded, failed or cancelled, is a 400 `SND-CLI-003` before any read (the kind message lists the registered kinds). |
| `POST /sindri/decisions/{decisionId}` | `Decide` (class `decision`, a human only: a session is `SND-AUTH-002`): body `{choice, note}`; answers `{commandId, sequence}`; appends one `decision.recorded` |
| `POST /sindri/items` | `CreateItem` (desk, API P3; `{title, repo?, body?, link?}`): appends one `item.created`; answers `{accepted: {commandId, sequence}, itemId}` |
| `POST /sindri/items/{itemId}/close` | `CloseItem` (desk, reads views; `{reason, itemVersion}`, `reason` the name `done` or `cancelled`, a number is 400 `SND-CLI-003`; `itemVersion` at least 1): appends one `item.state-changed` keyed to the version; answers `{commandId, sequence}`; a stale version or an item already closed is 409 `SND-ITEM-409`; refused 503 while the views stall (`SND-VIEW-001`) or rebuild (`SND-API-010`) |
| `POST /sindri/views/rebuild` | `RebuildViews` (desk class, terminal or app only; refused to a session `SND-AUTH-002`): records `views.rebuilt` and tells the projector to replay the ledger; answers `{commandId, sequence}` |
| `GET /sindri/ping` | `PingProcess`: `{pid, epoch}` |
| `GET /sindri/commands` | `ListCommands`: the command catalog (Command catalog, below) |
| `POST /sindri/stop` | `StopProcess` |
| `POST /sindri/profile/approve` | `ApproveProfile` (`{hash, answer?}`: `answer` is `{challenge, typed}`, needed to approve) |
| `POST /sindri/workflow/challenges` | `IssueChallenge` (`{action}`; issue, human at a terminal only): `approve <recipe> <version>` (or `approve <recipe>@<hash>`), `revoke <recipe>@<hash>`, `approve profile <hash6>`, and for a protected instance answer `grant n <address>`, `grant-tokens n <address>` or `accept <address>` (subject `instance:<address>@v<version>`). Answers `{challenge, action, expiresAt, approve?, revoke?}`: a one-time challenge, valid 5 minutes, held in the server process, bound to the action and its subject (the recipe hash and version plus the pins digest on every approval, so a child approved while the human reads the preview makes it stale; the live profile hash), with the server's preview |
| `POST /sindri/workflow/recipes` | `RegisterRecipe` (body `{text, file}`; desk): `{accepted, id, version, hash, added, standing}`; idempotent per hash |
| `GET /sindri/workflow/recipes?stale=true` | `ListRecipes` (a query): `{recipes: [{id, version, hash, standing, via, stale, inputs}]}`; `stale=true` keeps the approved ones that are stale |
| `GET /sindri/workflow/recipes/{id}@{hash}` | `ShowRecipe` (a query): the `RecipeView` (the approval preview is `recipe approve`'s); the hash in full or by a prefix of 8 or more digits |
| `GET /sindri/workflow/recipes/approve-all` | `GetApproveAllPlan` (a query; setup-once spec §5.3, §7.2): what one hold would approve, `{recipes: [{id, displayName, version, hash, previousApprovedHash, change: new\|updated\|second-look, changeWord, capabilities: [{id, sentence}], delta: {added, removed}, changedBlocks, children}], ownApproval: [{id, displayName, reason: gains-capability\|waits-for-child, sentence, added}], digest, empty, asOf}`, children first; `empty` is "Every recipe is approved." when nothing waits. `SND-SETUP-007` before a profile is approved. The rows come from the `IShippedRecipes` port (below) |
| `POST /sindri/workflow/recipes/approve-all` | `ApproveAllRecipes` (body `{challenge, typed}`: `approve-all <digest>`, issued by `POST /workflow/challenges`; a job, a human at a terminal only for now): answers `JobStarted` (like every job route) for the job of kind `ApproveAllRecipes` and the fixed key `approve-all`; a second start while one runs is `SND-JOB-409` with `runningJob`. The checks run in the gate in this order and write nothing: `SND-SETUP-007` (no approved profile), `SND-JOB-409`, then the challenge is redeemed last (`SND-RECIPE-009` when it is wrong, expired or the plan changed since), so a refusal that is not the owner's does not burn the hold. The job approves each recipe of the plan through the engine, skips a refused recipe and the recipes that wait on it, and carries on; one progress line per recipe (its display name); the job's result is `{approved: [{id, displayName, hash}], refused: [{id, hash, code, sentence, cli}], skipped: [{id, displayName, because}]}`. When nothing waits it answers 200 with that result empty and starts no job (still refused to a session: `SND-AUTH-002`) |
| `POST /sindri/workflow/recipes/{id}@{hash}/approve` | `ApproveRecipe` (body `{challenge, typed}`; protected): the hash as `sha256:…` or its 64 digits |
| `POST /sindri/workflow/recipes/{id}@{hash}/revoke` | `RevokeRecipe` (body `{challenge, typed}`; protected) |
| `GET /sindri/workflow/instances?address=&item=&state=&needsMe=` | `ListInstances` (a query): the `InstanceList` of the instances matching the search attributes (rows with their `WFI-` `id`, counts over the root set, the `empty` sentence); `address=` resolves one address, a bare item or an id (§6.1) to its one-row list; `needsMe=true` keeps what needs you, a mirrored parent once under its child (the CLI groups Decisions and Tasks) |
| `GET /sindri/workflow/instances/{instanceId}` | `GetInstance` (a query): one instance's `InstanceDetail`, including `stepRuns[]` (one entry per Step run in start order, folded from the instance's own ledger rows by `StepRuns.Fold`: token, step, attempt and `maxAttempts`, state `running|passed|failed|refused|cancelled|abandoned`, runtime, start, end, `durationMs`, outcome, tokens, closing actor; API spec §3.8); the route takes only a `WFI-` id (anything else is a 400); an unknown id, or another repo's for a session, is SND-ITEM-404 |
| `GET /sindri/workflow/instances/{instanceId}/events?after=N` | `GetInstanceEvents` (a query): the ledger rows the instance mirrored after sequence N (default 0); an unknown instance is SND-WF-001 |
| `POST /sindri/workflow/instances` | `StartInstance` (desk): starts a recipe on an item with its input facts sent by content; answers the instance's view, or finds the live one |
| `POST /sindri/workflow/instances/{instanceId}/nonce` | `IssueNonce` (an issue; sessions may ask): a nonce for one command on that instance |
| `POST /sindri/workflow/instances/{instanceId}/commands` | `ApplyCommand` (desk; `answer`, `command`, `cancel`; a grant or accept is protected and redeems its challenge): the Update with the nonce as its id; a repeat prints `already applied` |
| `POST /sindri/workflow/instances/{instanceId}/runs/{run}/complete` | `CompleteStep` (desk): reports an open desk run with its outcome and output facts by content |
| `GET /sindri/events?after=N[&item=<id-or-alias>][&types=<globs>][&instance=<address>]` | `StreamEvents` as server-sent events, the ledger's public events as `PublicEvent` envelopes (see Event stream); a bad cursor, filter or item is refused as a plain 400 or 404 before the first frame, a full house is 429 `SND-API-012` |
| `GET /sindri/events/catalog` | `GetEventCatalog` (a query): each public event type that maps to at least one API view, with its idempotency key shape and `invalidates`, the API views (`status`, `services`, `profile`, `commands`, `queue`, `items`, `item`, `timeline`) a consumer re-queries when it arrives (UI E-15). `command.handled` and the other non-public types are not listed, and neither is a public type with no view yet (the `context.*`, `recipe.*` and `workflow.*` types): the app reads a type that isn't listed as unknown and re-queries every visible view, where an empty `invalidates` would mean "re-query nothing"; an empty list is never served |
| `GET /sindri/ledger?after=N&item=&type=&max=` | `ReadLedger` (a query): pages the ledger for audits as `{events, next}` (see Event stream) |

A service's error is ProblemDetails with the `SND-*` code as `title`. The API's own failures carry a code too: an
unhandled exception in a handler is logged and answered 500 with `SND-API-005`, and a body or query value the request
binding refuses (JSON that doesn't read as the request, a malformed hash or id) is a 400 with `SND-CLI-003` and fixed
text naming the method and path (a framework's message is logged, never answered). The loopback guard's 400 and 403, an
unknown route's 404, a wrong method's 405 and a wrong `Content-Type`'s 415 carry no ProblemDetails body; the CLI
reports an error answer without a code as `SND-API-004`, never as "not running". Every protected action confirms the same way (engine spec §4.2, S1-D5): the CLI
asks `POST /sindri/workflow/challenges` for a challenge, shows its preview, reads the action text the human types and
sends both back; a mistyped, expired, used or stale challenge is refused (`SND-RECIPE-009` for a recipe,
`SND-PROFILE-004` for the profile) and records nothing. Since API P1 every request is classed from its caller headers and every command runs through the command
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
  Queries keep S1's loopback guard: a loopback `Origin` is answered, any other is the guard's bare 403. The one body query, `QUERY /sindri/context/subgraph`, also refuses a request with an `Origin` header through the same refusal (`SND-API-008`, "a body query from a foreign Origin is refused"; in process only: a cross-site browser request meets the guard's empty 403 first). QUERY is the IETF safe method with a body (draft); clients use `new HttpMethod("QUERY")` or `curl -X QUERY`.
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

- one of the agent-session variables, set to anything non-empty: the pinned harness probe's markers (below:
  `CLAUDECODE`, `CLAUDE_CODE_ENTRYPOINT`, `CLAUDE_CODE_EXECPATH`, `CLAUDE_CODE_SESSION_ID`, `CLAUDE_PID` for Claude
  Code; `CODEX_SANDBOX` and `CURSOR_AGENT`, provisional until the owner probes), then `AW_SINDRI_CHILD` (Sindri's own
  children); or
- an agent harness among the CLI's ancestors, nearest first, up to 32 levels, by the probe's ancestry patterns: invoked
  as `claude`, or an executable under `…/claude/versions/`; `codex` and `cursor-agent` (provisional). Each ancestor is
  named by the basename of its `argv[0]`, as `ps` shows it (macOS: `proc_pidinfo` for the parent pid, `sysctl
  KERN_PROCARGS2` for `argv` and `proc_pidpath` for the executable; Linux: `/proc/<pid>/stat`, `/proc/<pid>/cmdline`
  and `/proc/<pid>/exe`), because the kernel's own name is the executable's file: Claude Code runs as
  `~/.local/share/claude/versions/<version>`, invoked as `claude`. An ancestry that can't be read (a sandbox refuses
  process info) is ignored; the variables still apply.

Both lists live in `Host/Commands/Probe/PinnedProbe.cs` and are golden-tested against the probe reports (engine §6.2,
"The harness probe" below). `AW_SINDRI_PROVER=1` (set by `scripts/sindri-prove.sh human`) skips the parent-process check, never the variables, and
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

A command the API refuses with `SND-AUTH-002` names why the CLI ran as a session, the first variable or process it
found: `(this CLI ran as a session: CLAUDE_CODE_ENTRYPOINT is set | parent process claude | parent process 2.1.296
(~/.local/share/claude/versions/2.1.296) | no terminal)`. It exits 2.

**Who starts the control plane** (API spec §7):

| Verb | When it isn't running |
|---|---|
| `sindri`, `sindri --headless` | Starts it, except in an agent session: `SND-API-001 Sindri isn't running; ask the human to run 'sindri' (an agent session never starts it: <signal>)`, exit 1, nothing launched. When it runs, the summary prints as usual |
| `sindri serve` | Refused in an agent session (an agent variable is set, or a parent process is an agent harness without `AW_SINDRI_PROVER=1`): `SND-API-001 an agent session never starts Sindri (<signal>); ask the human to run 'sindri'`, exit 1 (P1 plan G6) |
| `sindri profile approve [--no-start] [<hash>]` | A human at a terminal starts it, then approves; `--no-start` prints `SND-API-001` instead. A malformed hash and a hash without a terminal are refused first, before anything starts. In an agent session or without a terminal it never starts it (P1 plan G8) |
| `sindri status`, `sindri stop` | Never start it: `not running`, exit 1 |
| `sindri doctor`, `sindri doctor --probe`, `sindri profile validate` | Run locally |

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

### The harness probe

`sindri doctor --probe [--harness <name>]` prints, as JSON and without calling the control plane, what this process
sees (engine §6.2): the **names** of every environment variable set (never a value, so no secret is recorded), the
whole ancestry up to launchd or init as `{name, executable}` (the home directory written `~`), whether stdin and stdout
are a terminal, and the harness's version (from a `…/versions/<v>` executable path, else `<command> --version`).
Without `--harness` it names the harness the pinned probe recognises (`unknown` when none).

One golden per harness lives in `tests/Aw.Sindri.Tests/Golden/Probe/`, either a report or
`{"harness": "...", "pending": "owner probe"}`. `PinnedProbe` reduces each report to markers and ancestry patterns, and
`Agent_variables_match_the_probe` checks the reduction: every marker is in the harness's report and in no human
terminal's; every other variable the harness adds over the plain terminal is listed as excluded with its reason (so a
new variable fails the test until someone decides); every ancestry pattern matches the harness's ancestry and no human
terminal's.

| Golden | State | Source |
|---|---|---|
| `claude-code.json` | pinned (2026-10-12 at Claude Code 2.1.296; re-probed 2026-10-14 at 2.1.299, same markers and ancestry; its Bash tool in Warp) | `scripts/sindri-prove.sh doctor --probe` from an agent session, `SINDRI="$HOME/.dotnet/dotnet artifacts/bin/Aw.Sindri.Host/debug/sindri.dll"` |
| `plain-terminal.json` | pinned (derived) | the variable names Claude Code was launched with (`ps eww` of the `claude` process: the owner's Warp shell's exported environment) and that shell's ancestry (`-zsh`, Warp, launchd). The owner may replace it with a real probe from a plain terminal |
| `codex-read-only.json`, `codex-workspace-write.json`, `codex-danger-full-access.json` | pending: owner probe | — |
| `cursor-agent.json`, `cursor-terminal.json` | pending: owner probe | — |

**Excluded from Claude Code's markers** (it sets them, a plain terminal doesn't, but they don't mark it reliably):
`AI_AGENT` (a cross-tool marker that an app hosting the human's terminal, like Cursor, may set there too; waits for the
`cursor-terminal` probe); `CLAUDE_CODE_BRIDGE_SESSION_ID`, `CLAUDE_CODE_CHILD_SESSION`, `CLAUDE_CODE_MESSAGING_SOCKET`,
`CLAUDE_CODE_MESSAGING_TOKEN`, `CLAUDE_CODE_SESSION_ATTENDED` (set only in some launch modes); `CLAUDE_EFFORT` (a setting
a human may export); `COREPACK_ENABLE_AUTO_PIN`, `GIT_EDITOR`, `NoDefaultCurrentDirectoryInExePath` (other tools'
settings humans set too); `AW_SINDRI_HOME`, `AW_SINDRI_PORT`, `SINDRI` (set by the prover for the run). Every variable
of the plain terminal is excluded by definition: humans see it.

**`sindri doctor`** adds one line per pinned harness. The harness it runs in is re-probed: `ok` when every marker is
still set and the version matches, else `warn` naming the missing variable or the new version. Every other harness's
installed version (`<command> --version`) is compared with the pinned one: `warn` on a mismatch, `ok` when equal or
not installed. A pending owner probe is always `warn` (so `doctor` exits at least 1 until the owner probes Codex and
Cursor).

**Owner steps.** Build once (`dotnet build src/Sindri/Aw.Sindri.Host`), then in the repo root, inside each harness, ask
it to run (Codex, Cursor's agent) or run yourself (Cursor's integrated terminal):

```bash
H=$(mktemp -d "${TMPDIR:-/tmp}/probe.XXXX")
AW_SINDRI_HOME=$H SINDRI="$HOME/.dotnet/dotnet artifacts/bin/Aw.Sindri.Host/debug/sindri.dll" \
  scripts/sindri-prove.sh doctor --probe --harness <name> | jq . > tests/Aw.Sindri.Tests/Golden/Probe/<file>.json
```

| Where | `<name>` | `<file>` |
|---|---|---|
| `codex exec -s read-only` (or `codex --sandbox read-only`) | `codex:read-only` | `codex-read-only` |
| `codex exec -s workspace-write` | `codex:workspace-write` | `codex-workspace-write` |
| `codex exec -s danger-full-access` | `codex:danger-full-access` | `codex-danger-full-access` |
| Cursor's agent (its terminal tool, or `cursor-agent`) | `cursor-agent` | `cursor-agent` |
| Cursor's integrated terminal, typed by you | `cursor-terminal` | `cursor-terminal` |

Use the prover's **session** mode as written (not `sindri-prove.sh human`, which unsets the very variables being
probed). In Codex's read-only sandbox the redirect can't write the file: let it print, and save the output yourself.
Then, in `PinnedProbe.cs`, turn the entry's `Owner(...)` into a pinned entry: its version (the report's), its markers
(variables the harness adds over `plain-terminal.json` that only it sets), its ancestry patterns, and every other added
variable under `Excluded` with a reason. Add the new markers to `scripts/sindri-prove.sh`'s `human` unset line and to the host smoke's `unset` line
(`src/Sindri/aot-smoke/run.sh`; a test checks each). `dotnet test --project tests/Aw.Sindri.Tests --filter-namespace "Aw.Sindri.Tests.Commands.Probe"` then
says what is still undecided. Re-probe the same way when `sindri doctor` warns that a version changed.

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

- **The stop skips the gate:** `StopProcess` is registered with `AddStopCommand`: the same chain without `CommandGate`, and after validation a `StoppingGate` that cancels the command holding the gate (every gated command runs under the gate's stopping token) and any that would wait for it. So a long command, such as a large review apply, can't keep `sindri stop` behind `SND-API-009`, and a refused caller cancels nothing.

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
Host facts: `ShellRows` (S1's shipped rows, and P2's `RebuildViews`) and `PendingRows` (API P3–P10a's actions, registered before their phase so
the app and doctor can list them; each phase binds its command type and flips its rows to `ready`). The engine and
context lanes add their own rows (the engine's `ApplyCommand:<variant>` rows land with the engine, not P1).

One row per **action id**: a command (`StopProcess`) or a variant (`CloseItem:cancelled`). A row carries its command
type and variant, action class, the services it grants (desk rows), `NeedsTerminal`, `SelfAudited`, `Destructive` (the
app confirms first), `ReadsViews` (disabled while the views rebuild), an optional `Shortcut`, the method and route, the
CLI form and the UI status: `ready`, `pending:<phase>` or `cli-only:<reason>`.

| Action | Class | Callers | UI |
|---|---|---|---|
| `StopProcess` | desk (destructive, self-audited) | human+terminal, human, service:host | `ready` |
| `RebuildViews` | desk | human+terminal, human | `ready` |
| `ApproveProfile` | protected (terminal) | human+terminal | `cli-only:typed-confirmation` |
| `RunDoctor`, `ValidateProfile` | local | none (CLI only) | `cli-only:local` |
| P3–P10a actions | per API spec §3.2 | per the table above | `pending:<phase>` |

**Which instance a launch belongs to (API follow-up to P10a).** An engine launch records the instance, Step and attempt it was made for: `launch.started`
and `launch.failed` carry `instance` (the instance address), `state` (the Step's state id) and `attempt` (the Step run's number, from the run token `{entry}.{n}`),
all null for an attended launch (an attach row records no step: it is a person's click, so `GET /launches/{attachId}` reads as unattached even when the session it shows belongs to an engine launch) and for events recorded before the fields existed. `GET /sindri/launches/{launchId}` serves them as `step{instance, state, attempt}`
(a launch already has its own `state`, requested, started or failed, so the three sit in one object, always present, members null when absent), and a session row serves
the same object as `launch` (null when no launch row is known; see `sindri-sessions.md`).

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
on the four carve-out routes with `issue` rows; queries are GET or QUERY, a command is never a GET or a QUERY and no POST serves a query, with no carve-out list; over the endpoint data source), `ARCH-CQRS-002` (no query handler takes
`ILedgerStore`, `ILease` or `IApprovals`, also through a handler's own Features helpers, followed one level; `GetStatusService` and `StreamEventsService` are allowlisted until P2's
`ILedgerReader`; the read-only approvals port is `IApprovedProfile`, which the context query handlers already take) and `ARCH-CQRS-003` (every command and issue has its row; a classified command has variant rows).

## Proving it

Proving a phase isn't a user feature, so its recipe isn't here: it is in API spec §12 and the phase's plan. The
wrapper's modes are in the header of `scripts/sindri-prove.sh`, and the API golden harness is in
`tests/Aw.Sindri.Tests/ApiGoldens/`.

## Event stream

API P5 (API spec §5). `GET /sindri/events` is the "go re-read" signal: the ledger's public events as server-sent events, for the app, `status --follow` and a prover's `curl -N` (`scripts/sindri-prove.sh curl --stream`). It carries no state to apply; a consumer re-queries the views the catalog's `invalidates` names.

- **Envelope.** `id` is the ledger sequence, `event` the type, `data` one `PublicEvent`: `{sequence, id, type, schemaVersion, occurredAt, actor, tenant, ring, item, correlationId, causationId, traceId, payload}`. On the stream, and on a session's `GET /sindri/ledger`, the payload is the catalog's public record for that type (fields it lacks are left out by construction); on a human's `GET /sindri/ledger` it is the whole stored payload. Either way every string is scrubbed and escaped (`SafeText`), so a terminal control sequence never reaches a reader. A payload that doesn't read as its public record is sent as `payload: null` and logged as a warning with its sequence and type (never the payload); the stream never stalls on one row. Only public types are sent: `command.handled` and `views.rebuilt` never appear.
- **Cursor.** `Last-Event-ID` when it holds a sequence, else `after`, else 0, so a reconnect resumes at n+1. A cursor past the ledger's head is a 400 `SND-CLI-003` before any frame, and so is a negative one ("after -1 is negative; a cursor is 0 or more"), on the ledger read too.
- **Filters.** `types=<glob,glob>` (each `a.b` or `a.*`, at most 16, else 400); `item=<id or tracker alias>`; `instance=<address>` (engine spec S1-D6). An item given by its id resolves through the ledger (its `item.created`), never the views, so an item created a moment ago works while the projector catches up; only an alias needs the views. An unknown item, or another repo's item for a session, is a plain 404 `SND-ITEM-404` before the first frame. The cursor moves past rows a filter skipped.
- **`workflow.instance-changed`.** A derived frame, opted into by naming it in `types` (`types=workflow.instance-changed&instance=<address>`; a stream without it sends the raw rows as before): `event: workflow.instance-changed`, no `id` line, `data: {"instance":"T-1/step/1","state":"checks","cursor":4211}` (the address, the state after the change, the ledger sequence of the change). On a frame, re-query `GET /workflow/instances/{address}`; the frame's `state` is the authoritative new state, and the view (read live, with no ledger sequence) can lag the frame, so if its `state` is not yet the frame's, retry a few times with a short bounded backoff. Such a stream opens at the ledger head when it names no `after` and sends no `Last-Event-ID` (it sends no `id:` line, so a reconnect has none): on every (re)connect, open the stream first and then re-query the instances you show, because frames sent while you were away are lost (re-querying before the stream is open leaves a gap). An explicit `after=` is honored. It is sent for the mirrored rows that change an instance's view (`workflow.started`, `.transition`, `.refused`, `.shifted`, `.notified`, `.expired`; not `.injection`), once per instance per page read at its newest row, and never stored, so a views rebuild sends none. It is no ledger type, so `GET /events/catalog` has no row for it. Raw `workflow.*` rows go out only if the caller's other globs match them. Spec §5.6.
- **Session scope.** A session sees the rows about its repo's items (`X-Sindri-Repo`; the item's repo is read from its `item.created` in the ledger) and the rows about no item, and a row whose payload names another repo is dropped. A session without a repo sees only item-less rows. A human or service sees every public row.
- **Wake.** The ledger signals each committed append (`ILedgerSignal`), waking the projector and every stream within milliseconds; the one-second poll remains the fallback. A full page reads again at once.
- **Ping.** A `: ping` comment line after 15 s without a write.
- **Errors.** A read error is sent as `event: error` with `data: {"code":"SND-…","detail":"…"}`, and the stream ends. A stop, or the client leaving, ends it without a frame and frees its slot.
- **Limits.** 16 open streams, 4 of them held for the human, so sessions get 12; one more is a 429 `SND-API-012` (re-query every 5 s). The `sindri.streams` gauge counts the open ones by caller class.
- **Signal.** The ledger's `ILedgerSignal.Pulse` fires after the commit, never inside it and never for a replayed (already recorded) command.

`GET /sindri/ledger` pages the same envelopes for audits: `after` (past the head is a 400), `max` 1 to 500 (default 100), `item` and `type` (a glob list) as above; the answer is `{events, next}`, where `next` is how far the read looked, set after a full page or while the read stopped short of the head (a filtered page can be short and still have more; a `next` that leads to an empty page with no `next` is the end). A human also gets the types outside the catalog's public ones, and every payload whole, public types included (an item's `body`, for one, which the public record leaves out), scrubbed and escaped. `command.handled` is an audit row, so it shows only on a human's `/ledger`, never on the stream or a session's read. A session gets the public records of public types, for its repo's items and for rows about no item. `sindri ledger` and `sindri status --follow` (Commands, above) are the CLI's clients.

## Views and the projector

API P2. `ProjectorHost` (a hosted service under `sindri serve`) runs the `Projector`, which folds the ledger into `views.db`, which the query
endpoints read instead of the ledger. It reads events after its cursor in pages, applies each to every `IProjection`
(`Core` folds over `IViewRows`: `ServicesProjection` over `service.ran`, `ProfileProjection` over `profile.approved` and
`profile.checked`) and moves the cursor with the rows in one transaction. It wakes once a second.

- **Events.** `service.ran {commandId, service, slot, outcome, code?}` is recorded by the scheduler for every run that failed or did work, and for the first ok after a failure; an idle run after an ok one writes nothing (a service reports `ScheduledResult.Quiet`). An unknown service in the approved profile is a failed run (`SND-SCHED-001`). `profile.checked {commandId, approved, live, drifted, changedPaths, approvedServices}` is recorded by the profile watch (every 30 s with the lease) when the pair changed. `views.rebuilt {commandId, cursorBefore, eventsReplayed}` records a `RebuildViews` request (`eventsReplayed` is the ledger head at the request); an automatic rebuild (missing `views.db`, schema change) only logs.
- **`views.ledgerHead` on `GET /status`** is the ledger's current head, read fresh; `cursor` is the projector's, so the gap is the lag. Query answers' `asOf` keep the projector's view.
- **`asOf`.** Every view answer carries `{cursor, ledgerHead, projectedAt, rebuilding, stalled}`. `GET /status` carries the same shape as `views` (plus `stalledAt`, `stalledType` and `text`, the rebuilding or stalled sentence, null when fresh).
- **Read your writes.** A query with `X-Sindri-After: <sequence>` (the `sequence` of a command's answer) waits up to 2 s for the cursor to reach it: a malformed value is 400 `SND-CLI-003`; still behind after 2 s, or stalled behind it (at once), is 503 `SND-API-011`. A command whose catalog row has `ReadsViews` waits for the ledger head the same way (stalled `SND-VIEW-001`, rebuilding `SND-API-010`); its handler doesn't run on any of them.
- **Stalls.** An event a projection can't fold stops the projector at that sequence (`SND-VIEW-001`, logged with the sequence and type); it retries every 30 s, and `sindri views rebuild` replays from the start. A rebuild request while rebuilding restarts the replay, and a request survives a pass that failed before the reset. When the projector can't run at all (views.db can't be opened, written or reset, or the ledger can't be read) no event is to blame: it shows as `stalled` with a null `stalledAt` and a `stalledType` of `<code>: <detail>` (the failing call's own code: `SND-VIEW-001` for views.db, `SND-LEDGER-00x` for the ledger) until a pass works; the fix is `sindri views rebuild`, or `sindri doctor` when the ledger is the cause. The status text, the list answers' `empty` sentence and the SND-VIEW-001 and SND-API-011 messages all say "can't be updated (...)" then, not an event number.
- **Health.** `GET /status` carries `health {word, problems, sentence}`. The word follows stalled, rebuilding, degraded, healthy; the problems come in fixed order: not running (`SND-API-001`), views, each failed service the approved profile names (its last run; a service an approval dropped doesn't count), the profile (`SND-PROFILE-005` not approved, `SND-PROFILE-003` changed since approval). A services view that can't be read is a views problem (`SND-VIEW-001`, "the views can't be read"), never zero services, unless the views are already stalled (that problem covers it). `sindri status` exits 1 unless the word is `healthy`.

The API goldens of this phase sit beside the P1 ones in `tests/Aw.Sindri.Tests/Golden/Api/`: `services/` (`unapproved`, `approved`, `ran-ok`, `ran-failed`, `rebuilding`, `stalled`, `after-not-reached`, `after-malformed`), `profile/` (`unapproved`, `approved`, `changed`, `stalled`), `views-rebuild/` (`accepted`, `accepted-while-stalled`, `refused-session`, `refused-origin`) and `status/` (`views-stalled`, `views-rebuilding`, `service-failed`, plus `running-*` with `views` and `health`). The harness drives `Projector.CatchUp` on a fake clock; a stall comes from a `service.ran` with an unparseable payload.

### Items and the queue (API P3)

- **Events.** `item.created {commandId, item, title, repo?, link?, body?, owner?}` (one event, `link` included, so a create is never two events, and a link given at create gives no alias: `GET /items/<tracker key>` resolves only after a later `item.linked`), `item.state-changed {commandId, item, from, to, version, reason?}` and `item.linked {commandId, item, record, alias?}` (written later by context sync; a link does not bump the item's version). Every one carries the ledger's `item` column. Public payloads (`ItemCreatedPublic`) leave out `body`. Keys: `item.created:<id>`, `item.state-changed:<id>:v<n+1>`; a close whose key is already taken by another command is `SND-ITEM-409` (a retried call is a new command, so it is never accepted again as a replay).
- **Views.** `ItemsProjection` (`items`: one row per item, key = id), `ItemAliasesProjection` (`item-aliases`: tracker key to item; a re-link moves it) and `ItemTimelineProjection` (`item-timeline`, key `<item>/<sequence:D19>`, so a range read is in order). `IViewReader.Range` reads a key range (the timeline's page and `ListItems`). The queue is grouped from `items` at query time; it has no table.
- **Items and states.** States: `new`, `queued`, `running`, `needs-decision`, `needs-answer`, `parked`, `taken-over`, `done`, `cancelled` (kebab case on the wire); P3 produces `new`, `done` and `cancelled`, the engine states come with API P11: the items projection folds `workflow.*` rows into `needs-answer` (an open desk run), `running` (a wait state or an agent run), `parked`, `taken-over`, `done` and `cancelled`, and `CloseItem` is refused (`SND-ITEM-409`, naming `sindri workflow cancel <address>`) while an instance is active; `queued` waits for the router phase. `actions` is `ItemActions.For(state)` (`CloseItem:done`, `CloseItem:cancelled` while the item is `new`, `queued` or `needs-decision`; none when it is running, waiting on an answer, parked, taken over or closed, since close is refused while an instance is active); `primaryAction` is a member of `actions` or null (null for every P3 state). `ItemSummary` is `{id, title, repo, state, step, since, primaryAction, actions, owner}`: no version, body or link. `owner` is reserved for multi-user (README X61), always null, never parsed; `createdBy` is the creating actor verbatim.
- **Repo scope.** A `human` or `service` caller sees every item; a `session` only those whose repo equals its `X-Sindri-Repo` (`ItemScope.Visible`). The resolver answers 404 for an item outside it, so no route confirms an item exists. `CreateItem` takes `repo` from the body, else the header, and refuses one the approved profile doesn't name (`SND-CLI-003`).
- **`SafeText`.** Titles, bodies and timeline lines are scrubbed through the text port (`RedactingScrubber` over `Aw.Redaction`) and cleaned for display: C0, C1 and DEL characters become `\xNN`, format and separator characters and lone surrogates `\u{XXXX}`. Titles and timeline lines also fold runs of tab, CR and LF to one space and are cut with `…` at their limit (title 200, timeline line 300); a body keeps its line breaks and tabs (CRLF becomes LF; a lone CR shows as `\x0D`) and is cut with `…` at 4,000 characters (`SafeText.CleanBlock`). The 400s of the item routes echo a refused value escaped, on one line and at most 80 characters. A create's limits: a title of 1 to 200 characters after trimming with no control character, a body of at most 4,000, both `SND-CLI-003`.
- **Empty sentences.** Every group has one (`Nothing is waiting on you.`, `Nothing is with you.`, `Nothing running.`, `Nothing queued.`, and `Nothing parked.` for `parked`, a reserved wire value no P3 group yields: a parked item sits in `waitingOnYou`, but `QueueGroup` keeps the member so a later phase doesn't change the enum); the queue's is ``Nothing in the queue. Add one with `sindri item add`.``; `ListItems`' is `No <state> items in <repo>.` for its filters (`No open items[ in <repo>].` without a state); a timeline's is `Nothing has happened to this item yet.`. Stalled and rebuilding views replace every one of them, the groups' included, with the views' own sentence (the rows are kept).
- **Goldens.** `queue/` (`fixture`, `empty`, `stalled`, `large`, `session-scoped`, `rebuilding`), `items/` (`list-empty`, `list-open`, `list-state-done`, `list-repo`, `list-session-other-repo`, `list-bad-state`, `created`, `created-repo`, `created-link`, `invalid-title`, `unknown-repo`, `refused-session`, `refused-origin`), `items-id/` (`new`, `after-close`, `not-found`, `cancelled`, `alias`, `with-body`, `session-other-repo`, `bad-id`), `items-id-timeline/` (`fixture` with its full page of 100 and a `next`, `page-2`, `closed`, `not-found`) and `items-id-close/` (`accepted`, `stale-version`, `accepted-cancelled`, `already-closed`, `not-found`, `refused-session`, `while-stalled`, `while-rebuilding`), all in `tests/Aw.Sindri.Tests/Golden/Api/` on a two-repo profile. P3 makes the three item rows (`CreateItem`, `CloseItem:done`, `CloseItem:cancelled`) `ready` in `commands/catalog.json`, with their CLI forms (the close forms take `[--version <n>]`). The `needs-decision` scenarios are API P4's (below), `queue/running` to API P11 (they need engine states).

### Requests, decisions and needs-you (API P4)

- **Events.** `request.filed {commandId, request, decision, kind, item?, repo?, text}` (one event: the request, its decision, its needs-you row and the item's `needs-decision` state are all projections of it) and `decision.recorded {commandId, decision, choice, note?, by, repo?}` (`repo` is the decision's repo, copied from its row, so a session sees an item-less decision only in its own repo). `request.filed` is public without its text and `decision.recorded` without its note (the stream and a session's ledger read omit them: there a session of the same repo sees the decision and the choice, not the note; a human's ledger read shows the stored payload. The repo-scoped views `/requests` and `/decisions` serve the text and the note to the request's repo, and a repo-less request's to every session, as they serve item titles).
- **Views.** `requests`, `decisions` and `needs_you` (migration `0003`), each keyed by id with indexed `repo`, `state` and `item`. `RequestsProjection`: `request.filed` opens a request and a pending `accept-request` decision (options `accept` and `decline`, the second `rejectLike`); `decision.recorded` settles both (and changes nothing when the decision is no longer pending, such as one voided by its item closing; a voided row's `decidedAt` is the time the item closed, and `itemVersion` is 0 for a decision with no item); an item closing as `done` or `cancelled` voids its open requests and pending decisions (notice `voided: the item was closed`, also their `cleared`). `NeedsYouProjection`: a pending decision is a `decision` row; a `service.ran` failure is a `failure` row whose `since` stays the first failure's and which the first ok run clears; a drifted `profile.checked` is a `profile` row (subject `{profile, <live hash>}`) and clears with the drift, taking with it every `failure` row of a service the approved profile no longer names. Every text field passes through the text port.
- **Item state.** An item that is `new` with a pending decision reads `needs-decision` and `waitingOn` names the decision; it is `new` again once none is pending (derived: no version bump, no extra event). Closing it voids the decision; deciding it afterwards is `SND-ITEM-409` "the item was closed".
- **Caps and scope.** A `session` caller may have 20 open requests in its repo and 50 in all (`SND-REQ-002`). A session's `Decide` is `SND-AUTH-002` (audited `refused`); a decision outside a session's repo is `SND-DEC-404`. Commands wait for the views to reach the ledger head inside the gate (`SND-API-011` after 2 s, `SND-VIEW-001` while stalled, `SND-API-010` while rebuilding). Two `Decide` calls at once record exactly one `decision.recorded`; the other is `SND-ITEM-409` "decided elsewhere".
- **Wire notes.** `recordedOnly` is a constant `true` for a pending or decided decision until the workflow engine acts on answers (API P11 does not change it: the P11 row doesn't list it), and the API serves both its sentence (`recorded; acted on once the workflow engine runs`) and the voided one, so a client keeps no copy. A `failure` row's `primaryAction` is `RunService` and its `cli` is `sindri services run <name>` (API P6); its actions are `RunService` and `PauseService`, except for a built-in service (it can't be paused), whose row offers `RunService` only. A built-in's failure row also outlives a profile check that does not name it (it clears on a successful run), as a failure of a service the profile still names does.
- **Goldens.** `needs-you/` (`empty`, `fixture`, `one-request`, `stalled`, `rebuilding`, `large`, `session-repo`, `after-decide`), `decisions/` (`pending`, `decided`, `empty`, `empty-decided`, `voided`, `by-item`, `stalled`, `session-other-repo`), `decisions-id/` (`accepted`, `declined`, `decided-elsewhere`, `voided`, `not-found`, `bad-choice`, `bad-id`, `refused-session`, `while-stalled`), `requests/` (`list-fixture`, `list-empty`, `list-state-accepted`, `list-by-item`, `list-session-other-repo`, `list-bad-state`, `filed`, `filed-item`, `invalid`, `invalid-kind`, `too-long`, `too-many`, `item-not-found`, `item-closed`, `refused-origin`), plus P4's rows in `queue/fixture` (item 2 `needs-decision` in `waitingOnYou`), `items-id/needs-decision` and `items-id/after-decide` (back to `new`).

## Jobs

API P6. A job is work a command starts and does not wait for: the command is accepted at once (one `job.started`), the work runs outside the command gate, and the job ends with one `job.finished` or `job.cancelled`. `RunService` is the first job; any lane adds its own.

- **Lifecycle.** `job.started {commandId, job, kind, key, item?, repo?, service?}` → `job.progress {commandId, job, round, line, item?, repo?}` per line → `job.finished {commandId, job, outcome, summary, code?, result?, item?, repo?}` (public form without `result`) or `job.cancelled {commandId, job, by, item?, repo?}`. The first terminal event wins in the fold; a cancel racing a finish shows one state and the ledger keeps both rows. `kind` is the catalog action id; `key` is the idempotency key within the kind (the service name for `RunService`), so a second start of a running `(kind, key)` is 409 `SND-JOB-409` naming `runningJob`.
- **Repo.** A job's repo is set once, at `job.started`, and its `job.progress`, `job.finished` and `job.cancelled` carry the same `repo` (and `item`), so a session never sees another repo's job or lines on the stream, the ledger read or `GET /jobs`. A session's job is in its own repo. For a human the request decides: `AddJob(…, repo: request => RepoName?)` names the repo the work is about (`RunService` and a job of no repo return null), never the checkout the human's shell stands in.
- **Limits** (`JobLimits`). 200 lines kept in the view, 200 characters per line, 2,000 progress events per job (then one `(progress truncated)` line and the rest are dropped), a 256-line queue to the writer that drops the oldest, a 16 KiB result, and 8 jobs running at once (the ninth start is 429 `SND-JOB-002`). A line is cleaned once, when it is written (`JobLines.Clean`: scrubbed, ANSI and OSC sequences escaped, one line), so the stream, `GET /ledger`, `GET /jobs` and the CLI all serve the same safe text.
- **Restart.** Jobs are not durable. The next start's recovery pass ends every job left `running` as `failed` with code `SND-JOB-001` and re-runs nothing; the human runs it again. A cancelled manual run of a service records no `service.ran`.
- **Links.** A started job answers `links {job, cancel, events}`; follow the events link and keep the frames whose `payload.job` is yours (the filter is client-side). `GET /jobs/{id}` is the polling fallback when the stream list is full (`SND-API-012`).
- **Callers.** `RunService` and `CancelJob` are class `job`: a human (terminal or not) and an in-process `service`; a session, or a call with no caller header, is refused `SND-AUTH-002`. `PauseService` and `ResumeService` are `desk`: a human only. `GET /jobs/{id}` and `GET /jobs` are open to every caller (a session sees the jobs of its repo and the repo-less ones).
- **CLI.** `sindri services` (cadence, paused, last run, next slot, running job), `services run <name> [--detach] [--json]`, `services pause|resume <name>`, `jobs [--state S] [--kind K]`, `jobs show <id>`, `jobs cancel <id>`. `services run` follows its job through `JobFollower`: it prints `job JOB-… started`, each line as served, then `succeeded: <summary>`, `failed: <summary> <code>` or `cancelled`. The first Ctrl-C posts `CancelJob` once and waits up to 5 s for the end; the second leaves at once. Exit 0 succeeded; 1 failed, cancelled, left, or Sindri stopped (`not running`); 2 refused or invalid. `--detach` prints `job JOB-… started` (with `--json`, the `JobStarted` body) and returns; `--json` otherwise prints the `JobStarted` body, then the final `JobView`, as NDJSON.
- **Adding a job in another lane.** (1) A request record implementing `IJob<TResult>` and a service `IService<TRequest, TResult>` whose work reports progress through `IJobContextAccessor.Current.Progress` and honours its token; (2) `services.AddJob<TRequest, TResult, TService>(request => JobKey.From(…), result => "summary")` in the feature's `Add…()` method, plus an `IJobAdmission<TRequest>` if it needs a pre-start check; (3) `group.MapJob<TRequest, TResult>("/route")` in the Host and a catalog row `JobRows.Job(action, typeof(TRequest), route, cli)`; (4) the result type in `SindriJsonContext`; (5) in the CLI, post to the route and hand the `JobStartedResponse` to `JobFollower.Follow(started, json, interrupt, again)`. The work runs in its own scope as the caller who started it, never under the command gate.
- **`job-probe`.** An opt-in diagnostic service, not a built-in: it runs only when a profile names it (`services: job-probe: {every: 24h}`), and the long job to start by hand (`sindri services run job-probe`, about 62 s). It reports 250 lines, one every 250 ms, and ends `ok: 250 lines`; line 5 holds an OSC-8 hyperlink, line 6 a credential-shaped fake and line 7 is 300 characters, so a run shows the escaping, the scrubbing, the cut at 200 characters and the 200-line window (the view keeps lines 51 to 250). Cancelling stops it within one line.
- **Goldens.** `services-name-run/`, `services-name-pause/`, `services-name-resume/`, `jobs-id/`, `jobs-id-cancel/`, `jobs/`, plus `services/` (`paused`, `running-job`, `manual-ok`: a manual run leaves the next slot where the scheduled one put it), `needs-you/after-manual-run` (the failure row is gone) and `events/job-lifecycle` (started, two lines, finished, the manual `service.ran`).

## Session collector (collector spec; CO-2a)

`SessionCollectorHost` (a hosted service under `sindri serve`, the `ProfileWatch` pattern: it runs only while the process holds the lease) runs one
pass every `sessions.pollSeconds` (2 by default) as the caller `service:session-collector`, through three internal services: `RestoreFleetHistory`
(once, after the lease: the last 24 hours of `fleet.changed` rows rebuild the set already reported), `CollectSessions` (the pass: the process table, Claude's
session records, the aw:fleet hook's state files, merged by the pure rules in `Aw.Sindri.Core/Sessions/`) and `RecordFleetChanges` (appends with the lease
grant: no command gate, audit row or catalog row, the `CheckProfileService` pattern). The pass has a 500 ms budget for the process table (past it the previous snapshot stays); every other source (Claude's records, the fleet state files, and the per-process environment, cwd and open files) gets its own fifth of it, and a source that fails or overruns is one
`problems` row and leaves the others intact; when the process table itself fails the previous snapshot stays and nothing is ledgered as gone.

- **A named exception to "queries read `views.db`":** the live snapshot (`ISessionSnapshot`) is memory only (collector spec CO3), as API P13's `GET /engine`
  reads Temporal live. The ledger gets one public event type, `fleet.changed {provider, sessionId, change, state, reason?, repo?, host, launchId?}`
  (`change` is `appeared` after two passes in a row, `asking` once it has lasted 5 s and at most 6 per session per hour, or `gone`); `running` and `idle` flips
  and leaving `asking` are never ledgered. Key `fleet.changed:<provider>:<id>:<change>:<observedAtMs>`; `Invalidates = [status]`. The payload holds no cwd, title or socket.
- **Reads:** `/proc` (the current user's processes by `status` `Uid:`; argv[0] to its first NUL only; the environment reduced to an allowlist), `~/.claude/sessions/*.json` (never
  `*.key`), `$AW_FLEET_DIR/<provider>/*.json`. The agent catalog is `config/launcher/builtins.json` (embedded) merged with `launcher.json`'s `agents` (`AW_LAUNCHER_CONFIG`,
  default `$AW_STATE_DIR/launcher.json`); `sessions.enabled`, `pollSeconds`, `providers` and `claudeSessionsDir` come from the same file, read once at start.
- **Test overrides:** `AW_SESSIONS_PROC_ROOT` and `AW_SESSIONS_CLAUDE_DIR`, for tests and the prover only; either shows `SND-SESSION-003`.
- **Host (CO-2b):** each session also carries its host (`tmux`, `warp`, `terminal`, `ide`, `ssh`, `t3`, `unknown`), found from the parent chain, `/proc/net/unix` and read-only `tmux -S <socket>` calls (`docs/csharp/sindri-sessions.md`, Host detection). The `fleet.changed` payload carries the host kind only.
- Telemetry: span `sindri.collector.pass`, gauge `sindri.sessions` (by provider and state), counters `sindri.collector.overrun` and `sindri.collector.pruned`.

## Worktrees (`IWorktrees`)

`Aw.Sindri.Core.Worktrees.IWorktrees` is the port the engine uses to make and remove git worktrees: the per-run worktree (engine spec §6.5, `<repo>/.claude/worktrees/sindri-<instance>-<run>`) and the phase worktree (`step.phase.seed`). `GitWorktrees` (Infrastructure) is the adapter; `FakeWorktrees` (`Aw.Sindri.Testing`) is the in-memory one, and `WorktreesContract` runs against both (unit lane for the fake, integration lane for git over a temp repo). Nothing calls it until the engine wires it in (BS-10).

- `Create(WorktreeRequest(RepoRoot, BaseCommit, Branch, Path))` runs `git worktree add -b <Branch> <Path> <BaseCommit>`, or without `-b` when the branch already exists at the base. It is idempotent: the same path on the same branch with HEAD still at the base returns the existing checkout.
- `Create` first runs `git worktree prune` (it drops only entries whose directory is gone), so a worktree deleted behind git's back is made again, never answered as a checkout. A checkout counts as the same only while its HEAD is still the base and it is on the branch: after commits or a detach, a repeated `Create` is `SND-WT-002`. The main checkout may hold the new worktree (`<repo>/.claude/worktrees/…`); another linked worktree may not.
- `Remove(path)` runs `git worktree remove`, then `git worktree prune`. A path that is already gone succeeds, so a retried cleanup is safe; it prunes the stale entry when the repo is known (the one this process made it in, else the repo of the nearest existing parent directory), and otherwise the next `Create` prunes it. `Remove` has no repo argument.
- **It never forces.** No `--force`, no reset, no fetch, no checkout over existing work: a different base or branch at the path is `SND-WT-002`, and a tree with uncommitted or untracked files is `SND-WT-004` and stays exactly as it is (ignored files, such as `bin/` and `node_modules/`, go with a clean tree). git runs with `LC_ALL=C` and no prompts, and `worktree add` runs the repo's `post-checkout` hook and any filter (LFS) under the 30 s budget. A git failure (the command, a missing repo, an unknown sha) is `SND-WT-003`; a bad request (a ref instead of a full sha, a relative path, a path inside another worktree) is `SND-WT-001`. Removing deletes no branch.
- The result type is `Result<Unit>` for `Remove` (the kernel has no non-generic `Result`); the codes are in the Errors table below.

## Scheduler

The control plane runs the services the **approved** profile names, on their cadence. It re-reads the approval once a
second, so an approval takes effect without a restart. Missed slots after a sleep are skipped, not replayed in a burst. Whenever the schedule is built (a start, or an approval of a changed profile), each service's first slot is anchored on its latest recorded `service.ran` (scheduled or manual, any outcome): a service that ran less than one cadence ago waits out the rest of the period, one that is overdue runs once at once, one with no recorded run runs at once, and a ledger that can't be read gives every service "at once" (logged `SND-SCHED-001`). An idle run after an ok one writes no `service.ran`, so such a service may catch up once when the schedule is built (a start or an approval). A
failed run (an error or an exception) is logged (`SND-SCHED-001`) and the next slot still runs; an approval it can't read
is logged once (`SND-SCHED-001`) and the current schedule keeps running. A profile service this build doesn't have is logged and recorded as a failed `service.ran` with `SND-SVC-404` (not `SND-SCHED-001`): its needs-you row has no `RunService` (running it answers the same 404), offers only `PauseService`, and points at `sindri profile validate` and the profile (remove or rename the service); health still lists it as a failed service. Nothing runs while the process holds no lease. S1
ships one service, `heartbeat` (API P6 adds the opt-in `job-probe`, see Jobs), which records `service.heartbeat` per slot; the index, intake and scorer join later as
blocks.

## MCP tools (API P8)

`/sindri/mcp` (one stateless Streamable HTTP endpoint, `Aw.Sindri.Host/Mcp/`) serves the `context_*` tools (docs/csharp/sindri-context.md S-1M) and these seven. Every call runs as `session:mcp` in the repo named by `X-Sindri-Repo`. Each tool binds its arguments (`McpArguments`), calls one service of the same decorated chain the HTTP route uses, fences the result and summarises it; no logic lives in the endpoint. `tools/list` returns 12 tools in this order: the five `context_*`, then the seven below.

| Tool | Arguments | Same as | Kind |
|---|---|---|---|
| `sindri_status` | none | `GET /status` | query, `readOnlyHint` |
| `sindri_queue` | none | `GET /queue` | query, `readOnlyHint` |
| `sindri_item` | `itemId` (an `ITM-` id or a tracker alias) | `GET /items/{itemId}` | query, `readOnlyHint` |
| `sindri_item_timeline` | `itemId`, `after?` (integer, 0 or more) | `GET /items/{itemId}/timeline` | query, `readOnlyHint` |
| `sindri_pending_decisions` | `itemId?` | `GET /decisions?pending=true` | query, `readOnlyHint` |
| `sindri_job` | `jobId` | `GET /jobs/{jobId}` | query, `readOnlyHint` |
| `sindri_request` | `kind` (`rescope`, `add-task`, `note`, `handback`), `text` (1 to 2,000 characters), `itemId?` | `POST /requests` (`FileRequest`) | request, `readOnlyHint: false`, `destructiveHint: false` |

There is no decision, desk, protected, job or step-report tool, and no `sindri_context` (use `context_query`): a session asks a human through `sindri_request`, and `McpSurfaceTests` (`tests/Aw.Architecture.Tests`) fails the integration lane if the MCP namespace references any command but `FileRequestRequest`, a challenge answer or the launch secret. Results are the route's body: an id that isn't an id is `SND-CLI-003`, an item or job of another repo is `SND-ITEM-404` / `SND-JOB-404`, a request over its caps `SND-REQ-001` / `SND-REQ-002`.

**Fencing.** `structuredContent` is the HTTP body with the free-text fields below wrapped by `UntrustedFence.Wrap` (source and author as listed) and `"trust": "untrusted"` added to the object that holds each one (`McpFencing` over the serialized body, per `FencePlan`). The HTTP routes are unchanged. Fencing the strings of a job `result` is a provisional owner default ("fence all free text", 2026-10-16), reversible by deleting its `FencePlan.Job` field. A path may end in `[]` for an array of strings. The text block carries ids, states and counts only, never record text.

| Tool | Fenced fields | Source | Author |
|---|---|---|---|
| `sindri_queue` | `groups[].items[].title` | `sindri:item` | none |
| `sindri_item` | `title`, `body` | `sindri:item` | `createdBy` |
| `sindri_item_timeline` | `entries[].line` | `sindri:timeline` | the entry's `actor` |
| `sindri_pending_decisions` | `decisions[].question`, `.note`, `.notice`, `.cleared` | `sindri:decision` | `filedBy` (question), `decidedBy` (note) |
| `sindri_job` | `summary`, every string inside `result` (any depth; numbers, booleans and nulls stay), `progress[].line` | `sindri:job` | `startedBy` for `summary` and `result`; none for `progress[].line` (a line carries no author) |
| `sindri_status`, `sindri_request` | none | | |

**Headers.** `UseMcpCaller` removes every `X-Sindri-*` request header except `X-Sindri-Repo`, then sets `X-Sindri-Client: mcp`; a caller, terminal, token, read-your-writes (`X-Sindri-After`) or launch-secret header can't ride an MCP call, and any `Origin` is `SND-API-008`.

**Read-your-writes.** `X-Sindri-After` isn't honoured on MCP, so `sindri_request` waits for the views to reach its own write (at most 2 seconds, `SindriTools.WaitForViews`, which a test holds equal to the `ReadYourWrites.Limit` the HTTP routes wait; the Host root depends on the Mcp namespace, so it can not read that constant) before answering; a `sindri_pending_decisions` right after it lists the new decision. On a timeout it still answers success, with "views catching up" in the text.

**The endpoint's own refusals** (shared by the `context_*` and `sindri_*` tools; every tool error is `isError` with `McpToolError {code, message}`, text `<code> <message>`):

| Code | Kind | Meaning |
|---|---|---|
| `SND-CONTEXT-021` | Invalid | The call has no `X-Sindri-Repo` (every MCP call is repo-scoped; a registration always writes it) |
| `SND-CONTEXT-022` | Invalid | An argument of the wrong type, a missing required one, an unknown one, or an enum value outside the schema |
| `SND-CONTEXT-023` | NotFound | Unknown tool (`sindri_decide` and every other absent tool) |

**Goldens.** `tests/Aw.Sindri.Tests/Golden/Mcp/<tool>/<scenario>.json`, plus `tools-list/default.json` (names, schemas, annotations; descriptions may be reworded and the golden re-recorded) and `errors/`. They sit outside `Golden/Api`, so the UI contract test doesn't enumerate them. The AOT smoke (`src/Sindri/aot-smoke`) lists the 12 tools and calls `sindri_queue`, `sindri_request`, `sindri_item` (`SND-CLI-003`) and `sindri_decide` (`SND-CONTEXT-023`); its profile names the repo `smoke`.

## Errors

**Extension members (RFC 9457 §3.2).** A slice may attach typed members to its `Error` with `error.WithExtension(name, value, typeInfo)` (or a `JsonElement` built with its own source-generated type info; `Aw.Kernel.ErrorExtensions`). The names are camelCase, at most 64 characters and not `type`, `title`, `status`, `detail`, `instance` or `traceId` (a bad or repeated name throws `ArgumentException`: a bug in the slice). `HttpResults.Problem` writes them as top-level members of the ProblemDetails body in the order attached, after the standard ones (ASP.NET's `traceId` follows), and cleans every string in them (values, and member names inside objects) with `SafeText.Clean`: scrubbed, escaped, one line, at most 200 characters; sessions and the human get the same body. An error without extensions answers byte for byte as before. The writer caps each array at 100 items and ends it with a `"…(N more)"` string item, and gives object member names that clean to the same text a `~2`, `~3` suffix; a slice should still bound its own lists. The middleware refusals that carry an `Error` (read-your-writes, caller context) go through `ApiProblems.Write(context, error, status)` and carry them too. The CLI (`Output.Error`) and the UI client (`SindriApiClient.Refusal`) ignore them and use the code and message. Each code that uses them lists the members in its row below. Not carried over MCP tool errors or the event stream's 400 yet.

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
| `SND-API-002` | It didn't answer the ping within 2 s, the CLI couldn't connect within 5 s, or the call outlasted `AW_SINDRI_CLI_TIMEOUT` (no cap unless set) |
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
| `SND-AUTH-003` | The launch secret doesn't match an open launch, or its launch already ended (403; API P10a, `session wrap` calls; a takeover check on the secret arrives with `step-report` in a later phase) |
| `SND-CLI-001` | Unknown command |
| `SND-CLI-002` | `sindri serve` didn't come up after starting it, or serve couldn't bring up the Temporal dev server (the pinned CLI is missing, it exited, or it didn't answer within 30 s), or `sindri stop` couldn't stop it |
| `SND-CLI-003` | Bad input: a CLI argument or setting that is malformed (a profile hash that isn't 64 hex digits, an `AW_SINDRI_PORT` that isn't 0..65535), or an API request body or value the control plane refused (a 400) |
| `SND-SCHED-001` | A background service run failed |
| `SND-ITEM-404` | No item has that id or alias, or a `session` caller asked for an item outside its repo (404) |
| `SND-ITEM-409` | The item changed since the caller looked (a stale `itemVersion`, or another close won the fact key), or it is already done or cancelled (409) |
| `SND-REQ-001` | A request has an unknown kind, no text, or more than 2,000 characters (400; API P4) |
| `SND-REQ-002` | Too many open requests: 20 in the repo or 50 in all, counted for a `session` caller (429; API P4) |
| `SND-DEC-404` | No decision has that id, or a `session` caller asked for one outside its repo (404; API P4) |
| `SND-DEC-001` | The choice isn't one of the decision's options, or the note is over 2,000 characters (400; API P4) |
| `SND-LAUNCH-001` | The terminal viewer didn't open the launch (503). On a tmux host the session keeps running and the code is on `launch.started`'s `viewer`; with no host it is a `launch.failed` (API P10a) |
| `SND-LAUNCH-002` | No terminal viewer is available here: no Warp, the template's binary isn't on `PATH`, or no GUI session (503; API P10a) |
| `SND-LAUNCH-003` | The agent CLI isn't on the login `PATH`, or the target isn't in the catalog (`detail: not-in-catalog`) (404; API P10a) |
| `SND-LAUNCH-004` | The item's repo isn't on disk (404; API P10a) |
| `SND-LAUNCH-006` | No session to resume or attach to; the mode falls back to Open (404; API P10a). `-005` is unassigned and stays unused |
| `SND-LAUNCH-007` | The launch was interrupted: Sindri restarted mid-launch, or the request passed its 30 s deadline; never retried on its own (503; API P10a) |
| `SND-LAUNCH-008` | A launch is already in progress for the item (409; API P10a) |
| `SND-LAUNCH-009` | The launch timed out: the 4 s host start (`launch.failed`, `detail.stage: host`), the viewer's window (a viewer outcome), or E-1's 10 s backstop (`stage: backstop`) (504; API P10a) |
| `SND-LAUNCH-010` | The target's spend today reached its `dailyBudget`, or a `dailyBudget` is set on a target with `usage: none` (`detail: no-usage-reader`) (429; API P10a) |
| `SND-LAUNCH-011` | `launcher.json` (or an agent entry in it) isn't valid: an unknown key in a section, a bad kind, a placeholder outside the closed set, an entry that lacks `label`, `bin`, `open` or `sessionId`, a `sessions.pollSeconds` outside 1 to 30. The collector reads the built-in agents and the default settings instead and shows one problem row (collector CO-2a). Each error names the section and key at fault (`sessions.pollSeconds is 1 to 30`), so a fix is per section even though the fallback is the whole file |
| `SND-LAUNCH-012` | The agent entry isn't approved for unattended Steps: `detail.reason` is `not-approved` or `hash-changed`, with the `current` and `approved` hashes (403; launcher SL8; API P10a) |
| `SND-LAUNCH-013` | A Step asked for a model or a policy and the entry's matching form is `none`; nothing launches (400; launcher L-17; API P10a) |
| `SND-LAUNCH-015` | The agent entry's `sessionIdPattern` timed out on a session id (2 s limit, non-backtracking), so the id was refused unjudged, not as a mismatch: an argv build (open or resume) fails with it, a heartbeat or exit report that carries the id returns it (the heartbeat still counts and the exit still ends the session), and `session wrap` prints it on stderr when a hook file or a scanned file hit the limit. `-014` is reserved by the sandbox spec (a gap, not a reuse) (launcher L-9) |
| `SND-LAUNCH-404` | No launch has that id, or a session caller may not see it: `GET /sindri/launches/{launchId}` (404; API P10a) |
| `SND-SESSION-001` | One of the session collector's sources failed or a pass overran its 500 ms budget: a problem row beside a normal list, never an exception message (collector CO-2a) |
| `SND-SESSION-002` | The session collector can't run here; the `detail` is `unsupported-platform`, `proc-unreadable` or `disabled`, and only these replace the list (collector CO-2a; the routes `GET /sessions` and `GET /sessions/{provider}/{id}` answer it, CO-3) |
| `SND-SESSION-003` | A test override (`AW_SESSIONS_PROC_ROOT`, `AW_SESSIONS_CLAUDE_DIR`) is set in this run: informational, one problem row beside a normal list; pruning of hook files is off and focus answers `copy` (collector CO-2a) |
| `SND-SESSION-404` | No live session has that ref, or it is outside a session caller's repo (the same answer for both; collector CA-5, CO-3) |
| `SND-SESSION-409` | The session at that ref is a different process than the caller looked at (`expectStartedAt` differs; collector CA-5; the preview read, CO-7a, answers it, and `FocusSession` will, CO-4a) |
| `SND-WT-001` | A worktree request isn't valid (400): `BaseCommit` isn't a full 40-character lower-case hex sha (a ref or a short sha), `RepoRoot` or `Path` isn't absolute, the branch is empty, has a space or starts with `-`, or the path is inside another linked worktree |
| `SND-WT-002` | The worktree path is already a worktree of another branch or at another commit, the branch is checked out in another worktree, or the branch exists at a commit other than the base: nothing is reset or moved, and the engine parks on this (409) |
| `SND-WT-003` | git failed: the command failed, the repo is missing, the sha isn't a commit of it, or the path isn't a worktree on remove; the engine fails on this (503) |
| `SND-WT-004` | `Remove` found uncommitted or untracked files in the worktree and left it as it is (409) |
| `SND-SECRET-001` | A secret pointer is malformed: no scheme, an unknown scheme, or a bad target. Expected `env:NAME`, `file:/absolute/path`, `keychain:<service>/<account>` or `op:<vault>/<item>/<field>`; the message names only the scheme, never the text after it (it may be a secret pasted by mistake) |
| `SND-SECRET-002` | A secret pointer doesn't resolve: the variable is unset or empty, the file is missing or empty, the keychain (macOS `security`, Linux `secret-tool`) or 1Password entry is missing, `security`, `secret-tool` or `op` isn't on PATH or ran past its timeout (5 s, 30 s), or `keychain:` is used on any other OS. On Linux `secret-tool` exits 1 both for a missing entry and when no D-Bus session or keyring is reachable, so `sindri serve` must run in the login session. The message names the pointer and the fix, never a value |
| `SND-SECRET-003` | A `file:` pointer is unsafe: the file has group or other permission bits (it must be 0600 or stricter) or isn't a regular file (a symbolic link, even to a 0600 file, or a directory; only the last path segment is checked) |
| `SND-ONYX-001` | Docker can't be used, and the message gives the fix for the cause it found: the CLI is not installed, the daemon is not running, permission denied (join the `docker` group), a missing Docker context, or the daemon is still starting (`docker version` took over its 5 s try and its 20 s retry). `WARNING:` lines in the CLI's output are ignored when choosing the cause |
| `SND-ONYX-002` | Docker Compose v2 is missing (`docker compose version` fails; the legacy `docker-compose` v1 isn't enough) |
| `SND-ONYX-003` | Not enough free disk (measured at `$AW_SINDRI_HOME`, or its nearest existing ancestor) or Docker memory for the pinned Onyx; the message says how much is free, how much is needed and where to raise it |
| `SND-ONYX-004` | The Onyx pins (`context/onyx.lock`, embedded in the build) are malformed, a downloaded file or a pulled image failed its pin (the message names it and both hashes' first 12 digits), or a compose image has no pin (the compose layout of the pinned release changed) |
| `SND-ONYX-005` | Onyx didn't answer within 15 minutes of the start, or answered with a version other than the pinned one; the message summarizes `service: state (health)` of every container |
| `SND-ONYX-006` | Onyx's port 3000 is taken by something else and so is every fallback port 3001-3010 |
| `SND-ONYX-007` | The Onyx login given isn't an admin (Onyx refused to make a user an admin, to reset a password or to make a token with 401 or 403). Log in as the first account created at the sign-up page, or make this one an admin in Onyx's admin users page, then run `sindri onyx up` again |
| `SND-ONYX-008` | Logging in, setting up Sindri's user, revoking or making its token or checking it failed: a wrong login, Onyx not answering, the dedicated address typed as the owner's login (refused before anything is sent), a registration Onyx refuses (invite-only: invite `sindri@example.com` once, reruns then need no invite; or a domain list: allow example.com; or a password rule, with Onyx's reason), a login of the dedicated user that is refused (the message says to activate it if it was deactivated), a token list or revoke that fails, a token answer with no token, or a token Onyx doesn't take as that user's admin token. Whenever a token may have been made and lost, the message says it is a live admin token nobody can read back, that the next run revokes it, and how to stop it now. Neither a password nor a token is in the message |
| `SND-ONYX-009` | The token couldn't be stored: neither the OS secret store nor `$AW_SINDRI_HOME/secrets/` took it. Make that directory writable by you (or install `libsecret-tools` on Linux), then run `sindri onyx up` again |
| `SND-ONYX-010` | Docker points at another machine (`DOCKER_HOST` or the current docker context isn't a local unix socket or named pipe, or couldn't be read); the message names the endpoint and says to switch to the local context |
| `SND-ONYX-012` | Sindri can't use its Onyx directory under the Sindri home (no permission to create or lock a file in it); the message names the directory |
| `SND-ONYX-011` | A `docker compose` command failed (`pull`, `up`, `stop` or `ps`); the message ends with the last lines it printed. A command that couldn't reach the daemon is `SND-ONYX-001` instead |
| `SND-VIEW-001` | The views are stalled or can't be updated: the projector stalled on an event (logged with its sequence and type), or it can't run (views.db or the ledger can't be used). `sindri views rebuild` clears an event stall or a views.db cause; `sindri doctor` diagnoses a ledger cause |
| `SND-JOB-404` | No job has that id, or a `session` caller asked for one outside its repo (404; API P6) |
| `SND-JOB-409` | Three causes, all 409: <ul><li>a job of that kind and key is already running: the error carries `runningJob: "JOB-…"`, and only this cause does;</li><li>a scheduled run of the service is in flight (`RunService`): no job exists yet, so no `runningJob`;</li><li>`CancelJob` met a job that already ended: no `runningJob`.</li></ul>Each detail is a sentence with a fix hint (API P6) |
| `SND-JOB-001` | The process stopped while the job ran. Only ever a job's `code` (the recovery pass writes it at the next start), never an HTTP response (API P6) |
| `SND-JOB-002` | Too many jobs are running (8) (429; API P6) |
| `SND-SVC-404` | No service of the approved profile, and no built-in, has that name (404; API P6) |

### Workflow errors (`WorkflowErrors`)

The workflow engine's catalogue (engine spec §8) is a second class, `Aw.Sindri.Core.Workflow.WorkflowErrors`, validated
together with `SindriErrors`. Retired codes stay reserved in `WorkflowErrors.Retired` and are never reused.

| Code | Kind | When |
|---|---|---|
| `SND-RECIPE-001` | invalid | Validation failed, or the recipe was registered before input kinds (a start or a first approval of it); fix each `RCP-*` line, or register the current file at a new version and approve it |
| `SND-RECIPE-002` | notFound | No recipe with that id or hash (`sindri recipe list`) |
| `SND-RECIPE-003` | conflict | Not approved (`sindri recipe approve`) |
| `SND-RECIPE-004` | — | Retired: a caller without a terminal gets the API's `SND-AUTH-002` |
| `SND-RECIPE-005` | conflict | Revoked; approve a new version |
| `SND-RECIPE-006` | conflict | A pinned block changed since approval; re-approve (`recipe list --stale`) |
| `SND-RECIPE-007` | conflict | Same id and version, other content; raise `version` |
| `SND-RECIPE-008` | conflict | Evals missing or failing (`sindri recipe eval <file>`) |
| `SND-RECIPE-009` | conflict | A recipe challenge expired, or its subject changed since the preview |
| `SND-RECIPE-010` | notFound | The recipe file can't be read |
| `SND-RECIPE-011` | conflict | A recipe is not one this build ships (its `id@hash` is not in the embedded set); approve it on its own |
| `SND-SETUP-007` | conflict | The approve-all plan or its challenge is asked for before a profile is approved |
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
| `SND-WF-015` | unavailable | An effect couldn't be sent: its block replied an undeclared outcome, or every retry failed; the instance parks (dispatch) |
| `SND-WF-016` | unavailable | A Step input has a descriptor but no runtime in this build; refused at `recipe approve` and at `workflow start`; if the runtime is gone after approval, the Step's `FillInput` parks the instance (`SND-WF-010`, the question names 016) and a restart tries again |
| `SND-WF-017` | conflict | The text a pull refilled differs from what the run recorded; nothing is served (re-run the Step) |
| `SND-WF-018` | conflict | A Step input, re-run on a cache miss, differs from what the run recorded; the Step does not run on different data (re-run the Step) |
| `SND-WF-019` | invalid | A Step input filled an invalid plan: a bad section name, a section over 256 KiB, a typed result over 256 KiB, or a plan over 1 MiB; the Step fails |
| `SND-BLOCK-001` | unavailable | A fact a block Step reads can't be read; the run ends as `step.failed` with the message as its reason |
| `SND-BLOCK-002` | unavailable | The approved profile a block Step's providers come from can't be read; the run ends as `step.failed` `profile-unreadable: <code>` |
| `SND-BLOCK-003` | unavailable | The item's repo (`fact:item`) couldn't be read, so a block Step's per-repo keys can't be resolved; the run ends as `step.failed` `repo-unreadable`, never with the box's list |
| `SND-BLOCK-004` | unavailable | A Step's run-area file (`$AW_SINDRI_HOME/workflow/runs/<WFI-id>/`, for example the scoping station's `rounds.jsonl`) can't be written or read; the Step fails with the reason, and the line is never dropped |

`sindri recipe validate <file> [--json]` and `sindri recipe eval <file>` are local checker verbs (engine spec W27): exit
0 pass, 1 fail, 2 usage or tool error. The recipe reference, the eval format, the `RCP-*` rule table and how to add
a block are in `docs/csharp/sindri-workflow.md`.

## Workflow views (API spec §3.8; rev 8 contract unit)

The instance, recipe and status view shapes the UI codes against (screens spec E-20, E-22, E-23) are records in
`Aw.Sindri.Core/Workflow/Views/` (ids typed where Core has a typed id; a state, workstream or fact-row key stays text, and
an address is the typed `InstanceAddress`), plus
`FactView` in `Features/GetInstanceFact`. The routes are served (API P11); the CLI reads the same bodies:

| Route | Slice | Shape |
|---|---|---|
| `GET /workflow/instances?root=&open=&recipe=&subject=&state=&needsMe=` (also `item`, `urgency`, `query`, and `address=` for the one-row list that resolves an address to its id) | `ListInstances` (engine) | `InstanceList` |
| `GET /workflow/instances/{id}` | `GetInstance` (engine) | `InstanceDetail` (with `stepRuns[]`, the Step runs folded from the instance's ledger rows) |
| `GET /workflow/instances/{id}/facts/{key}` | `GetInstanceFact` | `FactView` |
| `GET /workflow/instances/{id}/scoping` | `GetInstanceScoping` (scoping, API2) | `ScopingView` (docs/csharp/sindri-scoping.md) |
| `GET /workflow/instances/{id}/packets/{hash}/files/{name}` | `GetPacketFile` (scoping, API9) | the file as JSON or text (docs/csharp/sindri-scoping.md) |
| `GET /workflow/recipes/{id}` and `{id}@{hash}` | `ShowRecipe` (engine) | `RecipeView` |

**What the routes serve (API P11).**

- **List.** `ended` instances are listed unless `open=true` (the CLI sends it, except with `--item` or `--state`). `counts` come from the whole root set (`root=true`, ended rows included) before `recipe` or `open` narrows the rows: the root set is listed twice (all, then running only), a root that is no longer running is counted done from the listing without asking Temporal, and only the running ones are queried; `open` is every row not finished, `needsYou` the PARKED, ESCALATED and DECISION rows, `withYou` the WITH YOU rows, `running` the RUNNING rows, `done` the finished ones whatever their outcome: the DONE, FAILED, DIDN'T WORK OUT and CANCELLED rows (`InstanceCounts.Of`). The `address` lookup is one row with counts over that row alone. `empty` is the sentence for the filter when no row is listed: `nothing needs you` for `needsMe=true`, `No workflows are open.` for `open=true`, else `No workflows match.`. `needsMe=true` is the `NeedsYou = true` visibility filter with a parent whose open question mirrors a listed child dropped (`NeedsYouList.Shown`: once, under the child); the Decisions and Tasks sections of `workflow list --needs-me` are grouped client-side from each row's chip (PARKED, ESCALATED and WITH YOU under Decisions, DECISION under Tasks), each by address. A `session` caller lists the instances of its own repo's items only, in the rows, the counts and the `address` lookup (another repo's address is `SND-ITEM-404`): the item comes from the address and its repo from the ledger (`IItemRepos`), never the views.
- **Detail.** `title` is the item's title from the `items` view, else the address; `parent` is resolved from the instance's parent id (null for a root, or when the parent can't be read); `counts` are the item's timeline, decision and request rows (`context` stays null until CTX serves the lenses); `current.status` is the status record the CLI prints, and `status.text` is `sindri workflow status`'s text at the server's clock. A `session` caller reads only its own repo's instances: another repo's, or an instance on no item, is `SND-ITEM-404`, by the item's repo from the ledger (`IItemRepos`), never from the views. An unknown id answers the same `SND-ITEM-404` (`No workflow instance <id> in this repo.`), on the instance and the fact routes alike, so an id never says it exists in another repo (the `address=` lookup, which `workflow status` uses for an address or id, keeps `SND-WF-001` for one no instance has, and `SND-ITEM-404` for another repo's instance; `workflow events` relies on the `SND-WF-001` to read the trail of an instance Temporal has dropped).
- **Fact.** The body is read through `IFactContents` by the digest the instance holds for the key (`InstanceView.FactShas`), scrubbed, and capped at 256 KiB: a `text` fact is a string, any other type is its stored JSON (an object or array that loses its tail entries when cut and stays one, `truncated: true`). A key the recipe doesn't declare, or one not produced yet, is `SND-ITEM-404`. `decision` facts are the stored `{choice, provider, actor?}` object; `file`, `item` and `link` facts are their stored bodies.
- **Recipe.** `GET /workflow/recipes/{id}` is the approved version (`SND-RECIPE-002` when no such id is registered, `SND-RECIPE-003` when none of its versions is approved); `{id}@{hash}` is that registered version, approved or not, by the full hash or a prefix of 8 or more digits. `sindri recipe show <id>|<id@hash> [--json]` prints the same keys (`input: <key> type=<type> kind=<kind>`, `output: <key> type=<type> from=<state>`, `loop: <name> <from> -> <to> cap=<n>`); `--json` is the body. The standing, the approval and the approval preview are no longer in `recipe show`: `recipe list` has the standing and `recipe approve` prints the preview before it asks.
- **Display names (setup-once spec §7.7).** Every view that names a recipe carries `displayName` beside the id: `RecipeView`, each `RecipeSummary` row, `recipe: {id, hash, version, urgency, displayName}` on the instance detail and list rows, and each `children[]` group (`ChildGroup.displayName`). The name comes through `IRecipeDisplayNames` (the recipe's `displayName`, required on a recipe file once the engine lands `RCP-DISPLAY-001`). Until the engine supplies names the field is `null` and left out of the JSON, and the app makes the id readable itself; a raw id is never served as a name.
- **Approve-all and the engine.** The plan and the job's work go through one port, `IShippedRecipes` (`Plan`, and `Approve` over a `RedeemedPlan`, a type with no public constructor that only a redeemed `approve-all <digest>` challenge makes). The engine implements it from its embedded set, the registry and `RecipeCapabilitySet`, and its `Approve` is the internal `ApproveShippedRecipe` (same checks as `ApproveRecipe`, one `recipe.approved` per recipe with `batch`, `planDigest`, `heldMs`). Until then `NoShippedRecipes` answers an empty plan. `tests/Aw.Sindri.Testing` has `FakeShippedRecipes` and `ShippedRecipesContract`, which any implementation must pass. `ApproveAllRecipes` is a `job` row with `NeedsTerminal`: a session, the app and a service are `SND-AUTH-002` until the `AppConfirm` flag (setup-once spec §4 item 7) lets the app hold the confirmation.
- **CLI `--json`.** `workflow list --json` prints `InstanceList`, `workflow status --json` prints the `current.status` fields of the detail, `recipe show --json` prints `RecipeView`: the engine bodies (`ListInstancesResponse`, `GetInstanceResponse`, `{recipe, preview}`) are gone.
- **Item and needs-you states.** An open desk run is `needs-answer` (so `workflow.step` shows under `waitingOnYou`; `running` is a wait state or an agent run, as `workflow.item` has), and `queued` waits for the router phase, which has no event yet; the P11 row of the API spec says so.

**Golden contract.** Each scenario's fixture (`tests/Aw.Sindri.Testing/Views/WorkflowViewFixtures.cs`, `FactFixtures.cs`,
and the recipe declarations in `FixtureRecipes.cs`/`RecipeShape.cs`, one declaration per recipe) is serialized through the
host's own JSON options and pinned at `tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json` (`workflow-instances`,
`workflow-instance`, `workflow-instance-fact`, `workflow-status`, `workflow-recipe`; 43 files, in `WorkflowViewGoldenTests`).
Addresses are the engine's form (`ITM-01JK3/project/1`, child `ITM-01JK3/project/1/step.1`), every root has an item, and a
fixture's `id` is the engine's minting rule applied to its address (`WorkflowInstanceId.Of`, pinned by a test vector); every child row, parent reference and status child carries that `id` beside its address. The UI lanes build
their fake `ISindriApi` on these files, so a change to one is a contract change; re-record with `AW_GOLDEN_UPDATE=1`.

**Who may change the files.** The files change only at these points, and each change is announced to the UI lane: (1) engine
P4b, if its wording moves (the engine re-records); (2) when a recipe that still carries a placeholder hash lands, its hash
becomes the real one; (3) the `not-found` files, when the served ProblemDetails body changes: all three are recorded from the served routes in API P11 (`ApiGoldenTests` for `workflow-recipe`, `ServedWorkflowRoutesTests` for `workflow-instance` and `workflow-instance-fact`; re-record with `AW_GOLDEN_UPDATE=1`); (4) P8 re-records the desk-runtime states when the agent runtime lands, and schema v2 re-records the fan-out previews (PRJ-sync and project-many-children); (5) `workflow-recipe/not-found.json` is re-recorded from `GET /workflow/recipes/{id}` (done in API P11): its code and status stayed, its `detail` changed; (6) P4c recipe schema: every hash, `name`, `inputs[].kind` / `required` / `generate`, the triage label. Every
instance fixture carries its own status (address, recipe stamp, commands and cancel sentence come from its row), the status
text is rendered from the fields, and its commands are catalog rows of the same class (`WorkflowViewFixtureTests`).
`WorkflowViewConsistencyTests` and `WorkflowViewRecipeShapeTests` pin that the files agree with each other and with
`recipes/**` (ids, versions, hashes, states, orders, terminals, inputs and outputs, desk flags, child waits, loop caps, timeouts and kinds), and `WorkflowEngineInvariantsTests` walks every instance fixture against the engine's rules (one current state, v1 children, desk runtime, timeouts, run tokens, park answers), and the golden test reads every file
back into its record. `ServedWorkflowRoutesTests` serves the routes from the time-skipping server and checks them against these shapes.

The recipe files (`workflow-recipe/`) hold `id`, `hash` and `version` (they identify the recipe the view describes) and the E-22 additions (inputs, outputs, urgency, `orderedBy`,
states, loops with their `cap`), not the engine's own fields (transitions, evals, pins, open instances), until UI U1. There is one file per recipe of `FixtureRecipes.All` (every recipe the instance, list and status goldens
name), named by the id (`workflow-recipe/workflow.step.json`, `workflow.project.json`, `workflow.incident-response.json`, and the other six of `FixtureRecipes.All`), built from the same declaration as those instances. Files are named by recipe id; key a fake by `id`. One file there is not a recipe: `workflow-recipe/not-found.json` is the served unknown-recipe refusal (404, `SND-RECIPE-002`), recorded from the real host by `ApiScenarios`/`ApiGoldenTests` (through the `{id}` route), not a `WorkflowViewFixtures` body; a fake answers it for any id it doesn't know and never keys it as a recipe (no recipe id can be `not-found`: ids start with `workflow.`). `workflow.no-outputs.json` is a fixture-only preview: no instance names it, and its optional input is a future schema. `WorkflowViewRecipeShapeTests` pins that every recipe an instance, list row or child group names has such a file and that it equals what the instance goldens show, in both directions: the hash, version and states, the loops as (from, to, cap, name) against the cells' loop pairs and the declaration, the declared inputs (key, label, kind, required) against the input slots, and the declared inputs and outputs (key, label, type, from) against the fact rows.

**`status.text` is rendered verbatim by the UI and never parsed.** The fixtures render it with a test-only renderer that
is a provisional rendering in ASCII: it differs from main's `StatusLayout` and `InstanceWords` in wording, wrap width (about 100 columns, main's 120)
and which layouts carry the timeout and budget lines (main prints them beside an open desk task only), and P4b reconciles them. Its wording, the `since` clock and token counts
are provisional until engine P4b confirms them (the change then goes through point (1) above). The cancel
sentence is singular at 1 (a provisional owner default, 2026-10-14 night).

**Marked previews.** Since P4c a supplied input is served `required: true` and a `generate` input `required: false`; every input of a started instance is present. `workflow-instances/preview-missing-input` (ITM-01JKH) is still a preview (a started instance never misses an input), like the fan-out previews; it gives the UI data for the missing and
blocked renders. `workflow-recipe/workflow.no-outputs`' `fact:context` is a real `generate` input. `workflow-status/needs-answer-step` is a Step's text-answered park and `needs-answer-wait` a wait's `--resume` park.

## Hook contract

None in S1. The `Hooks → Sindri` cli edge in `deps.yaml` (sindri-nudge) is the TS sindri's until the switch; the C#
app must provide what that hook reads before it replaces the TS one.

## Checks

```bash
dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=unit"          # domain, slices, CLI text
dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=integration"   # SQLite contracts, YAML files, the API on loopback, the scheduler
dotnet test --project tests/Aw.Sindri.Engine.Tests --filter-trait "Lane=integration"   # the real-Temporal suites (Workflows, CompleteStep, ApplyCommand), split out for LANE-PKG-001
scripts/binaries-lock.sh fetch temporal && scripts/binaries-lock.sh fetch temporal-test-server   # the Temporal tests and the smoke run these pins
bash src/Sindri/aot-smoke/run.sh                                                # publish framework-dependent and drive the published apphost (expected.txt)
```

The item detail route (`GET /sindri/items/{itemId}`) carries a `launch` block (modes, default agent, attach and resume targets, confirm, blockers; UI spec E-2), and doctor prints `launcher`, `launcher:agents` and `launcher:adoption` rows. The session launcher (`launcher.json`, the agent catalog, terminal templates and the T3 Code fallback) is in `docs/csharp/sindri-launcher.md`; the profile reference is in `docs/csharp/sindri-profile.md`.
