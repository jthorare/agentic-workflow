# sindri-ui g1 run-canvas (builder digest)

Worktree /home/thor/repos/aw-wt/sindri-ui-run-canvas (slice N: -N siblings), branch feat/sindri-ui-run-canvas[-N], from origin/main 234c95b38.
Spec: ~/.agentic-workflow/mockups/f3/specs/run-canvas.md §8 slices 1-4, one PR each, each branched from the previous.

## Slice 1 (controls)
ZoomBorder already has MinZoom/MaxZoom/FitBounds on main (#522). Slice 1 = ContextEdgesControl -> GraphEdgesControl, PortSide, EdgeRoute.Between (horizontal/vertical cubic, k = clamp(dist/2, 40, 110)), MapEdge BrushKey + HitWidth. Context page does not place the edges control yet.
State: red tests written (EdgeRouteTests, GraphEdgesControlTests hit width, ContextControlsRenderingTests brush key), stub throws NotSupported. Not yet committed.
