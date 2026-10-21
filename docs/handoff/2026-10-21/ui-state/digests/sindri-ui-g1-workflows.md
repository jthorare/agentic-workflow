# G1 workflows builder digest (feat/sindri-ui-workflows*, worktrees /home/thor/repos/aw-wt/sindri-ui-workflows[-N])
Spec: ~/.agentic-workflow/mockups/f3/specs/workflows.md §8 slices 1-4, one PR each, chained branches.
## Slice 1 (in progress)
- red commit: tests RunStatusTests, Token/Contrast/ThemeResources rows, stub RunStatus + StatusBadge (not run red: MA0182 flagged unused types)
- green: RunStatus.From, Violet tokens + Size keys, Icons (Icon.Run*), Labels.Board.cs (Workflows* names), InstanceRowViewModel.Status
- MA0182: an internal type with no production use fails the build, so StatusBadge (copy in scratchpad StatusBadge.cs, badge-styles.txt) moves to slice 2 where BoardView uses it.
