# #1005 engine half: fact:graph (design note, Phase 1, no code)

## 1. Where lenses.json comes from today
- Recipe `gather` state (recipes/sindri/workflow.scoping.yaml:31-43) runs `step.scope.gather` with three Step inputs: `input.context.lenses` (depth 2, hops state/flow/dependency/component), `input.context.code`, `input.context.history`.
- ScopeGatherStep.Extra (Scoping/ScopeGatherStep.cs:25-34) passes the typed results as lensesView/codeView/historyView; LensGather.Assemble (Scoping/LensGather.cs:20) maps them into `fact:lenses` (4 entries, each with nodes[], edges[], items[]) and `fact:coverage`. LensBuilder (LensBuilder.cs:33) numbers nodes/items `L-<lens>-<n>` per gather (so these ids are NOT stable across a regather); node `key` (feature:/surface:/code:<path>) IS the map's id.
- Run-folder writer: StepFactWriter.Write (Infrastructure/Launch/StepFactWriter.cs:25) writes `<name>.json` per fact the Step Reads. The brief line is AgentStepBrief.cs:31; "You read:" (AgentStepBrief.cs:26) comes from descriptor Reads.
- Readers of fact:lenses (ScopingSteps.cs): draft :59, check :64, interview.questions :70, interview.apply :85, reanchor :91, challenge :97, evidence :111, freeze :121; RelevanceSteps.cs: review :16, judge :25, apply :30 (also writes it); gap-judge (GapJudgeStep.cs:65). gap-search does not read it (spec 966).
- Draft and challenge have NO IAgentStepInstructions today (only relevance-review, gap-search have one: RelevanceBlocks.cs:16, GapSearchBlocks.cs:16). So they get only the generic brief, which is why they parse the blob and grep. This change adds two instruction classes.

## 2. fact:graph shape
```
{ "version": 1, "mapVersion": "v13", "anchors": [id], "depth": 2, "hops": [..],
  "nodes": [{ "id": "feature:x" | "code:path" | "record:..", "kind", "path"?, "symbol"?, "summary", "rank": 1..N,
              "lens": "product|related|code|history", "lensItem": "L-code-3", "band", "citations": ["path:line"] }],
  "edges": [{ "from", "to", "type" }],
  "limits": [{ "why", "not" }],
  "size": { "nodes", "edges", "bytes" } }
```
Changes from the starting point: (a) `id` = the map key (same as lenses `key`), so context_node/neighbors take it as is; items with no key (records, decisions, commits, symbols) use their record id / `code:<path>#<symbol>`; (b) keep `lensItem` as an alias so SCP-CITE-001, the relevance verdict ids and the judge projection keep working unchanged; (c) edges carry only from/to/type (provenance/confirmed stay in lenses.json). Ordered by rank, nodes capped (proposal: 400 nodes / 300 KB, `limits` says what was cut with why:"cap").

## 3. Producer
Context builds it (rank, summary, #938 role globs, #993 edge limiting, citations are all map-side; gather cannot derive rank or summaries from the lens views). New Step input `input.context.graph` (beside ContextLensesInput.cs) calls a new facade method; ScopeGatherStep reads it as `graphView` (add to `_views`, ScopeGatherStep.cs:16, optional like code/history; a missing view is a recorded limit and fact:graph is the lenses projection without rank). Gather validates and writes it as `fact:graph` (a pure LensGather-style mapper `GraphGather`, no I/O). Signature wanted on IContextLenses:
`ValueTask<Result<ScopeGraphView>> ScopeGraph(ScopeGraphQuery query, ContextScope scope, CancellationToken cancellationToken);`
with `ScopeGraphQuery(IReadOnlyList<string> Anchors, MapRead Read, LensRange Range, string? Brief, int MaxNodes, int MaxBytes)` (Brief for relevance rank). ScopeGraphView mirrors the JSON above (it can reuse SubgraphView, Subgraph() already exists at IContextLenses.cs:~45, but lacks rank/summary/roles).

## 4. Brief changes
- New `ScopeDraftInstructions` and `ScopeChallengeInstructions` (IAgentStepInstructions, registered in ScopingServiceCollectionExtensions beside the Gather block), and edits to RelevanceReviewInstructions: "Read graph.json (nodes, edges, size, limits) first and work it with context_node, context_neighbors and context_query; they are scoped to it. Use grep or the checkout only to confirm what the graph shows. Stepping outside the graph is logged; say why in the finding." Gap search keeps its text (may go outside).
- Descriptors: draft, challenge, interview.questions, interview.apply, relevance-review swap `fact:lenses` for `fact:graph` in Reads, so lenses.json is neither in "You read:" nor in the run folder. check, evidence, freeze, gap-judge, relevance-judge/apply keep fact:lenses (provenance and SCP-LENS/REL checks). relevance-apply Writes fact:graph on "applied" too (drops the same entries from the graph).
- Size statement: IAgentStepInstructions.Lines is static, so the brief cannot print N nodes. Instruction says "graph.json's `size` and `limits` tell you how big it is and what was cut; read them first." (open question A).

## 5. Scope-map surfaces cite graph ids
- scope-map schema (schemas/scoping/scope-map.json surfaces[]): add `nodes: [id]` (minItems 0) and `origin: "graph"|"search"`; `key` stays and, when set, must be in `nodes`. Surface found only by grep: `origin: "search"`, `nodes: []`, `graphStatus` new/unknown, and `detail` names the query (DeskBlocks.Readers.cs:129-134 already defaults graphStatus).
- New packet check SCP-GRAPH-001 (free; used: SCHEMA CITE COV LENS ACC ANS TRUST CHAL MAP REL GAP FREEZE; ScopeRules.cs:8-31, add to `All` :35 at ScopeStage.Draft): every id in a surface's `nodes` is in fact:graph; an origin "graph" surface has at least one node; fail otherwise. A surface with origin "search" is a warning-free pass but counted in the check's message (visible to the owner). packet-side rule ids stay schema-only (ScopingRules.cs), so the schema change is one more PKT-SCP-004-like schema line only if graph.json joins the packet (see below).
- Packet: add `graph.json` to ScopePacketFiles.Names (ScopePacketFiles.cs:9, 14 -> 15 files) and PacketKinds scoping list (PacketKinds.cs:17); aw-packets schema rule PKT-SCP-006 for it. Lenses stay.

## 6. Which nodes the agent read
Needs context's side: context logs each context_* call per session (tool, node ids, inside/outside the scoped graph). Engine side: the session -> run correlation (instance address + run token, already in the launch env) and a fold at step.scope.evidence into `fact:runs` (ScopeRunRecord, Core/Scoping/ScopeRunRecord.cs): per Step run `graphReads: { read: [id], outside: [id] }`. No new fact; fact:runs already holds per-run records. Needs a context ledger/log query by session id (open question C).

## 7. Recipe edit list and what moves
workflow.scoping.yaml (v10 -> v11): gather inputs add `{ use: input.context.graph, with: { anchors:{fact:fact:anchors}, depth:2, hops:[...] } }` (:39); gather edge :136 and relevance-apply edge :139 produces add fact:graph; reanchor->gather loop unchanged; evidence/regather edges unchanged; outputs/facts list: add `fact:graph` (type payload) at :13; eval count comment (:204). ScopingSteps.cs descriptors as in 4. Seeds() (ScopingSteps.cs:~126) add fact:graph if facts need seeding (check).
Moves: scoping recipe hash and version; FixtureRecipes.Scoping (hash + states, FixtureRecipes.cs:105); scoping-interview.json fixture; ShippedDescriptorHashes.cs (Gather, Draft, Challenge, InterviewQuestions, InterviewApply, RelevanceReview, RelevanceApply); ShippedBlockSentences.cs (new input.context.graph sentence); evals recipes/sindri/evals/workflow.scoping/*.jsonl (every case that walks gather/relevance-apply, ~46, re-record produces); ScopingBlockRuntimesTests, ScopingBlocksTests, StepFactFilesTests, AgentStepBriefTests; golden workflow-instance JSON; docs/csharp/sindri-scoping.md and schemas/scoping/README.md; Aw.Packets.Tests scoping; Ui ScopeMapViewer (shows surface graph status; Aw.Sindri.Ui.Tests must run per the lane rule).

## Open questions
A. Size in the brief: ok to state it only inside graph.json (`size`), or extend IAgentStepInstructions to take the run's fact sizes?
B. relevance-review reads graph (verdict id = lensItem alias) vs both facts: I recommend graph only. gap-judge: keep fact:lenses or switch to graph?
C. Context: can it key its per-session call log by instance address + run token, and expose a read for evidence to fold? Is the "outside the graph" flag set by context?
D. Is fact:graph in the frozen packet (graph.json, 15th file) wanted, or provenance-only in the run area? It adds an aw-packets schema rule and hash churn.
E. Caps: 400 nodes / 300 KB OK? Who sets them (query params vs profile)?
F. When context returns no graph (not configured), gather falls back to a lenses projection without rank, or fails the Step? I take the fallback + recorded limit.
