# Engine handoff: 2026-10-22

Lane: engine (sindri-engine). Written at the team handoff, about 04:55Z. main is at `6a674c43d` (#595).

Box: `gh` is joithor, which has no push or merge rights on jthorare. Push to `jfork`/`upfork` (joithor/agentic-workflow-jthorare)
and open PRs with `--head joithor:<branch>`. The owner merges. Run heavy dotnet work through `heavy dotnet …` (~/.local/bin/heavy).

## PRs and branches in flight

| Unit | PR | Branch (jfork) | Head | State | What's left |
|------|----|----------------|------|-------|-------------|
| ci.yml scrub fix | #596 | fix/ci-recipe-versions-token | `2e7ad0b49` | Open, meta gate PASSED, merge-check READY | The owner merges it. Until it lands, any merge of main that stages `.github/workflows/ci.yml` is refused by the pre-commit scrub (SND-SCRUB-002 on ci.yml:334). Fix without `--no-verify`: `git diff 6a674c43d 2e7ad0b49 -- .github/workflows/ci.yml \| git apply --index`. Every engine branch below already carries that patch in its merge commit. |
| BS-3d build red-check, checks, ship green | #574 (draft; replaces #463) | feat/sindri-blocks-build-commands | `e9061c35e` | Draft, MERGEABLE, merges main `bf77125d1` (has #570) | `workflow.build` and `workflow.ship` are at v4 (main v3), re-pinned in StatePathOrderTests. Verified: build 0 warnings, Sindri unit 8631, Engine unit 166, Architecture integration 128. **Not run:** Sindri integration lane (stopped at handoff; the one red seen was the known tmux preview), scoped `dotnet format`. Then: the body from `~/.agentic-workflow/digests/bs3d-pr-body.md` (first line "Replaces #463"; fix its dangling "integration results below"), plus the footer → `gh pr edit 574 --body-file`, `gh pr ready 574`, `ready #574 <sha>` to meta. After it merges, meta closes #463. Re-approve: workflow.build, workflow.ship, then workflow.phase. |
| BS-2c a redesign edits the previous design | none yet | feat/sindri-design-revise-edits | `39896b427` | Pushed, merges main `bf77125d1` | `workflow.design` 3→4, `workflow.line` 2→3, re-pinned. Verified before the commit: build clean, whole-solution unit lane 17091 pass, Sindri integration 1751 with only known box reds (tmux preview, PosixProcessAncestry, one ContextApiGolden load timeout that passes 72/72 on rerun), recipe eval design 12/12 and line 6/6. Left: merge main `6a674c43d` (#595 is CodeIndex only; recheck the versions), open the PR with the body from `digests/bs2c-pr-body.md` (on the box) plus the footer, ready. Re-approve: design, then line. |
| BS-8 ship.pack + ship.freeze | none yet | feat/sindri-blocks-ship-pack | `7278e57fd` | Pushed, merges main `6a674c43d` | It edits no recipes, so there are no versions or re-approvals; `step.ship.pack`'s descriptor pin is already in ShippedDescriptorHashes; the Step count is 43. The gate on this merge is **not run** (stopped at handoff). On the previous merge (`024af5363`, main 8bbee9420): build 0 warnings, Engine unit 166, Ui unit 2585, Sindri unit 8348 with the 3 Launch reds that #592 has since fixed; Architecture 2 reds (ARCH-CYCLE-001 in Aw.Sindri.Ui, and `ContextMapViewModel` uses `Avalonia.Rect`); Sindri integration 5 reds (SetupRouteTests, 4 ContextApiGoldenTests). All 7 are in Ui and context code this branch doesn't touch, and the context map came from main (`d1c5f1d31`), so they're **probably on main**: confirm on a detached main worktree, and list them as pre-existing in the body. Body: `digests/bs8-pr-body.md`. Desk items in it: fact:review is `{hash, dir}`, fact:green carries a sha, base = stackParent → baseBranch → main, freeze compares recorded shas only (effect.pr.open must re-check the head). |
| BS-6b prove.holdout-log | none yet | feat/sindri-blocks-holdout | `7a5b14868` | Pushed, merges main `bf77125d1` | Post-merge build 0 warnings. **No tests since the merge.** Before it: holdout tests 67/68; the failing ProveHoldoutLogStepContract was rebound to BlockContract (`c315cfd0e`) and has never been run. It edits no recipes. Left: confirm the descriptor hash `9511ad11…` still pins, unit lanes (Sindri, Engine, Architecture), Sindri integration, scoped format, docs line, PR body (not written), draft PR, review, ready. |
| Per-round token budgets (owner bug) | none yet | fix/sindri-budgets-per-round | `d460e6a7f` (**wip**) | Pushed, merges main **`8bbee9420`, from before #570** (the wip commit body wrongly says #570 is in) | Core per-round budgets are committed in `70da4c54b`/`1ada58e2e`/`82832dbcd` (replay marker `token-budget-per-round-v1`). `d460e6a7f` holds budgets-3's uncommitted A, C, D edits, which are **unbuilt and untested as committed**: A (shipped recipes drop recipe-level `budget.tokens`; ShippedBudgetsTests; goldens), C (eval lines carry `tokens`; scoping cases budget-per-round and budget-backstop-parks), D (Grant tokens control: GrantTokensViewModel, InstanceFrameViewModel.Grant, GrantTokensPanel, ApplyCommandBody, GrantTokensTests). Left, in order: build and fix; check A also covers `workflow.step.yaml` (recipe-level 400k, per-state 200k → 1M); B merge main `6a674c43d`, every edited recipe main+1, re-pin StatePathOrderTests, ShippedDescriptorHashes and goldens; C eval green; D (AppConfirm.Require widened for GrantTokens with a test, catalog-row readiness drives IsReady, falls back to AppConfirm.Terminal, AtTerminal=false; screenshot opened and checked); E scoped format, PR body (marker, overspend visible only, recipe-level budgets removed, the re-approval order children first per OPERATIONS.md §2, ui as reviewer, the screenshot). Owner rule (2026-10-21 ~21:00Z): 1M per **run** is a backstop only, every per-state budget is 1M per round, and there is no recipe-level cap. Brief: `plans/sindri-engine/budgets-finish-brief.md` (on the box) and this row. |
| BS-13 proof feed + step.prove.collect | none | feat/sindri-proof-feed | `dc3731a9b` | Pushed (unchanged since the 2026-10-21 handoff) | As 2026-10-21 engine.md: finish C2 (collect descriptor and registration, workflow.prove with a collect state, evals including missing-item and stale-artifact), C3 red, C4 green, gate, PR. It's about 330 commits behind main: merge it first. |
| CI-red autofix | none | feat/sindri-ci-fix | `54ddcce1e` | Pushed (unchanged; red build not yet clean) | **Its brief, `ci-fix-brief.md`, is not on this box.** Rebuild it from the 2026-10-21 engine.md row (ports IPullRequests, IWorkspaceChanges, ISecretScan, IAgentStepInstructions; the agent-launch seam with marker `ci-fix-agent-workspace`; descriptors step.pr.ci-log, step.pr.ci-fix, effect.pr.push, gates diff-scope, secret-scan and autofix-cap; babysitter v+1; event pr.pushed; profile key `workflow.pr.autofixCap`), or ask the owner. It's about 470 commits behind main. |

Merged this session: #570 (E1, replaces #532), #592 (Launch probes on macOS), #593 (IsolatedGit: hook-free test repos), #594 (box
F1 bare `sindri` is the C# one, F2 a failed launch keeps the loop round), plus earlier #555, #556, #560.

## Worktrees (~/repos/aw-wt/)

Checked at handoff: every one below has head = jfork, 0 uncommitted files (only untracked `scratch/` notes), and no running process.

feat-sindri-blocks-build-commands `e9061c35e`, feat-sindri-blocks-holdout `7a5b14868`, feat-sindri-blocks-ship-pack `7278e57fd`,
feat-sindri-ci-fix `54ddcce1e`, feat-sindri-design-revise-edits `39896b427`, feat-sindri-proof-feed `dc3731a9b`,
fix-sindri-budgets-per-round `d460e6a7f`, fix-sindri-launch-path-rounds `4f00940f3` (merged), fix-sindri-launch-tests-macos `3bc5299bc` (merged),
fix-sindri-test-git-template `c478af067` (merged), fix-ci-recipe-versions-token `2e7ad0b49` (#596). The three merged ones can be removed with `git worktree remove`.
`feat-sindri-recipe-display-name` (#570) is already gone. Draft PR bodies are in `scratch/<unit>/pr-body.md` in each worktree and copied to
`~/.agentic-workflow/digests/{bs3d,bs2c,bs8}-pr-body.md`. Those files exist on this box only, so the rows above carry what they say.

## Builders (all stopped)

| Builder | Unit | Where it stopped |
|---------|------|------------------|
| bs3d-4 | #574 | merged `bf77125d1` and pushed; Sindri integration lane stopped mid-run; format not run |
| bs2c-3 | BS-2c | full gate green on the staged merge; the lead committed and pushed it as `39896b427` |
| bs8 | BS-8 | merged `6a674c43d` as `7278e57fd`; the lead stopped its gate chain (owner: no new tests) and pushed |
| bs6b | BS-6b | merged `bf77125d1` and pushed; built, no tests |
| budgets-3 | budgets | no final report; its edits, last touched ~01:50Z, were committed by the lead as wip `d460e6a7f` |

## Blocked

- Every main merge on the scrub, until the owner merges **#596** (or use the patch above).
- The CI fix, on its missing brief (see its row).
- BS-9 prove.before-after needs #574 merged. Forge wiring needs #574, BS-8 and the CI fix. ideas waits on `IPullRequests` for step.docs.scope: ping ideas with the PR number.

## Requests from other lanes (queued)

- **ui:** a server golden of a ledger row carrying `delta.frame` (#564 fields `actor`, `attempt`, `maxAttempts`, omitted when null).
  Two cases: a block run on attempt 3/3, and nothing open. Built from the real projector. ui then switches
  `tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/workflow-event-frame` to it. Send ui the PR number and the golden paths.
- **meta:** a hang timeout for test runs (`--timeout`/`--hangdump` in the shared `heavy` wrapper or in the lane runs). An orphaned
  unfiltered Sindri run held a heavy slot for 2.5 h on 2026-10-22. Write a brief and send the change to meta for review before it lands.

## Rules learned this session

- Builders never run a whole test project unfiltered. Run lanes separately (`--filter-trait "Lane=unit"`, then `"Lane=integration"`),
  start long runs with `run_in_background`, and wait for the notification. A heavy job whose shell has ppid 1 is orphaned: kill it by PID
  (`kill -9` on the test host if SIGTERM doesn't stop it).
- Never `--no-verify`. When the scrub flags main's own content, fix main (as in #596) and carry the patch.
- Version rule: an edited recipe is main's version + 1; whichever PR merges second re-versions.
- To meta: the first line of a ready message is `ready #N <sha>`, one line per PR.
- External GitHub writes (create, ready, comment) are blocked in turns auto-continued from teammate messages; batch them for the owner's turn.
- `tests/Aw.Architecture.Tests/packages.lock.json` gains an `Aw.Kernel.Data` edge on every build. That drift comes from main; revert it, don't commit it.

## Next steps, in order

1. The owner merges #596.
2. #574: run the Sindri integration lane and scoped format, edit the body, mark it ready, send `ready #574 <sha>`.
3. BS-2c: merge main, recheck versions, open the PR, ready.
4. BS-8: run the gate on `7278e57fd`, confirm the 7 reds are on main, open the PR, ready.
5. BS-6b: run its tests, write the body, open the PR, ready.
6. Budgets (the owner's bug, top priority after the ready PRs): build the wip, then items A–E as in its row.
7. The ui delta.frame golden, and the heavy hang-timeout brief (to meta).
8. BS-13, then the CI fix (rebuild its brief), then BS-9 and forge wiring (IPullRequests; ping ideas).
9. When the last line unit merges: send the owner the paste-ready e2e checklist (approve-all and profile re-approval; recipe re-approvals children first).
