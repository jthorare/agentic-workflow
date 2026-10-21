# #224 U3' pass 2 handoff (T9, T6)

Branch feat/sindri-ui-u3. origin/main merged (eaf2fdf lineage, incl. #256 lock file) before the gate.

## T9 (done)
- ShellViewModel hosts `NeedsYou` (NeedsYouViewModel, ShellViewModel.Home.cs; optional ctor arg `IMotionPreference? motion`, SindriApp passes platform.Motion; StillMotion default). Fed by ShowNeedsYou. G D filter (HomeFilter.Decisions) filters the list rows only; badge/summary count all.
- Keys on Home: J/K walk Needs you -> Running -> Queued and back (MoveOnHome; Queue.ClearSelection, IsFirstOnHome); A/X/Y/Enter go to the selected needs-you row; Esc on sheet close returns focus to the row; Shift+A = MarkAllSeen (window key).
- Deep links: needs-you/<rowId> and decision/<decisionId> select the row, open its sheet (NeedsYouViewModel.OpenSheetOn / SheetRow). Sheet arms NeedsYou/Decision above LANE ANCHOR U3' in ShellViewModel.Sheet.cs and ShellWindow.axaml.
- Views/NeedsYou: NeedsYouRow (own focus border via item-row, tint classes pulsing/outlined/stale, controls, notice, protected cli text), NeedsYouSheet, NeedsYouStyles.axaml. Chip is the one StateChipView bound to row.StateChip (StateChipView now names its word). HomePage panel: summary line, Mark all seen, banner, held pill, rows, empty sentence, "all N ->" Show all.
- Live region NeedsYouLive (flag IsNeedsYouAnnouncing changes before text). Existing tests adjusted: ShellItemsTests/QueueFocusTests use an empty needs-you; Home_panels test drops the NeedsYou placeholder; ShellSheetKindTests excludes NeedsYou/Decision.
- Commits: 6d... see git log: red tests, feat T9, view tests (red stub then green).

## T6 (done)
- DecisionSheetViewModel (+DecisionOption): options as served (first recommended), Note, Choose(optionId) sends Decide once (never retried; accepted ids remembered; IsSending guard), Refusal/RefusalCode as served, Outcome "Decided: <label>", IsCleared (form disabled, text kept), live announcement, Settled task for tests.
- Shell: ActionRequested -> Decide; refusal opens the Decision sheet (Refused event); sheet follows its row (FollowDecision). X sends the rejectLike option from the row; A only with the sheet open.
- DecisionSheet view in the Decision arm; DecisionLive region. Focus: after the last row clears, KeepFocus; next row takes focus via the selection change.
- Tests: ShellDecideTests (7), NeedsYouHomeTests (7), ShellNeedsYouTests (8).

## Gate (final head)
build -warnaserror 0 warnings; UI unit 1225/1225; integration 429 total, 423 passed, 0 failed, 6 skipped; Aw.Architecture.Tests 117/117; dotnet format exit 0; check-deps ok; sync-rules ok.

## Left
- Accepted outcome is not timed (plan: 2 s then clears on refresh); the cleared line is "<id> cleared", not the served cleared line from GET /decisions?pending=false; 409 refresh re-read and "focus on the control" are not asserted; Voided/Decided-elsewhere cleared line.
- FailureSheetViewModel / ProfileSheetViewModel (T6 sheets) not built: NeedsYouSheet shows served texts only.
- Decision/NeedsYou sheet Enter-into-sheet focus (sheet beside page rules) not done for needs-you rows; page-narrowing beside sheet uses the existing U2 layout.
- Not done from pass 1: CliText backstop, remaining goldens, requests/profile re-reads.
- T7, T8, T10-T20 remain.
