# Product Review: Sindri context retrieval engine (round 4)

_Reviewed by `/productReview` on 2026-10-10 | Mode: growth | slim_

Plan: `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (current revision, §14 through "Round 3").
I read the whole spec again from the start. I didn't reopen the owner-settled decisions: Onyx CE, the product graph
as the core, MCP in Phase 1, ring-1 work product, the issue-level backtest, and parallel agent-team delivery. Growth
here means the owner's sessions using the engine more over time. This is a single-user toolkit.

## Verdict: SHIP

There are no CRITICAL or HIGH concerns. All four round-3 product LOWs landed (table below). This fresh pass found four
MEDIUM gaps and two LOW ones. The MEDIUM ones:

- #1: the owner-check seeding path relies on a `map export` that no verb produces.
- #2: S-6's dedup signal depends on S-5B's sync, but the stage table has no edge from S-5B to S-6.
- #3: the backtest's graph arm only ever sees a deterministic, pre-review map, so the §9.3 gate could switch off the
  core layer.
- #4: the Phase 1 agent-rules snippet may go in before the engine has much to return in the work repo.

Each MEDIUM is fixed with one sentence or one edge. None changes the architecture or the phase order.

## Round-3 product findings: verified

| R3 | Status | Where |
|---|---|---|
| #1 Phase 7 stale waves | Resolved | Phase 7 Size: S-7A wave 4, S-7B wave 5, matching the stage table |
| #2 fixed migration numbers in phase prose | Resolved | Phases 1 and 3 now say "its `context.db` migration"; "Shared files" defines `NNNN_<stage>_<name>.sql` in the loader's form |
| #3 Phase 2 contradicting the S-2W row | Resolved | Phase 2 Size names S-2R and S-2W and says S-5A's owner check runs only once its base includes S-2W |
| #4 Phase 4 Files missing the `symbol` shape | Resolved | Phase 4 Files lists "the `symbol` shape in its file-path form (`--symbol <path>`, MCP `symbol`)" |

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Sound: MCP in the work repo plus the agent-rules snippet (S-1M), `item` (S-5B), then `context.retrieve` (Phase 10, blocked). One timing risk: the snippet ships in Phase 1, before the sources where the owner's PRDs actually live (#4) |
| Activation funnel | S-1C/S-2R/S-FX → S-1A → S-1M → owner check (`--scope local`, `context.mcp.enabled` approved) → first `context_query`. The step most likely to lose the owner is the *first answer*: Phase 1 serves repo docs only, and D4/D17 say the PRDs and TDDs are Linear Documents. Real value arrives at S-5B (wave 3) (#4). The owner-check home for stages after S-4 has no defined way to carry the reviewed map (#1) |
| 10x usage | Holds. The query path is local SQLite with no model call by default and has server-side caps. Team caps (3 building, 4 ready, stacks 2 deep) and per-stage homes and ports bound churn. Human time is the binding constraint at 10x stages, and the spec doesn't size it (#5) |
| Retention hooks | The 7-day MCP query count in `sources status` makes use visible. Probes that "don't regress" guard quality between phases. There's no target for that count, so "done at Phase 8 for the owner's use" (P-L1) has no usage criterion (folded into #4) |
| Viral coefficient | N/A: a single-user toolkit. Spreading to a second repo or ring goes through `context.repos` and per-repo `--scope local` registration. Not raised again |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | MEDIUM | **The R3-1 fix points at a verb that doesn't exist.** "Work data stays with the owner" says a check needing a reviewed map "seeds the scratch home by `map import` of an export". The only export in the spec is `map review --export` (§8.7), which writes the *review queue*, not the map. §8.5's CLI has `map import \| build \| status \| review`, with no `map export`. Without it, every owner check from S-4 on (S-4, S-5B, S-CF, S-7B, S-8) needs a fresh `map build` (labeling tokens) and a full re-review in the scratch home: repeated human work at every merge gate | Add `context map export <file>` to Phase 3 (S-3), next to `ImportMap`. It writes the current map plus overrides in the versioned import schema, outside worktrees, and its round trip is tested (export → import into an empty home → same nodes, edges and overrides). Say that the import format is the cross-version contract between a `main` build and a PR build |
| 2 | MEDIUM | **S-6's signal needs S-5B, but the DAG says S-3 · S-5A · S-FX.** The Phase 6 signal "Linear issues cited once with both `linear` and `onyx` refs" needs Onyx's `ENG-482` to resolve to the UUID key. §5.1 says an unresolved identifier is keyed `linear:pending/…` "and merged into the UUID key when the native sync resolves it". Sync is S-5B. On an S-5A base, the fixture would show the issue twice, or the owner would build query-time alias resolution that the spec doesn't describe | Add the edge S-5B → S-6 (S-6 moves to wave 4, off the critical path; the mermaid graph needs `S5B --> S6`). The alternative is to state that S-5A's fetch and search write the identifier→UUID alias, so dedup works without sync |
| 3 | MEDIUM | **The graph arm is structurally pessimistic, and §9.3 can gate the core off.** As-of maps are "deterministic extraction only, no labeling pass, plus only the overrides dated before the cut", and "hand-authored imports count as overrides with their import date". The owner reviews and imports the map *now*, after every sample's cut, so every sample sees an unreviewed, deterministic-only graph. §9.3 then says "a layer … is enabled only when its arm improves recall". Read literally, a weak `graph` arm switches off D1's core deliverable on a measurement that excludes the owner's review | Say in §9.3 that the `graph` layer is never gated off (it's the core). Its arm is reported to tune cascade depth and weights only. Optionally add a `graph+reviewed` arm, labeled `leaky` like edited records, so the report shows the gap between the deterministic and the reviewed map |
| 4 | MEDIUM | **The Phase 1 snippet may teach sessions to skip the tool.** Phase 1's goal is cited results "from the repos' PRDs and plans", but D4/D17 put the owner's PRDs and TDDs in Linear Documents, which arrive in S-5A/S-5B. If the work repo holds few docs, the agent-rules snippet ("before planning a change, call `context_query`") makes every session pay for a near-empty answer. That is the moment the habit forms, and the 7-day count can't tell "used" from "useful" | In S-1M's `owner-check.md`, record the work repo's doc-chunk count and the result count for 3 real questions (counts only). If they're thin, add the snippet at S-5B's owner check (when `--item` works) instead of S-1M. Give P-L1's "done for the owner's use" a usage line, for example MCP queries on most work days over 2 weeks after S-8, with at least one cite per result |
| 5 | LOW | **Owner effort isn't sized.** "Gates that stay sequential" lists the human steps but not how many there are or how long they take. These are: about 10 owner checks (S-1M, S-2W, S-3, S-4, S-5A, S-5B, S-6, S-CF, S-7B, S-8), `profile approve` per new key, map review (the example shows 46 proposals), confirming about 30 probes, Onyx setup and OAuth sign-off, the denylist file, and roughly 16 merges. The agent-side caps keep agents busy, but the owner is the bottleneck, and nothing schedules the owner's time | Add a short "Owner steps" table: step, stage, rough minutes, and whether it blocks a merge. The lead's batch message then cites it, so the owner can plan merge sessions |
| 6 | LOW | **S-7B doesn't wait for S-6, but its signal says `--arms all`.** Onyx depends on outside OAuth sign-off and may merge after S-7B. The `onyx` arm and the "Onyx `Limit` 20 vs 50" ranker arm would then never get a recorded decision | State that S-7B runs the arms whose adapters are merged, and that the `onyx` arms rerun as part of S-6's owner check (or of S-8 if S-6 is merged by then). Don't add a hard edge |

## Recommended Next Action

Make the four one-sentence edits: `map export` in S-3, the S-5B → S-6 edge, the "graph is not gated" line in §9.3, and
S-1M's owner check recording doc counts before the snippet goes in. Record them under a "Round 4" heading in §14, then
hand the spec to `/specToProvenPR`.

verdict: PASS
