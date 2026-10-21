# Sindri UI U2 proof fixes (PR #197, feat/sindri-ui-u2)

Merged origin/main first (32a1d563). Each item: red test commit, then fix; pushed after each.

- F1 chord: ShellChordTests; fix in ShellViewModel.Keys.cs. The chord ends by a TimeProvider timer (hint hides after 1 s, posted to the captured sync context); an expired chord is no chord, so a late G starts a new one.
- F4 nav: ShellNavTests; NavEntry is now an ObservableObject class with a settable Count, updated in place (no Navigation swap).
- F2 Enter: SheetEnterFocusTests; with the sheet beside the page and open on the selected row's item, Enter focuses the sheet's first action (title if none). NOT DONE: the Tab-off-the-row's-last-control route (the brief said only if clean in Avalonia; the row's own title button sits in the tab order, no clean hook), so Enter and F6 are the ways in.
- F3 ring: the page region gets a right margin equal to the beside-sheet's width (ShellWindow.ReservePage), so the row and its adorner-layer ring end where the sheet starts. Layout change: the page is narrower while a beside-sheet is open (was full width under it).
- F5/N1/N2/N7: CreateDialogLayoutTests. Refusal line is a Grid (detail wraps); TitleLive hidden unless HasTitleError; Cancel is a command-control button and the CommandControl announcer moved last with Height 0 so Create/Cancel align; Confirm/Keep share a row under the question; sheet dismiss accessible name "Close sheet" (visible text stays "Close").
- F6: Back button (NotFoundBack) in the 404 state, closes the sheet via the shell; hidden where the sheet covers the page (the header Back serves).

Gate (owner's reduced set): dotnet build tests/Aw.Sindri.Ui.Tests -warnaserror 0 warnings; Aw.Sindri.Ui.Tests unit+integration (non-heavy) 1076 passed / 2 skipped / 0 failed; dotnet format --verify-no-changes exit 0; check-deps ok; sync-rules --check up to date. Skipped per the owner: whole-solution build, Aw.Architecture.Tests, Aw.Sindri.Tests, check-rules.
Note: ShellItemsTests.A_row_served_elsewhere_... flaked twice in full runs (polling race, passes alone); not touched.
N1 caveat: TitleLive now appears with the error rather than being in the tree beforehand; Linux can't confirm the spoken announcement.
