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
    // also: PinSet.Of(mapped, visible, graph).PinOf(NodeKey) -> ConceptId?  (every visible pin at once, for the empty-answer counts)
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
- **`HoldsNode(key)`.** Code nodes: through their **home feature** (the one `implements` edge to a home leaf, map spec §2) and that leaf's `belongs-to concept:<id>`, never through tag features. This is exactly the map's own grouping, so path pins and `features:` pins both work and a file is in at most one pin (a home leaf belongs to at most one pin, §10.1). Features: `belongs-to concept:<id>`. Records and decisions: a `belongs-to` or `mentions` edge to the concept or to a member feature (`EdgeRules.cs:27` allows both kinds as sources; the map spec's §11 roll-up adds `record mentions concept`). The concept node itself. Built once per query into a key set; O(1) per check.
- **Stale map.** When the map's stored ruleset hash differs from `MapRuleset.Running(scope)` (computed over the caller's scope, so an edit to a pin of a repo the caller can't see doesn't fire it), file paths and feature membership can disagree (globs are current, `belongs-to` edges are from the last build). The answer then carries the note `pins were edited after map v41 was built, so some features may be grouped the old way (fix: sindri context map build)`. No refusal: the owner may be mid-edit.
- **Where it's built.** Q1a adds one step to `QueryPipeline.Run`, after the map is read and before the search (the graph is only read inside `AnchorFor` today): it builds the `PinSet`, the `concept=` `PinScope` and the anchor's pin once, and hands them to the legs, the graph part and the ranker as one internal `PinContext`. `mapped` is `ScopeRules.Mapped(settings, request.Scope.Repos)`. Later phases only read `PinContext`.
- **Ids and aliases.** `id` may be a pin id, `concept:<id>`, or one of a pin's aliases (§3.4), so `concept=ledger` works wherever `node=ledger` does.
- **Errors.** `SND-CONTEXT-1xx` `no pin is named 'bilin' (fix: try billing, auth)`, at most 5 suggestions, from visible pins only. With no pins at all: `no pins are set up (fix: add index.concepts to the profile)`. A second code when the approved profile has the pin but the map version has no `concept:<id>` node yet (checked on the whole map version before it is cut to the caller's scope, so a session whose scoped view has no member of the pin doesn't get it falsely): `pin 'billing' is newer than map v41 (fix: sindri context map build)`; the visibility check runs before it, so it never names a pin of a repo the caller can't see. A pin that matches nothing in this map: no error, the empty-answer note below says so.

### 3.2 `concept=` on `context_query` (G1)

- **Surface.** MCP `context_query` gains the text arg `concept` (the `_texts` allow-list and the error text in `ContextTools.Bind.cs:58`); its description: "a pin id from context_concepts (rows with pinned: true); only results inside that pin". CLI `sindri context query … --concept <id>` (`ContextVerbs.cs`), and its help line. `ContextQuery` gains `Concept` as an `init` property (not positional), which also reaches the HTTP route, `ISindriApi.ContextQuery`, the UI client and `FakeSindriApi` (API goldens change; the Ui tests run in that gate). Combines with any anchor kind and with `--repo`.
- **Where it applies**, beside the existing masks, so each leg's limit fills with in-pin results:
  - **Record legs.** `RecordStoreSource` and `DenseRecordSource` take an optional `PinScope` the way they take `AudienceScope`; a record passes when `HoldsNode(record:<key>)`. The audience mask runs first (`DenseRecordSource.cs:22,116`).
  - **Repo docs.** `SourceQuery` (`SourceQuery.cs:8`) gains an optional `init` `Pin` filter, and `IContextSource` a default-false `HonorsPin` so the pipeline knows which sources apply it; `RepoDocsSource` keeps a doc chunk when `HoldsPath(repo, path)` (a `services/billing/README.md` under the pin's glob) or when its record node `HoldsNode`. A doc under `docs/` is outside a pin whose globs name only code; the owner adds a doc glob to the pin's `paths` to bring it in (said in the profile doc). Sources that ignore the filter (Linear's live source) are left out when `concept=` is set, with a note, rather than returning unscoped hits.
  - **Graph part.** Candidates and nodes outside the pin are dropped, except the anchor itself and edge ends that are other pins or data stores (`thing` nodes, the tables the extractor finds; a billing lens still shows that billing writes `invoices`).
- **Scoped answers say so.** Every answer under `concept=` starts with `scoped to concept billing (map v41)`.
- **Empty is an answer, with a way out.** No in-pin results returns an empty result and a note built only from what the caller may see, after repo scope, audience and deny globs: `concept billing: no results. Of the results read, 7 were in auth and 3 in no pin (fix: concept=auth, or drop concept=)`; with none at all, `concept billing: no results, and none outside it either`; for a pin whose globs match nothing, `concept billing matches no file in this map (check the paths of pin 'billing' in index.concepts)`. Never a silent fallback to the unscoped search. **How the counts are made:** the lexical record leg and repo docs, while filling their limit, count the rows they skipped for the pin, by the pin they are in (`PinSet.PinOf`); those rows had already passed the audience mask. The dense leg masks inside its vector scan and is left out of the counts. They are counts among the rows read, not totals, and cost no second search. A pin is named only when it applies to a repo the caller can see; any other is counted under `no pin`.
- **A file in no pin is never returned with `concept=`** (strict; Open question 5). Records not linked to the graph have no pin.
- **What v1 covers.** Repo docs (by path), the graph part (code by path, features by `belongs-to`), and records that already have a graph link to a member feature or the pin: today those are the records the intent overlay links (`IntentOverlay.Apply`, rules and decisions). Unlinked Slack, Linear and GitHub records are not covered until #1373 (the map spec's §11) adds `record mentions feature` for every record; this spec doesn't schedule that. The note's `no pin` count shows how much is left out.
- **`concept=` and an anchor in another pin.** The filter wins: results are billing's only, the anchor's lens is cut to billing, and the answer notes `anchor concept:auth is outside concept billing, so its lens shows only billing's part`. No error.
- **A hint without `concept=`.** When a text question anchors to a pin through an alias and `concept=` is unset, the answer notes `anchored on concept:billing (alias "payments"); add concept=billing to see only billing`.
- **Sessions** read pins from the approved profile only (that is what `ContextScopes` maps). A draft map is human-only today (MCP never reads drafts, `ContextTools.cs:13`), and this keeps it so.

### 3.3 The pin leg in fusion (G2)

- **The anchor's pin.** `PinScope.PinOf(anchor)`: the anchor is a pin, or a node `HoldsNode` for exactly one pin. An anchor in no pin, a text question anchored to several pins, or a query with `concept=` set (every result is already in the pin) gets no leg.
- **The leg, after RRF.** `RankFusion.Fuse` gains an overload taking key-only legs, `Fuse(answers, IReadOnlyList<RankedLeg> legs)`, `RankedLeg(SourceId Source, IReadOnlyList<RecordKey> Keys)`. `PinLeg.Of(fused, scope)` (pure, `Aw.Context.Core/Ranking/PinLeg.cs`) takes the plain fusion's output, keeps the items inside the pin in their fused order, and the overload adds `1/(K + r)` for the r-th of them. No synthetic hits: `FusedItem.Hits` is unchanged, and `Why` gets `rrf:pin#r`, so `context explain` shows why an item rose. Ordering by fused score, not by raw `SourceHit.Rank`, avoids the ties sub-queries make (many hits share rank 1).
- **Strength.** A fixed `1/(K + r)`: the top in-pin item gains as much as a rank-1 hit from one more source. No weight knob until the backtest asks for one.
- **Determinism and safety.** The leg is a pure function of the fused order and keys; ties stay ordinal by key; `RankFusionTests`' cases are unchanged with the leg off. The leg only reorders items that already passed every leg's scrub, audience mask and deadline; a degraded source contributes nothing to it.
- **Switch.** `context.ranker.pinLeg: on | off` (default off until Q6). It touches `RankerSection` and `RankerRules`, `RankerPlan.For`, the profile doc and `ProfileDiff`; the backtest arm `ranker-pin` touches `Arm`, `Arms.Ranker`, `Arms.Name`, `Arms.PendingStage` and the report writers. `context explain` always says whether the leg ran: `pin leg: concept:billing` when it did, otherwise the reason (`pin leg: off (context.ranker.pinLeg)`, `pin leg: anchor in no pin`, `pin leg: anchor in two pins`, `pin leg: not used under concept=`).

**Worked example.** Query "how are payments refunded": the word "payments" is an alias of billing, so the question anchors on `concept:billing` (§3.4). Two legs, repo docs and the record store's lexical leg. ENG-482 is in billing because the intent overlay links it to `feature:billing.refunds` (a v1-covered record, §3.2):

| Item | in billing | docs leg | lexical leg | plain RRF | pin leg | with leg |
|---|---|---|---|---|---|---|
| `notify/docs/refund-emails.md` | no | #1 | #1 | 1/61 + 1/61 = 0.03279 | none | 0.03279 |
| `billing/docs/refunds.md` | yes | #2 | #3 | 1/62 + 1/63 = 0.03200 | #1: 1/61 | 0.04840 |
| `linear:ENG-482` "Refund partial captures" | yes | none | #2 | 1/62 = 0.01613 | #2: 1/62 | 0.03226 |

Plain RRF puts the notify doc first. With the leg, the billing doc is first; ENG-482 stays third but closes most of the gap to the notify doc (0.03226 vs 0.03279), which stays in the answer.

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
- **Rules** (in `Aw.Sindri.Core/Profile/IndexRules.cs`): each alias 1 to 60 chars, at least one letter or digit, no control characters and none of the Unicode categories Cf (format), Zl and Zp (line and paragraph separators); at most 20 per pin; an alias made only of words the matcher skips (`the`, `of`, …, `AnchorMatch._small`) is refused, since it could never match. Aliases are shown through the same untrusted-text fence as labels wherever they render (`context_concepts`, the anchor line, `context explain`). The clash check compares the matcher's normalized form (`AnchorMatch.Words`: lower case, singular), so `invoice` and `Invoices` clash: an alias equal, in that form, to another pin's id, name or alias is refused with both key paths.
- **Into the map.** The concept pass writes the aliases as an `aliases` attribute on `concept:<id>`, next to its terms (`ConceptTermsAttribute`). Not `NodeAlias`, which is key-to-key.
- **Anchoring, new code.** `AnchorMatch.Resolve` (`node=`), after the key and label lookups and before `NearFeature`: a whole-alias match resolves to the concept. `FromText`: a concept node also matches when all the words of one of its aliases are in the question. The longer-match-wins rule covers both; when an alias and a node label tie at the same length, the label wins (it is the node's own name). Two pins' aliases can't tie: the clash rule refuses that.
- **Shown.** `context explain` and the anchor line say `anchored on concept:billing (alias "payments")`.
- **Glossary and concepts list.** `ConceptRow` gains `Pinned` (bool; `Source` already says `override` for a pinned concept, so a new source value would collide) and `Aliases`. The concept glossary lists a pin's aliases first, marked `owner`, then its derived terms; `ConceptTermsRow` gains that mark, and the duplicate `Aw.Sindri.Ui/Api/Context/ConceptGlossaryView.cs` follows. This is how an agent finds pin ids: `context_concepts`, rows with `pinned: true`.

### 3.5 The code index (G4)

- `SymbolQuery` (`Aw.CodeIndex.Core/Facade/SymbolQuery.cs:10`) gains `Within`, a set of repo-relative file paths, as an `init` property. Internal only: not on the MCP or CLI surface; Sindri sets it.
- Sindri's `FindCodeSymbolsService` resolves `--concept <id>` with `PinScope.Of` (Sindri already references Context) over the current map and sets `Within` to the paths of the code files the pin holds (their home feature belongs to it). This covers path pins and `features:` pins alike, uses the map's own grouping, and needs no glob matching or first-pin-wins logic in the code index: the filter is set membership on `files.path`.
- **Filter before the limit.** The reader streams rows in the query's order and stops at `Limit` accepted rows (no SQL `LIMIT` when `Within` is set), bounded by a scan cap (`10 × Limit` rows), with a note when the cap stops it; zero accepted rows gives `concept billing: no symbols named 'refund' in it`.
- When the code index is newer or older than the map, a file the map doesn't know is outside every pin; the note `code index and map differ by N files` says so (numbers only).
- Surface: `sindri code-index symbols <name> --concept <id>` and the MCP twin's `concept` arg. No schema change, no rebuild.

## 4. Tests

**Core (unit lane).**
- `PinScope`: first pin wins per repo; a path only an earlier pin of another repo matches is still this pin's in this repo; code nodes through their home feature (a file tagged to two pins' features is in one; a `features:`-only pin holds its files); features via `belongs-to`; records via `mentions`; `concept:billing` and `billing` both resolve; a pin in a repo the caller can't see answers like a typo; ids built over mapped repos match the map's (no `billing-2`); the stale-map note when the hashes differ.
- `PinLeg`: the §3.3 table exactly; no leg with no anchor pin, with two, or with `concept=`; ties ordinal by key; `Fuse` with no legs equals today's `Fuse` on every existing `RankFusionTests` case.
- `AnchorMatch`: `node=ledger` resolves to `concept:billing`; `FromText` alias match, singular and plural; a label beats an alias of the same length; the anchor line names the alias.
- `IndexRules`: length, count, control and format characters, clash in normalized form with both key paths. `MapRuleset`: an alias edit changes the hash; a pin with no aliases keeps the old hash.

**Features / integration.**
- `context_query concept=billing` over the map spec's §10 two-pin fixture (`billing`; `auth` with the 6 `auth*` middleware files) returns only billing results from every leg and the graph part; `concept=auth` never returns a billing result; the empty answer's note gives in-scope counts per pin and never a count from a repo or audience the caller can't see; `concept=` with an anchor in another pin gives the note; the newer-than-map error.
- `context_concepts` shows `pinned: true` and the aliases.
- `sindri code-index symbols refund --concept billing` returns only billing symbols and fills `Limit` with them; the scan-cap note.
- HTTP / UI client / fake: the `concept` field round-trips; API goldens updated; `Aw.Sindri.Ui.Tests` in the gate.
- Backtest: `ranker-pin` and `ranker-rrf` both report. The backtest feeds only the catalog's doc sources (`QueryPipeline.Lexical.cs:59-62`), so Q6 measures docs; record legs are judged by hand on the real map.

## 5. Errors and notes (literal texts)

| When | Text |
|---|---|
| unknown id | `SND-CONTEXT-1xx: no pin is named 'bilin' (fix: try billing, auth)` (at most 5, visible pins only) |
| no pins at all | `SND-CONTEXT-1xx: no pins are set up (fix: add index.concepts to the profile)` |
| pin newer than the map | `SND-CONTEXT-1xx: pin 'billing' is newer than map v41 (fix: sindri context map build)` |
| scoped answer | `scoped to concept billing (map v41)` |
| zero results | `concept billing: no results. Of the results read, 7 were in auth and 3 in no pin (fix: concept=auth, or drop concept=)` |
| zero everywhere | `concept billing: no results, and none outside it either` |
| pin matches nothing | `concept billing matches no file in this map (check the paths of pin 'billing' in index.concepts)` |
| stale map | `pins were edited after map v41 was built, so some features may be grouped the old way (fix: sindri context map build)` |
| anchor in another pin | `anchor concept:auth is outside concept billing, so its lens shows only billing's part` |
| alias anchor, no concept= | `anchored on concept:billing (alias "payments"); add concept=billing to see only billing` |
| a source can't filter | `linear: left out under concept= (it can't be scoped to a pin)` |
| explain, pin leg | `pin leg: concept:billing` / `pin leg: off (context.ranker.pinLeg)` / `pin leg: anchor in no pin` / `pin leg: anchor in two pins` / `pin leg: not used under concept=` |
| code-index scan cap | `concept billing: stopped after 500 symbols scanned; narrow the name` |
| code-index none | `concept billing: no symbols named 'refund' in it` |

Error numbers are taken when Q1 is built. Profile refusals name the key path, like the others.

## 6. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Depends on |
|---|---|---|
| Q1a | `PinScope` and `PinSet` (ids over mapped repos, visibility, `HoldsPath`, `HoldsNode`, `PinOf`), the `PinContext` step in `QueryPipeline.Run`, the errors, the stale-map note | none |
| Q1b | `aliases` through the profile: `ConceptSettings`, `ConceptPin`, the three copy sites, `PinIds.Identity`, `ProfileDiff`, `MapRuleset.Pin`, `IndexRules`, the profile doc | none |
| Q2a | `concept` on the request: `ContextQuery`, MCP, CLI, HTTP, UI client and fake, goldens, the scoped line, the anchor notes. Merges **after** Q2b and Q2c, so main never accepts `concept=` without filtering | Q1a; merge after Q2b, Q2c |
| Q2b | Record legs and repo docs take the pin (`SourceQuery.Pin`, the two record sources, `RepoDocsSource`), the empty-answer note and counts, sources that can't filter | Q1a |
| Q2c | Graph part cut to the pin | Q1a |
| Q3 | `RankedLeg` overload of `Fuse`, `PinLeg`, `context.ranker.pinLeg`, the `ranker-pin` arm | Q1a |
| Q4 | `aliases` attribute on concept nodes, `Resolve` and `FromText`, `ConceptRow` `pin` / `Aliases`, glossary mark. First red test: `Node_ledger_resolves_to_the_billing_pin_by_its_alias` | Q1a, Q1b |
| Q5 | Code index `Within` (a path set), streamed limit, the notes, `--concept`. First red test: `Symbols_under_a_pin_fill_the_limit_with_in_pin_hits` | Q1a |
| Q6 | Backtest on the real map: `ranker-pin` vs `ranker-rrf` on the backtest's pin-anchored questions (count reported; under 20 means the hand check decides), plus a hand check of alias anchoring and `concept=` on 10 questions the owner writes from their last week of sessions. The owner decides the `pinLeg` default from its report | Q2a, Q2b, Q2c, Q3, Q4 |

**Parallel plan.** Q1a and Q1b run at once (two builders: Core concepts vs Sindri profile). Then Q2a, Q2b, Q2c, Q3, Q4 and Q5 run at once, six builders. All pin resolution (including the anchor's pin) is Q1a's `PinContext` step, so no later phase adds a step to `QueryPipeline.Run`; each reads `PinContext` at its own call site (Q2b the source construction, Q2c the graph part, Q3 the `Ranked` call). Q2b and Q2c take the `PinScope` through an internal pipeline input their tests set, so they don't need Q2a's request field; Q2a then wires the field to it. **Merging.** Core public API changes go in `Aw.Context.Core/PublicAPI.Unshipped.txt`. Appended lines merge by keeping both sides; Q1b and Q4 change existing signatures (`ConceptPin`, `ConceptRow`), which needs `*REMOVED*` lines, so Q1b merges first of its pair and Q4 first after Q1a. The lead runs `dotnet build -warnaserror` after each merge. The profile doc rows (Q1b, Q3) and `QueryPipeline.Run` hunks (Q2b, Q2c, Q3) are expected conflicts the lead resolves. Q6 starts when Q2a, Q2b, Q2c, Q3 and Q4 are merged.

**Bar for turning the pin leg on (Q6).** `ranker-pin` beats `ranker-rrf` by at least 2% on recall@10 or MRR over the pin-anchored backtest questions with neither falling, and in the hand check no owner question loses its right answer from the top 5. The backtest covers docs only (§4), so the hand check is what judges records.

## 7. Security

The pin filter only narrows: it runs after repo scope, the audience mask and deny globs, never instead of them, and the pin leg reorders only results that passed all three. The empty-answer counts and the unknown-pin suggestions use only what the caller may see (§3.1, §3.2). Aliases are owner profile input, validated at approval (§3.4). `Within` is not exposed to callers. Sessions read the approved profile's pins only.

## 8. Fixes to other docs (in this PR)

- `2026-10-27-sindri-context-map-structure-design.md`: Open question 14 and §10.5 point here and say the `MaxConcepts` part is dropped.

## 9. Open questions for the owner

1. **Pin leg on by default?** *Recommend off until Q6 meets the bar in §6, then on.* One profile line either way.
2. **Real query expansion with alias words** (a search for "ledger" also searches "billing")? *Recommend not now.* Anchoring already lifts the pin's results through the leg, and expansion widens every search with more noise. Revisit if Q6's hand check shows alias questions still miss.
3. **Drop the concept cap from #1372?** The cap the issue wanted pins to count against no longer exists. *Recommend dropping it and closing that part of #1372; on approval the lane comments on #1372 saying so.* A hard ceiling on concepts shown would be a separate small map-build issue.
4. **`concept=` on the code lens and on `callers` / `callees` / `similar`?** *Recommend not now.* They start from one symbol or file, so scoping them helps less; add when someone asks.
5. **Strict `concept=`:** a result in no pin is never returned. *Recommend strict.* The alternative (unpinned results ranked last) makes `concept=` a soft preference and duplicates the pin leg. The cost: until the map spec's §11 links records to features, most Slack, Linear and GitHub records have no pin and don't show under `concept=`; the note's `no pin` count makes that visible.
