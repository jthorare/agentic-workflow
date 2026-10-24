# ui lane handoff, 2026-10-22

Lane: the F3 UI redesign of the Sindri app (Avalonia), every demo screen. Specs are on main at
`docs/superpowers/specs/2026-10-20-sindri-ui-f3-redesign/specs/<screen>.md`; each spec's §8 gives the build order by slice.
PRs go to `jthorare/agentic-workflow` from `joithor:<branch>` (the remote is `jfork`/`upfork`, both `joithor/agentic-workflow-jthorare`).

## PRs and branches

| PR | Branch | Head | State | What's left |
|---|---|---|---|---|
| #598 | `fix/sindri-ui-macos` | f3dac47a2 | ready, in meta's gate | Fixes three tests that fail on macOS on main: CreateItemDialogTests:269 (Ctrl vs Cmd), ShellWindowTests:1796 (expected start focus), and the ContextControlsRenderingTests dot grid (dot not pixel-centred in `ZoomBorder.Dots`). Gate passed build, deps, rules, packages and no-any before the handoff; aot-smoke exit 1 still to be attributed by meta (the PR touches no AOT path) |
| #597 | `fix/sindri-ui-context-arch` | cedf9e0da | ready, after #598 in meta's gate | Fixes main's ARCH-UI-001 and ARCH-CYCLE-001 from #581. The map layout types moved to `ViewModels/Context/Layout` as plain `MapPoint`/`MapRect`/`MapVector`, with the Avalonia conversions in `Views/Controls/MapGeometryExtensions.cs`. Pre-merge: Architecture 130/130, Context tests green. The post-merge re-run is left to the gate |
| #599 | `feat/sindri-ui-setup-4` (also `wip/sindri-ui-setup-4`) | fa0318b98 | open, NOT ready (1 blocking review finding) | Setup slice 4. Setup 113/113, routing 6/6. Its 3 failures are main's (fixed by #597/#598). The repo row builds against api #528's contract (#528 still open). LEFT: fix the review findings below, then `ready #599` after #597 and #598 merge |
| #584 | `feat/sindri-ui-needs-you-3` | dd7bd8b13 | open, NOT ready; do not gate | Needs-you slice 3 (hold and typed approve in the needs-you bar). Round-1 review items 1-3 and the nits are done (NeedsYou 211/211 at 9f1367a15). LEFT: item 4, the headless view test. **The head commit dd7bd8b13 is a `wip:` that adds that test with debug markers, and it hangs.** See "#584 hang" below |
| #603 | `feat/sindri-ui-workflows-2` (also `wip/sindri-ui-workflows-2`) | 1799ce8e8 | open, not ready (needs review) | Workflows slice 2, board and toolbar (workflows.md §8.2), finished by ui-wf2 after the handoff call. Build clean. Workflows namespace 208/208, WorkflowsBoardFramesTests 5/5 (frames at 1440, 900 and no-match checked by eye). Ui unit lane 2617 with 1 failure, StripRequeryTests: fixed and the class re-run 10/10, but the whole lane was not re-run. Architecture 127/130, failing only ARCH-UI-001/CYCLE-001 (main, fixed by #597) and ARCH-INFRA-001 (two public CodeIndex types, main since #595, not ui's: the context lane). Rendering classes not run. Departures, in the PR body: no red-only commit; the Done lane still uses the unfiltered root read, not `open=false` (move it in slice 3); the Merge lane is always empty; filter counts are over drawn runs; list tests pin `ViewMode = List`. Check the `packages.lock.json` change from 33a17082e. LEFT: lead review (lean-reviewer), merge main, `ready #603` |
| (none) | `wip/sindri-ui-run-canvas-2` | 52ae8d9e4 | WIP, never built | Run canvas slice 2 (graph model and layout, run-canvas.md §8.2). Contains main 6a674c43d and #597's cedf9e0da (checked). RunStates, RunLayout, RunCanvasLayout and RunGraphBuilder use the plain `MapRect`/`PortSide`. Mirrors #564's `actor`/`attempt`/`maxAttempts` as `FrameSummary`, with spec-shaped goldens under `tests/Aw.Sindri.Ui.Tests/Golden/SpecShaped/workflow-event-frame`. LEFT: build, Run namespace, golden and Architecture tests, format, push `feat/sindri-ui-run-canvas-2`; open the PR after #597 merges. Accepted: no separate red commit, `BranchesServed` false |

Merged on 2026-10-21/22: #558 needs-you s2, #565 context query, #568 confirm controls, #569 workflows s1, #573 F3 docs, #581 context s2, #590 main fixes.

## Worktrees (all under ~/repos/aw-wt/ unless noted; all committed and pushed, checked at handoff)

| Worktree | Branch | Head | Unpushed |
|---|---|---|---|
| fix-sindri-ui-macos | fix/sindri-ui-macos | f3dac47a2 | none |
| fix-sindri-ui-context-arch | fix/sindri-ui-context-arch | cedf9e0da | none |
| wip-sindri-ui-setup-4 | wip/sindri-ui-setup-4 (= feat/sindri-ui-setup-4) | fa0318b98 | none |
| feat-sindri-ui-needs-you-3 | feat/sindri-ui-needs-you-3 | dd7bd8b13 | none |
| wip-sindri-ui-workflows-2 | wip/sindri-ui-workflows-2 (= feat/sindri-ui-workflows-2) | 1799ce8e8 | none |
| wip-sindri-ui-run-canvas-2 | wip/sindri-ui-run-canvas-2 | 52ae8d9e4 | none |
| wip-sindri-ui-context-2 | feat/sindri-ui-context-2 | e5273871f | none (#581 merged; stale, can be swept) |
| /Users/joi/personal/aw-wt-ui-timeout | fix/sindri-ui-request-timeout | 9f79f53b2 | none (#550 merged; stale, can be swept) |

## Builders

All are stopped. None has a test run or heavy job left (checked: no ui heavy jobs or testhosts at handoff).
- ui-ctx2b / ui-ctx2c: #597 (done). ui-macos: #598 (done). ui-s4 / ui-s4b: #599 (PR open, stopped before the review findings).
- ui-ny3 / ui-ny3b: #584 item 4. Stopped before the marker run got the heavy slot; it never ran.
- ui-wf2: workflows s2. Stopped with its green build still queued; the build never ran.
- ui-rc2 / ui-rc2b: run canvas s2. Stopped with the round still queued; it never ran.
- ui-rev599: the lean-reviewer on #599. Its findings are below.

## #599 review, round 1 (lean-reviewer, head fa0318b98: 1 blocking, 2 should-fix, 1 nit)
1. BLOCKING. `Api/SindriApiClient.Setup.cs:13-25`: all five writes (`AddRepo`, `RegisterMcp`, `SetupOnyx`, `ConnectOnyx`, `DeferOnyx`) in the
   production client still `throw new NotSupportedException()`, left from the red stub step. Once #528 lands and AddRepo is marked ready,
   pressing Add crashes the app. `SindriApiClientSetupWritesTests` and the `SetupWrites` contract cases should fail; "113/113" covered only the Setup
   namespace, so the Api namespace was never run. Fix: implement them. The first four are POSTs to `SetupRoutes.*` returning `JobStarted`;
   `DeferOnyx` POSTs and returns `OnyxDeferView`. Send the same human caller and ui client headers as the other writes. `ConnectOnyx` must never
   log or echo the body. Then run the Api namespace.
2. Should-fix. `ViewModels/Labels.SetupForms.cs:137-148`: `SetupRefusal` doesn't map SND-PROFILE-002 (#528: the profile changed while being written).
   Map it to a "try again" sentence. Add a spec-shaped golden `setup-repos/profile-changed`, using #528's text "The profile changed while Sindri was
   writing it. Try again.", and a test.
3. Should-fix. `Api/Setup/AddRepoRequest.cs:6`: `Name` is dead, because the form has no name field (an accepted departure). Drop it from the record
   and the JSON context, or say in the PR why it stays.
4. Nit. The `Route`/`CoreRoute` split preserves behaviour, and the anchors keep their order. But the U3'/U8 arms now sit after CoreRoute's, so a
   future needs-you path that a CoreRoute arm already matches would lose to it.
Checked, no findings: the seven setup-repos goldens are byte-identical to #528's; VMs are plain; no production file is over 300 lines; the Onyx
password is cleared on send.

## #584 hang (what's known)
- The typed-box test (`NeedsYouConfirmViewTests`, in dd7bd8b13) blocks for over 3 min at ~0-1% CPU: a blocked await, NOT the layout loop first suspected.
  All the fake's gates in that test are null (`WaitFor(null)` returns). `PressApprove()` on a confirm row only raises `ConfirmFocusRequested`, which
  `NeedsYouPage.OnConfirmFocusRequested` posts at `DispatcherPriority.Loaded`. Neither blocks. Remaining suspects: `Connected(...)` with
  `AppChallengeAnswer` set (does `NeedsYou.Count > 0` ever become true with that fixture?) and the `Until`/`Settle` pair. The markers in the file write
  `/tmp/ny3-marks.txt`; the last marker shows the blocked statement. Remove them before a real commit.
- The hold test doesn't hang. It fails `IsFocused` at its line 58, because focus is posted at Loaded priority and `Settle` doesn't run Loaded jobs.
  Try `Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded)` before the assert.
- No app bug found so far. If the cause is test-only, say so in the commit. Then `ready #584` naming the fix.

## Blockers
- The heavy lock is per SESSION: a lead and all its builders share one slot (`AW_SESSION_HEAVY_SLOTS=1`), so ~15 min per round, and every builder
  stalled in the queue tonight. Raising it is the owner's call. Box rule (meta): long tests run in the background with a lane or class filter, and
  never a whole project unfiltered; kill your own runs when you stop.
- api #528 (setup repos) is open; #599's repo row depends on it.
- engine owes a server golden of a ledger row with `delta.frame` (block run 3/3, and no run open), generated from the projector. When it lands,
  switch run canvas's FrameSummary contract test from the spec-shaped goldens to engine's.
- Engine owns the Grant tokens button (GrantTokens in AppConfirm); ui lead-reviews its view diff when asked.

## Next steps, in order
1. Meta gates #598, then #597. On a failure, fix it in its worktree and send ready again.
2. #599: fix the round-1 review above (the blocking item first), run the Api AND Setup namespaces plus Architecture, then `ready #599`.
3. #584: run the hold and typed tests alone, bounded, and read the markers. Fix the hang, remove the markers, merge main, run checks
   (NeedsYou namespace, the view test class, Architecture), push, `ready #584`.
4. #603 workflows s2: lean-reviewer review, fix, merge main, re-run the Ui unit lane once, `ready #603`. Slice 3 moves the Done lane to `open=false`.
5. Run canvas s2 (after #597 merges): build, fix, Run namespace + goldens + Architecture, push `feat/sindri-ui-run-canvas-2`, PR, review, ready.
6. Context s3 (after #597 merges): selection and the inspector (context.md §8.3: click, J/K, hover dim, the 480 px inspector into NodeHost/SourcesHost,
   `MapPath` shortest path, conflict section, Accept/Reject `ReviewContextMap:accept|reject`). Red first: MapPathTests, ContextHoverTests, the
   selection tests. Keep the layout in ViewModels as plain types.
7. Setup s3 (after #599): the two confirmations in their rows (spec setup.md).
8. Needs-you s3b: single-recipe typed approve (the ApproveRecipe catalog row is on main; it needs version and hash from GET /workflow/recipes).
9. Then, per screen: workflows s3-4, run canvas s3-5 (s3 page and keys, s4 inspector with StepRuns, s5 failed states), context s4-5, setup s5
   (live wiring), needs-you s4.
10. Follow-ups: the needs-you bar's NeedsYouTerminal should use AppConfirm.Terminal/ProtectedActionButton, then drop the LockButtonWalk allow-list.

## How the lane works
- One builder per unit, each in its own worktree. Brief = goal, spec section, red tests, scoped checks, push branch. Run build + tests + format as
  ONE heavy job per round. Build with `-warnaserror` first, then `dotnet test --no-build`, one filter per run, and check the total is above 0.
  ALWAYS include `tests/Aw.Architecture.Tests`.
- Ready protocol: lead review for small diffs, a lean-reviewer for big ones; merge main; send meta `ready #N <sha>`.
  If the pre-merge checks are green and the merge is clean, the post-merge re-run can be left to meta's gate (say so in the ready).
- Builders don't bypass the pre-commit scrubber. A known false positive (ci.yml:334, SND-SCRUB-002) goes to the lead.
- Builders forget to push: check the remote yourself. Run ListAgents after any /clear; builders from before it keep running.
