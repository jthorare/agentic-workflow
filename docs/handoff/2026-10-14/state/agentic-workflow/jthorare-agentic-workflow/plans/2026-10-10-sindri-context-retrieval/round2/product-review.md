# Product Review: Sindri context retrieval engine (round 2)

_Reviewed by `/productReview` on 2026-10-10 | Mode: growth | slim_

Plan: `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md` (revised, with §14 and "Parallel
delivery with an agent team"). I didn't reopen the owner-settled decisions (Onyx CE, the product graph as the core,
MCP in Phase 1, ring 1 as the first target, the issue-level backtest). "Growth" means the owner's sessions using the
engine more over time, and its spread to more repos and rings.

## Verdict: ITERATE

One HIGH concern (#1). The `context.db` migration-number reservations in the new parallel-delivery section break
`Aw.Kernel.Data`'s runner rule. `MigrationChecks.CheckSequence` refuses any migration set that isn't exactly 1..n,
with no gaps. A stage branch built on its dependencies' branches carries a gapped set: S-5A on S-1A has {001, 003},
and S-4 on S-3 has {001, 002, 005}. Those sets fail their own tests. The scheme also makes merges happen in number
order, which defeats the parallel waves. The fix takes one line. Every round-1 product finding is resolved by the
text, except the usage measure from P-4, which was dropped without an answer (#4). The other new concerns are
MEDIUM or LOW.

## Round-1 product findings: verified

| R1 | Status | Where |
|---|---|---|
| P-1 (HIGH) file-level `implements` | Resolved | Phase 4 extractor emits deterministic file-level `implements`; §9.1 truth uses them; §1 diagram; §5.2 note |
| P-2 Phase 1 corpus / Linear before Onyx | Resolved | D17, §5.2, §11, Phases 5/6 swapped; Phase 1 signal runs in the work repo |
| P-3 probe runner early | Resolved | Phase 3 ships `eval probes`; from Phase 3 on, every phase signal includes "probes don't regress" |
| P-4 trigger + usage measure | **Partly.** The trigger is resolved (§8.5 agent-rules snippet, status shows registration and snippet). The usage measure (queries/day, sessions calling, share of results with a cite) isn't in the spec and isn't answered in §14 | See #4 |
| P-5 30-sample floor | Resolved | §9.1 count before Phase 7; below 30, the owner's recorded decision decides, and nothing is enabled automatically |
| P-6 review cost | Resolved | §8.7 impact order, bulk accept for deterministic proposals, `--export/--apply` |
| P-L1 done line | Answered, acceptable | Done at Phase 8; Phases 9 and 10 wait on their blocks |
| P-L2 Phase 2 timing | Answered, consistent with the DAG | S-2R is wave 0; S-5A depends on S-2R. One small gap: see #6 |

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Stronger than round 1. MCP in the work repo with the agent-rules snippet (S-1M, wave 2); the `item` shape (S-5B, wave 3, earlier now that Linear comes before Onyx); `context.retrieve` (Phase 10, blocked). Parallel delivery brings the first two levers forward by about two waves compared with serial phases |
| Activation funnel | S1 merge → S-1C/S-FX → S-1A (CLI/HTTP) → S-1M (MCP in session) → S-3 (hand map, probe confirmation) → S-5B (`--item`). Two kinds of drop-off risk. The owner's own gates on the critical path: probe confirmation in S-3 and map review in S-4. And the merge queue, which grows faster than serial delivery and has no stack-depth limit (#3) |
| 10x usage | The query path holds (local SQLite, no model call by default, p95 ≤ 2 s). At 10x stages in flight, the bottleneck moves to the owner's merges and to rebase churn in stacks. Re-proving a rebased stage isn't defined (#2) |
| Retention hooks | The per-item cadence comes earlier (S-5B). Retention still can't be seen: `query_log` records a `client` per query, but no surface reports usage, so the owner can't tell whether sessions call `context_query` after the snippet is added (#4) |
| Viral coefficient | N/A in the usual sense: a single-user toolkit. The internal version is a second repo or ring. `context.repos` exists, but there is still no step for onboarding a second repo (map scope, probes). Round 1 listed this under Add only; not re-raised as a concern |

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | HIGH | **Migration reservations break the runner and force merges into number order.** "S-1A `001`, S-3 `002`, S-5A `003`, S-5B `004`, S-4 `005`, S-7A `006`, S-8 `007`". `Aw.Kernel.Data`'s `MigrationChecks.CheckSequence` requires versions 1, 2, 3, … with "no gaps, duplicates or reordering". A stacked or parallel stage's branch only holds its dependencies' migrations. S-5A on S-1A has {1, 3}, and S-4 on S-3 has {1, 2, 5}, so their own tests fail. The rule "tested on the schema of every migration below its number" can't run either, because the lower ones aren't on the branch. Even if renumbered on rebase, the fixed numbers make merges happen in number order: S-4, on the critical path, would wait on S-5B, and S-7A on both | Reserve **slots by name, not number**. Each stage writes its migration as the next number on its own base. When it rebases onto `main` after a dependency merges, it takes the next free number (an `.sql` rename; the content is unchanged, and nothing has shipped to the owner's db until merge). Put the merge-order rule in `stages.md`: migration numbers follow merge order. Drop the "tested on every migration below its number" sentence in favour of the existing per-migration test on a copy of the previous schema |
| 2 | MEDIUM | **Rebasing a stack invalidates the proof, and nobody owns it.** `/specToProvenPR`'s evidence and zero-blocking review are bound to the PR's commits. After a dependency merges, "the lead rebases the stack", but the lead "never writes product code", and conflicts "are resolved by the later PR's rebase". By then the stage owner's job has ended at "PR ready". A rebased stage (S-3 onto S-1A's merged form after merge-gate edits, say) is no longer the proven commit | State who rebases (the stage owner, resumed from `stages.md`). State what re-proof a rebase needs: a clean rebase with no conflicts in the stage's own files reruns the unit and integration lanes and re-checks the signal; a rebase with conflicts or merge-gate changes in the dependency reruns hand test, evidence and a review pass. The lead only detects and dispatches |
| 3 | MEDIUM | **The concurrency cap doesn't limit work waiting on the owner.** "Stages waiting at the merge gate don't count against the cap", so review-clean PRs can pile up. The owner's merge bandwidth is named as a bound but never enforced. A deep unmerged stack (S-1A → S-3 → S-4 / S-5B → S-7A) multiplies rebase churn (#2) whenever the owner asks for a change low in the stack | Add a second limit: at most N (for example 4) stages ready and unmerged, or a stack depth of 2 or less above `main`. When it's hit, idle teammates do prep work only. The lead's batch message already exists; this keeps it short |
| 4 | MEDIUM | **No usage measure (round-1 P-4, second half, dropped without an answer).** The snippet is how sessions get told to use the engine, but nothing shows whether they do. `query_log` already has the data (client, timestamps, cites) | Add one line to `context sources status`, or to a `context usage` view: queries per day by client, MCP sessions in the last 7 days, and the share of results with at least one cite. No text, only counts, which is consistent with §7 logs. Phase 1 (S-1A) already owns `query_log` and `SourcesStatus`, so this is XS |
| 5 | MEDIUM | **S-7A is missing a dependency on the extractor.** §9.1: "the map used for a sample is built at the cut commit with the extractor version under test". The extractor is S-4, but S-7A depends only on S-3 and S-FX. Without S-4, the harness can only replay hand imports, so the `graph` arm and the as-of map build can't be shown on the fixture corpus in S-7A's own signal | Either add the S-4 → S-7A edge (S-7A moves to wave 4, beside S-7B; the critical path doesn't change, since S-7B already waits on S-4), or scope S-7A explicitly to import-only as-of maps behind an `IMapBuilder` port, with the extractor-backed builder plugged in by S-4 or S-7B. Update the mermaid graph to match |
| 6 | LOW | **The S-2R wiring follow-up isn't a stage.** "The `Aw.Context` wiring rides in S-1A, or in a one-line follow-up if S-2R merges after S-1A". The follow-up has no row, and S-5A depends on S-2R, not on the wiring. Intent sources could land with the profile patterns compiled in `Aw.Redaction` but never applied in `Aw.Context` | Make the wiring an explicit row (S-2W: depends on S-1A and S-2R; XS), and make S-5A depend on S-2W instead of S-2R |
| 7 | LOW | **The Phase 4 signal uses a shape that §8.1 places in Phase 9.** The Phase 4 signal runs `--symbol src/layout/SplitPane.ts`. §8.1 lists `symbol` under "Phase 9" (its anchors say "file level from Phase 4"), and Phase 4's Files list doesn't add the shape or the CLI flag | Mark the `symbol` shape as "4 (file), 9 (symbol)" in §8.1, and add the shape plus the `--symbol` path-only form to Phase 4's Files |
| 8 | LOW | **No spend bound for the team.** Three stage owners, each with implementer subagents and `/review` fan-outs, run continuously ("recommended answer: run continuously"). The spec bounds model egress for the product (labeling and backtest caps) but not for building it | Add a per-stage or per-wave token budget to `stages.md` that the lead reports in its status line (scorer can measure it). When it's exceeded, the lead stops starting new stages until the owner answers. Optional for a personal toolkit, but cheap |

verdict: NEEDS_WORK
