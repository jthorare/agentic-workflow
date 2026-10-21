# sindri-ui-r8 review run 2: handoff

Worktree /home/thor/repos/agentic-workflow/.claude/worktrees/sindri-ui-r8, branch feat/sindri-ui-r8-contract, head d0f3a325 (pushed).
Fix list: ~/.agentic-workflow/jthorare-agentic-workflow/plans/sindri-ui/r8-review-run2-fixes.md

## Done (all pushed, Ui unit lane was 857/857 after Q3; later targeted classes green)
- Q1 arch-0: 00a9bfe8 red, 0ff41613 fix (client anchors after Recipe(...), no Calls.cs mention)
- Q2 arch-1: 1db9bf4c red, e6c21dad fix (anchors inside Route() switch above workflow arm, arm GET-only)
- Q3 arch-2/3/4: 535a4a70 red, 1f19bfb1 fix (SheetLink kind, SheetContent panel with anchors, doc updated).
  DEVIATION: the plan's "insert immediately above your own marker" is contradictory for U8 (it would land between the markers),
  so the rule is: U3' inserts immediately ABOVE its marker, U8 immediately BELOW its marker; never between. Every marker says so.
- Q4 testing-0/3: fcd93b2e (tests only, proved by mutation: ConfigureAwait(false), generation guard removed, real-time Task.Delay each fail a test)
- Q5 testing-1: 92a3dd99 (tests only, mutation: removing !OverlayOpen, IsNavigationEnabled, IsChromeVisible gates each fails a test)
- Q6 testing-2: 711cc299 (test only, mutation: a ProtectedLabel mention in ShellText.cs fails it)
- Q7 corr-0/2: bd96f8d1 red, c3e7296a fix (WorkflowAnswers table of recipe goldens + stand-in "no-outputs"; IsNullOrEmpty(hash))
- Q8 corr-1: 73436016 red, d0f3a325 fix (WorkflowAnswers.FactGoldens scenario -> bound instance goldens; project-many-children packet/evidence now 404)

## Left (cheap items, none started)
- a11y-0: ProtectedActionButton.axaml CopyAnnouncementRegion: IsVisible="{Binding IsOpen}" (joins tree on popover open, AttachButton IsRegionInTree pattern);
  ProtectedActionViewModel.Announcement initial value = Label (one resting text; today it starts at Labels.Copy). Tests: ProtectedActionTests (VM), Views/ProtectedActionButtonTests.
- a11y-1: unsafe CLI (Cli empty): popover sentence "The served command can't be shown", Copy named "Copy: no command served" (CopyName today = "Copy: " + Cli) or hidden.
- a11y-2: ShellWindow.axaml.cs Escape(): Esc with a pending chord only cancels the chord (sheet stays open); OnKeyDown already cancels the chord for non-chord keys (Esc cancels), so make Escape() return true right after when a chord was pending. Test in ChordKeysTests (sheet open + G + Esc -> sheet still open, chord gone).
- a11y-3: stale region comment at AttachButton.axaml ~line 46 ("it joins the tree when the menu opens ..." check against IsRegionInTree behavior).
- a11y-4: Labels.Protected.cs OpenInAgentFor: "Open in an agent: <subject>, available with API P10a" (label-in-name order; update ProtectedActionTests/ButtonTests expectations).
- corr-3: GoTo(page, filter) sets the home filter once (ShellViewModel.Chords.cs PressKey calls GoTo then SetHomeFilter; GoTo may also set it).
- Then: merge origin/main (2bc5384a or newer; keep both sides on add-only files; re-derive anything copying the catalog golden), full gate via ~/.agentic-workflow/bin-r1b/heavy under timeout 600:
  Ui unit+integration lanes, Aw.Architecture.Tests, Aw.Sindri.Tests unit lane, `heavy dotnet format --verify-no-changes` (report exit status),
  node scripts/check-deps.mjs, node scripts/check-rules.mjs, LC_ALL=C scripts/sync-rules.sh --check, whole-solution `heavy dotnet build -warnaserror`.
  Not yet run: the Ui integration lane, format, architecture tests after Q1-Q8 changes.
- Push, `gh pr view 215 --repo jthorare/agentic-workflow --json mergeable`, add a "Review run 2" section to the PR body (gh api -X PATCH repos/jthorare/agentic-workflow/pulls/215 -F body=@file), keep the last line.

## Notes
- Tool calls over 120 s get backgrounded; use timeout 118 or run_in_background. Pass one glob per --filter-method / --filter-class (repeat the flag).
- Analyzers (MA0076, MA0002, IDE0042, IDE1006 '_' prefix for private static readonly) fail the build in tests.
