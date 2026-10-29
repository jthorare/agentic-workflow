# Sindri context: concept pins scope and steer queries (#1372, query side)

**Status:** draft for the owner's read (owner q141, 2026-10-28). Not ready until the owner has read it. Review rounds and their findings are in the PR body.
**Lane:** ideas. **Coordinated with:** context (2026-10-29: no ranking work in flight; the boost goes into fusion, not a post-sort; ties stay ordinal).
**Builds on:** `2026-10-27-sindri-context-map-structure-design.md` §10 (pins shape home features and the roll-up; merged), whose §10.5 and Open question 14 left this half to a separate design. `2026-10-10-sindri-context-retrieval-design.md` (D3 Sindri owns ranking, D13 rerank off by default, §8.2 the ranking steps). `2026-10-19-sindri-code-index-design.md` (CI4, CI7, CI13).

## 0. Why

A pin (`index.concepts` in the profile, `ConceptPin`, `Aw.Context.Core/Engine/ConceptPin.cs:11`) is where the owner says how *they* divide the product. Since §10 of the map-structure spec the map build respects pins: home features never span two pins, and pins roll up `depends-on` / `reads` / `writes`. The **query side** still ignores them:

1. `context_query` can't be scoped to a pin. "How do refunds work in billing" returns a `notify` doc about refund emails next to the billing ones.
2. A query can be anchored on a pin (`node=concept:billing` works, `Aw.Sindri.Host/Mcp/ConceptTools.cs:25`), but nothing in ranking prefers the docs and records that sit inside it.
3. The words a team uses for an area ("payments", "invoicing", the old name "ledger") are not on the pin, so a question using them never anchors to it. `AnchorMatch.FromText` (`Aw.Context.Core/Graph/AnchorMatch.cs:77`) matches node labels only.
4. An agent can't tell which concepts are pins: `context_concepts` rows say `derived` or `override` (`ConceptRow.cs:10`).
5. The code index's symbol search (`ICodeIndex.FindSymbols`, `Aw.CodeIndex.Core/Facade/ICodeIndex.cs:54`) can't be scoped to a pin either.

## 1. What the code does today (read from main 70cb128fa)

- **What `context_query` fuses.** `QueryPipeline.Run` (`Aw.Context.Features/QueryContext/QueryPipeline.cs:42-83`) searches the catalog's sources (repo docs, `RepoDocsSource`; Linear, skipped at `:197`) plus the record store's lexical and dense legs (`RecordStoreSource`, `DenseRecordSource`, built in `WithRecordStore`, `QueryPipeline.Lexical.cs:59-85`), fuses their ranks (`RankFusion.Fuse`, RRF, `K = 60`, ties by key, ordinal), then multiplies by `1 + Σ weight × feature` (`FeatureRanker.Boost`, five features in `RankFeatures.cs`), then an optional model rerank (off by default). **Code is not a fused leg.** Code files reach a `context_query` answer as the graph part's candidates (`GraphAnswer` → `GraphPart.Candidates`, the cascade from the anchor), which never enter `Fuse`. `ICodeSearch` / `GitCodeSearch` serve the code lens (`GetCode/LexicalCandidates.cs:22`), not `context_query`.
- **A map version stores a hash of the pins, not the pins** (`MapRuleset.cs:18-36,44-45`). At query time pins come from the approved profile (`ContextScopes` maps the approved profile's repos, each with its `Concepts`, `ContextScopes.cs:10-54`). `MapRuleset.Running(scope)` (`:41`) gives the hash the current pins would build; the map stores the hash it was built with.
- **Aliases on graph nodes are key-to-key** (`NodeAlias(NodeKey Alias, NodeKey Target)`, `NodeAlias.cs:8`), validated as keys (`MapDocumentValidator.cs:101`). A free word like "ledger" can't be one. Concept terms live on the concept node as an attribute (`ConceptTermsAttribute.cs:7-12`), read by `ConceptGlossaryBuilder`.
- **The code index has its own glob matcher**, `InputRules.MatchesAny` (`Aw.CodeIndex.Core/InputRules.cs:86-92,159-185`): `*`, `?`, `**`, `prefix:` / `suffix:` / `regex:`, ignoring case. Context's `PathDenylist.MatchesExactCase` (`PathDenylist.cs:39`), which homing uses (`HomeFeatures.cs:66`), keeps case. The symbol query cuts in SQL (`ORDER BY … LIMIT`, `SqliteIndexReader.Symbols.cs:48`).
- **There is no concept cap.** `MaxConcepts` is gone from the code; `ConceptPlan.cs:11` says "with no fixed number of concepts" and derived concepts are limited by merging small groups (`ConceptGrouping.Merge`). The map-structure spec's mentions of it are stale (fixed in this PR, §8).
- There is no query expansion anywhere.

## 2. Goals and non-goals

**Goals**
- G1. `context_query concept=<pin>` (MCP, CLI, HTTP) returns only results inside the pin: fused docs and records, and the graph part's nodes.
- G2. When a query is anchored on a pin, or on a node inside exactly one, the fused docs and records inside that pin rank higher, through fusion, switchable as a backtest arm. (Code needs no boost: the graph part already starts its cascade from the anchor.)
- G3. Pin `aliases` make a question that uses the team's words anchor to the pin; `context_concepts` shows which concepts are pins and their aliases.
- G4. `sindri code-index symbols --concept <pin>` is scoped the same way.

**Non-goals**
- N1. **No pin column in any store.** Pins are matched at read time against the approved profile (§3.1). A stored pin id needs a code-index schema bump and rebuild, and goes stale on the next pin edit because the code index has no ruleset hash.
- N2. **No concept cap.** The issue asked pins to count against `MaxConcepts`; that cap no longer exists, and the catch-all it guarded against (#1339) is handled by merge-based grouping (Open question 3).
- N3. No query expansion with alias words (Open question 2).
- N4. No change to the map build, homing or roll-up beyond writing the `aliases` attribute (§3.4).
- N5. No `concept=` on the code lens (`GetCode`) or on `callers` / `callees` / `similar` (Open question 4).

## 3. Design

### 3.1 One pin resolver: `PinScope` (public, `Aw.Context.Core/Concepts/PinScope.cs`)

```csharp
public sealed class PinScope
{
    public static Result<PinScope> Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, string id, GraphSnapshot graph);
    public ConceptId Pin { get; }
    public bool HoldsPath(ContextRepoName repo, string repoRelativePath);   // first pin wins, per repo
    public bool HoldsNode(NodeKey key);
    public static ConceptId? PinOf(NodeKey anchor, IReadOnlyList<ContextRepo> mapped, GraphSnapshot graph);   // the anchor's pin, or null
}
```

- **Ids.** `Of` runs the internal `PinIds.Resolve` / `PinIds.Of` (`PinIds.cs:14,32`) over `mapped`, the repos the map is built from (`ScopeRules.Mapped(settings, scope.Repos)`, as `MapRuleset.Running` does), so `concept=billing` names the same pin the map shows, never a `billing-2` made from a smaller set. `id` may be `billing` or `concept:billing`.
- **Visibility.** The lookup and the "nearest ids" of the error consider only pins that apply to a repo in `visible` (the caller's resolved repos). A pin that applies only to repos the caller can't see answers exactly like a typo.
- **`HoldsPath(repo, path)`.** True when the path matches this pin's `paths` in that repo's pin order (`RepoConcepts.For`: the profile's pins, then the repo's) and no earlier pin of that repo matches it: first pin wins, as in homing. Matched with `PathDenylist.MatchesExactCase`, the same matcher homing uses.
- **`HoldsNode(key)`.** Code nodes: by their path only (`HoldsPath`), never through feature tags, so a file is in at most one pin, as in homing. Features: `belongs-to concept:<id>`. Records and decisions: a `belongs-to` or `mentions` edge to the concept or to a member feature (`EdgeRules.cs:27` allows both kinds as sources; the map spec's §11 roll-up adds `record mentions concept`). The concept node itself. Built once per query into a key set; O(1) per check.
- **Stale map.** When the map's stored ruleset hash differs from `MapRuleset.Running(scope)`, paths and graph membership can disagree (globs are current, `belongs-to` edges are from the last build). The answer then carries the note `pins changed since map v41; results use the new globs for files and the map's grouping for features (fix: sindri context map build)`. No refusal: the owner may be mid-edit.
- **Errors.** `SND-CONTEXT-1xx` `no pin is named 'bilin' (fix: try billing, auth)`. A second code when the approved profile has the pin but the map has no `concept:<id>` node yet: `pin 'billing' is newer than map v41 (fix: sindri context map build)`. A pin that matches nothing in this map: no error, the empty-answer note below says so.

### 3.2 `concept=` on `context_query` (G1)

- **Surface.** MCP `context_query` gains the text arg `concept` (the `_texts` allow-list and the error text in `ContextTools.Bind.cs:58`); its description: "a pin id from context_concepts (rows with source 'pin'); only results inside that pin". CLI `sindri context query … --concept <id>` (`ContextVerbs.cs`), and its help line. `ContextQuery` gains `Concept` as an `init` property (not positional), which also reaches the HTTP route, `ISindriApi.ContextQuery`, the UI client and `FakeSindriApi` (API goldens change; the Ui tests run in that gate). Combines with any anchor kind and with `--repo`.
- **Where it applies**, beside the existing masks, so each leg's limit fills with in-pin results:
  - **Record legs.** `RecordStoreSource` and `DenseRecordSource` take an optional `PinScope` the way they take `AudienceScope`; a record passes when `HoldsNode(record:<key>)`. The audience mask runs first (`DenseRecordSource.cs:22,116`).
  - **Repo docs.** `SourceQuery` (`SourceQuery.cs:8`) gains an optional `Pin` filter; `RepoDocsSource` keeps a doc chunk when `HoldsPath(repo, path)` (a `services/billing/README.md` under the pin's glob) or when its record node `HoldsNode`. Sources that ignore the filter (Linear's live source) are left out when `concept=` is set, with a note, rather than returning unscoped hits.
  - **Graph part.** Candidates and nodes outside the pin are dropped, except the anchor itself and edge ends that are other pins or things (a billing lens still shows that billing writes `invoices`).
- **Empty is an answer, with a way out.** No in-pin results returns an empty result and a note built only from what the caller may see, after repo scope, audience and deny globs: `concept billing: 0 results. In scope: auth 7, no pin 3`, or `concept billing matches no file in this map (check index.concepts[0].paths)`. Never a silent fallback to the unscoped search. The counts come from the hits the legs already read and dropped for the pin (those had passed the audience mask), so they cost no second search.
- **A file in no pin is never returned with `concept=`** (strict; Open question 5). Records not linked to the graph have no pin, so until the map spec's §11 roll-up lands, `concept=` returns mostly repo docs and linked records; the note's `no pin` count shows how much that is.
- **`concept=` and an anchor in another pin.** The filter wins: results are billing's only, the anchor's lens is cut to billing, and the answer notes `anchor concept:auth is outside concept billing`. No error.
- **Sessions** read pins from the approved profile only (that is what `ContextScopes` maps). A draft map is human-only today (MCP never reads drafts, `ContextTools.cs:13`), and this keeps it so.

### 3.3 The pin leg in fusion (G2)

- **The anchor's pin.** `PinScope.PinOf(anchor)`: the anchor is a pin, or a node `HoldsNode` for exactly one pin. An anchor in no pin, a text question anchored to several pins, or a query with `concept=` set (every result is already in the pin) gets no leg.
- **The leg, after RRF.** `RankFusion.Fuse` gains an overload taking key-only legs, `Fuse(answers, IReadOnlyList<RankedLeg> legs)`, `RankedLeg(SourceId Source, IReadOnlyList<RecordKey> Keys)`. `PinLeg.Of(fused, scope)` (pure, `Aw.Context.Core/Ranking/PinLeg.cs`) takes the plain fusion's output, keeps the items inside the pin in their fused order, and the overload adds `1/(K + r)` for the r-th of them. No synthetic hits: `FusedItem.Hits` is unchanged, and `Why` gets `rrf:pin#r`, so `context explain` shows why an item rose. Ordering by fused score, not by raw `SourceHit.Rank`, avoids the ties sub-queries make (many hits share rank 1).
- **Strength.** A fixed `1/(K + r)`: the top in-pin item gains as much as a rank-1 hit from one more source. No weight knob until the backtest asks for one.
- **Determinism and safety.** The leg is a pure function of the fused order and keys; ties stay ordinal by key; `RankFusionTests`' cases are unchanged with the leg off. The leg only reorders items that already passed every leg's scrub, audience mask and deadline; a degraded source contributes nothing to it.
- **Switch.** `context.ranker.pinLeg: on | off` (default off until Q6). It touches `RankerSection` and the known keys in `SettingsReader`, `RankerPlan.For`, the profile doc and `ProfileDiff`; the backtest arm `ranker-pin` touches `Arm`, `Arms.Ranker`, `Arms.Name`, `Arms.PendingStage` and the report writers. When it's off and the anchor has a pin, `context explain` says `pin leg off (context.ranker.pinLeg)`.

**Worked example.** Query "how are refunds issued", anchored by the alias "payments" to `concept:billing` (§3.4). Two legs, repo docs and the record store's lexical leg:

| Item | in billing | docs leg | lexical leg | plain RRF | pin leg | with leg |
|---|---|---|---|---|---|---|
| `notify/docs/refund-emails.md` | no | #1 | #1 | 1/61 + 1/61 = 0.03279 | none | 0.03279 |
| `billing/docs/refunds.md` | yes | #2 | #3 | 1/62 + 1/63 = 0.03200 | #1: 1/61 | 0.04840 |
| `linear:ENG-482` "Refund partial captures" | yes | none | #2 | 1/62 = 0.01613 | #2: 1/62 | 0.03226 |

Plain RRF puts the notify doc first. With the leg, the billing doc is first, and ENG-482 moves just under the notify doc, which stays in the answer.

### 3.4 Aliases (G3)

```yaml
index:
  concepts:
    - id: billing
      name: Billing
      paths: ["packages/api/src/services/billing/**"]
      aliases: ["payments", "invoicing", "ledger"]
```

- **Shape.** `ConceptPin` gains `Aliases`; `ConceptSettings` (`Aw.Sindri.Core/Profile/ConceptSettings.cs:15`, unmapped members refused) gains `aliases`; `ConceptPin.Of` and the three copy sites (`MapSetupReader.cs:69`, `ContextScopes.cs:54`, `ContextEngineChecks.cs:56`) change in one commit. `PinIds.Identity` includes them. `ProfileDiff.IndexConcepts` (`ProfileDiff.Index.cs:41-44`) shows a change. `MapRuleset.Pin` (`MapRuleset.cs:44`) appends an alias segment only when the list is non-empty, so pins without aliases keep their hash and no map rebuilds on upgrade. The profile doc table (`docs/csharp/sindri-profile.md`, `index.concepts`) gains the row.
- **Rules** (in `Aw.Sindri.Core/Profile/IndexRules.cs`): each alias 1 to 60 chars, at least one letter or digit, no control or Unicode format characters; at most 20 per pin. The clash check compares the matcher's normalized form (`AnchorMatch.Words`: lower case, singular), so `invoice` and `Invoices` clash: an alias equal, in that form, to another pin's id, name or alias is refused with both key paths.
- **Into the map.** The concept pass writes the aliases as an `aliases` attribute on `concept:<id>`, next to its terms (`ConceptTermsAttribute`). Not `NodeAlias`, which is key-to-key.
- **Anchoring, new code.** `AnchorMatch.Resolve` (`node=`), after the key and label lookups and before `NearFeature`: a whole-alias match resolves to the concept. `FromText`: a concept node also matches when all the words of one of its aliases are in the question. The longer-match-wins rule covers both; when an alias and a node label tie at the same length, the label wins (it is the node's own name). Two pins' aliases can't tie: the clash rule refuses that.
- **Shown.** `context explain` and the anchor line say `anchored on concept:billing (alias "payments")`.
- **Glossary and concepts list.** `ConceptRow.Source` gains `pin` (a pinned concept) beside `derived` / `override`, and `ConceptRow` gains `Aliases`. The concept glossary lists a pin's aliases first, marked `owner`, then its derived terms; `ConceptTermsRow` gains that mark, and the duplicate `Aw.Sindri.Ui/Api/Context/ConceptGlossaryView.cs` follows. This is how an agent finds pin ids: `context_concepts`, rows with `source: pin`.

### 3.5 The code index (G4)

- `SymbolQuery` (`Aw.CodeIndex.Core/Facade/SymbolQuery.cs:10`) gains `Within` and `Without` (repo-relative globs) as `init` properties. Internal only: not on the MCP or CLI surface; Sindri sets them.
- Sindri's `FindCodeSymbolsService` resolves `--concept <id>` with `PinScope.Of` (Sindri already references Context), setting `Within` to the pin's paths in that repo and `Without` to the earlier pins' paths (first pin wins).
- **One matcher per package, one dialect.** CodeIndex reuses `InputRules.MatchesAny`, given a case-sensitive mode for this use (pins match with case, like homing). A parity test runs `tests/fixtures/pin-globs.json` (glob, path, expected; including `prefix:`, `suffix:`, `regex:` and case) through both `PathDenylist.MatchesExactCase` and `InputRules.MatchesAny(caseSensitive)`; a disagreement fails both suites.
- **Filter before the limit.** The reader streams rows in the query's order and stops at `Limit` accepted rows (no SQL `LIMIT` when a filter is set), bounded by a scan cap (`10 × Limit` rows), with a note when the cap stops it.
- Surface: `sindri code-index symbols <name> --concept <id>` and the MCP twin's `concept` arg. No schema change, no rebuild.

## 4. Tests

**Core (unit lane).**
- `PinScope`: first pin wins per repo; a path only an earlier pin of another repo matches is still this pin's in this repo; code nodes by path only (a file tagged to two pins' features is in one); features via `belongs-to`; records via `mentions`; `concept:billing` and `billing` both resolve; a pin in a repo the caller can't see answers like a typo; ids built over mapped repos match the map's (no `billing-2`); the stale-map note when the hashes differ.
- `PinLeg`: the §3.3 table exactly; no leg with no anchor pin, with two, or with `concept=`; ties ordinal by key; `Fuse` with no legs equals today's `Fuse` on every existing `RankFusionTests` case.
- `AnchorMatch`: `node=ledger` resolves to `concept:billing`; `FromText` alias match, singular and plural; a label beats an alias of the same length; the anchor line names the alias.
- `IndexRules`: length, count, control and format characters, clash in normalized form with both key paths. `MapRuleset`: an alias edit changes the hash; a pin with no aliases keeps the old hash.
- Glob parity: `pin-globs.json` through both matchers.

**Features / integration.**
- `context_query concept=billing` over the map spec's §10 two-pin fixture (`billing`; `auth` with the 6 `auth*` middleware files) returns only billing results from every leg and the graph part; `concept=auth` never returns a billing result; the empty answer's note gives in-scope counts per pin and never a count from a repo or audience the caller can't see; `concept=` with an anchor in another pin gives the note; the newer-than-map error.
- `context_concepts` shows `source: pin` and the aliases.
- `sindri code-index symbols refund --concept billing` returns only billing symbols and fills `Limit` with them; the scan-cap note.
- HTTP / UI client / fake: the `concept` field round-trips; API goldens updated; `Aw.Sindri.Ui.Tests` in the gate.
- Backtest: `ranker-pin` and `ranker-rrf` both report. The backtest feeds only the catalog's doc sources (`QueryPipeline.Lexical.cs:59-62`), so Q6 measures docs; record legs are judged by hand on the real map.

## 5. Errors and notes (literal texts)

| When | Text |
|---|---|
| unknown id | `SND-CONTEXT-1xx: no pin is named 'bilin' (fix: try billing, auth)` |
| pin newer than the map | `SND-CONTEXT-1xx: pin 'billing' is newer than map v41 (fix: sindri context map build)` |
| zero results | `concept billing: 0 results. In scope: auth 7, no pin 3` |
| pin matches nothing | `concept billing matches no file in this map (check index.concepts[0].paths)` |
| stale map | `pins changed since map v41; results use the new globs for files and the map's grouping for features (fix: sindri context map build)` |
| anchor in another pin | `anchor concept:auth is outside concept billing` |
| a source can't filter | `linear: left out under concept= (it can't be scoped to a pin)` |
| pin leg off | `pin leg off (context.ranker.pinLeg)` |
| code-index scan cap | `concept billing: stopped after 500 symbols scanned; narrow the name` |

Error numbers are taken when Q1 is built. Profile refusals name the key path, like the others.

## 6. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Depends on |
|---|---|---|
| Q1a | `PinScope` (ids over mapped repos, visibility, `HoldsPath`, `HoldsNode`, `PinOf`), the two errors, the stale-map note | none |
| Q1b | `aliases` through the profile: `ConceptSettings`, `ConceptPin`, the three copy sites, `PinIds.Identity`, `ProfileDiff`, `MapRuleset.Pin`, `IndexRules`, the profile doc | none |
| Q2a | `concept` on the request: `ContextQuery`, MCP, CLI, HTTP, UI client and fake, goldens, the anchor-in-another-pin note | Q1a |
| Q2b | Record legs and repo docs take the pin (`SourceQuery.Pin`, the two record sources, `RepoDocsSource`), the empty-answer note and counts, sources that can't filter | Q1a |
| Q2c | Graph part cut to the pin | Q1a |
| Q3 | `RankedLeg` overload of `Fuse`, `PinLeg`, `context.ranker.pinLeg`, the `ranker-pin` arm | Q1a |
| Q4 | `aliases` attribute on concept nodes, `Resolve` and `FromText`, `ConceptRow` `pin` / `Aliases`, glossary mark | Q1a, Q1b |
| Q5 | Code index `Within` / `Without`, case-sensitive `MatchesAny`, the parity fixture, streamed limit, `--concept` | Q1a |
| Q6 | Backtest on the real map: `ranker-pin` vs `ranker-rrf`, plus a hand check of alias anchoring and `concept=` on 10 owner questions; sets the `pinLeg` default | Q2b, Q3, Q4 |

**Parallel plan.** Q1a and Q1b run at once (two builders: Core concepts vs Sindri profile). Then Q2a, Q2b, Q2c, Q3, Q4 and Q5 run at once, six builders. All pin resolution (including the anchor's pin) is in Q1a, so no later phase adds a shared step to `QueryPipeline.Run`; each phase touches its own lines there (Q2a the request, Q2b the source construction, Q2c the graph part, Q3 the `Ranked` call). Every phase that adds Core public API appends to `Aw.Context.Core/PublicAPI.Unshipped.txt`, so the lead merges them one at a time and resolves that file by keeping both sides. Q6 starts when Q2b, Q3 and Q4 are merged.

**Bar for turning the pin leg on (Q6).** `ranker-pin` at least equals `ranker-rrf` on recall@10 and MRR over the backtest, and in the hand check no owner question loses its right answer from the top 5.

## 7. Security

The pin filter only narrows: it runs after repo scope, the audience mask and deny globs, never instead of them, and the pin leg reorders only results that passed all three. The empty-answer counts and the unknown-pin suggestions use only what the caller may see (§3.1, §3.2). Aliases are owner profile input, validated at approval (§3.4). `Within` / `Without` are not exposed to callers. Sessions read the approved profile's pins only.

## 8. Fixes to other docs (in this PR)

- `2026-10-27-sindri-context-map-structure-design.md`: Open question 14 and §10.5 point here and say the `MaxConcepts` part is dropped.

## 9. Open questions for the owner

1. **Pin leg on by default?** *Recommend off until Q6 meets the bar in §6, then on.* One profile line either way.
2. **Real query expansion with alias words** (a search for "ledger" also searches "billing")? *Recommend not now.* Anchoring already lifts the pin's results through the leg, and expansion widens every search with more noise. Revisit if Q6's hand check shows alias questions still miss.
3. **Drop the concept cap from #1372?** The cap the issue wanted pins to count against no longer exists. *Recommend dropping it and closing that part of #1372.* A hard ceiling on concepts shown would be a separate small map-build issue.
4. **`concept=` on the code lens and on `callers` / `callees` / `similar`?** *Recommend not now.* They start from one symbol or file, so scoping them helps less; add when someone asks.
5. **Strict `concept=`:** a result in no pin is never returned. *Recommend strict.* The alternative (unpinned results ranked last) makes `concept=` a soft preference and duplicates the pin leg. The cost: until the map spec's §11 links records to features, most Slack, Linear and GitHub records have no pin and don't show under `concept=`; the note's `no pin` count makes that visible.
