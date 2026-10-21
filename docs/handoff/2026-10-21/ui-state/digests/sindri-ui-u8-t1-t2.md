# Sindri UI U8 T1 + T2 handoff

Branch feat/sindri-ui-u8 (worktree ~/repos/aw-wt/sindri-ui-u8), head 572a45989. origin/main (e036d72, #224 merged) merged in before T1 green; no conflicts.

## Commit pairs
| Slice | Red | Green |
|---|---|---|
| T1 WorkflowsViewModelTests (12 incl. extra Recipe_options_survive_a_filter) and InstanceRowViewModelTests (8) | ebdc51328 | 77abf1ff4 |
| T2 ShellWorkflowsTests (G_W_shows_the_Workflows_page_not_the_placeholder, Workflows_nav_count_is_counts_open, Workflow_events_reread_the_list, Refresh_never_moves_focus_scroll_or_input) and WorkflowsFramesTests (u8_workflows_open, _recipe_filter, _empty, _stalled, _unreadable) | 23a083a28 | dd4757006 |
| Nav test update (Workflows is no longer a placeholder) + format | | 572a45989 |

Frames saved at artifacts/bin/Aw.Sindri.Ui.Tests/debug/frames/u8-workflows-*.png.

## Decisions and not taken
- RequeryTests.Workflow_events_reread_the_list is named `ShellWorkflowsTests.Workflow_events_reread_the_list` (headless class; needs the page).
- "Collapsed groups": the spec/plan give no rule for which groups start collapsed. All start expanded; the human collapses one (header button), a re-read keeps it. The done line is page-level, not per group (counts.done is root-wide, Q-API-3).
- Blocked dash: the served data does not say which stage needs a missing input. The current state's cell reads "—" (the engine badge text follows it, e.g. "— ▲ parked: needs-answer"). Only preview-missing-input exercises it.
- Taken-over cell badge: Labels.EngineBadge is empty for taken-over; the cell reads "▌ <served reason>" (Labels.CellBadge).
- not taken: 404 "not served" state on the page (N1: the route exists; an unreadable body is the real case, shared sentence Labels.StripUnreadable). A 404 or other failure keeps the rows as they were.
- not taken: row keys (J/K, Enter-on-row selection, A/B/X), per the T1/T2 test lists; Enter on the title button and a click open the instance (deep link, lands on the not-served sheet until T3/T4, N5).
- not taken: the ⇄ loop mark in column heads; a polite live region for the empty/unreadable/stale notes (they are plain text blocks; no flag/text announcer yet).
- ShellViewModel.cs got two constructor lines (Workflows = new ...; InitWorkflows();) outside the anchor, as the anchor sits after the constructor; all state is in ShellViewModel.Workflows.cs. ShellWindow.axaml line swap as planned (plus an xmlns).
- Test support: FakeSindriApi.LastInstancesQuery (below the U8 anchor), FixtureInstances.List(scenario), FrameHost.Frame optional prefix.
- Two list reads per event now (strip: root; page: root+open). Names WorkflowsPageEmpty/Stale/Unreadable avoid Home's same-named controls.

## Gate (all on 572a45989 unless noted)
- build Ui.Tests -warnaserror and Desktop -warnaserror: 0 warnings, 0 errors
- Ui.Tests unit: 1407 passed, 0 failed
- Ui.Tests integration: 526 total; first run 1 failure (NavTabClickTests Workflows, fixed in 572a45989, re-run 8/8); the touched classes re-run 123/123. Full integration was not re-run after the fix.
- Aw.Architecture.Tests: 117/117
- dotnet format --verify-no-changes: exit 0
- check-deps: ok; sync-rules --check: up to date
