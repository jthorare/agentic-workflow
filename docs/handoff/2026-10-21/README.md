# Handoff — 2026-10-21

The sindri team (meta + five lanes) is pausing because usage is nearly out. Pick up from here on another machine.

One file per lane, each written by that lane:

| File | Lane | Owns |
|------|------|------|
| [meta.md](meta.md) | meta | merge queue, gates, box health, how to run the team |
| [api.md](api.md) | api | HTTP/CLI/MCP surfaces, code-index read surfaces (CI-10a) |
| [engine.md](engine.md) | engine | line recipe blocks (engine half), forge wiring, recipe versions |
| [context.md](context.md) | context | code index (CI-*), index ↔ context map sync (F1P-*) |
| [ui.md](ui.md) | ui | UI redesign demo screens |
| [ideas.md](ideas.md) | ideas | line recipe blocks (ideas half), lens packs, PR babysitter recipes |

Each lane file lists: every branch and PR in flight (number, branch, head sha, state, what's left), every worktree under `~/repos/aw-wt/` or elsewhere with whether it has unpushed or uncommitted work, what's blocked and on whom, and the next step.

## Tonight's goals (owner, 2026-10-20)

1. Context retrieval wired to the codebase index.
2. The index and the context map kept in sync.
3. The UI redesign finished through every demo screen.
4. The line recipe blocks fully built out.

## Moving to another machine

Lane worktrees and digests live on this box only (`~/repos/aw-wt/*`, `~/.agentic-workflow/digests/*`). Everything that matters must be **pushed** to a branch; each lane file says which branches carry unpushed or uncommitted work, so nothing lives only on this box.
