# Sindri S1 — App Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (native: the session implements every task; the
> owner asked for a run to ready with no further questions). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `src/Sindri`, a new C# application: the `sindri` NativeAOT binary that is both the CLI and the control-plane
process (lease, ledger, YAML profile, command layer + loopback API + event stream, scheduler with a heartbeat, doctor).

**Team:** from S1's draft PR on, the epic runs as an agent team (`stages.md` "Parallel execution"): this session orchestrates; S1b, S2 and S3 run in parallel lanes, each a worker on `/specToProvenPR`.

**Architecture:** Full-shape package (Core · Features · Infrastructure · Host), scaffolded `--host http`. One binary,
two modes: `sindri serve` runs the control plane in the foreground (what bare `sindri` spawns detached, and what
launchd will run later); every other verb is a typed HTTP client of it, except `doctor` (local, read-only, because it
must work when the process is down). Every write goes through a Features service (§17.27.1) under the host lease's
fencing epoch; every state change is an enveloped, idempotent ledger event.

**Tech Stack:** .NET 10, ASP.NET Core minimal API (`CreateSlimBuilder`, RDG), `Microsoft.Data.Sqlite` via
`Aw.Kernel.Data`, System.Text.Json via `AwJson`, YamlDotNet 18.1.0 (parser + representation model only; MIT;
published 2026-06-26), xUnit v3 on MTP, Vogen typed ids.

**Spec:** factory addendum §17.29, §17.28 (S2 must fit), §17.20–§17.22 D2, §17.27.1; design spec §5.2, §9.1, §10.3,
§11.1; readiness §4a/§4b; `.agents/rules/csharp.md`, `csharp-testing.md`. Stage map: `stages.md` (same dir).
Verification plan: `../../proof/sindri-s1-app-shell/verification-plan.md` (created before any code).

## Global Constraints

- Reimplementation, not a port: TS `sindri/` is a reference for *what*; nothing matches TS output, files or schemas.
- No TS state read or written. C# state lives under `AW_SINDRI_HOME`, default `$AW_STATE_DIR/sindri-app`
  (`$AW_STATE_DIR` default `~/.agentic-workflow`), never `$AW_STATE_DIR/sindri/` (TS). Home 0700, files 0600.
- Nothing in `setup.sh`, providers, MCP registration or launchd changes.
- Errors: `SND-<AREA>-NNN` (areas used in S1: `LEASE`, `PROFILE`, `LEDGER`, `CLI`, `API`, `SCHED`), in
  `SindriErrors` + `docs/sindri/errors.md` gets a "C# app" section. Exit codes `0` ok, `1` attention, `2` error.
- Output: plain-text state words, `--json` on every read verb, `NO_COLOR` and non-TTY honoured.
- File ≤ 300 lines, method ≤ 40, complexity ≤ 10, params ≤ 5, ctor deps ≤ 5 (split signal) / 7 (error).
- Tenant and ring on every row: single values `local` / `0` until P4.
- API listens on 127.0.0.1 only (scaffold `LoopbackBinding`), port `AW_SINDRI_PORT` (default 7410; 0 = free port).
- Builds/tests under `~/.agentic-workflow/bin-r1b/heavy`. Commit messages in files (`-F`); the hook blocks "main".

## Review Focus

1. **Stale lease after a crash or sleep** — a `kill -9` or a laptop sleep must not lock the user out for longer than the
   TTL, and must never let two processes act: dead-holder proof (same boot, pid gone or start time differs) takes over
   at once; a live holder is refused; an expired-but-alive holder (paused) loses on its next renew and stops. Tests:
   `Lease/LeaseDecisionTests` (pure) and `LeaseContract` cases `takes_over_dead_holder`, `paused_holder_loses_on_renew`.
2. **Profile edited after approval** — the running process keeps the approved snapshot; doctor warns; approve shows the
   diff. Test: `ApproveProfile/…_runtime_uses_approved_snapshot_not_live_file`.
3. **Second `sindri` from another terminal while one is starting** — the client must not spawn a second server; a
   spawned server that loses the lease exits 1 with `SND-LEASE-001` and the client reports the winner. Test:
   `StartProcess/second_start_is_refused_while_lease_held`, plus journey 6.
4. **Stale endpoint file** (process died, `control.json` remains) — `status` says `not running`, never hangs: client
   timeout 2 s and epoch check against `/sindri/status`. Test: `ControlPlaneClientTests.stale_endpoint_reads_as_not_running`.
5. **Bad YAML** (tabs, duplicate keys, anchors, unknown keys, wrong types) — one clear `SND-PROFILE-002` with file, key
   path, expected type and a fix; never a stack trace. Test: `ValidateProfile/*` theory over fixtures.

---

## File structure

```
src/Sindri/
  Aw.Sindri.Core/
    SindriErrors.cs                 SND-* catalogue
    VogenDefaults.cs                (scaffold)
    Ids/HostId.cs, Ids/ProfileHash.cs, Ids/ServiceName.cs        typed ids (string)
    Lease/LeaseHolder.cs            {HostId Host, int Pid, DateTimeOffset PidStart, string BootId}
    Lease/LeaseGrant.cs             {LeaseHolder Holder, long Epoch, DateTimeOffset ExpiresAt}
    Lease/LeaseRow.cs               current row as read (holder, epoch, expires) or none
    Lease/LeaseDecision.cs          pure: Decide(row?, candidate, now, HolderLiveness) -> Take|Refuse(why)
    Lease/HolderLiveness.cs         enum Alive | ProvenDead | Unknown
    Lease/ILease.cs                 port: Acquire, Renew, Release, Read
    Lease/IProcessProbe.cs          port: Liveness(LeaseHolder) ; Self() -> LeaseHolder
    Lease/LeaseTiming.cs            TTL 60 s, renew 20 s (constants)
    Ledger/LedgerEvent.cs           stored event: Seq, Envelope fields, Epoch, IdempotencyKey, PayloadJson
    Ledger/NewEvent.cs              to append: type, idempotency key, payload json, item?
    Ledger/ILedgerStore.cs          port: Append(NewEvent, EventContext, LeaseGrant), ReadAfter(seq,max), SchemaStatus()
    Ledger/SchemaStatus.cs          {int Applied, int Expected}
    Profile/SindriProfile.cs        typed profile record (schemaVersion, user, tenant, repos, services, api)
    Profile/ServiceSettings.cs, Profile/RepoEntry.cs, Profile/ApiSettings.cs
    Profile/IProfileSource.cs       port: Load() -> Result<LoadedProfile> (typed + canonical bytes + path)
    Profile/LoadedProfile.cs
    Profile/IApprovals.cs           port: Latest() -> ProfileHash?, Snapshot(hash) -> LoadedProfile, Save(LoadedProfile)
    Profile/ProfileHashing.cs       pure: sha256 over canonical JSON
    Scheduling/IScheduledService.cs port: Name, RunOnce(ServiceRun, ct)
    Scheduling/ServiceSchedule.cs   pure: Due(now) / Next(after) per cadence
    Doctor/DoctorCheck.cs           {string Name, CheckState State, string Detail, string? Fix}
    Doctor/CheckState.cs            Ok | Warn | Fail
    Control/IControlPlaneClient.cs  client port (status, stop, profile approve/validate)
    Control/IEndpointFile.cs        port: Read/Write/Delete control.json {url, pid, epoch}
    SindriCoreServiceCollectionExtensions.cs
  Aw.Sindri.Features/
    StartProcess/   StartProcessRequest/Response/Service   acquire lease, record process.started
    GetStatus/      ...                                    lease, profile, services last run, api
    StopProcess/    ...                                    release lease, record process.stopped, signal shutdown (IShutdown port)
    ValidateProfile/ ...
    ApproveProfile/ ...                                    hash check, snapshot, profile.approved event (fenced)
    RunDoctor/      ...                                    the four checks
    RecordHeartbeat/ ...                                   the heartbeat service's write
    StreamEvents/   ...                                    events after seq (durable cursor)
    SindriFeaturesServiceCollectionExtensions.cs
  Aw.Sindri.Infrastructure/
    SindriSettings.cs, SettingsFromEnvironment.cs          home dir, port
    Ledger/Migrations/0001_events.sql, 0002_lease.sql, (outbox via SqliteOutboxSchema at 3)
    Ledger/SqliteLedgerStore.cs, Ledger/LedgerEventMapper.cs, Ledger/LedgerMigrations.cs
    Lease/SqliteLease.cs
    Lease/PosixProcessProbe.cs      kill(pid,0) via Process.GetProcessById + StartTime; boot id: sysctl kern.bootsessionuuid / /proc/sys/kernel/random/boot_id
    Profile/YamlProfileSource.cs    YamlDotNet RepresentationModel -> JsonNode -> AwJson typed (strict)
    Profile/YamlToJson.cs           node conversion; refuses anchors/aliases, duplicate keys, tags
    Profile/FileApprovals.cs        approved/<hash>/profile.json snapshots (0600)
    Control/HttpControlPlaneClient.cs, Control/FileEndpoint.cs
    SindriInfrastructureServiceCollectionExtensions.cs
  Aw.Sindri.Host/
    Program.cs                      Compose → TelemetryRuntime → Entry.Run(args)
    Composition.cs, SindriJsonContext.cs, LoopbackBinding.cs, LoopbackGuard.cs, Pipeline.cs, HttpResults.cs, ValueObjectValidation.cs (scaffold)
    Endpoints/EndpointTable.cs      MapGroup("/sindri"): GET status, POST stop, POST profile/validate, POST profile/approve, GET events (SSE)
    Server/ServeCommand.cs          acquire (StartProcess) → bind → write control.json → run scheduler + lease renewer → shutdown releases
    Server/SchedulerHost.cs         BackgroundService: loop over ServiceSchedule with TimeProvider
    Server/LeaseRenewer.cs          BackgroundService: Renew every 20 s; on loss → stop the app (exit 1)
    Server/HeartbeatService.cs      IScheduledService → RecordHeartbeat
    Commands/CommandTable.cs        sindri | --headless | serve | status | stop | profile validate|approve | doctor
    Commands/StartCommand.cs        spawn `sindri serve` detached if not running; wait ≤ 10 s for control.json + status; print banner + summary
    Commands/Banner.cs, Commands/SummaryText.cs, Commands/CommandOutput.cs, Commands/Terminal.cs (TTY/NO_COLOR)
tests/Aw.Sindri.Tests/ (by feature folders matching the slices + Lease/, Ledger/, Profile/, Server/, Commands/)
tests/Aw.Sindri.Testing/ A.cs builders, InMemoryLedgerStore, InMemoryLease, FakeProcessProbe, LedgerStoreContract, LeaseContract
docs/csharp/sindri.md               the C# contract: verbs, routes, exit codes, files, schema, decisions
src/Sindri/aot-smoke/run.sh + expected.txt
```

## Tasks

### Task 0: Pin YamlDotNet, scaffold the package

**Files:** `Directory.Packages.props` (+ lock refresh), then the scaffold output, `deps.yaml` (Sindri node note; edge
`Sindri → Kernel` project edges come from the scaffold), `AgenticWorkflow.slnx`, `tests/Aw.Architecture.Tests/Packages.cs`.

- [ ] Add `<PackageVersion Include="YamlDotNet" Version="[18.1.0]" />`, restore, commit alone
      (`chore: pin YamlDotNet 18.1.0 for the Sindri profile reader`).
- [ ] `node scripts/scaffold/new-package.mjs --name Sindri --tier 3 --error-prefix SINDRI --shape full --host http
      --binary sindri --first-slice GetStatus --behavior reports_not_running_when_no_lease_is_held --route "GET /sindri/status"`
      (if `--error-prefix` must be AW-form, convert to `SND-` codes in Task 1). Make the Host an AOT entry point (scaffold
      http hosts: check `AwAotEntryPoint`/`PublishAot` lines; add them if absent, as the cli template does).
- [ ] Heavy build + unit lane: the scaffold's one red test fails, everything else green. Commit red
      (`feat: scaffold the Sindri app package (S1)`).

### Task 1: Errors and typed ids (Core)

- [ ] Tests `SindriErrorsTests.catalogue_validates` (exists) and `Ids/*JsonRoundTripTests` for `HostId`, `ProfileHash`
      (64 lowercase hex), `ServiceName` (kebab-case, ≤ 40).
- [ ] `SindriErrors`: `LeaseHeld` SND-LEASE-001 conflict ("held by {host}/{pid} since {t}"), `LeaseLost` SND-LEASE-002,
      `LeaseStale` SND-LEASE-003 (epoch not current), `ProfileMissing` SND-PROFILE-001 notFound, `ProfileInvalid`
      SND-PROFILE-002 invalid, `ProfileHashMismatch` SND-PROFILE-003 conflict, `ProfileNeedsTerminal` SND-PROFILE-004,
      `ProfileNotApproved` SND-PROFILE-005, `LedgerSchema` SND-LEDGER-001, `LedgerDuplicate` SND-LEDGER-002 (idempotent
      replay, reported as success-no-op by callers), `NotRunning` SND-API-001 unavailable, `ApiTimeout` SND-API-002,
      `UnknownVerb` SND-CLI-001, `Usage` SND-CLI-002, `ServiceFailed` SND-SCHED-001.
- [ ] Commit.

### Task 2: Lease decision (pure, Core)

Tests (`Lease/LeaseDecisionTests`, unit): `free_row_is_taken_at_epoch_one`; `expired_row_is_taken_with_epoch_plus_one`;
`live_holder_is_refused_with_who_and_since`; `proven_dead_holder_is_taken_before_expiry`; `unknown_liveness_waits_for_expiry`;
`same_holder_reacquire_is_refused` (a second serve in the same pid can't happen; refuse rather than re-enter);
`renew_by_current_epoch_extends_ttl`; `renew_by_stale_epoch_is_lease_lost`.
- [ ] Write tests → red → implement `LeaseDecision.Decide(LeaseRow? row, LeaseHolder candidate, DateTimeOffset now,
      HolderLiveness liveness) -> Result<LeaseGrant>` and `LeaseDecision.Renew(LeaseRow row, LeaseGrant grant, now)` →
      green → commit.

### Task 3: Ledger store port, contract, SQLite adapter

`ILedgerStore`:
```csharp
ValueTask<Result<LedgerAppend>> Append(NewEvent e, EventContext context, LeaseGrant grant, CancellationToken ct); // Appended(seq) | AlreadyRecorded(seq)
ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long seq, int max, CancellationToken ct);
ValueTask<Result<SchemaStatus>> Schema(CancellationToken ct);
```
Append checks, in one `BEGIN IMMEDIATE`: the lease row's epoch equals `grant.Epoch` and holder matches (else
SND-LEASE-003); idempotency key unique (replay → `AlreadyRecorded`, no second row). Columns: `seq` INTEGER PK, `id`,
`type`, `schema_version`, `occurred_at`, `actor`, `tenant`, `ring`, `item` NULL, `correlation_id`, `causation_id`,
`trace_id`, `epoch`, `idempotency_key` UNIQUE, `payload_json`. Lease table: single row (`id = 1` CHECK), `host`, `pid`,
`pid_start`, `boot_id`, `epoch`, `acquired_at`, `expires_at`; epoch never decreases (kept on release: release sets
`expires_at = now`, holder columns stay for "last holder"). Migration 3 = `SqliteOutboxSchema.CreateMigration(3)`.
- [ ] `LedgerStoreContract` (Testing) cases: `append_then_read_after_returns_it_with_envelope`; `replay_same_key_is_already_recorded_once`;
      `append_with_stale_epoch_is_refused_and_writes_nothing`; `read_after_is_ordered_and_bounded`; `schema_reports_applied_and_expected`.
- [ ] `InMemoryLedgerStore` + `SqliteLedgerStore` both pass (`InMemoryLedgerStoreContractTests` unit,
      `SqliteLedgerStoreContractTests` integration on a temp file). Migration test: each migration applies on a copy of the
      previous schema; `outbox_commands` exists after 3.
- [ ] Commit.

### Task 4: Lease port, contract, SQLite adapter, process probe

`ILease`: `Acquire(LeaseHolder, ct) -> Result<LeaseGrant>`, `Renew(LeaseGrant, ct) -> Result<LeaseGrant>`,
`Release(LeaseGrant, ct) -> Result<Unit>`, `Read(ct) -> Result<LeaseRow?>`. The SQLite adapter reads the row, asks
`IProcessProbe.Liveness(row.Holder)` only when the row is unexpired and the host matches, and applies `LeaseDecision`
in one `BEGIN IMMEDIATE`.
- [ ] `LeaseContract` cases: `first_acquire_gets_epoch_one`; `second_acquire_refused_while_live`; `takes_over_dead_holder`
      (FakeProcessProbe → ProvenDead) with epoch+1; `release_then_acquire_increments_epoch`; `paused_holder_loses_on_renew`
      (FakeTimeProvider past TTL, another holder acquires, first renew → SND-LEASE-002); `release_by_stale_grant_is_refused`.
- [ ] `InMemoryLease` (unit) and `SqliteLease` (integration) pass. `PosixProcessProbe` integration tests:
      `self_is_alive`; `exited_child_pid_is_proven_dead` (start `/bin/sh -c exit`, wait, probe); `different_boot_id_is_proven_dead`.
- [ ] Commit.

### Task 5: Profile — YAML reader, typed record, hashing, approvals

Profile v1 (strict, unknown keys refused):
```yaml
schemaVersion: 1
user: thor
tenant: local          # optional, default local
repos: []              # optional: [{ name, path }]
services:
  heartbeat: { every: 30s }   # cadence: <n>s|m|h, 5s..24h
api: { port: 7410 }    # optional
```
- [ ] `YamlToJson` tests (unit): mapping/sequence/scalars to JSON; `duplicate_key_is_refused_with_line`;
      `anchor_or_alias_is_refused`; `tagged_scalar_is_refused`; `tab_indentation_reports_line_and_column`. Scalars: plain
      `true/false` → bool, integers → number, `null`/`~` → null, everything else string (YAML 1.2 core, no `yes/no`).
- [ ] `YamlProfileSource` tests: valid fixture loads; theory over invalid fixtures (unknown key, wrong type, missing
      `user`, cadence `0s`, schemaVersion 2) → SND-PROFILE-002 whose message names file, key path, expected type, fix.
      Missing file → SND-PROFILE-001 with fix `create profile.yaml (see docs/csharp/sindri.md)`.
- [ ] `ProfileHashing`: hash = sha256 hex of `AwJson.Serialize(profile)` (canonical: typed, defaults applied), so
      comment-only edits don't change the hash. Test `comment_only_edit_keeps_hash`, `value_edit_changes_hash`.
- [ ] `FileApprovals` (integration): `save_then_latest_returns_hash`; snapshot file mode 0600, dir 0700; `latest_is_most_recent_approval`.
- [ ] Commit.

### Task 6: Slices — StartProcess, GetStatus, StopProcess, RecordHeartbeat, StreamEvents

Each via `/new-slice --files-only` (copy RS0016 lines from the build output), red first, then green.
- `StartProcess` (`StartProcessService(ILease, IProcessProbe, ILedgerStore, IApprovals, EventContextFactory)`):
  acquire → append `process.started` {pid, epoch, profileHash?} key `process.started:{epoch}` → response {grant, approved hash?}.
  Tests: `starts_and_records_epoch`; `second_start_is_refused_while_lease_held` (SND-LEASE-001, no event written).
- `GetStatus`: lease row + liveness, approved hash, live-profile hash (changed?), last run per service (from ledger
  `service.*` events), api url. Test `reports_not_running_when_no_lease_is_held` (the scaffold's red test),
  `reports_heartbeat_age_and_epoch`, `flags_profile_changed_since_approval`.
- `StopProcess`: append `process.stopped` (fenced), release lease, `IShutdown.Request()`. Tests: `stop_releases_and_records`,
  `stop_with_stale_grant_is_refused`.
- `RecordHeartbeat`: append `service.heartbeat` {at} key `service.heartbeat:{epoch}:{slot}` where slot = scheduled time.
  Test `replayed_slot_is_a_noop`.
- `StreamEvents`: `ReadAfter(seq, 100)`; Host turns it into SSE with a 1 s poll. Test `returns_events_after_cursor`.
- [ ] DI test (`HostTests`) resolves every `IService<,>`. Commit per slice.

### Task 7: Slices — ValidateProfile, ApproveProfile, RunDoctor

- `ValidateProfile`: load live profile → {hash, valid} or SND-PROFILE-002.
- `ApproveProfile(hash?, confirmed)`: no hash → returns {hash, diff vs latest approval (key paths changed)} and changes
  nothing; with hash → must equal live hash (SND-PROFILE-003), `confirmed` must be true (the CLI sets it only after the
  TTY prompt; SND-PROFILE-004 otherwise), saves snapshot, appends `profile.approved` {hash} key `profile.approved:{hash}`.
  Test `runtime_uses_approved_snapshot_not_live_file` (scheduler cadence read from approvals, not the live file).
- `RunDoctor` (local, read-only ports): checks `lease` (ok held epoch N by pid | ok free | warn expired holder alive |
  fail held by a dead holder → fix `sindri`), `ledger schema` (ok n/n | fail skew → fix), `profile` (ok approved |
  warn changed since approval | fail none approved / invalid), `api` (ok reachable + epoch matches | fail unreachable →
  fix `sindri` | warn epoch mismatch). Exit 0 / 1 any warn / 2 any fail. Tests per state.
- [ ] Commit per slice.

### Task 8: Host server mode — serve, endpoints, SSE, scheduler, renewer

- Endpoints in `EndpointTable` (one `Handle` each; SSE via `TypedResults.ServerSentEvents`).
- `ServeCommand`: `StartProcess` (refused → stderr `SND-LEASE-001 …`, exit 1) → build `LoopbackBinding` with the port →
  start → write `control.json` {url, pid, epoch} 0600 → run until shutdown (SIGTERM, SIGINT, `/sindri/stop`) → on exit:
  `StopProcess` if not already stopped, delete `control.json`. SIGHUP ignored (the detached child must survive the
  terminal closing).
- `SchedulerHost`: reads approved profile's services; for each registered `IScheduledService` whose name is in the
  profile, runs on cadence via `TimeProvider` timers; a failure logs `SND-SCHED-001` and the next slot still runs;
  appends `service.failed`.
- `LeaseRenewer`: renew every 20 s; on SND-LEASE-002 logs and stops the host with exit 1 (fencing: no further writes).
- Tests (integration, `WebApplicationFactory`/in-proc with fakes + FakeTimeProvider): `status_endpoint_returns_json`;
  `stop_endpoint_releases_lease_and_shuts_down`; `events_endpoint_streams_heartbeats`; `scheduler_runs_heartbeat_on_cadence`
  (advance fake time 3 × cadence → 3 events); `failing_service_does_not_stop_the_schedule`; `lost_lease_stops_the_host`.
  Loopback tests come from the scaffold.
- [ ] Commit.

### Task 9: Host client mode — CLI verbs, start/spawn, banner, output

- `CommandTable`: `[]` → start + UI note; `--headless` → start (no UI note); `serve`; `status [--json]`; `stop`;
  `profile validate [--json]`; `profile approve [<hash>]`; `doctor [--json]`; anything else → SND-CLI-001 + usage, exit 2.
- `HttpControlPlaneClient` (Infrastructure, port in Core): reads `control.json`, 2 s timeout; connection refused or
  epoch mismatch → SND-API-001 not running. Test `stale_endpoint_reads_as_not_running` (file points at a closed port).
- `StartCommand`: running → print banner + summary; not running → spawn `Environment.ProcessPath serve` detached
  (stdout/stderr → `$HOME/logs/serve.log`), poll status ≤ 10 s; refused child → report the winner from status. Banner is
  ASCII only; summary lines: `profile`, `services`, `api`, `ui` (`not built yet (S1b)`; omitted under `--headless`),
  `lease`, `ready`. Exit 1 when the profile isn't approved (attention), 0 otherwise.
- `profile approve <hash>`: requires an interactive TTY (`Console.IsInputRedirected` false), prompts for the first 6
  hash chars, then calls the API with `confirmed: true`; non-TTY → SND-PROFILE-004 exit 2.
- `Terminal`: no ANSI when `NO_COLOR` set or stdout redirected; state always a word.
- Tests (unit): `CommandTable` routing + unknown verb; `SummaryText` golden (`Golden/summary-approved.txt`,
  `summary-unapproved.txt`); `no_ansi_when_no_color`; `approve_without_tty_is_refused`.
- [ ] Commit.

### Task 10: AOT smoke, docs, gate files

- `src/Sindri/aot-smoke/run.sh` (copy the Packets one's shape): publish to a temp dir with lock files set aside, run
  with a temp `AW_SINDRI_HOME`: `profile validate`, `--headless`, `status --json`, second `serve` (expect exit 1),
  `stop`, `doctor` (expect exit 2), compare with `expected.txt` (pids/ports/times masked).
- `docs/csharp/sindri.md`: verbs, routes, exit codes, files and modes, schema, events and keys, decisions (doctor is
  local; profile YAML via AwJson; terminal-bound approve enforced in the CLI in S1, device passkeys later; no API token
  in S1, loopback guard only; state dir `sindri-app`).
- `docs/sindri/errors.md` C# section; `docs/sindri/README.md` pointer; `AGENTS.md` commands block (`dotnet run --project
  src/Sindri/Aw.Sindri.Host -- …`, the aot-smoke line); `scripts/ci/changed-areas.sh` if it lists src areas;
  `rules/` records if a new check id is added (none planned); `.env.example` gets `AW_SINDRI_HOME`, `AW_SINDRI_PORT`.
- [ ] Full merge gate (AGENTS.md "Merge Gate") under heavy. Commit.

## Self-review

- Spec coverage: CLI (T9), process + lease + fencing (T2, T4, T8), profile (T5, T7), ledger + envelope + idempotency +
  outbox (T3), command layer + API + stream (T6–T8), scheduler + heartbeat (T6, T8), doctor (T7), UI → S1b (stages.md).
- S2 fit: `ILedgerStore.Append` is fenced and idempotent by key, which the engine's transitions need; `LeaseGrant`
  carries the epoch every transition checks; the YAML reader is the recipe reader.
- Types: `LeaseGrant`, `LeaseRow`, `LeaseHolder`, `ILedgerStore.Append(NewEvent, EventContext, LeaseGrant, ct)` used
  consistently in T3, T4, T6, T8.
