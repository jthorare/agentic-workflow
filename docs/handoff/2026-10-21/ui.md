# ui lane handoff (sindri-ui), 2026-10-21

The F3 redesign of the Sindri app (Avalonia, `src/Sindri/Aw.Sindri.Ui`), built screen by screen from the specs ideas wrote
(`~/.agentic-workflow/mockups/f3/specs/{context,workflows,run-canvas,setup,needs-you}.md` on the old box; copies of the
lane's own notes are on branch `wip/sindri-ui-lane-state` under `docs/handoff/2026-10-21/ui-state/`).

Every ui builder hit the usage limit at about 04:05Z and was stopped. Every worktree's work in progress is pushed, so nothing lives only on the old box.
`main` was at ccddefc at the time of writing.

## Branches, newest state

| Unit | Branch | Head | Pushed | PR | State | What's left |
|---|---|---|---|---|---|---|
| Setup slice 1: shared HoldButton, TypedConfirmBox, AttentionFill, ApproveAllDialog rebuilt on them, AppConfirm | `feat/sindri-ui-confirm-controls` | a23c84084 | yes | none yet | Built and green: unit 2399, integration 735 passed, 10 skipped, 0 failed, Rendering 30. Arch not finished. Main is merged through 914017793 (#539), so it lacks #538, #534, #541, #540, #548, #550 and #551; #540 touches Ui files. | Review round 1 (`ui-state/reviews/confirm-controls-1.json`) found 2 issues: **CC1**, HoldButton.cs:42 subscribes `PointerCaptureLost` with Tunnel routing, but the event is Direct-only, so a lost capture mid-hold never cancels and the approval can go out after the person let go (subscribe directly); **CC2**, no view-level tests for key-up, capture-lost, repeat-ignored, LostFocus and Space/Enter. Three cheap suggestions are promoted to fixes: **CC3**, the accessible name starts with the visible text; **CC4**, announce the typed match (`Labels.TypedMatches`); **CC5**, OnDetachedFromVisualTree stops the timer and unhooks. Then: merge main, red tests first, one gate, open the PR, ready. ApproveProfile/ApproveRecipe hosts are NOT in this slice (they come in setup slice 3 and needs-you slice 3). |
| Setup slice 4: agents list, map meter + Retry, Onyx form, folder picker, repo row | `wip/sindri-ui-setup-4` (local branch was `feat/sindri-ui-setup-4`) | 1da61cf74 | yes | none | One WIP commit on 204f8be9d (#540's head). **Not built.** | Finish, red commit first (`SetupViewModelTests.Map_row_has_no_button_unless_it_failed`), merge main, gate, PR. The repo row targets api #528 (POST /sindri/setup/repos, 200 JobStarted, SND-PROFILE-002); Onyx and RegisterMcp stay on fixtures behind catalog readiness. Also apply `wip/sindri-ui-540-extras`. Also change #540's SpecShaped fixture text from "recipes" to "workflows" (api #544: "Approve the workflows", "setup · workflows", "Approve all new workflows (N)"; ids stay `recipes`, `setup:recipes`). |
| #540 review extras: setup toggle names start with the step title; a 404 drops stale rows | `wip/sindri-ui-540-extras` | f3630423e | yes | none | One commit on 204f8be9d, with tests. Not gated. | Fold it into setup slice 4, or its own small PR. |
| Workflows slice 1: RunStatus, Violet tokens and sizes, run icons, Labels.Board | `feat/sindri-ui-workflows` | 8a0ef6495 | yes | none | Committed; gate state unknown (check the builder digest). StatusBadge moved to slice 2 (MA0182 fails on an unused internal type). | Gate, independent review, PR, ready. **Check for overlap** between RunStatus here and run-canvas's `RunStates` (ViewModels/Run) before both land. |
| Workflows slice 2 (board) | `wip/sindri-ui-workflows-2` | c7f5e48a9 | yes | none | WIP on top of slice 1. Not built. | Finish, gate, PR after slice 1. |
| Run canvas slice 2 | `wip/sindri-ui-run-canvas-2` | ce674a94b | yes | none | WIP on 692b78400 (#548). Not built. | Merge main, finish, gate, PR. |
| Context slice 2: ContextMapLayout, the map view models, the first visible map | `wip/sindri-ui-context-2` | 55b5ae39c | yes | none | Two commits: the red set (never run, because the heavy queue was ~40 min per job), then the uncompiled green code plus the lead scratchpad under `.wip-handoff/scratchpad/` (`impl/apply.sh` copies it in; edit the paths inside it). | Run red, commit, apply green, **delete `.wip-handoff/`**, build, gate, PR. Use `GraphEdgesControl` (renamed in #548) and drop its MA0182 pragma once the page places it. Accepted departure: the layout lives in Views/Context/Map, because UiRules bans Avalonia types under ViewModels. |
| Needs-you slice 2: action bar, keys A/D/X/Enter, the desk report route (CompleteStep) | `wip/sindri-ui-needs-you-2` | 30a0e8bf3 | yes | none | Red commit fcffd9e70, then green WIP. Not built. | Gate, PR. Decisions to accept: outcomes approve/revise/drop; a protected complete shows terminal copy with its CLI; send-back on a terminal-only desk step sends outcome rework. |
| Lane state: digests, briefs, review files | `wip/sindri-ui-lane-state` | 276552c9b | yes | never merge | Files under `docs/handoff/2026-10-21/ui-state/`. | Read them. Don't merge this branch. |

Merged today: #521 needs-you inbox, #522 context controls, #540 setup checklist in Settings plus the Home line, #548 run-canvas graph edges. Earlier: #465 #468 #471 #472 #476 #483 #491.

## Demo screens

| Screen | On main | In flight | Next |
|---|---|---|---|
| Needs you | slice 1, the inbox (#521) | slice 2, action bar and keys (WIP) | slice 3: approve rows (after confirm-controls and api #544), slice 4 |
| Context map | slice 1, ZoomBorder and edges (#522) | slice 2, the first visible map (WIP) | slices 3 and 4 |
| Workflows board | none | slice 1 pushed (no PR); slice 2 WIP | slices 3 and 4 |
| Run canvas | slice 1, GraphEdgesControl and PortSide (#548) | slice 2 (WIP) | slices 3 and 4 (steps[].needs/produces from api #546) |
| Setup | slice 2, checklist in Settings plus the Home line (#540) | slice 1 controls (pushed, review fixes due); slice 4 (WIP) | slice 3: the two confirmations in their rows; slice 5: live wiring |
| Jobs, approve-all, step runs | merged (#472, #471, #491) | none | follow-ups below |

## Blockers and peers

- The usage limit: everything is stopped until it resets.
- The heavy-slot queue: about 22 jobs for 3 slots made a red run wait ~40 min. Fold red, green and gate into ONE heavy job per slice.
- api: #544 needs-you rows for setup:recipes and recipe:<id> (approved, waits on the owner's go); needs-you PR 2 (stop{n,of}, timeout) building; #546 wf-rows (startedBy, startedAt, lineStage, rootAddress; `?root=true&recipe=&open=false`; steps[].needs/produces) draft; #528 AddRepo ready with the meta; #547 code-index reads (ui's CI-10b Health card is queued after the demo path).
- engine: #532 (displayName on the recipe and its states, why, transitionLabels) is in the meta's queue.

## Rules the next lead needs

- Builders are Sonnet or Haiku, never Opus.
- Every gate runs: `set -eo pipefail`; `MSBUILDDISABLENODEREUSE=1`; build -warnaserror; scoped format plus `dotnet format <Ui.Tests csproj> --verify-no-changes --no-restore --diagnostics IDE0001`; Ui unit; the FULL Ui.Tests integration lane; Rendering; Arch.
- After merging main, check partial classes for CS0102 (Labels, FakeSindriApi, FakeSindriHandler), duplicate [JsonSerializable], files over 300 lines (AW1005), and fake route arms that swallow other routes.
- Every ready carries review counts: an independent read-only reviewer for a big diff, a lead review for a small one. Fix issues only; resolve nits in writing.
- Run `dotnet clean` in a worktree after its ready. Remove worktrees only through the meta's sweep.
- The push hook blocks a command containing both "push" and "main".

## Follow-ups (small)

- Move the "Pannable map" literal (ZoomBorder.cs) to Labels.
- Merge ApproveAllJob into the jobs client.
- Approve-all follows runningJob on a 409.
- The #483 leftovers: a cancel-during-retry test and a once-per-batch list refresh.

## Next step

After the reset: for each row above, start one fresh Sonnet builder on its branch. Brief it to read its digest in `ui-state/digests/sindri-ui-g1-<unit>.md` and check `git status` and `git log` first. Order: confirm-controls fixes and PR first (it unblocks setup 3 and needs-you 3), then workflows slice 1 PR, then the WIP slices.
