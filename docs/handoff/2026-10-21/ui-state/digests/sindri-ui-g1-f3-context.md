# sindri-ui f3-context slice 1 (builder digest)

Worktree /home/thor/repos/aw-wt/sindri-ui-f3-context, branch feat/sindri-ui-f3-context (merged origin/main d88864267).
Scope: slice (1) only: tokens Wire/WireHi/LaneFill/RingAnchor, ZoomBorder, ContextEdgesControl, CoverageStrip. Not placed in the page.

## Commits (all pushed)
- e08dfd033 test: tokens red (11 failures seen)
- feat: tokens (Tokens.axaml)
- 9db351a67 test: controls red (stubs; 57 Ui.Tests + 7 rendering failed, token tests passed)
- 71d884a09 feat: ZoomView, ZoomBorder, EdgeRoute (+RouteStep/RouteStepKind/MapEdge), ContextEdgesControl, CoverageStrip (+CoverageArea/CoverageLevel), ControlStyles
- merge origin/main

## Files
src/Sindri/Aw.Sindri.Ui/Views/Controls/{ZoomView,ZoomBorder,EdgeRoute,RouteStep,RouteStepKind,MapEdge,ContextEdgesControl,CoverageStrip,CoverageArea,CoverageLevel}.cs, ControlStyles.axaml (styles for the 3 controls)
Tests: Ui.Tests Theme/{TokenTests,ContrastTests}, Controls/{ZoomViewTests,EdgeRouteTests}, Views/{ZoomBorderTests,ContextEdgesControlTests,CoverageStripTests}; Rendering.Tests Views/ContextControlsRenderingTests
Gate scripts (untracked, excluded): .gate-focus.sh, .gate-full.sh

## Decisions / notes
- Controls live in Views/Controls (the repo's convention), not Aw.Sindri.Ui/Controls as the spec says.
- Hit test is pure geometry (EdgeRoute.DistanceTo) because headless drawing has no stroke hit-testing.
- MA0182 pragmas on the 3 controls ("until slice 2 places the control"), same pattern as AttachButton.
- Wire contrast 2.4:1 on Canvas (decoration; spec: 1.3+); WireHi/RingAnchor equal InkSubtle/InkMuted values.
- Zoom clamp 0.62..1.0; fit centres, left-aligned when the world is wider; pan kept with 120 px of world on stage.

## Gate
(see below, filled when run)

## Result (2026-10-20)
PR #522 (jthorare/agentic-workflow), head a0ae16a20 pushed. Gate on a0ae16a20's tree (last fix = one-line IDE0001, rechecked: build 0 warnings, CoverageStripTests 6/6, format + IDE0001 clean):
Ui.Tests unit 2316 pass; integration 706 pass / 10 skipped; Rendering.Tests 28 pass; Architecture.Tests 128 pass; build -warnaserror 0 warnings.
Left: slice 2+ (VM, layout, placing controls, labels). Questions: none blocking.

## Review round 1 (2026-10-20)
Fixes C1-C4, C7-C9 in one commit 55881f937 (pushed; tests and fixes could not be split: the API changed, tests would not compile). Merge origin/main 360d797cc kept.
Gate: build -warnaserror 0 warnings; Ui.Tests unit 2321, integration 715 pass/10 skipped; Rendering 30; Architecture 128; scoped format + IDE0001 clean.
C5, C6, C10, C11 resolved in writing. Left: nothing.
