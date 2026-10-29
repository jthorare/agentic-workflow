# Sindri context: concept pins scope and steer queries (#1372, query side)

**Status:** draft for the owner's read (owner q141, 2026-10-28). Not ready until the owner has read it. Review rounds and their findings are in the PR body. Revised 2026-10-29 for meta's five points, and folds in #1701's owner-authored concepts half (meta, 2026-10-29); #1701's pages-with-actions half is its own spec.
**Lane:** ideas. **Coordinated with:** context (2026-10-29: no ranking work in flight; the boost goes into fusion, not a post-sort; ties stay ordinal).

## The owner's steps: install -> works

0 lines of YAML, 0 commands, at most 3 clicks. After `./setup.sh` and the first map build (setup-once spec), on the app's Context page:

1. On a concept card, press **Pin this**. A sheet opens filled in from the map: the name, a definition line (the concept's summary), the team's words (aliases, from the concept's terms), and the features it holds. Edit any of them, or leave them.
2. Press **Pin and approve**. Sindri writes the pin into the profile and approves it in the same step, since the pin is the only change waiting. (When other changes are waiting too, the button says **Pin**, and step 3 is the normal **Review and approve** in the needs-you bar.)
3. Nothing else. The approval starts the map refresh by itself. While it builds, the card says `Pinned · the map is being rebuilt with it`; when it lands, the card shows what moved (`+4 features, 3 records moved here from Notify`).

From then on `context_query` questions that use the team's words anchor on the pin, `concept=billing` scopes an answer to it, and the pin survives every rebuild and publish. Editing works the same way: **Edit pin** on a pinned card, the same sheet, the same one-click approve. **New pin** on the page's head does it for a concept the map didn't find. The profile's YAML (§3.4) is the power path for what the sheet doesn't cover (`paths` globs, `dependsOn`, `owns`); nobody needs it to get started.
**Builds on:** `2026-10-27-sindri-context-map-structure-design.md` §10 (pins shape home features and the roll-up; merged), whose §10.5 and Open question 14 left this half to a separate design. `2026-10-10-sindri-context-retrieval-design.md` (D3 Sindri owns ranking, D13 rerank off by default, §8.2 the ranking steps). `2026-10-19-sindri-code-index-design.md` (CI4, CI7, CI13).

## 0. Why

A pin (`index.concepts` in the profile, `ConceptPin`, `Aw.Context.Core/Engine/ConceptPin.cs:11`) is where the owner says how *they* divide the product. Since §10 of the map-structure spec the map build respects pins: home features never span two pins, and pins roll up `depends-on` / `reads` / `writes`. The **query side** still ignores them:

1. `context_query` can't be scoped to a pin. "How do refunds work in billing" returns a `notify` doc about refund emails next to the billing ones.
2. A query can be anchored on a pin (`node=concept:billing` works, `Aw.Sindri.Host/Mcp/ConceptTools.cs:25`), but nothing in ranking prefers the docs and records that sit inside it.
3. The words a team uses for an area ("payments", "invoicing", the old name "ledger") are not on the pin, so a question using them never anchors to it. `AnchorMatch.FromText` (`Aw.Context.Core/Graph/AnchorMatch.cs:77`) matches node labels only.
4. An agent can't tell which concepts are pins: `context_concepts` rows say `derived` or `override` (`ConceptRow.cs:10`).
5. The code index's symbol search (`ICodeIndex.FindSymbols`, `Aw.CodeIndex.Core/Facade/ICodeIndex.cs:54`) can't be scoped to a pin either.
6. A pin can only be made by editing the profile's YAML, then `sindri profile approve`, then waiting for the next refresh. #1701 asks for the opposite: the owner types their company's concepts into the app, with definitions and the words people use, and the map is re-evaluated from that list, with the owner's input surviving rebuilds and kept apart from machine suggestions.

## 1. What the code does today (read from main 70cb128fa)

- **What `context_query` fuses.** `QueryPipeline.Run` (`Aw.Context.Features/QueryContext/QueryPipeline.cs:42-83`) searches the catalog's sources (repo docs, `RepoDocsSource`; Linear, skipped at `:197`) plus the record store's lexical and dense legs (`RecordStoreSource`, `DenseRecordSource`, built in `WithRecordStore`, `QueryPipeline.Lexical.cs:59-85`), fuses their ranks (`RankFusion.Fuse`, RRF, `K = 60`, ties by key, ordinal), then multiplies by `1 + Σ weight × feature` (`FeatureRanker.Boost`, five features in `RankFeatures.cs`), then an optional model rerank (off by default). **Code is not a fused leg.** Code files reach a `context_query` answer as the graph part's candidates (`GraphAnswer` → `GraphPart.Candidates`, the cascade from the anchor), which never enter `Fuse`. `ICodeSearch` / `GitCodeSearch` serve the code lens (`GetCode/LexicalCandidates.cs:22`), not `context_query`.
- **A map version stores a hash of the pins, not the pins** (`MapRuleset.cs:18-36,44-45`). At query time pins come from the approved profile (`ContextScopes` maps the approved profile's repos, each with its `Concepts`, `ContextScopes.cs:10-54`). `MapRuleset.Running(scope)` (`:41`) gives the hash the current pins would build; the map stores the hash it was built with.
- **Aliases on graph nodes are key-to-key** (`NodeAlias(NodeKey Alias, NodeKey Target)`, `NodeAlias.cs:8`), validated as keys (`MapDocumentValidator.cs:101`). A free word like "ledger" can't be one. Concept terms live on the concept node as an attribute (`ConceptTermsAttribute.cs:7-12`), read by `ConceptGlossaryBuilder`.
- **The code index has its own glob matcher**, `InputRules.MatchesAny` (`Aw.CodeIndex.Core/InputRules.cs:86-92,159-185`): `*`, `?`, `**`, `prefix:` / `suffix:` / `regex:`, ignoring case. Context's `PathDenylist.MatchesExactCase` (`PathDenylist.cs:39`), which homing uses (`HomeFeatures.cs:66`), keeps case. The symbol query cuts in SQL (`ORDER BY … LIMIT`, `SqliteIndexReader.Symbols.cs:48`).
- **There is no concept cap.** `MaxConcepts` is gone from the code; `ConceptPlan.cs:11` says "with no fixed number of concepts" and derived concepts are limited by merging small groups (`ConceptGrouping.Merge`). The map-structure spec's mentions of it are stale (fixed in this PR, §8).
- There is no query expansion anywhere.
- **What the app already has** (read from main 934fc1c2c). The Context page opens on the concept layer (`ConceptLayerViewModel`, `ConceptTileViewModel`): one card per concept with its size line, its top terms (`Labels.ContextConceptTerms`, read from `ConceptTermsAttribute`) and a `Set by a person` mark for pinned or overridden concepts (`IsSetByPerson`). The app already writes the profile for the owner: **Add repository** runs the `AddRepo` job, which appends through the versioned profile write and leaves the approval as its own step (`AddRepoService.cs:10-14`). The app approves the profile through the `ApproveProfile` setup step (`Labels.Setup.cs:15`). The map refresh is a job, `RefreshContextMap` (build, settle, publish decision, publish; `ContextMapRefreshService.cs:42`, `RefreshContextMapService.cs:13-19`). Nothing queues it when pins change; it runs on the `context-rebuild` cadence or by hand.
- **The other owner-input paths, audited for #1701.** Overrides (`Aw.Context.Core/Overrides`, table `map_override`: accept, reject, rename, merge, move, add, delete, propose, undo) are keyed by node key and applied over every rebuild and import, so they already survive rebuilds; they correct single nodes, not "which evidence belongs to this concept". Importing an authored map (`ImportMapService`) replaces the current version with a file, behind the protected-command gate; it is not a way to keep a concept list. Pins are the only input that changes how the build *groups* evidence (`ConceptPlan.Pinned`), so they are the owner's concept list, and this spec gives them an app surface rather than adding a fourth mechanism. A rename override on a derived concept carries into **Pin this** (the sheet shows the renamed name).

## 2. Goals and non-goals

**Goals**
- G1. `context_query concept=<pin>` (MCP, CLI, HTTP) returns only results inside the pin: fused docs and records, and the graph part's nodes.
- G2. When a query is anchored on a pin, or on a node inside exactly one, the fused docs and records inside that pin rank higher, through fusion, switchable as a backtest arm. (Code needs no boost: the graph part already starts its cascade from the anchor.)
- G3. Pin `aliases` make a question that uses the team's words anchor to the pin; `context_concepts` shows which concepts are pins and their aliases.
- G4. `sindri code-index symbols --concept <pin>` is scoped the same way.
- G5. The owner makes, edits and removes pins from the app's concept cards in at most 3 clicks, with name, definition, aliases and features pre-filled from the map (§3.6). The same writes exist as CLI verbs for scripts and agents to *propose* (a session can write, never approve).
- G6. Approving a profile whose pins changed queues the map refresh by itself; the stale-map notes show only while that refresh runs (§3.7).
- G7. The concept layer shows the owner's pins apart from machine suggestions, what each pin covers, what is in no pin, which features two pins claim, and what moved on the last refresh (§3.8).

**Non-goals**
- N1. **No pin column in any store.** Pins are matched at read time against the approved profile (§3.1). A stored pin id needs a code-index schema bump and rebuild, and goes stale on the next pin edit because the code index has no ruleset hash.
- N2. **No concept cap.** The issue asked pins to count against `MaxConcepts`; that cap no longer exists, and the catch-all it guarded against (#1339) is handled by merge-based grouping (Open question 3).
- N3. No query expansion with alias words (Open question 2).
- N4. No change to homing or the roll-up. The one build change is that pin aliases join the intent overlay's name match, so a record that says "payments" links to `concept:billing` (§3.4, Q9); that is what makes re-evaluation reassign records, not only features (#1701).
- N6. No named pages and actions (#1701's other half): its own spec.
- N5. No `concept=` on the code lens (`GetCode`) or on `callers` / `callees` / `similar` (Open question 4).

## 3. Design

### 3.1 One pin resolver: `PinScope` (public, `Aw.Context.Core/Concepts/PinScope.cs`)

```csharp
public sealed class PinScope
{
    public bool Holds(RecordKey record);   // a fused item's key: repo docs by path, records by node
    // also public: PinSet.Of(mapped, visible, graph) with PinOf(NodeKey) -> ConceptId? (every visible pin at once, for the counts and the anchor)
    public static Result<PinScope> Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, string id, GraphSnapshot graph);
    public ConceptId Pin { get; }
    public bool HoldsPath(ContextRepoName repo, string repoRelativePath);   // first pin wins, per repo
    public bool HoldsNode(NodeKey key);
    public static ConceptId? PinOf(NodeKey anchor, IReadOnlyList<ContextRepo> mapped, GraphSnapshot graph);   // the anchor's pin, or null
}
```

- **Ids.** `Of` runs the internal `PinIds.Resolve` / `PinIds.Of` (`PinIds.cs:14,32`) over `mapped`, the repos the map is built from (`ScopeRules.Mapped(settings, scope.Repos)`, as `MapRuleset.Running` does), so `concept=billing` names the same pin the map shows, never a `billing-2` made from a smaller set.
- **Visibility.** The lookup and the "nearest ids" of the error consider only pins that apply to a repo in `visible` (the caller's resolved repos). A pin that applies only to repos the caller can't see answers exactly like a typo.
- **`HoldsPath(repo, path)`.** True when the path matches this pin's `paths` in that repo's pin order (`RepoConcepts.For`: the profile's pins, then the repo's) and no earlier pin of that repo matches it: first pin wins, as in homing. Matched with `PathDenylist.MatchesExactCase`, the same matcher homing uses.
- **`HoldsNode(key)`.** Code nodes: through their **home feature** (the one `implements` edge to a home leaf, map spec §2) and that leaf's `belongs-to concept:<id>`, never through tag features. This is the map's own grouping, so path pins and `features:` pins both work. A leaf can `belongs-to` two pins when a `features:` glob and another pin's `paths` both claim it (`ConceptPlan.Pinned` doesn't apply first-pin-wins); `PinSet` then gives it to the first in `RepoConcepts.For` order, so `HoldsNode` and `HoldsPath` agree and a file is in one pin. Features: `belongs-to concept:<id>`. Records and decisions: a `belongs-to` or `mentions` edge to the concept or to a member feature (`EdgeRules.cs:27` allows both kinds as sources; the map spec's §11 roll-up adds `record mentions concept`). The concept node itself. Built once per query into a key set; O(1) per check.
- **Stale map.** When the map's stored ruleset hash differs from `MapRuleset.Running(scope)` (computed over the repos the map was built from, not the caller's narrower scope, so it doesn't fire on every query of a session that sees fewer repos; the note names no pin, so an edit to a pin the caller can't see only says that pins were edited), file paths and feature membership can disagree (globs are current, `belongs-to` edges are from the last build). Since the approval queues the refresh (§3.7), this is normally a short window, and the note says so: while a `RefreshContextMap` job queued for this ruleset hash is queued or running, `pins changed; map v42 is being built with them, so some features are grouped the old way until it lands`. Only when no such job is running (it failed, or the box was down) does the note carry a fix: `pins changed after map v41 was built and the refresh for them didn't finish (fix: Refresh on the Context page, or sindri context map refresh)`. No refusal either way.
- **Where it's built.** The map is read inside `AnchorFor` today, and the anchors come out of it. Q1a makes `AnchorFor` also return the `VisibleMap` it read (its `Snapshot` is the whole stored graph, its `Version.Ruleset` the stored hash) and builds `PinContext` right after it: the `PinSet`, the `concept=` `PinScope` and the anchor's pin, handed to the legs, the graph part and the ranker. `PinSet` is cached per (map version, approved profile hash), not rebuilt per query. With `concept=` and no readable map: `SND-CONTEXT-1xx: concept= needs a built map (fix: sindri context map build)`. `mapped` is `ScopeRules.Mapped(settings, request.Scope.Repos)`. Later phases only read `PinContext`. Q1a also adds `string? Concept = null` to `PipelineInputs` (`PipelineInputs.cs`, which already carries the test override for the ranker); `PinContext` resolves `inputs.Concept ?? request.Query.Concept`, so Q2b and Q2c are tested through it before Q2a exists. With no pins at all, `PinContext` is empty and skips every lookup.
- **Ids and aliases.** `id` may be a pin id, `concept:<id>`, or one of a pin's aliases (§3.4), so `concept=ledger` works wherever `node=ledger` does.
- **Errors.** `SND-CONTEXT-1xx` `no pin is named 'bilin' (fix: try billing, auth)`, at most 5 suggestions, from visible pins only. With no pins at all: `no pins are set up (fix: add index.concepts to the profile)`. A second code when the approved profile has the pin but the map version has no `concept:<id>` node yet (checked on the whole map version before it is cut to the caller's scope, so a session whose scoped view has no member of the pin doesn't get it falsely): `pin 'billing' is newer than map v41 (fix: sindri context map build)`; the visibility check runs before it, so it never names a pin of a repo the caller can't see. A pin that matches nothing in this map: no error, the empty-answer note below says so.

### 3.2 `concept=` on `context_query` (G1)

- **Surface.** MCP `context_query` gains the text arg `concept` (the `_texts` allow-list and the error text in `ContextTools.Bind.cs:58`); its description: "a pin id from context_concepts (rows with pinned: true); only results inside that pin". CLI `sindri context query … --concept <id>` (`ContextVerbs.cs`), and its help line. `ContextQuery` gains `Concept` as an `init` property (not positional), which also reaches the HTTP route, `ISindriApi.ContextQuery`, the UI client and `FakeSindriApi` (API goldens change; the Ui tests run in that gate). Combines with `--repo` and with every query shape: with `text`, `node` and `item` it filters the results as below; with `symbol` the symbol is the anchor and its lens is cut to the pin like any graph part; with `diff`, changed files outside the pin are left out of the anchor set and the answer notes `concept billing: 4 of 9 changed files are outside it`.
- **Where it applies**, beside the existing masks, so each leg's limit fills with in-pin results:
  - **Record legs.** `RecordStoreSource` and `DenseRecordSource` take an optional `PinScope` the way they take `AudienceScope`; a record passes when `HoldsNode(record:<key>)`. The audience mask runs first (`DenseRecordSource.cs:22,116`).
  - **Repo docs.** `SourceQuery` (`SourceQuery.cs:8`) gains an optional `init` `Pin` filter, and `SourceDescriptor` a `HonorsPin` flag (the repo's idiom for source traits; no interface in `src` has a default member) so the pipeline knows which sources apply it; `RepoDocsSource` keeps a doc chunk when its record node `HoldsNode` (the doc's record links to the pin or a member feature) or, failing a link, when `HoldsPath(repo, path)` (a `services/billing/README.md` inside the pin's code paths). Docs come in **through links, not globs**: the owner is never asked to add a `docs/**` glob to a pin. A doc under `docs/` with no link is left out until #1373 makes it a linked record (the list below). Sources that ignore the filter (Linear's live source) are left out when `concept=` is set, with a note, rather than returning unscoped hits.
  - **Graph part.** Candidates and nodes outside the pin are dropped, except the anchor itself and edge ends that are other pins or data stores (`thing` nodes, the tables the extractor finds; a billing lens still shows that billing writes `invoices`).
- **Scoped answers say so.** Every answer under `concept=` starts with `scoped to concept billing (map v41)`.
- **Empty is an answer, with a way out.** No in-pin results returns an empty result and a note built only from what the caller may see, after repo scope, audience and deny globs: `concept billing: no results. Of the results read, 7 were in auth and 3 in no pin (fix: concept=auth, or drop concept=)` (visible pins with results, most first, at most 5, then `no pin`); with none at all, `concept billing: no results, and none outside it either`; for a pin whose globs match nothing, `concept billing matches no file in this map (check the paths of pin 'billing' in index.concepts)`. Never a silent fallback to the unscoped search. **How the counts are made:** the lexical record leg and repo docs, while filling their limit, count the rows they skipped for the pin, by the pin they are in (`PinSet.PinOf`); those rows had already passed the audience mask. The dense leg masks inside its vector scan and is left out of the counts. They are counts among the rows read, not totals, and cost no second search. A pin is named only when it applies to a repo the caller can see; any other is counted under `no pin`.
- **A file in no pin is never returned with `concept=`** (strict; Open question 5). Records not linked to the graph have no pin.
- **What v1 covers, and what it leaves out until #1373.** `concept=` ships before #1373 (repo docs and tracker items becoming records linked to features; open, measured there at 583 docs synced, 2 records). Waiting would hold back the half that works now, and the `no pin` count in every empty answer shows the gap honestly. What is in and out:

  | Kind | Under `concept=` in v1 | Why |
  |---|---|---|
  | Code files | in, through their home feature | the map already homes every file (map spec §2) |
  | Features, concepts, data stores | in | `belongs-to` and the roll-up edges are built today |
  | Repo docs inside a pin's code paths | in, by path | no link needed: the path is the evidence |
  | Records the intent overlay links (link rules, name matches, and from Q9 pin aliases) | in | they have a `mentions` or `belongs-to` edge |
  | Repo docs elsewhere (`docs/**`, top-level READMEs) | **out** until #1373 | only 2 of 583 synced docs are records today, so they have no link to follow |
  | Linear, Slack and GitHub records no rule or name links | **out** until #1373 | same: no edge, so no pin; strict `concept=` won't guess (Open question 5) |

  Q9 (aliases in the overlay's name match) shrinks the last row before #1373 lands: a ticket titled "payments retry" links to billing through the alias.
- **`concept=` and an anchor in another pin.** The filter wins: results are billing's only, the anchor's lens is cut to billing, and the answer notes `anchor concept:auth is outside concept billing, so its lens shows only billing's part`. No error.
- **Aliases respect visibility.** Alias anchoring (`Resolve`, `FromText`) and the hint below consider only pins that apply to a visible repo, like `concept=`.
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

The app's sheet (§3.6) writes this; the YAML is the power path, for scripts and for the keys the sheet shows read-only.

```yaml
index:
  concepts:
    - id: billing
      name: Billing
      paths: ["packages/api/src/services/billing/**"]
      aliases: ["payments", "invoicing", "ledger"]
```

- **Shape.** `ConceptPin` gains `Aliases` as an `init` property with a `WithAliases(..)` method, like its `Declaring(..)` (`ConceptPin.cs`), so its constructor and every existing call stay as they are; `ConceptSettings` (`Aw.Sindri.Core/Profile/ConceptSettings.cs:15`, unmapped members refused) gains `aliases`; the three copy sites (`MapSetupReader.cs:69`, `ContextScopes.cs:54`, `ContextEngineChecks.cs:56`) change in one commit. `PinIds.Identity` includes them. `ProfileDiff.IndexConcepts` (`ProfileDiff.Index.cs:41-44`) shows a change. `MapRuleset.Pin` (`MapRuleset.cs:44`) gains them in Q9, not before (Q1b leaves the hash alone, so Q1b alone never triggers a rebuild). The profile doc table (`docs/csharp/sindri-profile.md`, `index.concepts`) gains the row.
- **Rules** (in `Aw.Sindri.Core/Profile/IndexRules.cs`): each alias 1 to 60 chars, at least one letter or digit, no control characters and none of the Unicode categories Cf (format), Zl and Zp (line and paragraph separators); at most 20 per pin; an alias made only of words the matcher skips (`the`, `of`, …, `AnchorMatch._small`) is refused, since it could never match. Aliases are shown through the same untrusted-text fence as labels wherever they render (`context_concepts`, the anchor line, `context explain`). The clash check compares the matcher's normalized form (`AnchorMatch.Words`: lower case, singular), so `invoice` and `Invoices` clash: an alias equal, in that form, to another pin's id, name or alias is refused with both key paths.
- **Anchoring reads them live; the build reads them too.** Query-time anchoring reads aliases from the approved profile through `PinSet`, so a new alias anchors the moment it is approved, before any rebuild. From Q9 the build also uses them: the intent overlay's name match (`IntentOverlay.Apply`) treats each alias like the concept's label, so a record whose title or body names an alias gets `record mentions concept:<id>`. That makes aliases part of `MapRuleset.Pin` and an alias edit changes the hash. Round 3 kept them out of the hash because a hash change rebuilds the whole map; with §3.7 the approval queues that rebuild in the background anyway, so the cost is build time nobody waits on, and the gain is that re-evaluation reassigns records by the owner's words (#1701). Not `NodeAlias`, which is key-to-key.
- **Anchoring, new code.** `AnchorMatch.Resolve` (`node=`), after the key and label lookups and before `NearFeature`: a whole-alias match resolves to the concept. `FromText`: a concept node also matches when all the words of one of its aliases are in the question. The longer-match-wins rule covers both; when an alias and a node label tie at the same length, the label wins (it is the node's own name). Two pins' aliases can't tie: the clash rule refuses that.
- **Shown.** `context explain` and the anchor line say `anchored on concept:billing (alias "payments")`.
- **Glossary and concepts list.** `ConceptRow` gains `Pinned` (bool; `Source` already says `override` for a pinned concept, so a new source value would collide) and `Aliases`. The concept glossary lists a pin's aliases first, marked `owner`, then its derived terms; `ConceptTermsRow` gains that mark, and the duplicate `Aw.Sindri.Ui/Api/Context/ConceptGlossaryView.cs` follows. This is how an agent finds pin ids: `context_concepts`, rows with `pinned: true`.

### 3.5 The code index (G4)

- `SymbolQuery` (`Aw.CodeIndex.Core/Facade/SymbolQuery.cs:10`) gains `Within`, a set of (repo, repo-relative path) pairs, so a same-named file in another repo never passes, as an `init` property. Internal only: not on the MCP or CLI surface; Sindri sets it.
- Sindri's `FindCodeSymbolsService` resolves `--concept <id>` with `PinScope.Of` (Sindri already references Context) over the current map and sets `Within` to the paths of the code files the pin holds (their home feature belongs to it). This covers path pins and `features:` pins alike, uses the map's own grouping, and needs no glob matching or first-pin-wins logic in the code index: the filter is set membership on `files.path`.
- **Filter before the limit.** The reader binds this repo's paths as a JSON array and filters in SQL (`AND f.path IN (SELECT value FROM json_each($within))`) before its `LIMIT`, so the limit fills with in-pin rows and an empty answer really means none; zero rows gives `concept billing: no symbols named 'refund' in it`.
- When the code index is newer or older than the map, a file the map doesn't know is outside every pin; the note `code index and map differ by N files` says so (numbers only).
- Surface: `sindri code-index symbols <name> --concept <id>` and the MCP twin's `concept` arg. No schema change, no rebuild.

### 3.6 Pin this, Edit pin, New pin (G5)

- **Where.** Each derived concept card on the concept layer gets a **Pin this** button; each pinned card gets **Edit pin**; the layer's head gets **New pin**. All three open one sheet, `PinSheetViewModel` (`Aw.Sindri.Ui/ViewModels/Context/Concepts/`), with four fields:

  | Field | Pre-filled from (Pin this) | Goes into |
  |---|---|---|
  | Name | the card's name (a rename override's name when there is one) | `name` |
  | Definition | the concept's summary | `summary` |
  | The words people use | the concept's terms (`ConceptTermsAttribute`), best first, up to 20, each checked against the alias rules (§3.4); a term the rules refuse is left unticked with the reason beside it (`clashes with auth's "login"`) | `aliases` |
  | Features | the concept's member features, by exact key, each with a tick | `features` (exact keys, never globs) |

  `id` is not a field: `PinIds` derives it from the name, and **Edit pin** keeps the pin's existing id even when the name changes, so links and `concept=` keep working. New pin starts empty, with a feature picker over the map's features (search by label). **Unpin** on the Edit sheet removes the pin; the concept falls back to a derived one on the next refresh.
- **The write.** The sheet sends `PinConcept` (`POST /api/v1/context/pins`, body: the four fields plus the profile version it read), a job like `AddRepo`: it rewrites the pin's entry in `index.concepts` through the same versioned profile write (`SetupProfileChanged`, "The profile changed while Sindri was writing it. Try again", on a version clash), validates with `IndexRules` before writing, and records `context.pin.written`. Pins made in the app go in the profile's top-level `index.concepts`; a per-repo pin edited in the app stays where it is.
- **The approve.** When the pin is the only difference between the live and approved profile (`ProfileDiff` has one `index.concepts` entry and nothing else), the button reads **Pin and approve** and the job approves the hash it just wrote, as the owner's own approval (the app is a human's surface; the approval records `by: owner (app)`). Otherwise the button reads **Pin**, and the needs-you bar's existing **Review and approve** step shows the whole diff. Either path is at most 3 clicks (open sheet, the button, and Review and approve only when other changes are waiting).
- **CLI and agents.** `sindri concept pin <concept> [--name T] [--summary T] [--alias W]... [--feature K]...`, `sindri concept unpin <id>`: the same job. An agent session may run them; its write lands in the live profile and waits for the owner's approval like any other session write (`SND-AUTH-002` refuses only the approve). This is how a lane drafts a concept list for the owner to approve in one go.
- **Who wins.** A YAML edit and an app edit go through the same versioned write, so whichever lands second sees `SetupProfileChanged` and re-reads. The sheet shows `paths`, `dependsOn` and `owns` read-only with `edit in the profile` beside them, so nothing set in YAML is lost by an app edit.

### 3.7 The approval queues the refresh (G6)

- After `profile.approved`, the setup service (`SetupService`, which already reacts to approvals for MCP registration) compares `MapRuleset.Running(scope)` with the current map version's stored ruleset hash. When they differ and no `RefreshContextMap` job for that hash is queued or running, it queues one (`JobKey` = the hash, so two approvals in a row queue one job). An approval that changes nothing the map reads queues nothing.
- The refresh is the existing job end to end (build, settle, publish decision, publish), so the owner's approval of the pin is also the approval that lets the map change; a publish held by `MapPublishDecision` shows its hold on the card the way the Context page shows holds today.
- The card's line while the job runs: `Pinned · the map is being rebuilt with it`; when the job fails: `Pinned · the rebuild failed (SND-…): Refresh to try again`, with a Refresh button that queues the same job.

### 3.8 What the concept layer shows (G7)

- **Owner's vs suggested.** Pinned cards keep `Set by a person` and come first, in profile order. Derived cards read `Suggested` and sort after them. A suggestion never changes a pin: the build's merge-based grouping only fills what pins leave (`ConceptPlan.Pinned`), and a derived concept that would take a pin's name gets a different id (`PinIds`).
- **Coverage.** A pinned card's size line as today (`4 features · 3 records · 2 decisions · 40 code files`), plus `2 of its features are gone from the map` when listed feature keys no longer exist (a rename in the code), with Edit pin as the fix.
- **In no pin.** The layer's head line: `Pins hold 120 of 300 features; 180 are in suggestions or nowhere` (counts over the map's features), so the owner sees how much of the product their list covers.
- **Claimed twice.** When a feature is listed by two pins (or matched by one's `features` and another's `paths`, round 3's T2), the second pin's card says `3 features are also in Billing, which keeps them (it comes first)`, with Edit pin as the fix.
- **What moved.** After a refresh lands, each pinned card shows the difference against the previous version's `belongs-to` and `mentions` edges into it: `+4 features, 3 records moved here from Notify` (at most two sources named, then `and 2 more`). It goes away on the next refresh. This is the "show what re-evaluation changed" #1701 asks for, after the fact; a preview before approving is not in this spec (Open question 6).
- **Survives rebuilds and publish.** Pins live in the profile, so a rebuild, a publish or an import never drops them; the ids are stable through `PinIds` over the mapped repos; overrides keyed on `concept:<id>` keep applying. Tested in §4.

## 4. Tests

**Core (unit lane).**
- `PinScope`: first pin wins per repo; a path only an earlier pin of another repo matches is still this pin's in this repo; code nodes through their home feature (a file tagged to two pins' features is in one; a `features:`-only pin holds its files); features via `belongs-to`; records via `mentions`; `concept:billing` and `billing` both resolve; a pin in a repo the caller can't see answers like a typo; ids built over mapped repos match the map's (no `billing-2`); the stale-map note when the hashes differ.
- `PinLeg`: the §3.3 table exactly; no leg with no anchor pin, with two, or with `concept=`; ties ordinal by key; `Fuse` with no legs equals today's `Fuse` on every existing `RankFusionTests` case.
- `AnchorMatch`: `node=ledger` resolves to `concept:billing`; `FromText` alias match, singular and plural; a label beats an alias of the same length; the anchor line names the alias.
- `IndexRules`: length, count, control and format characters, clash in normalized form with both key paths. `MapRuleset`: an alias edit leaves the hash unchanged. `PinSet`: a leaf claimed by two pins goes to the first.

**Features / integration.**
- `context_query concept=billing` over the map spec's §10 two-pin fixture (`billing`; `auth` with the 6 `auth*` middleware files) returns only billing results from every leg and the graph part; `concept=auth` never returns a billing result; the empty answer's note gives in-scope counts per pin and never a count from a repo or audience the caller can't see; a pin that applies only to an unseen repo and is also newer than the map answers like a typo; an alias of an unseen pin never anchors; `concept=` with an anchor in another pin gives the note; the newer-than-map error.
- `context_concepts` shows `pinned: true` and the aliases.
- `sindri code-index symbols refund --concept billing` returns only billing symbols and fills `Limit` with them; the scan-cap note.
- HTTP / UI client / fake: the `concept` field round-trips; API goldens updated; `Aw.Sindri.Ui.Tests` in the gate.
- `PinConcept`: writes the four fields into `index.concepts`; Edit keeps the id when the name changes; a version clash gives `SetupProfileChanged` and writes nothing; an alias the rules refuse is refused with its key path; **Pin and approve** approves only when the pin is the sole diff, otherwise leaves the approval waiting; a session's write lands and its approve is `SND-AUTH-002`; YAML-only keys (`paths`, `dependsOn`, `owns`) survive an app edit.
- The approval queues one `RefreshContextMap` when the ruleset hash changed, none when it didn't, one for two approvals in a row; the stale-map note has the "being built" text while it runs and the fix text after it fails.
- Q9: a record naming an alias gets `mentions concept:<id>` after a refresh; an alias edit changes `MapRuleset.Pin`.
- Survival: pin, refresh, publish, rebuild again: the pin's id, name, summary, aliases and feature membership are the same, and an override on `concept:<id>` still applies.
- **Ui (unit + integration lanes):** the sheet's pre-fill from a derived card (name, summary, terms ticked or refused with a reason, features); Pin and approve vs Pin by the diff; the card lines (being rebuilt, rebuild failed, gone features, claimed twice, what moved); the head's coverage line; owner's pins first, `Suggested` after. Rendered frames of the three-click flow through `aw-ui-driver` (the proof for #1701's "enter a concept list and re-evaluate").
- Backtest: `ranker-pin` and `ranker-features` both report. The backtest feeds only the catalog's doc sources (`QueryPipeline.Lexical.cs:59-62`), so Q6 measures docs; record legs are judged by hand on the real map.

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
| stale map, refresh running | `pins changed; map v42 is being built with them, so some features are grouped the old way until it lands` |
| stale map, no refresh | `pins changed after map v41 was built and the refresh for them didn't finish (fix: Refresh on the Context page, or sindri context map refresh)` |
| card, rebuilding | `Pinned · the map is being rebuilt with it` |
| card, rebuild failed | `Pinned · the rebuild failed (SND-…): Refresh to try again` |
| card, gone features | `2 of its features are gone from the map` |
| card, claimed twice | `3 features are also in Billing, which keeps them (it comes first)` |
| card, what moved | `+4 features, 3 records moved here from Notify` |
| layer head, coverage | `Pins hold 120 of 300 features; 180 are in suggestions or nowhere` |
| sheet, term refused | `clashes with auth's "login"` |
| sheet, read-only keys | `edit in the profile` |
| anchor in another pin | `anchor concept:auth is outside concept billing, so its lens shows only billing's part` |
| alias anchor, no concept= | `anchored on concept:billing (alias "payments"); add concept=billing to see only billing` |
| a source can't filter | `linear: left out under concept= (it can't be scoped to a pin)` |
| explain, pin leg | `pin leg: concept:billing` / `pin leg: off (context.ranker.pinLeg)` / `pin leg: anchor in no pin` / `pin leg: anchor in two pins` / `pin leg: not used under concept=` |
| code-index none | `concept billing: no symbols named 'refund' in it` |
| code-index scoped answer | `scoped to concept billing (map v41)` |
| code index and map differ | `concept billing: the code index and map v41 differ by 12 files; those count as outside it` |
| alias refused (approval) | `index.concepts[0].aliases[2]: 'Invoices' clashes with index.concepts[1].aliases[0] 'invoice' (aliases match without case or plural)` |

Error numbers are taken when Q1 is built. Profile refusals name the key path, like the others.

## 6. Phases and parallel plan (for /specToProvenPR)

| Phase | What | Depends on |
|---|---|---|
| Q1a | `PinScope` and `PinSet` (ids over mapped repos, visibility, `HoldsPath`, `HoldsNode`, `PinOf`), the `PinContext` step in `QueryPipeline.Run`, the errors, the stale-map note. First red test: `Pin_of_a_file_is_the_pin_of_its_home_feature`. | none |
| Q1b | `aliases` through the profile: `ConceptSettings`, `ConceptPin`, the three copy sites, `PinIds.Identity`, `ProfileDiff`, `MapRuleset.Pin`, `IndexRules`, the profile doc. First red test: `An_alias_that_clashes_ignoring_case_and_plural_is_refused`. | none |
| Q2a | `concept` on the request: `ContextQuery`, MCP, CLI, HTTP, UI client and fake, goldens, the scoped line, the anchor-in-another-pin note. First red test: `Context_query_takes_concept_and_answers_scoped`. Merges **after** Q2b and Q2c, so main never accepts `concept=` without filtering | Q1a; merge after Q2b, Q2c |
| Q2b | Record legs and repo docs take the pin (`SourceQuery.Pin`, the two record sources, `RepoDocsSource`), the empty-answer note and counts, sources that can't filter. First red test: `Record_legs_under_a_pin_fill_their_limit_with_in_pin_records`. | Q1a |
| Q2c | Graph part cut to the pin. First red test: `A_lens_under_a_pin_keeps_only_its_nodes_and_the_stores_they_write`. | Q1a |
| Q3 | `RankedLeg` overload of `Fuse`, `PinLeg`, `context.ranker.pinLeg`, the `ranker-pin` arm. First red test: `Pin_leg_lifts_the_billing_doc_above_the_notify_doc`. | Q1a |
| Q4 | Aliases from `PinSet` into `Resolve` and `FromText`, `ConceptRow` `Pinned` / `Aliases`, glossary mark, the alias anchor line and the `concept=` hint. First red test: `Node_ledger_resolves_to_the_billing_pin_by_its_alias` | Q1a, Q1b |
| Q5 | Code index `Within` (a path set), streamed limit, the notes, `--concept`. First red test: `Symbols_under_a_pin_fill_the_limit_with_in_pin_hits` | Q1a |
| Q6 | Backtest on the real map: `ranker-pin` vs the default arm (`ranker-features`) on the backtest's pin-anchored questions (count reported; under 20 means the hand check decides), plus a hand check of alias anchoring and `concept=` on 10 questions. **The lane drafts them** from the owner's last week of session transcripts (questions actually asked about an area, with the expected top answer beside each), at least one using an alias word outside its pin ("payments" in a notify question), and puts them in `tests/fixtures/backtest/pin-questions.md` as a checklist; **the owner only ticks keep or drop** (a dropped one is replaced from the next candidates). The owner decides the `pinLeg` default from its report | Q2a, Q2b, Q2c, Q3, Q4 |
| Q7 | `PinConcept` job, route, CLI `concept pin` / `unpin`, the sheet (Pin this, Edit pin, New pin), Pin and approve. First red test: `Pin_this_prefills_name_summary_terms_and_features_from_the_card` | Q1b |
| Q8 | The approval queues `RefreshContextMap` on a ruleset-hash change; the two stale-map texts; the card's rebuilding / failed lines. First red test: `Approving_a_pin_change_queues_one_refresh` | Q1a |
| Q9 | Aliases in the overlay's name match and in `MapRuleset.Pin`; the layer's coverage, claimed-twice and what-moved lines. First red test: `A_record_naming_an_alias_mentions_the_pin_after_a_refresh` | Q1b, Q8 |

**Parallel plan.** Q1a and Q1b run at once (two builders: Core concepts vs Sindri profile). Then Q2a, Q2b, Q2c, Q3, Q4 and Q5 run at once, six builders. All pin resolution (including the anchor's pin) is Q1a's `PinContext` step, so no later phase adds a step to `QueryPipeline.Run`; each reads `PinContext` at its own call site (Q2b the source construction, Q2c the graph part, Q3 the `Ranked` call). Q2b and Q2c take the `PinScope` through an internal pipeline input their tests set, so they don't need Q2a's request field; Q2a then wires the field to it. **Merging.** Core public API changes go in `Aw.Context.Core/PublicAPI.Unshipped.txt`. Appended lines merge by keeping both sides; Q1b and Q4 change existing signatures (`ConceptPin`, `ConceptRow`), which needs `*REMOVED*` lines, so the merge order is Q1b, then Q1a, then Q4 before the other Q2/Q3/Q5 phases. The lead runs `dotnet build -warnaserror` after each merge. The profile doc rows (Q1b, Q3) and `QueryPipeline.Run` hunks (Q2b, Q2c, Q3) are expected conflicts the lead resolves. Q6 starts when Q2a, Q2b, Q2c, Q3 and Q4 are merged. Q7 starts with Q1b's merge and runs beside the Q2 wave (a seventh builder, Ui plus a Sindri job; its gate runs `Aw.Sindri.Ui.Tests`). Q8 runs beside Q2 too (Sindri setup service only). Q9 starts after Q8, beside Q6; it changes the map hash, so it merges after Q6's backtest has read the map it measures. The owner's three-click flow (§0) is proven once Q7 and Q8 are both merged, before Q9.

**Bar for turning the pin leg on (Q6).** `ranker-pin` beats `ranker-features` by at least 2% on recall@10 or MRR over the pin-anchored backtest questions with neither falling, and in the hand check no owner question loses its right answer from the top 5. The backtest covers docs only (§4), so the hand check is what judges records.

## 7. Security

The pin filter only narrows: it runs after repo scope, the audience mask and deny globs, never instead of them, and the pin leg reorders only results that passed all three. The empty-answer counts and the unknown-pin suggestions use only what the caller may see (§3.1, §3.2). Aliases are owner profile input, validated at approval (§3.4). The app's pin writes go through the versioned profile write and the owner's approval; a session can write a pin but never approve it (`SND-AUTH-002`), and Pin and approve approves only the hash the same job wrote, only when it is the sole change. `Within` is not exposed to callers. Sessions read the approved profile's pins only.

## 8. Fixes to other docs (in this PR)

- `2026-10-27-sindri-context-map-structure-design.md`: Open question 14 and §10.5 point here and say the `MaxConcepts` part is dropped.

## 9. Open questions for the owner

1. **Pin leg on by default?** *Recommend off until Q6 meets the bar in §6, then on.* One profile line either way.
2. **Real query expansion with alias words** (a search for "ledger" also searches "billing")? *Recommend not now.* Anchoring already lifts the pin's results through the leg, and expansion widens every search with more noise. Revisit if Q6's hand check shows alias questions still miss.
3. **Drop the concept cap from #1372?** The cap the issue wanted pins to count against no longer exists. *Recommend dropping it and closing that part of #1372; on approval the lane comments on #1372 saying so.* A hard ceiling on concepts shown would be a separate small map-build issue.
4. **`concept=` on the code lens and on `callers` / `callees` / `similar`?** *Recommend not now.* They start from one symbol or file, so scoping them helps less; add when someone asks.
5. **Strict `concept=`:** a result in no pin is never returned. *Recommend strict.* The alternative (unpinned results ranked last) makes `concept=` a soft preference and duplicates the pin leg. The cost: until the map spec's §11 links records to features, most Slack, Linear and GitHub records have no pin and don't show under `concept=`; the note's `no pin` count makes that visible.
6. **A preview of what a pin edit will move, before approving?** *Recommend not now.* A preview means a dry-run build per edit (minutes on the real map). The after-the-fact "what moved" line (§3.8) plus Unpin as the undo covers it; add the preview if the owner finds themselves unpinning often.
