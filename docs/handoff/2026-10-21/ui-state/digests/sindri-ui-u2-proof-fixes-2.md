# Sindri UI U2 proof fixes, pass 2 (PR #197, feat/sindri-ui-u2)

Final pushed head: fa63d2fc. origin/main merged first (7c97ff37); no conflicts.

- nr-0: red 8c3fa9ca, fix a63cab78. IsTitleBlank (raised from the Title setter); TitleLive IsVisible binds it. Test: visible before a blank submit, hidden once a title is typed.
- nr-1: 31a94bdb. Back test now asserts focus is the Page border (deep link) and the opener RowFrame (J, Enter on a missing item). Both were already green on the existing code once Back has focus when pressed (the old assertion simply could not fail); no code change.
- nr-2: 51b7f818 (UiThread.Run, awaited Queue.Refresh). nr-3: 276727e8 (same for ShellNavTests). The flake was the red; ran the pair 3 times green.
- nr-4: f26c612a. Queued-context test (G, 2 s, G, yield, Q). Green on existing code; the Press branch is what it pins.
- nr-5: red 37edfa9a, fix b8b9a21b. ShellViewModel.IsSelectedRowOpen (row drawn + sheet shows it) is the EnterIntoSheet guard; CommandControl.FocusRun returns bool and the heading is the fallback. Added other-row, covering-at-1000 and disabled-first-action tests (only the fallback was red).
- nr-6: red 08e7b367, fix d3c5b177. Writing the test found a real bug: OnSheetDrag threw ArgumentException on a negative width. OnSheetDrag and ReservePage now clamp to Min/MaxWidth. Deviation (narrowed page, provisional owner default) recorded in docs/csharp/sindri-ui.md before "Selection is focus".
- nr-7: ef1dbcf3. Navigation is get-only.
- nr-8: fa63d2fc. TabOutOfOpenRow in ShellWindow.Keys.cs, called from OnKeyDown after Esc/F6; uses FocusManager.FindNextElement (Avalonia's KeyboardNavigationHandler is internal). Headless test: Tab reaches the sheet within 4 presses with the earlier stops inside the row. I could not show it red without breaking the build (IDE0051), so the test is green-with-fix only.

## Gate (final commit fa63d2fc), via heavy
- dotnet build tests/Aw.Sindri.Ui.Tests -warnaserror: 0 warnings, 0 errors.
- Aw.Sindri.Ui.Tests, run 1: unit 482 passed / 0 failed; integration 284 passed / 2 skipped / 0 failed.
- Run 2: unit 492 passed / 0 failed; integration 284 passed / 2 skipped / 0 failed.
  (Unit total differs between runs, 482 vs 492; not investigated, no failures either time.)
- dotnet format --verify-no-changes: exit 0. check-deps: ok. sync-rules --check: up to date.

## Notes
- Test filters need a leading glob (`--filter-method "*Name*"`); `-warnaserror` on `dotnet test` gives exit 5.
- Not run (per scoped gate): whole-solution build, Architecture/Sindri tests, check-rules.
