# Sindri context retrieval engine: design session digest (2026-10-10)

Task: brainstorm (superpowers:brainstorming, architectural path) the C# Sindri context retrieval engine, write
docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md (ending in **Phases**, one PR each, with an S1 dependency
and an observable app signal per phase), run /autoplan, fix blocking findings, answer the rest in the doc, open a docs-only DRAFT PR, then STOP for approval.
No code. Never edit .claude/worktrees/sindri-s1-app-shell (read-only).

## Settled
- Reframe accepted: the "pieces exist, add retrieval" premise is wrong. The core deliverable is the PRODUCT GRAPH
  (features/surfaces hierarchy + actions + things/state; part-of / depends-on / affects edges; codeRefs, provenance, confidence)
  plus the cross-layer LINKS: intent graph (business) <-> product graph (Sindri-owned, SQLite) <-> code graph (index + graphify).
  Retrieval = cascade expansion over the product graph + RRF across adapters. Generalize linecheck ADR-4/6/8
  (~/Downloads/linecheck-mvp/engineering.md) beyond web routes (commands, panes, keybindings, settings keys).
- Business-intent engine: ONYX CE (MIT outside ee/, ~55 connectors incl. Linear/Slack, OpenSearch hybrid search,
  CE MCP server: one `search` tool + resources). NOT ConnectAI (user's decision). Run unmodified, no fork; Sindri stores refs,
  cursors and derived links, not the corpus.
- Prism: personal only, ring 0 only, last.

## Open (questions still to ask, one at a time)
2. first target repo for the product map (+ test fixture app)
then the user's list: adapter contract; graph/storage model; query planner + citations; scrubbing (Aw.Redaction scrubs
secrets only, so the engine owns a C# ingest sanitizer); surfaces (block/CLI/HTTP/MCP); evaluation (backtest arms, corpus,
bars: TS scope backtest is recall>=0.60, precision>=0.60, beat brief-only baseline); S1 dependencies (profile v1 has no source
config or secret pointers; IScheduledService exists; the API is GetStatus only).

## Key refs
addendum §17.3 (L139), §17.29 (L1979), Q2 (L2100ish); core §6.2 index, §7.5 scoping/backtest, §8.4 scrub, §11.2 adapters;
docs/sindri/scope.md, index.md; sindri/src/scope/source.ts (sanitizeIngest, RefTable); issue #75.

## Settled (cont.)
- Q2 first target: the user's professional work product (ring 1: work accounts, human-gated, assist; company OAuth for Onyx needs
  the user's sign-off; labeling-pass model egress behind a profile switch; map output never in a repo). Plus a fixture app for tests.
- Q3 contract: IContextSource {Describe, Search, Fetch, Probe} + optional IChangeFeed {Changes(cursor)}; SourceRef(Source, Id,
  Version=blob sha|updated_at, Permalink); NO rate limits (all local); per-query deadline; errors Unavailable|NotFound|Fatal;
  degraded partial answers; engine (not adapters) sanitizes+scrubs; xUnit contract base in Aw.Sindri.Testing (or Aw.Context.Testing).
- Ranking: Onyx = recall only (its OpenSearch path is fixed 0.5/0.5 min-max hybrid, no rerank, no recency: TODOs in search.py).
  Sindri ranker: multi-subquery candidates (k~50) + graph neighbors -> RRF -> graph/intent features (cascade distance, edge type,
  provenance, supersession, recency) -> optional rerank (backtest arm, off by default) -> weights are profile artifacts.
- Q4 model: package Aw.Context (tier 1, no Sindri imports). Nodes Feature/Surface/Action/Thing/Flow + Decision (own wording,
  verbatim quote cite, supersedes) + ref nodes Record/CodeUnit. Edges part-of, on, moves-to, reads, writes, depends-on, step-of,
  implements, tests, decides, mentions, supersedes. `affects` computed at query time (depth cap). Stable keys + aliases; provenance
  deterministic|model|human + confidence; human override layer survives rebuilds; orphaned not deleted. SQLite context.db persists
  graph, links, cursors, 14-day scrubbed snippet cache (content hash: never re-send read text), query log (hashes). Live: record
  text, code text, Onyx search. Typed conflicts in results (code = behavior truth, decisions = intent truth).
- Prior art: Atlassian Teamwork Graph + Code Context + CLI; Unblocked (4 retrievers, authority/recency/relevance, reformulate once);
  context-graph (MIT, rules-first linking list); Pendo UI feature tags; linecheck ADR-4/6/8. None has a product layer.

## Settled (cont. 2)
- Q5 planner: shapes node/symbol/item/text (+diff later); anchors -> expand -> subqueries -> RRF -> features -> opt rerank ->
  section packing (graph, decisions, code, records; refs-only overflow; chars/4 estimate) -> cites [Cn] with version; result JSON
  {query, anchors, sections[items{kind,node,text,score,why[],cites}], conflicts, suggestedAnchors, degraded, truncated, budget, mapVersion, asOf, cites}.
- Q6 scrub: IngestSanitizer (new C#) -> IRedactor.Scrub (Blocked drops text, keeps ref) -> trust tag; re-scrub at every egress
  (CLI/HTTP/MCP/labeler); Aw.Redaction gains profile extra patterns (own phase); unversioned cites -> version: unstable; stale flagged.
- Onyx: standard, scoped by its own connector settings. Its Linear connector = issues+comments only -> native read-only Linear
  adapter for projects, Linear Documents (PRDs/TDDs live there, answer A), updates, issue<->PR attachments. Doc version = updatedAt;
  as-of depends on Linear doc history API (verify; else leaky).
- Q7 eval: issue-level backtest (3 related work projects; each issue w/ shipped PR = sample; truth = PR changed files -> nodes;
  git issue keys as fallback); map quality (accept/reject labels, unmapped rate); ~30-question probe set; dimension + ranker arms;
  bars: recall/precision >=0.60 & > brief-only; file recall@20 >=0.50; probe p@5 >=0.80, cite correctness 1.0; map precision >=0.80;
  unmapped <=0.20; p95 2s/5s; no model call per query w/o rerank.
- Surfaces: MCP in PHASE 1 (Streamable HTTP /mcp in the process; manual `claude mcp add`); HTTP+CLI too; block last.
- S1 deps confirmed; phases 1-10 confirmed (see chat list). NEXT: write spec, self-review, commit on branch, /autoplan, fix, draft PR, STOP.

## Status (end of session 2026-10-10)
Spec written, autoplan run (15 HIGH fixed), draft PR #162 open on docs/sindri-context-retrieval (73e361b). STOPPED for owner approval.
Note: a parallel session uses the main checkout (it was on docs/sindri-workflow-engine-design); work on this branch from a separate worktree.
- 2026-10-10: autoplan rounds 1-5 done; round 5 all PASS; MEDIUMs applied (0b95a10). PR #162 draft, awaiting owner approval.
