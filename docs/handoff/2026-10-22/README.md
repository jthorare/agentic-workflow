# Handoff — 2026-10-22

The sindri team (meta + five lanes) hands off to fresh sessions. Every session's context is past its useful size, and the box
is under memory pressure. Pick up from here, on this machine or another.

One file per lane, each written by that lane:

| File | Lane | Owns |
|------|------|------|
| [meta.md](meta.md) | meta | merge queue, gates, box health, how to run the team |
| [api.md](api.md) | api | HTTP/CLI/MCP surfaces, sessions collector, add repo, setup MCP |
| [engine.md](engine.md) | engine | line recipe blocks (engine half), budgets, forge wiring, recipe versions, box fixes |
| [context.md](context.md) | context | code index (CI-*), index ↔ context map sync (F1P-*), lens reads (S-9) |
| [ui.md](ui.md) | ui | F3 UI redesign, every demo screen |
| [ideas.md](ideas.md) | ideas | line recipe blocks (ideas half), lens packs, bugfix Steps, PR babysitter recipes |

Each lane file lists:
- every branch and PR in flight: number, branch, head sha, state, and what's left;
- every worktree it owns, with whether it still has uncommitted or unpushed work (it shouldn't: see below);
- every builder it was running, and where that builder stopped;
- what's blocked, and on whom;
- the next step, in order.

## Objective (owner, 2026-10-21)

Demo-ready Sindri against web-app:
1. The factory line built out and proven end to end on web-app.
2. The UI redesign complete (the approved F3 prototype, specs on main at `docs/superpowers/specs/2026-10-20-sindri-ui-f3-redesign/`).
3. Context retrieval reading the code index, with the map and the index in sync.

## Nothing lives only on this box

Every lane pushed all in-flight work before writing its file. Unfinished work is pushed as a WIP commit on its own branch, and
the lane file names the branch and sha. Digests under `~/.agentic-workflow/digests/` and worktrees under `~/repos/aw-wt/` exist
on this box only; these files are the portable copy.

## Starting the next team

1. Owner: `aw-meta-launch` (or `aw-meta-launch --no-open` from a meta session) to recreate the panes.
2. Each lane session: read `docs/handoff/2026-10-22/<lane>.md` on this branch and continue from its "Next steps".
3. Meta: read `meta.md`, then run `/meta`.
