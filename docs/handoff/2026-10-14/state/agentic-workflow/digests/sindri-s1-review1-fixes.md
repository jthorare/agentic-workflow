# S1 review run 1 — fix brief (lane A worker)

Worktree: `/Users/thor/repos/agentic-workflow/.claude/worktrees/sindri-s1-app-shell`, branch `feat/sindri-s1-app-shell`, PR #163.
Read first: `~/.agentic-workflow/digests/sindri-lane-brief.md` (process rules, tool quirks, heavy wrapper), then
`docs/csharp/sindri.md`, `.agents/rules/csharp.md`, `.agents/rules/csharp-testing.md`. The review state is
`~/.agentic-workflow/jthorare-agentic-workflow/reviews/163.json` (run 1; "fix" items below; leave "written" ones alone).

Rules: TDD for every behaviour change (red test first, in the same commit or the one before). Atomic commits, one
logical fix each, `fix:`/`test:`/`docs:`. Build and test only through `~/.agentic-workflow/bin-r1b/heavy`. Keep
`dotnet build AgenticWorkflow.slnx -warnaserror`, `dotnet format AgenticWorkflow.slnx --verify-no-changes`, the
Sindri tests and `tests/Aw.Architecture.Tests` green. Public-API files: take the lines from the build's RS0016/RS0017,
never by hand. Push when done (no force-push). Don't run the full merge gate (the orchestrator does), and don't run `/review`.

## Fixes

### A. Approval flow (ApproveProfileService)
1. Read `held.Current` once into a local; pass it to `Refusal` and use it for the append (no `!`).
2. **Append the `profile.approved` event first, then `approvals.Save`.** A failed append leaves `approvals.Latest`
   unchanged. A retry after a failed save works: the append returns AlreadyRecorded, then the save runs. Also make
   "already approved" check the ledger too, so a missing event gets recorded.
3. Idempotency key `profile.approved:<hash>:<previous approved hash or "none">`, so re-approving an earlier hash
   (A → B → A) is recorded. Update the key table in docs.
4. Tests: a store whose append fails leaves the approval unchanged; A → B → A records three events; a retry after a
   failed save completes.

### B. Lease and renewer
5. `LeaseDecision.Acquire`: refuse an **expired** lease whose holder is proven **Alive** on this host (only `Unknown`
   holders from another machine wait for expiry; `ProvenDead` is taken at once). The adapters must therefore ask about
   liveness for any same-host row, live or expired. Update the contract test
   `A_paused_holder_loses_on_renew_after_another_took_over` so its paused holder is on another host (Unknown), and add
   a test for "expired but alive on this host is refused".
6. `LeaseDecision.Liveness`: start times differing by at most 2 s count as the same process (Linux jitter).
7. `LeaseRenewer`: only `SND-LEASE-002` (LeaseLost) stops the process. Other errors are logged and retried at the
   next tick, until `now >= grant.ExpiresAt`; past expiry, stop. Exit code through an injected sink (a small Host
   interface over `Environment.ExitCode`), not set directly, so the test never touches process state.
8. Stop/renew race: `HeldLease` gets a compare-and-set (`Replace(expected, next)`, which holds the new grant only if
   `Current` is still `expected`). The renewer uses it, and stops renewing once `IShutdown`'s stopping signal fires.
   StopProcess clears first, then releases.

### C. Control plane process
9. `ServeLauncher`: start serve in its own session, so Ctrl-C during the start wait or a process-group kill doesn't
   reach it: `/usr/bin/perl -MPOSIX -e 'POSIX::setsid(); exec @ARGV'` (fall back to plain exec when perl is missing),
   with `umask 077` in the shell line and `OwnerOnly.RestrictFiles(log)` after the launch, so serve.log is 0600.
10. `OwnerOnly.EnsureDirectory`: chmod 0700 only when this call created the directory.
11. Port: `ServeCommand` validates `AW_SINDRI_PORT` before taking the lease (an integer 0..65535; anything else →
    stderr `SND-API-003 … (fix: …)`, exit 2). `Listen` also catches `SocketException` (privileged port, refused bind)
    and handles it like `IOException`. `ProfileRules`: `api.port` 0 or 1024..65535.
12. `SchedulerHost`: catch exceptions at the `Refresh` and `RunOne` boundaries (Host may catch `Exception`), log
    `SND-SCHED-001`, keep the schedule going. File adapters (`FileApprovals`, `FileEndpoint`): map
    `IOException`/`UnauthorizedAccessException` to SND-PROFILE-005 / SND-LEDGER-002 instead of throwing.
    Tests: a failing service doesn't stop later slots; an unknown service name is logged and skipped; an unreadable
    approval doesn't crash the scheduler.

### D. Client
13. `HttpControlPlaneProbe`: accept a ping only when both pid **and** epoch match the record.
14. `ControlPlaneClient`: catch `TaskCanceledException` (when not caller-cancelled) → SND-API-002.
    `Verbs.Status`: the "not running:" prefix only for SND-API-001; SND-API-002 keeps its own message (exit 2).

### E. Shape and domain
15. `GetStatusResponse.Profile.NeedsApproval` (bool), computed in `GetStatusService`. The CLI uses it for the exit
    code and the ready line; delete `SummaryText.NeedsApproval`. (Lane B, the UI, will use the field.)
16. Move `HeartbeatService` to Features (`RecordHeartbeat/HeartbeatService.cs`, internal, registered in
    `AddSindriFeatures`). Host's own registrations (`ProcessShutdown`, `IShutdown`, the exit-code sink) go in an
    `AddSindriHost()` extension in Host, so `Compose` only calls `Add…()` methods, registers the clock and passes the
    environment (plus the AwJson options singleton the rules allow).
17. `StopReason` enum in Core (`Stop`, `Signal`, `PortInUse`, `LeaseLost` if used) for `StopProcessRequest.Reason` and
    `ProcessStopped.Reason` (STJ writes enums through the context; add the type to `SindriJsonContext` if needed).
18. `EventTypes.ServiceRun(ServiceName)` returns `service.<name>`; `GetStatusService` and the heartbeat use it.
19. Fix the Core csproj comment (it is the TS deps note), and add `sindri-app/ledger.db` to the Sindri node's `db` list
    in `deps.yaml` (keep `node scripts/check-deps.mjs` green; if a `db` path isn't allowed there, put it in the note).

### F. Tests
20. Contract suites in `Aw.Sindri.Testing`, each bound to its fake (unit) and real adapter (integration):
    `ProcessProbeContract` (self alive; a gone pid → Running=false, StartedAt null; a reused pid's start time differs),
    `EndpointFileContract` (fake + `FileEndpoint`: write then read; a damaged record reads as none; Remove only for the
    same pid), `ProfileSourceContract` (fake + `YamlProfileSource`: valid loads, missing → SND-PROFILE-001, invalid →
    SND-PROFILE-002; the base gets abstract hooks to put a valid, invalid or no profile in place), and
    `ControlPlaneProbeContract` (no record → SND-API-001; a recorded live endpoint answers; a pid or epoch mismatch →
    SND-API-001). Bind the real probe against a minimal loopback server in the integration lane, plus a stale record
    (closed port) and a timeout (a listener that never answers → SND-API-002).
21. Failure paths the doc promises: serve with the port taken → exit 2 SND-API-003 (integration: bind a listener
    first); `profile validate` exits 0/1/2; approve with a stale hash → exit 2; unknown verb → exit 2 SND-CLI-001;
    a ledger with a newer schema → doctor `fail ledger` (`DoctorRules.Ledger`).
22. `SchedulerHostTests`: the negative test waits for a counted loop iteration (a probe `IApprovals` counting
    `Latest` calls), not a real-time sleep. Drop the 100 ms sleep in the lost-lease test. Move the two scheduler
    tests to the unit lane if they no longer need real time.
23. `ServeTests`: remove the `[Collection(nameof(ProcessEnvironment))]` (it sets no env vars). Remove the unused
    `A.GetStatusRequest()` builder (and its builder file) or use it. Drop the no-op clock assertion in `RunDoctorTests`
    and rewrite its `is var p` tuple assertion plainly. Move `Settings_default_…` out of `LedgerFileTests` into its own file.

### G. AOT smoke and docs
24. `src/Sindri/aot-smoke/run.sh`: bound every invocation (perl alarm 30 s; print `exit 124` on timeout). Anchor the
    host-name mask on the `held by` text. If the journey output changes because of the fixes, regenerate `expected.txt`
    and check the diff by eye. Run it under heavy; it must print `aot-sindri-smoke: ok`.
25. `docs/csharp/sindri.md`: the epoch check (now true); serve.log 0600 (now true); `status` exits 2 on SND-API-002;
    YAML: "a subset of the YAML 1.2 core schema (null, booleans, decimal integers and decimals; everything else is a
    string)"; the guard's 400/403 and unknown routes have no ProblemDetails body; the approve route's `confirmed` is the
    CLI's claim, and the terminal check is the only gate in S1; the new approval order and key; port validation;
    detached in its own session; the renewer's retry rule; expired-but-alive refusal; the 2 s start-time tolerance.
    AGENTS.md's smoke line: "…a refused second serve, the approve preview and its non-terminal refusal, status, stop
    and doctor…".

## Report back
List the commits, the test counts (Sindri, Architecture), the AOT smoke result, and anything you couldn't do and why.
