# Sindri UI U8 T7 + T8 handoff

Branch feat/sindri-ui-u8 (worktree ~/repos/aw-wt/sindri-ui-u8). Start 49b7e0a94 (== origin). origin/main stayed at baea6b6b3 the whole time (already merged), so no merge was needed.
Note: the hook blocks any command with both "push" and "main": commit+push in one command, fetch in another.

## Commit pairs
| Slice | Red | Green |
|---|---|---|
| T7 keys + tab strip: WorkflowsKeysTests (11), FrameTabsViewModelTests (5), WorkflowsKeysViewTests (9 headless, [Collection(HeadlessWindows.Name)]) | 097ad1b22 | f69c3ea4d |
| T8 docs + spec | | 84bfe3bdb |

Frames: artifacts/bin/Aw.Sindri.Ui.Tests/debug/frames/u8-keys-selected, -keys-pending-output, -keys-cancel-ask, -keys-lock-popover, -keys-phase-note, u8-frame-tabs.

## What T7 added
- WorkflowsViewModel is partial (WorkflowsViewModel.Keys.cs): SelectedAddress/SelectedRow, Select, Deselect, Press(key) for J K Enter A X B N, KeyNote. Selection is an address; Show/ReadDone call Reselect (kept by address, cleared when the row is gone). SelectedRow is limited to drawn rows (a collapsed group's selection reads as none).
- Ctor changed: WorkflowsViewModel(api, clipboard, clock) and InstanceRowViewModel(summary, clipboard, clock) (rows own a ProtectedActionViewModel for a protected answer, plus CanAnswerAtDesk, CanCancel, IsSelected).
- Navigation/FrameIntent (None, PendingOutput, AskCancel); OpenRequestedEventArgs carries it; ShellViewModel.Workflows wires it to InstanceFrameViewModel.Intend(address, intent) before Navigate. ShellViewModel.Press calls WorkflowsKeyPressed (page-scoped, not in the sheet).
- Frame: ApplyIntent after the read: AskCancel calls Expanded.AskCancel(); both raise FocusRequested and keep WantedFocus (the sheet's view binds late, after Navigate returns, so an event alone was lost; the view reads WantedFocus when its DataContext arrives and calls FocusServed). InstanceFrameView retries after each layout pass until the target (FactCopyCli of the awaiting row, or ExpandedKeep) holds focus.
- FrameTabsViewModel/FrameTabViewModel/Views/Instance/FrameTabs: Timeline, Decisions, Requests with "arrives with U8b", Context "arrives with U9"; a null context hides the number. Plain text, not buttons.
- Views: InstanceRow has the lock button (WorkflowProtected) under the chip so A reaches its popover (also when compact), a selected class, and a SelectEvent (focus in a row selects it). WorkflowsPage moves focus with the selection (J/K) and draws WorkflowsKeyNote (live, polite). ProtectedActionButton now shows its popover when the model's IsOpen becomes true. ShellWindow.Workflows.cs: a cleared selection posts KeepFocus.
- ShellWait sources now include shell.Workflows and shell.Instance (test support).

## Not taken
- not taken: the ⇄ loop mark in the Workflows column heads (T1/T2 skipped it; the list's state cells serve loops per row, but no column-level pairing is specified, and it was not on T7's list).
- not taken: B and N do not send or open anything: they only say "Hand back arrives with U8b" / "Start arrives with U5'" in a note under the head (as the plan says). B names its phase for any selected row (the list does not say whether handback is offered beyond the row's actions).
- not taken: the tab strip's tabs are plain text, not focusable controls (nothing to do yet; the content arrives with U8b/U9).
- not taken: PublicAPI.Unshipped.txt changes: every new type is internal, and ProtectedActionButton's change adds no public member.
- not taken: restoring focus to the row after a re-read replaces the selected row's view model (a signature change); the window's KeepFocus keeps focus on a usable control instead.
- Spec edits (screens spec): NOW is the served `now`; header drawing drops the round (r1/3 stays on the band) with a Header bullet; drawings illustrative, states come from the recipe; CHILDREN group recipe is workflow.step. The line numbers had moved (146, 156/184, 122/126, 172 are now 151, 189, 120-ish, 177).

## Extra commits
- 3c0fbd51a: InstanceFramesTests' Buttons() lookup is scoped to the InstanceFrame (the Workflows rows behind the sheet now carry lock buttons; 3 tests had failed on the first gate).
- Merge of origin/main e711c3e97 (#276, docs only), no conflicts, pushed.

## Gate (final head, after the merge)
- whole solution dotnet build -warnaserror: 0 warnings, 0 errors
- Ui.Tests full (unit + integration): 2090 total, 0 failed, 10 skipped
- Aw.Architecture.Tests: 117/117
- dotnet format --verify-no-changes: exit 0
- check-deps: ok; sync-rules --check: up to date
