# sindri-ui-g1-context-2 (builder digest)
Task: context.md slices 2,3,4 (one PR each, chained). Worktrees: slice2 /home/thor/repos/aw-wt/sindri-ui-context-2 (feat/sindri-ui-context-2); slice3 /home/thor/repos/aw-wt/sindri-ui-context-3; slice4 -context-4.
Scratchpad (all green code written, waiting for the red run): /tmp/claude-1000/-home-thor-repos-agentic-workflow/9d94207a-bc5b-4ae0-8853-ea701a7de3cc/scratchpad/impl (apply.sh copies it into the worktree).
## State (slice 2)
- Working tree holds the RED set only (uncommitted): Views/Context/Map/{MapBand,MapShape,MapNodeLayout,MapEdgeLayout,MapBandLayout,MapLayout,ContextMapLayout(stub throws)}.cs, ViewModels/Labels.ContextMap.cs, tests/.../Context/ContextMapLayoutTests.cs.
- Heavy queue is slow (~40 min wait). Red run queued; log in scratchpad/red1.log. After it shows failures: commit red (git add those), push, then run impl/apply.sh, build, fix, gate.
- NOTE: ViewModels may not use Avalonia.* types (UiRules), so the layout lives in Views/Context/Map (namespace Aw.Sindri.Ui.Views.Context.Map) with plain accessors X/Y/Width/Height/MiddleX/MiddleY.
## Decisions made in code
- Rows (tops): intent records 88, decisions 52 (card H124), anchors 350, row1 chips 370 (grid cols after anchor), surfaces 482, actions/data 568, files 734, tests 802; chip H52; 7 cols per row, wrap +86, bands grow, gap 52/48.
- Edge dedupe on link else (from,to,type); a conflict replaces other edges between its pair with a dashed "conflicts" edge. Half column shift (95) only for a straight vertical skip link.
- Old text lists (Bands/Edges/Coverage/Searched, ContextMore expander, ContextBandViewModel) removed; old labels removed by apply.sh.
- Stage height: page is in a ScrollViewer, so ContextPage.axaml.cs sets stage Height = viewport - top - 24 (min 420).
- Pinned/As of: "At approval" disabled with tooltip; Live checked.
