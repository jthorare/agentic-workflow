# Product Review: Sindri context retrieval engine (the product graph and its retrieval)

_Reviewed by `/productReview` on 2026-10-10 | Mode: growth_

Plan: `docs/superpowers/specs/2026-10-10-sindri-context-retrieval-design.md`. Read with addendum §17.3 and §17.29.4.
Settled decisions were not reopened: Onyx CE as the intent engine, the product graph as the core deliverable, MCP in
Phase 1, ring 1 as the first target, and the issue-level backtest on the owner's three Linear projects. This review
asks whether the scope, phase order and success criteria deliver them. In growth mode, "growth" here means the
owner's sessions using the engine more over time, and its spread to more repos and rings. There are no external
users, so viral items are read in that sense.

## Verdict: ITERATE

One HIGH concern (#1): as written, the success criteria can't be measured on the owner's corpus until Phase 9.
Backtest truth is "files its PRs changed, mapped to nodes through `implements`", but only the Phase 9 code adapter
creates `implements` links, and that adapter depends on the code index block, which has no spec yet. Phase 7's
signal and Phase 8's arm-gated enabling would therefore run with no ground truth. The fix is small: the Phase 4
extractor should emit file-level `implements` edges from git. With that fix, and the MEDIUM items handled in the
plan step, the plan is sound. The premise correction (§1) is right, and the hand-authored map in Phase 3 is a good
fast path to value.

## Lens checklist (growth)

| Lens item | Result |
|---|---|
| Growth levers | Three levers. (1) MCP in the owner's Claude Code sessions (Phase 1): built in, but nothing prompts a session to call it (#4). (2) The `item` shape, which ties queries to the Linear item being worked on (Phase 6): the strongest built-in lever, and it comes late (#2). (3) The `context.retrieve` workflow block (Phase 10), which makes retrieval automatic but is blocked on the workflow engine. Reuse in ring 2 through the separable `Aw.Context` (D5) is a fourth lever, but no onboarding path exists for it (Add) |
| Activation funnel | The path is: register MCP by hand → add the profile `context:` key → search repo docs (P1) → import a hand map (P3) → extract and review a map (P4) → company OAuth sign-off for Onyx (P5) → the native Linear adapter (P6). The biggest drop-off is P1–P4 on ring 1. The owner's PRDs and TDDs live in Linear Documents (D4), not in repos, so until P5/P6 the only intent source on the first target is thin. P5 also has an external gate, the company OAuth sign-off (#2) |
| 10x usage | At 10x queries per session the query path holds: there is no model call without rerank, the p95 target is 2 s, and the store is local SQLite. At 10x repos or map size, the first thing to break is the owner's review time. Assist mode makes every extracted node a draft, and unmapped ≤ 0.20 depends on review volume. The review UX, a CLI accept/reject, has no ordering or bulk path (#6) |
| Retention hooks | The work has a natural per-item cadence: each Linear item started is a moment to query. The plan doesn't use it until Phase 6, and doesn't push it at all until Phase 10. `query_log` exists, but no usage measure is reported back, so the owner can't see whether sessions actually use the engine (#4) |
| Viral coefficient | N/A in the usual sense: it's a single-user personal toolkit. The internal equivalent is spread to a second repo or ring. D5 makes the package liftable, but the spec gives no step for adding a second profile repo to the map and the probes (LOW, see Add) |

## Strengths

1. **The premise correction is right and it drives the scope.** The plan builds the product graph and the join,
   which nobody else builds, and consumes intent search, which is commodity. That avoids a weaker copy of
   Onyx/Glean (§1, D1).
2. **Phase 3's hand-authored map gives cascade answers early,** before extraction quality is known. That is the
   fastest honest path to the core value, and the override layer (D8) keeps the owner's work safe across rebuilds.
3. **D3 and D4 are grounded in code, not marketing.** Onyx's fixed 0.5/0.5 fusion with unused `hybrid_alpha`, and
   its Linear connector's missing projects and documents, are cited by file and line. That justifies "recall from
   Onyx, ranking in Sindri" and the native Linear adapter.
4. **The result contract is useful.** Every claim is cited with a version, `stale` and `unstable` are shown rather
   than hidden, `why[]` explains each placement, and typed conflicts (D10) are reported, never resolved silently.
   It serves the owner, evolve and backtest debugging at once.
5. **Arm-gated enabling (§9.3) carries over §17.3 Proof.** No layer or ranker stage is on by default without
   beating the baseline, and the rerank stays off until it beats the bars (D13).

## Concerns

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | HIGH | **The success criteria can't be measured until Phase 9.** §9.1 truth maps PR-changed files to nodes "through `implements`", and file recall@20 needs code results. Only Phase 9's `code` adapter produces `implements` links, and it waits on the code index block, which has no spec (§11). Phase 4's extractor lists deterministic anchors (registries, routes, test ids) but never says it emits `CodeUnit` refs. So Phase 7's signal (per-arm node recall on the three projects) and Phase 8's "the profile enables only the arms that passed" have no valid ground truth on the owner's corpus | Make Phase 4 emit **file-level** `implements` edges, with `CodeUnit` refs read from git at the map's commit sha, from every deterministic anchor (the file that registers the command, route or setting). That needs no code index. Phase 9 then refines them to symbols. State in §9.1 that node truth uses these file-level links until Phase 9, and add a Phase 4 signal: N% of anchors carry an `implements` file ref |
| 2 | MEDIUM | **On the stated first target, real value starts at Phase 5 or 6, behind an external gate.** The owner's PRDs and TDDs are Linear Documents (D4), so the Phase 1 `repo-docs` source has little ring-1 intent to return. Phase 1's signal query ("split pane orientation") doesn't say which repo it runs on. Onyx (P5) waits on company OAuth sign-off, and the native Linear adapter (P6) comes after it even though the backtest corpus (issue↔PR attachments, PRDs) needs Linear, not Onyx | Name the Phase 1 corpus (work repo or this toolkit) and what it should return. Consider **swapping Phases 5 and 6**: the native Linear adapter, with secret pointers moved alongside it, uses a personal read-only key, gives PRDs and TDDs, the `item` shape and the backtest corpus, and isn't blocked on the Onyx OAuth setup. Onyx then adds Slack and recall on top. This keeps D2 and D4 unchanged; only the order moves |
| 3 | MEDIUM | **Phases 4–6 ship layers without measurement.** The eval harness is Phase 7, so the extractor, the Onyx linking and the Linear links all land judged only by their phase signal. That runs against the spec's own Proof rule. The probe set (§9.2.3: about 30 questions, about one minute, no Linear corpus needed) doesn't depend on Phase 7 | Move a **slim probe runner** (probe file outside repos, precision@5, citation correctness) into Phase 3, built against the hand map, and make "probes don't regress" part of each later phase's signal. Phase 7 keeps the backtest, the corpus builder and the arms |
| 4 | MEDIUM | **The MCP tool is available but nothing gets it called, and its use isn't measured.** Before the switch, nothing in `setup.sh`, the hooks or the skills may point at the C# build, so no session is told when to call `context_query` until the Phase 10 block, which waits on the workflow engine. `query_log` records queries but no use metric is surfaced | Phase 1 adds a documented manual step next to the MCP registration: a short instruction for the owner's work-repo agent rules ("before starting a Linear item or a design question, call `context_query`; cite `[Cn]`"). From Phase 6, use the `--item` form. Add a usage line to `context sources status` (queries per day, sessions calling, share of results with at least one cite) so retention can be seen |
| 5 | MEDIUM | **The 30-sample floor may make the bars unreachable.** "A run with fewer than 30 issue samples … can't pass", and layers are enabled only by passing arms. If the three projects together have fewer than 30 issues with merged PRs, nothing can ever be enabled on ring 1 | Count eligible issues now; the TS `sindri scope --backtest linear:<project>` can do it. If the count is below 30, define the small-corpus gate up front: probe precision@5 and map precision decide enabling, and the backtest is reported as `small sample` for direction only |
| 6 | MEDIUM | **Owner review time is the scaling bottleneck.** Assist mode makes the extracted map a draft. Map precision and the unmapped rate both depend on accept/reject volume, and `map review` gives no ordering, batching or bulk path. At 10x nodes, or a second repo, review becomes the cost | Phase 4 orders the review queue by impact (nodes touched by recent issues or PRs first, then by confidence), and allows bulk accept of deterministic nodes above a confidence threshold, recorded as human overrides. Report the remaining review backlog in `map status` |
| 7 | LOW | **There is no "done" line for the whole spec.** Phases 9 and 10 depend on blocks that have no spec (code index, workflow engine), so the spec can't be finished on its own | Define engine v1 as done when Phases 1–8 are shipped and the §9.4 bars are met, or the small-corpus gate if #5 applies. Track Phases 9 and 10 as follow-ons owned by those specs |
| 8 | LOW | **Phase 2 comes after Phase 1 on a ring-1 profile.** If Phase 1 runs on the work repo, work identifier shapes leave over MCP before the profile patterns exist. The impact is small because the same sessions already read those repos | Run the Phase 1 signal on a non-work repo, or fold the Phase 2 wiring into 1b. Either is fine |

## Scope Suggestions

### Cut
- **Nothing structural.** Each phase earns its place. The `diff` shape is already out of scope, which is right.
  Phase 8's rerank arm could slip past v1 if the corpus is small (#5), since it can't be judged on fewer than 30
  samples.

### Keep
- **Phase 3's hand-authored map before the extractor:** the fastest route to the core value, and the seed for the
  probe set.
- **D7 (cascade at query time) and D8 (override layer):** these keep the graph trustworthy as it changes, and trust
  is what brings the owner back.
- **MCP in Phase 1 (D11):** the right surface for the main consumer. It needs a trigger (#4) to turn into use.

### Add
- File-level `implements` edges from the Phase 4 extractor (#1).
- A slim probe runner in Phase 3, used as the regression gate for every later phase (#3).
- A usage line in `context sources status` and the manual agent-rules snippet (#4).
- An impact-ordered review queue with bulk accept for deterministic nodes (#6).
- One short paragraph on adding a second repo or profile: map scope, probes, and the separation check. It's the
  internal path for growth to more rings.

## Key Questions for the Team

1. How many issues across the three Linear projects have a merged PR linked by attachment, or by the issue key in a
   PR title or branch? Is it at least 30?
2. Which repo and which docs does Phase 1 search for the owner on ring 1, and what should the first useful answer
   look like there?
3. Can the native Linear adapter go before Onyx (swap Phases 5 and 6) without breaking D2 or D4? Is the company
   OAuth sign-off for Onyx already scheduled?
4. Do the Phase 4 deterministic anchors always resolve to a source file at the map's commit sha, so that file-level
   `implements` truth is available without the code index?

## Recommended Next Action

Before `/specToProvenPR`, amend the Phases section:
- Phase 4 emits file-level `implements` edges and gets a matching signal.
- A slim probe runner moves into Phase 3.
- Decide the Phase 5/6 order.
- Record the eligible-issue count, with the small-corpus gate if it is under 30.

Then rerun this lens or `/autoplan`.

verdict: NEEDS_WORK
