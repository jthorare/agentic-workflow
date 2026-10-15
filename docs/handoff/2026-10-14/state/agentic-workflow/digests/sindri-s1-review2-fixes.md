# S1 review run 2 — fix brief (lane A worker)

Worktree: `~/repos/agentic-workflow/.claude/worktrees/sindri-s1-app-shell`, branch `feat/sindri-s1-app-shell`,
PR #163, now at `0c3a2f1`. Read first: `~/.agentic-workflow/digests/sindri-lane-brief.md` (process rules, tool quirks,
the heavy wrapper; the scratchpad is shared, so keep your scratch files in your own subdirectory), then
`docs/csharp/sindri.md` and the C# rules. The review state is `~/.agentic-workflow/jthorare-agentic-workflow/reviews/163.json`
(run 2). The same rules as run 1 apply: TDD for each behaviour change, atomic commits, build and test only through heavy,
build each commit, take public-API lines from RS0016/RS0017, push at the end (no force-push). Don't run /review or the full gate.

## Fixes

1. **Approval key (issue).** `profile.approved:<hash>:<previous>` collides on A,B,A,B. Make the key unique per approval
   and stable across a retry. When the latest recorded `profile.approved` event already names `asked` (the event exists
   and only the save failed), don't append; just save. Otherwise key it `profile.approved:<hash>:after:<seq of the latest
   profile.approved event, or 0>`. Check that the preview (`Approved`), `status` (`NeedsApproval`) and `validate` agree
   after each step. Test: a, b, a, b, a gives five events, and every surface reports approved after each step. The retry
   after a failed save still records exactly one event. Fix the doc sentence about A, B, A (each approval is recorded).
2. **Stop vs renewal.** `StopProcessService`: call `shutdown.Request()` before `lease.Release(...)` (still after
   `held.Clear()`). In `LeaseRenewer`, when `held.Current` is no longer the grant it renewed, return quietly: it was a
   stop, not a loss. Test it: a renewal that runs right after the release exits 0 and logs no loss.
3. **Scheduler boundary.** In `Refresh` and `RunOne`, filter the catch on `!ct.IsCancellationRequested` instead of
   `e is not OperationCanceledException`, so a service's own timeout is logged and later slots still run. The logged
   error is `SindriErrors.ServiceFailed.With(...)`, not the exception's type name. Test: a service that throws
   TaskCanceledException doesn't stop the schedule.
4. **Start races.** `StartCommand`: launch only on SND-API-001; return SND-API-002 as it is. If the launched child exits
   with an SND-LEASE-001 line, keep polling the probe until the deadline (a concurrent start won), and fail only if the
   probe never answers. Test both paths over a fake launcher (item 5).
5. **Contract suites** for `IControlPlaneLauncher`/`ILaunchedProcess` (a fake with a scripted exit and last error; the
   real `ServeLauncher` against a stub executable in the integration lane) and `IShutdown` (`ProcessShutdown` and
   `FakeShutdown`: Request cancels Stopping, a second call is harmless, and it stays cancelled). In the launcher's real
   binding, check that the log is 0600 (a pre-created 0644 log is narrowed, and a fresh one is created 0600). When
   /usr/bin/perl exists, check that the child's session id (`ps -o sess= -p <pid>`) differs from the test process's.
6. **Error codes.** Add `SND-PROFILE-006` (approvals unreadable or unwritable, `Unavailable`) and use it for the
   `FileApprovals` read and save I/O paths. Add `SND-CLI-003` (invalid command input, `Invalid`) for a malformed hash
   in `profile approve` and a malformed `AW_SINDRI_PORT`. `SND-API-003` stays for a port that is really taken. Update the
   catalogue table, the CLI table and the tests.
7. **Composition.** Register `SchedulerHost` and `LeaseRenewer` in `AddSindriServe()` (Host), and give `HostTests` a
   case that composes and validates the serve composition, resolving the hosted services. Replace `PortFor`'s second
   container: resolve the approved `api.port` from the app's own container through a small service, or through
   GetStatus/ValidateProfile, before binding.
8. **Domain rule.** `ProfileRules.NeedsApproval(ProfileHash? approved, ProfileHash? live)` in Core, called by
   `GetStatusService`, with a unit test.
9. **Tests.** The renewer stop test: `ScriptedLease.Renew` requests shutdown and returns success; assert the grant
   isn't replaced and the exit code is 0. Skip the chmod-based failure tests when `Environment.IsPrivilegedProcess`
   (xUnit v3 `Assert.SkipWhen`). Add a comment on `SteppedClock.Armed` explaining that a zero-length wait arms no timer.
10. **Launcher.** perl: `exec @ARGV or die "SND-CLI-002 exec $ARGV[0]: $!\n"`, so `LastError` finds a line.
11. **Docs and comments.**
    - SND-API-002: "the ping within 2 s, or the call itself within 10 s".
    - The `YamlToJson` summary: "a subset of the YAML 1.2 core schema (null, booleans, decimal integers and decimals;
      no exponents, hex, octal or .inf/.nan)".
    - `OwnerOnly` and sindri.md: "the directory itself is made 0700; parents it creates keep the umask".
    - The no-ProblemDetails list: 400, 403, 404 and 405.
    - The new codes and the approval key.

## Report back
The commits (each builds), test counts (Sindri, Architecture), the AOT smoke result (rerun it under heavy; regenerate
`expected.txt` only if a journey's text changes, and say how), and anything not done, with the reason.
