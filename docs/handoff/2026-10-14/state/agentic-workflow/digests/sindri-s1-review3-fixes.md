# S1 review run 3 — fix brief (lane A worker)

Worktree `/Users/thor/repos/agentic-workflow/.claude/worktrees/sindri-s1-app-shell`, branch `feat/sindri-s1-app-shell`,
PR #163, now at `c23529b2`. Read `~/.agentic-workflow/digests/sindri-lane-brief.md` first; keep scratch files in your own
scratchpad subfolder (`scratchpad/s1-laneA/`). The same rules as before: red-first for each behaviour change, atomic
commits that each build, heavy for builds and tests, public-API lines from RS0016/RS0017, push at the end (no force-push).
Don't run /review or the full gate. Review state: `~/.agentic-workflow/jthorare-agentic-workflow/reviews/163.json` (run 3).

## Fixes

1. **Errors without a body (issue).** `ControlPlaneClient.Problem`: a non-2xx answer with no ProblemDetails code must not
   become SND-API-001. Add `SND-API-004` ("the API answered HTTP <n> without an error code", `Unavailable`) and exit 2.
   Also make the server write ProblemDetails for an unhandled exception: an exception handler in `Pipeline` that maps it
   to a 500 ProblemDetails with a catalogue code (add `SND-API-005` "internal error in the control plane", or reuse a
   fitting one), with the exception logged. Map a binding or validation failure (the RDG's 400 and
   `ValueObjectValidation`'s 400) to a ProblemDetails with `SND-CLI-003` where you can, or document that it has no body.
   Tests: a handler that throws → the CLI prints the catalogue code and exits 2, never "not running"; an approve with a
   malformed hash body → 400 with a code (or the documented behaviour).
2. **This child's log only.** `ServeLauncher.Launch` records `serve.log`'s length before starting the child.
   `LastError()` reads only the lines after that offset. Extend `ControlPlaneLauncherContract` or the real binding test
   so an old `SND-LEASE-001` line from an earlier run doesn't count.
3. **The deadline message keeps the holder.** When the deadline passes after the child exited, `StartCommand` adds
   `: the started one exited with <its last SND line>` to the message.
4. **StartCommand on a TimeProvider.** Inject `TimeProvider` (from CommandTable's clock) and use it for the deadline and
   the poll delay. Its tests drive a `FakeTimeProvider` or `SteppedClock` and assert outcomes and probe calls, not elapsed
   wall time. Remove the `TimeSpan? budget` constructor parameter if it's no longer needed.
5. **Fail closed on the listener.** If a serve host composed with `AddSindriServe()` starts without `ServePort.Listen`
   having run, startup fails (throw in the Kestrel callback or assert in ServeCommand) instead of Kestrel binding
   localhost:5000. Test it. Use `ServePort.Chosen` for this check, or remove it.
6. **A clean end on stop mid-call.** EventStream: a cancellation from `Stopping` during `service.Handle` ends the stream
   cleanly (restructure so the read's `OperationCanceledException` is caught when the token is cancelled, then
   `yield break`). LeaseRenewer: catch `OperationCanceledException` around `Renew` when stopping, and return. Tests for
   both, bounded.
7. **Approved, one rule.** `ValidateProfile` and `GetStatus.NeedsApproval` use the same "approved" rule as the approve
   preview (saved AND the latest recorded event names the live hash). Put the shared decision in Core next to
   `ProfileRules.NeedsApproval`, and test the save-failed-then-edited edge.
8. **The default port test.** Replace `Without_an_approval_the_port_is_the_default` with a real test: resolve
   `ServePort` from the serve composition with no approval and no variable, call `Listen`, and assert 7410 and the
   listen options without starting the app. Or drop it.
9. **Docs.** Remove the stray comma in the serve row. Directory modes: "Each directory Sindri asks for (the home,
   `approved/…`, `logs`) is made 0700 when Sindri creates it; parents created along the way keep the umask, and an
   existing directory keeps its mode; files are 0600." List the answers without a ProblemDetails body accurately after
   item 1, add the new codes to the catalogue, and add `SND-API-004` to the exit table.

## Report back
The commits, test counts (Sindri, Architecture), the AOT smoke result (under heavy; touch `expected.txt` only if a
journey's text changes, and say how), and anything not done, with the reason.
