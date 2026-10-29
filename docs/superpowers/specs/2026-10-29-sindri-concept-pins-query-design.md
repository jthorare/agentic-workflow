# Sindri context: concept pins scope and steer queries (#1372, query side)

**Status:** draft for the owner's read (owner q141, 2026-10-28). Not ready until the owner has read it.
**Lane:** ideas. **Coordinated with:** context (ranking shape, 2026-10-29: no ranking work in flight; the boost goes into fusion, not a post-sort).
**Builds on:** `2026-10-27-sindri-context-map-structure-design.md` §10 (pins shape home features and the roll-up; merged), whose §10.5 and Open question 14 left this half to a separate design. `2026-10-10-sindri-context-retrieval-design.md` (D3 Sindri owns ranking, D13 rerank off by default, §8.2 the ranking steps). `2026-10-19-sindri-code-index-design.md` (CI4, CI7, CI13).

## 0. Why

A pin (`index.concepts` in the profile, `ConceptPin`, `Aw.Context.Core/Engine/ConceptPin.cs:14`) is where the owner says how *they* divide the product. Since §10 of the map-structure spec, the map build respects pins: home features never span two pins, and pins roll up `depends-on` / `reads` / `writes`. The **query side** still ignores them:

1. `context_query` can't be scoped to a pin. "How do refunds work in billing" searches every repo-wide hit, and a `refund` helper in `services/notify` ranks next to the billing one.
2. A query's anchor can be a pin (`node=concept:billing` works, `Host/Mcp/ConceptTools.cs:25`), but nothing in ranking prefers hits that sit inside that pin.
3. The words a team uses for an area ("payments", "invoicing", the old name "ledger") are not on the pin, so a question that uses them never anchors to it. `AnchorMatch.FromText` (`Aw.Context.Core/Graph/AnchorMatch.cs:77`) matches node labels only.
4. The code index's symbol search (`ICodeIndex.FindSymbols`, `Aw.CodeIndex.Core/Facade/ICodeIndex.cs:54`) can't be scoped to a pin either.

## 1. What the code does today (read from main 70cb128fa)

- **Two code stores, not one.** `context_query`'s code results come from Context's own git-based search: `ICodeSearch` (`Aw.Context.Core/Code/ICodeSearch.cs:10`, `GitCodeSearch` runs `git grep` at a commit) and `ICodeSymbolSource`. It never calls `ICodeIndex`. The code index (`src/CodeIndex`, per-repo SQLite, schema `IndexSchema.cs:11-47`, `user_version = 1`) is read only by Sindri's `CodeIndexReads/*` (`FindCodeSymbolsService` and friends) and the code-index jobs. The issue's "pin ids on code-index rows" and "`concept=` on `context_query`" therefore land in two places.
- **The code index has no ruleset hash.** A schema change is a rebuild from scratch (`IndexSchema.cs:5`, `HeaderReader.cs:17-20`); `IndexMeta` stamps the schema and parser versions, not the profile. A stored pin column would go stale on every pin edit with nothing to notice.
- **Fusion is RRF over ranks** (`Aw.Context.Core/Ranking/RankFusion.cs`, `K = 60`, score = Σ 1/(K + rank), ties by key, ordinal), then `FeatureRanker.Boost` multiplies by `1 + Σ weight × feature` over five features (`RankFeatures.cs`: cascade, edge, provenance, standing, recency), then an optional model rerank (off by default). Called from `QueryPipeline.Ranking.cs:11`.
- **Scope filters** today: repo (`ScopeRules.Resolve`, `QueryPipeline.cs:47`), audience (inside each scan, `QueryPipeline.Lexical.cs:64-83`, `DenseRecordSource.cs:116`) and demoted code (`DemotedCode.IsDemoted`).
- **Aliases already exist on graph nodes** (`NodeAlias`, `GraphSnapshot.Aliases`); `AnchorMatch.Resolve` takes "key, name or alias" for `node=`; `FromText` does not look at them.
- **There is no concept cap.** `MaxConcepts` is gone from the code; `ConceptPlan.cs:11` says "with no fixed number of concepts" and derived concepts are limited by merging small groups (`ConceptGrouping.Merge`), not by a count. The map-structure spec's mentions of `MaxConcepts` (`:256`, `:273`, `:319`) are stale.
- **The glossary** (`Aw.Context.Core/Glossary/*`) is TF-IDF over feature-vector clusters, built from vectors; no human input. There is no query expansion anywhere.

## 2. Goals and non-goals

**Goals**
- G1. `context_query concept=<pin>` (MCP and CLI) returns only hits inside the pin.
- G2. When a query is anchored on a pin, or on a node that belongs to one, hits inside that pin rank higher, through fusion, switchable as a backtest arm.
- G3. Pin `aliases` make a question that uses the team's words anchor to the pin, and show in the glossary.
- G4. `sindri code-index symbols --concept <pin>` (and its MCP twin) is scoped the same way, with the same glob rules.

**Non-goals**
- N1. **No pin column in any store.** Pins are matched at read time by their globs (§3.1). Reason: a stored pin id needs a code-index schema bump and full rebuild, and goes stale on the next pin edit because the index has no ruleset hash. Read-time matching costs one glob test per candidate path, and pins change without any rebuild.
- N2. **No concept cap.** The issue asked pins to count against `MaxConcepts`; that cap no longer exists (§1), and the catch-all it was meant to prevent (#1339) is handled by merge-based grouping. This spec fixes the stale text in the map-structure spec instead (§7, Open question 3).
- N3. No rewriting of the lexical query with alias words (true query expansion). Aliases set the anchor; expansion is a later backtest arm if the anchor alone doesn't move the numbers (Open question 2).
- N4. No change to the map build, home features or roll-up (shipped in §10).

## 3. Design

### 3.1 One pin matcher: `PinScope`

New pure type in `Aw.Context.Core/Concepts/PinScope.cs`:

```csharp
public sealed record PinScope(ConceptId Pin, IReadOnlyList<string> Paths, IReadOnlySet<NodeKey> Members)
{
    public static Result<PinScope> Of(ConceptId pin, IReadOnlyList<ResolvedPin> pins, GraphSnapshot graph);
    public bool HoldsPath(string repoRelativePath);   // PathDenylist.MatchesExactCase(path, Paths), first-pin-wins (below)
    public bool HoldsNode(NodeKey key);               // Members, see below
}
```

- **Which pins.** `PinIds.Resolve` (`Aw.Context.Core/Extract/PinIds.cs:14`), the function the map build already shares between home features and concepts, so `concept=billing` means the same pin the map shows. An id that names no pin is `SND-CONTEXT-1xx` "no pin is named 'X' (fix: try billing, auth)" with the nearest ids, like `UnknownAnchor`.
- **First pin wins**, as in homing (§10.1 of the map spec, `HomeFeatures.cs:66`): a file two pins match belongs to the first in `RepoConcepts.For` order. `HoldsPath` therefore checks that no earlier pin matches the path. Otherwise `concept=auth` and `concept=billing` could both return a file the map puts only in `billing`.
- **Members** (graph side) are the pin's concept node, the features that are `belongs-to concept:<id>`, and the code nodes `in`/`implements` those features' files (from the snapshot, no extra read). A record node is a member when it has a `mentions` edge to a member feature or to the concept (the §11 roll-up makes those edges; until it lands, records reach a pin only through links the overlay already shows).
- **Repos.** A pin's `paths` are repo-relative. A profile pin applies in every repo the caller may see; a repo's own pin only in that repo (`RepoConcepts.For` already returns pins per repo).

### 3.2 `concept=` filter on `context_query` (G1)

- **Surface.** MCP `context_query` gains the text arg `concept` (added to `ContextTools.Bind.cs`'s `_texts` allow-list and its error text at `:58`). CLI `sindri context query ... --concept <id>` (`ContextVerbs.cs`). `ContextQuery` gains `ConceptId? Concept`. It combines with any anchor kind (`text`, `node`, `item`, `symbol`) and with `--repo`.
- **Where it applies**, beside the existing scope filters, inside each leg so limits are filled with in-pin hits rather than cut afterwards:
  - **Code leg:** `CodeSearchQuery` gains `PinScope? Pin`; `GitCodeSearch` drops paths `HoldsPath` refuses *before* its `Max` cut (it already filters denied paths there, `GitCodeSearch.cs:30,47`). No pathspec translation: git's glob dialect differs from `PathDenylist`'s, and one matcher is the point.
  - **Record legs (lexical, dense):** a record passes when its record node `HoldsNode`. Applied in the scan next to the audience mask, same place as `DenseRecordSource.cs:116`.
  - **Graph part (lens):** nodes outside the pin are dropped from the answer, except the anchor itself and the edges' far ends that are other pins or things (a lens on a billing feature with `concept=billing` still shows that billing writes `invoices`).
- **Empty is an answer.** No in-pin hits returns an empty result with a note `concept billing: 0 hits in scope (412 outside)` (numbers only), never a silent fallback to the unscoped search.
- **Draft maps.** `concept=` reads the pins from the map version the query reads (`MapVersion` / `--draft`), so a pin edit not yet built doesn't half-apply.

### 3.3 The pin leg in fusion (G2)

The context lane asked for the boost to go into fusion, not a sort after it, and for ties to stay deterministic. So the pin enters `RankFusion.Fuse` as **one more ranked list**, not as a sixth `FeatureRanker` feature:

- **The anchor's pin.** The anchor is a pin (`concept:billing`), or a node that `HoldsNode` for exactly one pin. An anchor in no pin, or one a text question matched to several pins, gets no pin leg. `concept=` also sets it when no anchor names a pin.
- **The leg.** `PinLeg.Of(answers, scope)` (pure, `Aw.Context.Core/Ranking/PinLeg.cs`) builds a `SourceAnswer` with source id `pin`: every hit from the other legs whose path or node is in the pin, ordered by its best rank in any leg, ties by canonical key, ordinal. `Fuse` then adds `1/(K + rank)` for it like any source. A hit in the pin gets at most one extra term; a hit outside gets none.
- **Why a leg and not a weight.** RRF only sees ranks, so the leg can't let a raw score dominate. Its size is bounded by the other legs' hits (it adds no new records). The `rrf:pin#3` line goes into the item's `Why` like other sources, so `context explain` shows why a hit rose.
- **Determinism.** The leg's order is a pure function of the other legs' ranks and keys. `RankFusionTests`' ordinal tie-breaks by source id hold, and `pin` sorts after the real source ids it never shares a rank slot with.
- **Scrub and degraded paths are untouched.** The leg reorders hits that already passed every leg's scrub and deadline. It can't bring back a hit a leg dropped (a deadline-degraded source contributes no hits to the leg either), and scrubbing stays per field after fusion (context's ask 3).
- **Switch.** `context.ranker.pinLeg: on | off` (default `on` after the backtest, `off` until then; Open question 1), and a backtest arm `ranker-pin` beside `ranker-rrf` / `ranker-features` / `ranker-rerank` (`Aw.Context.Core/Eval/Backtest/Arms.cs`). The profile key goes in the profile doc and `ProfileDiff` like the other `context.ranker.*` keys.

**Worked example.** Query "how are refunds issued", anchored by alias to `concept:billing` (§3.4). Lexical leg: `notify/refund-email.ts` #1, `billing/refunds.ts` #2, `billing/ledger.ts` #3. Code leg: `billing/refunds.ts` #1, `notify/refund-email.ts` #2. Without the pin leg, `refunds.ts` scores 1/62 + 1/61 = 0.03253 and `refund-email.ts` 1/61 + 1/62 = 0.03253, a tie broken by key. With it, the pin leg is `refunds.ts` #1 (best rank 1), `ledger.ts` #2 (best rank 3), so `refunds.ts` gets +1/61 (0.04893) and `ledger.ts` rises from 1/63 (0.01587) to 1/63 + 1/62 (0.03200), just under `refund-email.ts`. The notify file stays in the answer; it just isn't first.

### 3.4 Aliases (G3)

```yaml
index:
  concepts:
    - id: billing
      name: Billing
      paths: ["packages/api/src/services/billing/**"]
      aliases: ["payments", "invoicing", "ledger"]
```

- **Shape.** `ConceptPin` gains `Aliases` (`IReadOnlyList<string>`); `ConceptSettings` (`Aw.Sindri.Core/Profile/ConceptSettings.cs:15`, unmapped members refused) gains `aliases`. Every place that copies the pin's shape gets it: `MapSetupReader.cs:69`, `ContextScopes.cs:54`, `ContextEngineChecks.cs:56`, `ProfileDiff.IndexConcepts` (`ProfileDiff.Index.cs:41-44`), `MapRuleset.Pin` (`MapRuleset.cs:44`, so an alias edit changes the ruleset hash and the next build carries it), the profile doc table (`docs/csharp/sindri-profile.md` `index.concepts`, which `check-profile-doc` checks).
- **Rules** (in `IndexRules`): each alias 1 to 60 chars, at least one letter or digit, no control chars; at most 20 per pin; an alias equal to another pin's id, name or alias, ignoring case, is refused with both key paths (an alias must point at one pin). Refusals name the key path, like the others.
- **Into the map.** The concept build writes each alias as a `NodeAlias` on `concept:<id>`. `AnchorMatch.Resolve` already takes aliases, so `node=ledger` resolves to `concept:billing` with no new code.
- **Text anchoring.** `AnchorMatch.FromText` also matches a concept node when all the words of one of its aliases are in the question (same `Words` rules as labels: lower-case, singular). The longer-match-wins rule stays, so "billing ledger export" anchors on a node labelled "Ledger export" if one exists, not on the pin.
- **Glossary.** `ConceptGlossaryBuilder` lists a pin's aliases as its terms first, marked `owner`, before the TF-IDF terms (the `TermsPerConcept = 6` budget counts derived terms only). The vector glossary is untouched.

### 3.5 The code index (G4)

- `SymbolQuery` (`Aw.CodeIndex.Core/Facade/SymbolQuery.cs:10`) gains `IReadOnlyList<string>? Within`: repo-relative globs. The code index filters the symbol's `files.path` before its `Limit` cut. It doesn't know about pins: Sindri's `FindCodeSymbolsService` resolves `--concept <id>` to the pin's paths, plus the earlier pins' paths as exclusions for first-pin-wins (`Within` and `Without`).
- **One glob dialect, two matchers.** CodeIndex can't reference Context (deps tiers; `deps.yaml:673-677` already records a CodeIndex copy that diverges by design). So CodeIndex gets its own matcher, and a shared case file `tests/fixtures/pin-globs.json` (glob, path, expected) is run by both `PathDenylist`'s tests and CodeIndex's. A case where they disagree fails both suites.
- **Surfaces.** `sindri code-index symbols <name> --concept <id>` and the MCP twin's `concept` arg. `callers` / `callees` / `similar` get it later if asked (Open question 4).
- No schema change, no rebuild (N1).

## 4. Errors

- `SND-CONTEXT-1xx` (number at build time): `no pin is named 'X'` + nearest ids; exit 2 on the CLI, a tool error over MCP.
- `concept=` with a map version older than the profile's pins: the pins come from that map version (§3.2), so this can't half-apply.
- Profile refusals (§3.4) at approval, with key paths.

## 5. Tests

**Core (unit lane):** `PinScope` first-pin-wins (a path both pins match is only in the first); members via `belongs-to` and via record `mentions`; an unknown id gives the nearest ids. `PinLeg`: the §3.3 worked example's numbers exactly; no anchor pin gives no leg; an anchor in two pins gives no leg; a hit only in a degraded source never enters the leg; ties ordinal by key; `RankFusionTests`' existing cases unchanged with the leg off. `AnchorMatch.FromText` with aliases: whole-alias match, singular/plural, longer label wins. `IndexRules`: alias length, count, clash with another pin's id/name/alias. `MapRuleset`: an alias edit changes the hash. Glob parity: `pin-globs.json` in both suites.

**Features / integration:** `context_query concept=billing` over a two-pin fixture (the map spec's §10 worked example: `billing` and `auth` with the 6 `auth*` middleware files) returns only billing hits from every leg; `concept=auth` returns the 6 middleware files and never a billing one; zero in-pin hits gives the empty answer and the count note. A backtest run with `ranker-pin` vs `ranker-rrf` reports both arms. `sindri code-index symbols refund --concept billing` returns only billing symbols, and the `Limit` fills with in-pin hits.

**Profile:** `check-profile-doc` passes with `aliases`; the approval diff shows an alias change.

## 6. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Depends on | Lane |
|---|---|---|---|
| Q1 | `PinScope` + the unknown-pin error; `aliases` through the profile (ConceptSettings, the three copies, ProfileDiff, MapRuleset, IndexRules, doc) | none | ideas |
| Q2 | `concept=` on `context_query`: MCP, CLI, code leg, record legs, graph part, the empty note | Q1 | ideas |
| Q3 | `PinLeg` in fusion, `context.ranker.pinLeg`, the `ranker-pin` arm | Q1 | ideas, context reviews |
| Q4 | Aliases into the map (`NodeAlias`), `FromText`, glossary terms | Q1 | ideas |
| Q5 | Code index `Within`/`Without`, the glob parity fixture, `--concept` on `code-index symbols` | Q1 (parity fixture only) | ideas |
| Q6 | Backtest on the real map: `ranker-pin` vs `ranker-rrf`; set the `pinLeg` default | Q3, Q4 | ideas + owner |

**Parallel plan.** Q1 first (one builder, about half a day). Then Q2, Q3, Q4 and Q5 run at once with four builders: they touch different files (`QueryPipeline*` and `GitCodeSearch`; `RankFusion`/`Ranking`; `AnchorMatch` and the concept build; `src/CodeIndex` and `CodeIndexReads`). The only shared file is `ContextQuery.cs` (Q2 adds `Concept`); Q3 reads the anchor's pin from `PinScope`, not from the request, so it doesn't wait on Q2. Q6 runs when Q3 and Q4 are merged.

## 7. Fixes to other docs (in this PR)

- `2026-10-27-sindri-context-map-structure-design.md`: replace the three `MaxConcepts` mentions (`:256`, `:273`, `:319`) with a pointer to this spec's N2, and Open question 14's "separate designs" with a pointer here.

## 8. Open questions for the owner

1. **Pin leg on by default?** *Recommend off until the Q6 backtest shows `ranker-pin` at least matches `ranker-rrf` on the real map, then on.* It's one profile line either way.
2. **Real query expansion with alias words** (searching "ledger" also searches "billing")? *Recommend not now (N3).* Anchoring already brings the pin's hits up through the leg, and expansion widens every lexical search with more noise. Revisit if Q6 shows alias questions still miss.
3. **Drop the concept cap from #1372?** The cap the issue wanted pins to count against no longer exists. *Recommend dropping it and closing that part of #1372.* If you still want a hard ceiling on the number of concepts the map shows, that's a map-build change and a separate small issue.
4. **`--concept` on `callers` / `callees` / `similar`?** *Recommend only on `symbols` now.* The others start from one symbol, so scoping them is less useful; add when someone asks.
5. **A file in no pin with `concept=`**: never returned (strict). *Recommend strict.* The alternative, returning unpinned hits ranked last, makes `concept=` a soft preference and duplicates the pin leg.
