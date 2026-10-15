# Product Review: Sindri context retrieval engine (round 3, final)

_Reviewed by `/productReview` on 2026-10-10 | Mode: growth | slim_

Plan: `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (revision 4, with the §14 "Round 2"
table). I didn't reopen the owner-settled decisions. Growth means the owner's sessions using the engine more over time,
and the engine spreading to more repos and rings. This is a single-user toolkit.

## Verdict: SHIP

No CRITICAL or HIGH concerns. The round-2 HIGH (pre-numbered migrations) is fixed in "Shared files". All seven
round-2 MEDIUM and LOW product findings are resolved in the normative sections. What's left is four LOW leftovers
(#1–#4): phase prose that wasn't updated to match the new stage table. Each one is a one-line edit, and none of them
changes behaviour.

## Round-2 product findings: verified

| R2 | Status | Where |
|---|---|---|
| #1 HIGH migration numbering | Resolved | Shared files: `NNN_<stage>_<name>.sql`, next free number on the branch it sits on, renumbered on rebase, frozen at merge; only throwaway homes run unmerged ones. Phase 1/3 prose still says "migration 1/2" (#2 below, LOW) |
| #2 rebase ownership / re-proof | Resolved | The team: the stage owner is assigned until merge, rebases its own branch, and redoes the step-9 re-check (the journey once, plus `/review` if the resolution touched non-test source); the lead only signals. Brief item 3 rebuilds `$SINDRI` after each rebase |
| #3 merge-gate caps | Resolved | Keeping agents busy: ≤ 3 building, ≤ 4 ready-unmerged, stacks ≤ 2 deep; at a cap only prep runs; merge-order batch message |
| #4 P-4 usage measure | Resolved (narrower than suggested, sufficient) | §8.5: `context sources status` prints MCP queries in the last 7 days from `query_log` (client `mcp`). This answers the real question: do sessions call it after the snippet? |
| #5 S-7A → S-4 edge | Resolved | Stage table (S-7A depends on S-3 · S-4 · S-FX, wave 4) and mermaid `S4 --> S7A`; critical path updated. Phase 7 prose still has the old waves (#1) |
| #6 S-2W stage | Resolved in the table | S-2W row (S-1A · S-2R, XS, wave 2); S-5A "merges after S-2W"; mermaid dotted edge. Phase 2 prose still has the old "rides in S-1A, or a one-line follow-up" (#3) |
| #7 Phase 4 `--symbol` | Partly | §8.1 now says "4 (files), 9 (symbols)". Phase 4's Files list still doesn't name the `symbol` shape or the `--symbol` path form its signal uses (#4) |
| #8 token budget | Resolved | Each owner reports tokens at every step change; > 3M per stage pauses for the human; tokens appear in the lead's status line |

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Unchanged and sound. MCP in the work repo with the agent-rules snippet (S-1M, wave 2); `item` (S-5B, wave 3); `context.retrieve` (Phase 10, blocked). The caps keep the levers' arrival speed tied to the owner's merge bandwidth instead of piling up stacks |
| Activation funnel | S-1C/S-2R/S-FX → S-1A → S-1M → owner check (`--scope local`, `context.mcp.enabled` approved) → first `context_query` in a session. The owner checks are now explicit steps per stage (`owner-check.md`), so the human side of the funnel is written down instead of implied. No new drop-off risk |
| 10x usage | The query path holds (local SQLite, no model call by default, server-side caps). At 10x stages, the caps (4 ready, depth 2) bound rebase churn, and per-stage homes and ports remove contention for shared state. The heavy-lock slots are the remaining throttle, which is intended |
| Retention hooks | Now visible: the 7-day MCP query count in `sources status` shows whether sessions keep calling the engine. A per-item cadence via `--item` (S-5B) and probes that don't regress guard quality between phases |
| Viral coefficient | N/A: a single-user toolkit. The internal version (a second repo or ring) works through `context.repos`, but still has no onboarding step. That's a later concern, not raised again |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | LOW | **Phase 7 prose has stale waves.** It says "S-7A (the harness on the fixture corpus; wave 3) and S-7B (the owner's corpus; wave 4)", but the stage table, after the S-4 → S-7A edge, has S-7A at wave 4 and S-7B at wave 5 | Change Phase 7's Size line to waves 4 and 5 |
| 2 | LOW | **Phase prose still uses fixed migration numbers.** Phase 1 says "`context.db` migration 1" and Phase 3 says "`context.db` migration 2". Under the named scheme, S-3's number is whatever is next on its base, and it will be 3 if S-5A merges first with a migration. A stage owner reading the phase text could hard-code 002 | Say "S-1A's first migration" and "S-3's migration (named, numbered per Shared files)" |
| 3 | LOW | **Phase 2 prose contradicts the S-2W row.** Phase 2 still says "the `Aw.Context` wiring rides in S-1A, or in a one-line follow-up if S-2R merges after S-1A", while the stage table makes it S-2W, merging before S-5A. Separately, S-5A can be built and its owner check run on a branch that doesn't yet hold S-2W, so ring-1 identifier patterns aren't applied during that owner check. The owner's own machine and count-only records limit the harm | Replace the Phase 2 sentence with "wiring is stage S-2W". In S-5A's row or brief, say its owner check runs after it has rebased onto a base that contains S-2W |
| 4 | LOW | **Phase 4 Files doesn't list the shape its signal uses.** The signal runs `--symbol src/layout/SplitPane.ts`; §8.1 now places file-level `symbol` in Phase 4, but Phase 4's Files list doesn't include the `symbol` shape (file form), its anchor resolution through file-level `implements`, or the CLI/MCP `symbol` argument | Add "the `symbol` shape, file-path form (`--symbol`, MCP `symbol`)" to Phase 4 Files |

verdict: PASS
