# S1 review run 4 — fix brief (lane A worker)

Worktree `~/repos/agentic-workflow/.claude/worktrees/sindri-s1-app-shell`, branch `feat/sindri-s1-app-shell`,
PR #163, now at `e8ee0ce1`. Read `~/.agentic-workflow/digests/sindri-lane-brief.md` first. Scratch files go only in
`scratchpad/s1-laneA/`. The same rules apply: red-first for each behaviour change, atomic commits that each build (no
commit whose tests fail at it), heavy for builds and tests, public-API lines from RS0016/RS0017, push at the end (no
force-push). Don't run /review or the full gate. Review state: `~/.agentic-workflow/jthorare-agentic-workflow/reviews/163.json` (run 4).

**The next review, run 5, is the last one allowed.** So get this round complete and consistent: after it, every surface,
doc and XML comment must agree with the code.

## Fixes

1. **One approval rule, everywhere (blocking).**
   - `RunDoctorService`/`DoctorRules.Profile`: judge with `ProfileRules.IsApproved`/`NeedsApproval`, using the
     recorded approval. "Saved but not recorded" or "not the latest recorded" is a `warn` with the fix
     `sindri profile approve`.
   - `SummaryText.Profile`: when `NeedsApproval` is true and the live hash equals the saved one, show "approval not
     recorded in the ledger; fix: sindri profile approve". Add a flag to `ProfileStatus` if needed.
   - Add doctor and the summary to the "every surface agrees" test (saved == live with no recorded event, and the
     save-failed-then-edited-back case).
   - Then make sure the XML on `IsApproved` and the doc's "every surface" wording are literally true.
2. **A ledger read error is not "not approved".** `RecordedApproval.Latest` returns `Result<RecordedApproval?>` (Ok(null)
   means none recorded; the ledger's error otherwise). Validate returns the error (exit 2). Status puts it in
   `ProfileStatus.Problem`. Approve's preview refuses rather than appending with `after:0`. Move the ledger-reading helper
   out of Core's public API into Features (internal), keeping only pure decisions in Core. Test with a ledger fake that
   fails reads.
3. **Pin the host environment.** `WebApplicationOptions { Args = [], EnvironmentName = Environments.Production }` in
   `LoopbackBinding`. Test: with `ASPNETCORE_ENVIRONMENT=Development` set (in the `ProcessEnvironment` collection), a
   malformed approve body still gets a 400 with `SND-CLI-003`.
4. **Listener check before the lease.** In `ServeCommand`, check `ServePort.Chosen` right after `Listen(...)` and before
   `StartProcess`, and refuse with an `SND-` line and exit 2. Keep `ServeListenerCheck` as a backstop, and make sure its
   failure can't leave the lease held without a stop record. Test it.
5. **No framework message in a response.** `ValueObjectValidation` writes fixed text (the method, the path, and "a value
   in the request isn't valid for this route") and logs the exception message.
6. **Exit-code docs.** Both approve rows say "1 when it isn't running (`SND-API-001`)". The `<hash>` row's exit-2 list
   gets "2 on any other error (e.g. …)". So does the stop row. Check every row of the CLI table against
   `Output.Error`/`Verbs` once more.
7. **Wording.**
   - `SND-CLI-003` scope: bad CLI input, plus a refused API body or value; fix it in the doc and the XML.
   - `SND-API-004`'s summary: drop "a body it couldn't read".
   - The no-body list: add 415, or drop "Only".
   - `ServePort.Chosen`'s doc: point at `ServeListenerCheck` and say it right.
   - The `SND-API-005` detail: "the control plane logged it", without naming serve.log.
   - The `ProfileStatus.NeedsApproval` param doc: describe the current rule.
   - `ILaunchedProcess.LastError` and the doc: "only lines written since this start count, not an earlier run's".
   - `ServeLauncherContractTests`: `Assert.Equal("no error line in the log", child.LastError())`.

## Report back
The commits (each builds and its tests pass), the test counts (Sindri, Architecture), the AOT smoke result (under heavy),
and anything not done, with the reason.
