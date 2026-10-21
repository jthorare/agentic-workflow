# Sindri UI U8 T3 + T4 handoff

Branch feat/sindri-ui-u8 (worktree ~/repos/aw-wt/sindri-ui-u8), head d873a9ff3 (== origin). origin/main had not moved since the earlier merge.
Note: a bare `git push` is blocked by the hook (the hook's cwd is the primary checkout); use `git -C <worktree> push origin feat/sindri-ui-u8`.

## First: full integration lane on 572a45989
526 total, 0 failed (10 skipped). Nothing to fix.

## Commit pairs
| Slice | Red | Green |
|---|---|---|
| T3 InstanceFrameViewModelTests (12 incl. 3 extras: address-no-row, re-read done, re-read gone), StatesBandViewModelTests (9), ExpandedStateViewModelTests (17 incl. an Attach chip extra) = 44 | 76faf20aa | 1046a515b |
| T4 ShellInstanceTests (6: the 5 planned + Esc returns focus to the Workflows row), InstanceFramesTests (12 frames + Focus_order_reads_before_it_acts) = 19 | 18cba5bb0 | d873a9ff3 |

Frames: artifacts/bin/Aw.Sindri.Ui.Tests/debug/frames/u8-frame-*.png.

## Decisions / not taken
- Shell claim (Sheet.cs arm below the U8 marker, ShellViewModel.Instance.cs) was done in the T4 green, since ShellInstanceTests is what tests it (the plan put it in T3).
- View classes are InstanceFrameView and ExpandedStateView (not InstanceFrame/ExpandedState): Avalonia's name generator makes a field named after the x:Name, which clashed with the class. StatesBand keeps its name.
- not taken: AttachControlViewModel in the frame. Its primary ("Open instance") and chevron model don't fit inside the instance's own frame. The frame reuses its Labels (CancelAsk, ConfirmCancel, KeepStep, AttachPendingName, AvailableWithP10a): a pending Attach chip on step.agent, and each served command as label + CLI + Copy + "Available with U8b" chip; protected commands are ProtectedActionButton; Cancel asks first with Keep focused ("Cancel it" sends nothing and says so).
- not taken: Back as its own initial-focus control. The sheet header's Back is the shared one; the frame's InstanceBack follows the item sheet's not-found-back pattern (hidden while the sheet covers). FrameFocus.Back exists on the VM.
- Header state/detail come from the status header (`escalated: gate`); the recipe state stays on the band (Q-OWN-1 provisional).
- Subject "Open in" uses TopLevel.Launcher in view code-behind (http/https links only); no new port.
- The frame keeps its content on a re-read that fails or finds not-found (Note = InstanceGone); a done instance keeps content with Note = InstanceDone.
- Id for a frame: hinted (OpenInstance), Workflows row, or strip row; else the list resolves the address.
- Esc/Back return focus: OpenerStrip is now also set for strip Enter/click; new OpenerWorkflow for Workflows rows (ShellWindow.Instance.cs; Focus.cs Neighbour calls SheetOpenerFrame to stay under CA1502).
- Sheet width: ShownSheetWidth = 760 for Workflow sheets; ResizeSheet is ignored while an instance sheet is open, so the user's width is kept for other sheets.
- Reflow: InstanceRow/RecipeGroup react to PageLayout tier >= Narrow (id, title, state; column heads hidden). Hidden Grid columns stay at width 0: fewer definitions than cell columns crashes Grid.Measure.
- Edited ShellSheetKindTests.The_placeholder_shows_for_every_kind (Workflow is claimed now).
- Test support added: FakeSindriApi.LastInstanceId, FixtureInstances.Detail/Status/Parse.
- Not done (later tasks): facts, children, keys, tabs (T5-T7); docs and PublicAPI files (T8).

## Gate (head d873a9ff3)
- dotnet build (whole solution, -warnaserror, includes Ui.Tests and Desktop): 0 warnings, 0 errors
- Ui.Tests full (unit + integration): 2002 total, 0 failed, 1992 passed, 10 skipped
- Aw.Architecture.Tests: 117/117
- dotnet format --verify-no-changes: exit 0
- check-deps: ok; sync-rules --check: up to date
- The last edit (removing an unused MA0182 pragma) was rebuilt with -warnaserror on the Ui project, clean.
