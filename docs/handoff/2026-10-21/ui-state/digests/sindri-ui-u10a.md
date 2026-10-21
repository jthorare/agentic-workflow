# Sindri UI U10a handoff (read-only recipe sheet)

Branch `feat/sindri-ui-u10a` at ~/repos/aw-wt/sindri-ui-u10a, cut from feat/sindri-ui-u8, with origin/main (and the u8 branch) merged in. No PR opened.

## Done
- T0: lane anchor `LANE ANCHOR U10` (kinds: Recipe) below U8's arms in ShellViewModel.Sheet.cs, after InstanceSheetScroll in ShellWindow.axaml, in sindri-ui.md; LaneAnchorTests updated, ShellSheetKindTests updated.
- T1: RecipeSheetViewModel (header id@hash12 · vN · urgency, name template, not-found/unreadable/refused with Back, failed re-read keeps content, "no longer served" note). `DeepLink` accepts one `@` in recipe links only (recipe/id@hash). Fake serves a recipe by hash prefix (8+ hex, with/without sha256:), records LastRecipeRead; FixtureInstances.Recipe/RecipeIds.
- T2: RecipeStatesViewModel/RecipeStateNodeViewModel (served order, branch folds, loop between neighbours = "⇄ name ≤cap" link, other loops = notes, outputs under their from state, no terminal marks), RecipeFactsViewModel/RecipeFactRowViewModel (inputs/outputs, null label -> key, generate line, unknown type -> payload, no-outputs sentence).
- T3: Views/Recipes/{RecipeSheetView,RecipeStatesBand}; Border-wrapper names, peer tree asserted from the sheet down (RecipeFramesTests, 2 windows, 4 frames); sheet width 760 (ShownSheetWidth, ResizeSheet skip).
- T4: instance frame "Recipe ▸" link (pinned id@hash12, stacks on trail, Back returns to frame), Workflows group-head "Recipe ▸" button (approved id, opens over the current page, no trail), deep link. Both use OpenSheet, not Navigate, so the page behind does not change.
- T5: docs/csharp/sindri-ui.md "U10a" section, Lane anchors section, screens spec 4.3 build note.
- Fixed new-analyzer sites from main: WorkflowsLegend.Name const (CA1802), LauncherContract RealTime.NewGuid (RS0030).

## Not taken / deviations
- Recipe link accessible name is "Recipe <subject>, open" (plan said "Open recipe <id>"): the name must contain the visible word "Recipe" (WCAG 2.5.3).
- Esc-returns-focus-to-opener for the recipe sheet relies on the generic sheet opener; no new focus test (no headless focus test added, to save the integration budget).
- J1-J7 live proofs (sindri-prove, AT-SPI) not run.
- Gaps G1-G8 kept as provisional defaults (nothing terminal, approval, gates, stats shown).

## Gate (final head, see report)
Whole-solution build -warnaserror 0/0; Ui unit 1647 pass; Ui integration 580/590 run (590 total incl. skips), 0 failed, 4m07s wall under box-wide heavy-lock contention (budget 90 s not measured cleanly); Rendering 21/21; Arch 119/119; format exit 0; check-deps ok; sync-rules ok.

## Review run 1 fixes + proof findings (head 5a1f8bba6)
- r1-0: `ReturnFocus` handles a Recipe leaving (new FrameIntent.FocusRecipe / FrameFocus.Recipe -> the frame's `InstanceRecipe` link). Headless test at 1600 px in ShellInstanceFocusTests.
- r1-1: `OpenerRecipeGroup` recorded by the group-head button, cleared on every open/reopen, resolved in `SheetOpenerFrame` (`OpenerRecipeFrame`); headless test at 1000 px in the same window.
- r1-2: `Labels.RecipeUnreadable`. r1-3: no step links; only served loops link states (docs updated). r1-4: `InitialFocus` removed.
- Proof 1: Esc follows U8's rule (ends the whole stack; focus to the Workflows row that opened the first frame, tested). A deep-linked frame has no opener and Esc lands on the page region as for every U8 frame; not changed (U8's behavior, not a recipe case). Back lands on the Recipe link.
- Proof 2: empty loop link TextBlock now hidden (`HasLink`); test: no nameless Text node in the band. Proof 3: the second id (RecipeTitle) dropped; the header `id@hash12` is the one, now body-strong.
- Not taken: a not-found-recipe-stacked-on-frame Back variant (same `Back()` path; the fake always knows an instance's pinned hash).
- Gate at 5a1f8bba6: build -warnaserror 0/0; Ui unit 1657/0 failed; Ui integration 591 total, 0 failed, 1m36s wall (budget 90 s; merge gate should run lane-budgets.mjs on a Release build); Rendering 21/21; Arch 119/119; format 0; check-deps ok; sync-rules ok.
