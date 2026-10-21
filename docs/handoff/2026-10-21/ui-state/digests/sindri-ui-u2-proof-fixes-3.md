# #197 U2 proof fixes, pass 3 (B7)
Head pushed: f2ce9fc5 (feat/sindri-ui-u2). Merged origin/main (b43f85f) first as cf19c224.

Commits
- test: red: the title column keeps its width beside the sheet; a row draws its own focus border (the red commit did not compile: it lacked two usings, added in the fix commit)
- fix: rows draw their own focus ring and the page runs full width under the sheet (B7), f2ce9fc5

Gate (scoped, via heavy): build tests/Aw.Sindri.Ui.Tests -warnaserror 0 warnings; unit 800/800; integration 288 total, 286 passed, 2 skipped (macOS-only), 0 failed; dotnet format exit 0; check-deps 0; sync-rules --check 0. No test-host errors.

Changes
- ShellWindow.axaml.cs: removed ReservePage and the page right margin; OnSheetDrag keeps the clamp.
- Focus now: Views/Queue/QueueStyles.axaml. Border.item-row and Buttons inside it have FocusAdorner null. Row on :focus-visible gets a 2 px Brush.PrimaryHover border on its own Border (padding reduced so content does not move). A focused button in a row (title, primary action) gets the same 2 px border on its PART_ContentPresenter. The ring is in the page's visual tree, so the sheet covers it and the page ScrollViewer clips it. Applies to ItemRow and CompactItemRow.
- ItemRow.axaml / QueuePage.axaml header: title column is now `*` with MinWidth 160.
- SheetEnterFocusTests: replaced the two F3 tests (page reserve, ring over sheet) with: drag clamp (sheet width clamped, page margin default), title column >= 160 and title button width > 0 at 1290, row has no adorner and shows its border, title button has no adorner and shows its border.
- docs/csharp/sindri-ui.md: the "page narrows" deviation paragraph replaced by a short "Focus rings on rows" note.
Not covered: the primary-action CommandControl's inner button relies on the generic `Border.item-row Button` style; not separately tested.
