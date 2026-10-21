# #224 U3' proof fixes handoff

Final pushed head: e29a2961 (feat/sindri-ui-u3). origin/main had not moved (no merge).

## Causes and fixes (red test commit, then fix)
- B1 focus dropped ~2 s after J/K: NeedsYouViewModel.Rows and WorkflowsStripViewModel.Rows were fresh lists on every re-read, so the ItemsControl rebuilt every container (and the row's Controls were new objects each time). Fix: one ObservableCollection each, patched in place with Patch.To; NeedsYouRowViewModel.Controls cached and rebuilt only when the served actions differ; the strip keeps a row VM while its Signature (everything but age) is unchanged. Tests: Views/NeedsYouFocusTests (row, row control, cleared row -> next row), Workflows/StripReconcileTests. ae21fb07 red, 8f2c57dd fix.
- B2 no Undo: NeedsYouSeenViewModel.UndoOffered/UndoText were bound by nothing. Fix: HomePage header "N marked seen · Undo" (NeedsYouUndo, NeedsYouUndoButton, a plain tab stop), polite live region NeedsYouSeenLive in ShellWindow (flag first, never empty, announced once), KeepFocus when the offer goes. Tests: Views/NeedsYouUndoTests. b52895e0 red, 43206518 fix.
- B5 reduce motion: ShellViewModel.ApplyAppearance never told the Needs-you rows. Fix: it calls NeedsYou.Seen.SetInAppReduceMotion. Tests: Shell/ShellReduceMotionTests, plus a view test in NeedsYouHomeTests (outlined class, NewWord, no pulsing). b00291d8 red, 6ef3d217 fix.
- B6 empty strip + log flood: a 200 in P4's older shape fails to parse (Invalid, SND-UI-003) and ReadStrip ignored it; the client logged every parse failure. Fix: WorkflowsStripViewModel.MarkUnreadable (IsUnreadable/UnreadableText, Labels.StripUnreadable, view element WorkflowsUnreadable); SindriApiClient logs an unreadable answer once per route and distinct message, until the route reads well. Tests: Shell/StripRequeryTests, Api/SindriApiClientTests, Views/WorkflowsStripFramesTests. 14900cc3 red, dff0f307 fix. Doc says main still serves P4's shape until API P11 / #250.
- B4: NeedsYouEntry.Question (mapped from the pending decision), DecisionSheetViewModel.Question prefers it, else the title. DecisionsFramesTests updated to assert the served question. f4b483a5 red, a1960538 fix, e29a2961 (test update).
- F1-F3 (scripts/sindri-ui-fixture.sh, bash test first): items --large files 12 requests with no repo (FIXTURE_REPO when set; the profile has no repos); request sends `item`; workflows passes --fact fact:item (JSON {id}) / fact:request (text) from files under $AW_SINDRI_HOME/ui-fixture-facts, prints "started <recipe> on <item>", dies with the CLI's output on a refusal. 647d3e4d red, 54073e97 fix.
- docs/csharp/sindri-ui.md updated (dbf5d6b5).

## Gate on e29a2961
- dotnet build tests/Aw.Sindri.Ui.Tests -warnaserror: 0 warnings, 0 errors
- Ui unit lane: 1310/1310
- Ui integration lane: 478 total, 468 ok, 10 skipped (macOS-only), 0 failed, no "error", no exit 134
- Aw.Architecture.Tests: 117/117
- dotnet format --verify-no-changes: exit 0
- check-deps ok; sync-rules up to date; sindri-ui-fixture.test.sh: 110 ok, 0 not ok

## Left
- Not re-proved in the real app (the prover re-runs J1, J3, J4, J7, J9 on the new head).
- After X/A/409 focus: covered when the row clears (next row) and by KeepFocus; a 409 that keeps the row relies on the in-place reconcile (no separate test of the 409 path beyond DecisionsFramesTests' existing one).
- No key for Undo (button is reachable by Tab only).
- Nits N1-N4 untouched. Strip unreadable sentence applies only to kind Invalid; other failures still keep rows.
- Pass-6 leftovers unchanged (owner answer on the fold default, 1.3 cap test, CliText backstop).
