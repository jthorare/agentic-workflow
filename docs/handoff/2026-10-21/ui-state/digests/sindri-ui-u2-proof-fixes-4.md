# #197 U2 proof fixes, pass 4 (B8, N9)
Merged origin/main first (fd1e38a7). Head: format exit 0, check-deps ok, sync-rules ok (on 1a0a14fa).

Commits
- test: red - the page narrows beside the sheet and adapts, a focused title does not move the layout (B8, N9), 5eb0ee45
- fix: the page narrows beside the sheet and its rows and Home adapt; focusing a title never moves the layout (B8, N9), 1a0a14fa

Design (no ContainerQuery; a view-side inherited class)
- ShellWindow reserves the sheet's clamped width as the page region's right margin again (page = window minus sheet) and sets
  PageLayout.Tier (Views/Shell/PageLayout.cs, PageTier.cs) on the page region from its SizeChanged. ItemRow, QueuePage (header) and HomePage read it.
- Breakpoints (page region width) and columns (id, title*, repo, state, step, since, action):
  - Wide >= 1120: 150,*,150,130,110,70,260
  - Mid 1000-1120: drops step and since
  - Narrow 790-1000: also drops repo; Home stacks Running over Queued (one column)
  - Tight < 790: id 100, title *, action 170 (state, repo, step, since dropped; state stays in the row's name)
  Title column MinWidth 160 stays (B7). At 1280 + 520 sheet the page is 760 (Narrow); with a 760 sheet it is 520 (Tight).
- N9: title presenter has a permanent transparent 2 px border with -2 margin; command-control presenter padding 9,3 on focus; selected row
  padding 14,8,16,8 (its 2 px bar no longer shifts content). Found while testing: selection on focus moved the title 2 px right.
- Doc: docs/csharp/sindri-ui.md paragraph replaced. Test SheetEnterFocusTests drag clamp now expects the page margin = the sheet width.
- New tests: tests/Aw.Sindri.Ui.Tests/Views/ResponsivePageTests.cs (9).

Gate (scoped, via heavy): build tests/Aw.Sindri.Ui.Tests -warnaserror 0 warnings; unit 800/800; integration 297 total, 0 failed, 2 skipped (macOS-only);
no test-host errors. format / check-deps / sync-rules: format exit 0, check-deps ok, sync-rules ok (on 1a0a14fa).
Not covered: compact (Home) rows have no column tiers (they fit once Home stacks); primary-action button visibility with a real action is asserted by column width only (fixture has no primary action).
