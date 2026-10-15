# Plan: Sindri application design epic (PR #170)

This plan is four documents, reviewed as one. Read all four, from the worktree `~/repos/agentic-workflow/.claude/worktrees/sindri-epic` (branch `docs/sindri-app-epic`):

1. `~/repos/agentic-workflow/.claude/worktrees/sindri-epic/docs/sindri/README.md`: "The application", "Design specs for the application", and "Cross-spec reconciliation" X1–X18. This is the core doc and the index.
2. `~/repos/agentic-workflow/.claude/worktrees/sindri-epic/docs/superpowers/specs/2026-10-10-sindri-api-surface-design.md`: control plane API (CQRS, callers and action classes, projections, SSE, the shared `/sindri/mcp` endpoint, `sindri open`), phases P0–P11.
3. `~/repos/agentic-workflow/.claude/worktrees/sindri-epic/docs/superpowers/specs/2026-10-10-sindri-workflow-engine-design.md`: recipes, blocks, validator, instances, timers, approval (rev 5), phases P1–P7.
4. `~/repos/agentic-workflow/.claude/worktrees/sindri-epic/docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`: product graph, Onyx and Linear adapters, planner, evals, phases 1–10.

Background (read only as needed): factory addendum `~/repos/agentic-workflow/.claude/worktrees/sindri-epic/docs/superpowers/specs/2026-10-09-sindri-factory-addendum.md` §17.27–§17.29; S1 contract `docs/csharp/sindri.md` on `origin/feat/sindri-s1-app-shell` (`git -C ~/repos/agentic-workflow/.claude/worktrees/sindri-epic show origin/feat/sindri-s1-app-shell:docs/csharp/sindri.md`); dashboard spec `2026-10-09-sindri-dashboard-design.md`.

## Review scope
Each spec already passed its own /autoplan rounds; don't re-review settled internals. Review the **cross-spec coherence** after the X1–X18 reconciliation applied on 2026-10-11:
- contradictions still left between the three specs (names, routes, classes, codes, files, phase numbers);
- ownership gaps: things two specs assume the other owns, or nobody owns;
- build order: S1 → API P0–P2 → engine P2+ and context Phase 1 → the rest; stacked PRs #166–#169 already built ahead of API P1;
- the owner's decision that the UI is a full desk client (starts workflows including scoping, answers parks, takes over and hands back, hands off to Warp with Claude Code, Codex or Cursor), with `protected` actions copy-the-CLI;
- the one shared MCP endpoint `/sindri/mcp` and per-repo registration;
- the provider-neutral model port (X18): every model call through Judge's `IModelProvider`, sessions through `ILauncher` targets;
- the reserved UI design slot: what the UI design spec must settle that these specs leave open.

Threat model: gates guard against honest mistakes by agents, not a hostile agent (repo convention). Single-owner personal toolkit.
