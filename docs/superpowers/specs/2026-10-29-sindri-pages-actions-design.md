# Sindri context: named pages and their actions, linked to concepts (#1701, pages half)

**Status:** approved by the owner on 2026-10-29 (§8 records the answers). Review rounds and their findings are in the PR body.
**Lane:** ideas. **Builds on:** `2026-10-29-sindri-concept-pins-query-design.md` (#1667: pins, `index.concepts`, the pin sheet, the profile writer; P1b-2 waits for its Q7a), `2026-10-27-sindri-context-map-structure-design.md` (homes, roll-up, §11 records). **Coordinates with:** #1702 / #1716 (source links on the node panel). #1565 part B (surfaces and actions missing from `map/whole`) is closed on main (#1573, #1584, #1596), so nothing here waits on it.

## The owner's steps: install -> works

0 lines of YAML, 0 commands. On the app's Context page, after the map has been built once:

1. Open a concept (say Self-scheduling, a pin or a concept the map found). Its Why drill has a **Pages and actions** section: the screens, dialogs and frontend routes the map found whose code sits in this concept, each marked `Suggested`, and any you've already named, marked `Set by a person`.
2. On a suggested page, press **Name this page**. A sheet opens filled in from the map: a name made from the route or component (`/schedule/week` -> `Schedule week`, `ScheduleWeekPage` -> `Schedule week`), its actions with the names the map gave them (`Submit schedule`), and for each action the concepts it relates to, pre-ticked from where its code lives. Rename anything into your team's words, tick a second concept on an action (`Submit a schedule` relates to Self-scheduling **and** Approval workflow), add an action the map missed, or leave it.
3. Press **Save and approve** (the pin rule: one click when this is the only waiting change, else **Save** and the needs-you bar's **Review and approve**; until then the row says `Waiting for approval`). The page shows on the next read of the map: nothing rebuilds (§3.3).

That is **2 clicks** for a page the map found under a concept. A page whose code has no home in any concept is under **Pages in no concept** on the Context page's head: 3 clicks (open the list, Name this page, Save and approve). For many at once, **Accept all suggested** on a concept's Pages and actions (or on Pages in no concept) opens one sheet listing every suggested page there with its pre-filled name, actions and ticked concepts; untick the wrong ones, rename any, and **Save all and approve**: **2 clicks for all of them**, plus one per page you untick. **New page** does it for a page the map didn't find. **Add several pages** takes a pasted list: one page per block, its first line the name, then one action per line, optionally `-> Self-scheduling, Approval workflow`; it shows each block as a preview row before anything is written.

From then on, from a concept you find its pages and actions; from an action, its page, every concept it relates to, the decisions and records about it, and the code that implements it when the map knows it. **The owner picked the mockups (#1744, 2026-10-29); the build matches them.** Pages and actions in a concept's Why drill is **A**, a list grouped by page, so each action's page shows at a glance (`mockups/screens/section-a-*`). Name this page is **A**, one column in the same 520 px sheet as the pin sheet. Accept all suggested is **B**, stacked rows where a row opens into the page's fields (§3.6 as written). Pages in no concept is **A**, a sheet with the same section as a concept's, and Esc goes back. The files are under `docs/superpowers/specs/2026-10-29-sindri-pages-actions-design/mockups/`. How many of this repo's pages land under a concept and how many under Pages in no concept is measured in P2m on the owner's map and written into this section before P2b starts. A repo with no web frontend shows the empty state until its views are recognized: on this repo P2m measured 0 pages, because its 112 routes are `Host/Endpoints` `.cs` files and `IsPage` counts a route only from `.tsx` or `.jsx` (§3.2), and its 11 `*Page` and 3 `*Dialog` `.axaml` views aren't surfaces at all (Open question 4).

## 0. Why

#1701 asks for two things. The concept list is #1667. This is the other: "This is a page called 'XYZ' and it has these 'actions' like 'submit a schedule' that relates to this other concept." Today:

1. The map has the node kinds: `Surface` (keys `surface:<kind>/<name>`, kinds in `SurfaceKindList.cs:9`: screen, pane, dialog, route, api, command, keybinding, setting, …) and `Action` (`NodeKind.cs:14-19`); an action is `on` a surface (`EdgeRules.cs:16`), and code `implements` either (`:22`). `ActionPass` derives actions from named click and submit handlers (key `action:<Component>.<words>`, label the words, `ActionPass.cs:100-117`), state-changing routes (`action:route/<METHOD>:<path>`, `:133`) and titled commands, at confidence 0.8 (`:9-15`). Route surfaces are keyed `surface:route/{Method}:{Path}` and labelled `{Method} {Path}` (`Recognizers.cs:104,108`); a component is a `screen` when its name ends in Page or Screen, a `dialog` for Dialog or Modal, else a `pane`, and test ids are `pane`s too (`:117-125`).
2. Nothing links an action or a page to a concept: `belongs-to` runs only from features, records and decisions (`EdgeRules.cs:27`).
3. The owner can't name or correct them: an `add` override makes a node with one `part-of` parent and nothing else (`Overrides/OverrideApplier.Working.cs:119-137`), and no app screen makes overrides.
4. They don't reach the Why view: the Why band holds records and decisions only (`Ui/ViewModels/Context/Whole/NodeBands.cs:12-13`); surfaces and actions are in the What band (`:14-19`).
5. So in the issue's screenshot a migration's table shows where a page should: the map has no page to show.

## 1. Goals and non-goals

**Goals**
- G1. The owner names pages and their actions, and links each action to one or more concepts, from the app in at most 3 clicks for a page the map found.
- G2. From a concept: its pages and actions. From an action: its page, its concepts, its Why (decisions that `decides` it or its page, records that `mentions` either) and the code that `implements` it. Both ways, in the Why drill and the node panel.
- G3. Owner-named, suggested and code-backed are three visibly different things, in words.
- G4. Named pages survive every rebuild, publish and import, with stable ids; a page whose route vanished stays, and says so; a removed page stays removed.
- G5. Missing evidence stays explicit: an owner action with no code says `No code found yet`, never a guess.

**Non-goals**
- N1. No directory tree: an action relates to any number of concepts, a page to none or many.
- N2. No new extraction: suggestions come from what `ActionPass` and the recognizers already find (`Recognizers.cs:19-28`). Better extraction is #1565's.
- N3. No product nodes made up from tables or migrations: a `thing` stays a `thing`.
- N4. Flows are not authored here (Open question 3).
- N5. Source links are #1702's; this spec shows the ones the node panel already has (#1716).
- N6. Tracker issues and docs reach a page or action the way they reach any node: as records with a `mentions` edge from the existing linker. Nothing new here.

## 2. What the code does today (main 7ad84c8cf)

- **The read path.** `MapAccess` is the one function from a stored snapshot to what a caller sees (`MapAccess.cs:15-30`, body `:60-85`): the intent layer is merged (`:77`), the deny cut is decided and applied (`:81-82`), the overrides are applied (`:83`), then a session's view is cut to its repo (`ForSession`, `:88-99`, through `GraphSnapshot.ScopedTo` and `RepoScope`). Lenses, status, export and the review queue all read through it; `ConceptReader.cs:43`, `SearchMapService.cs:56` and `GetWholeMapService.cs:48` call `ScopedTo` again on its output.
- **The deny cut never cuts a Human node or edge for its evidence** (`DenyCut.cs:216,219`): only a non-Human one whose evidence is all denied.
- **Repo scope is read from the graph only.** `RepoScope.NodesFor` keeps the nodes at either end of an `implements` or `tags` edge evidenced `repo:<repo>/…`, and the repo's code files (`RepoScope.cs:15-25`); `WithProduct` adds actions `on` a kept surface (`:74-86`); `WithConcepts` adds concepts a kept node `belongs-to` (`:121-126`). Surface and action keys carry no repo, so the same `GET:/health` in two repos is one node.
- **A concept's drill** is `ConceptScope.Nodes` (`WholeMap/ConceptScope.cs:25-50`): the concept's `belongs-to` members, two levels of neighbours (concept nodes and other concepts' members skipped, `:39`), then a forward product closure capped at 200 nodes (`ClosureCap`, `:16`). `WholeMapBuilder.OpenedConcept` keeps only edges between the nodes it shows.
- **Bands are a UI decision** (`NodeBands.cs:11-21`): Intent ("Why") = record, decision; Product = feature, surface, action, thing, flow, concept; Code = code.
- **The Why drill** is `WholeMapBandDrill.cs` and `NodePanelViewModel.cs`; the server side is `WholeMapBuilder` and `AreaAssigner` (surfaces placed by `part-of`, actions by `on`, `AreaAssigner.cs:84-85`); the per-node intent lists are `LensIntent.Build` over `LensScope.IntoScope` (`LensIntent.cs:12-44`, `LensScope.cs:42`).
- **Owner input in Context.** Pins reach the engine as `ProfileRepoEntry.Concepts` (`ProfileRepoEntry.cs:9`), mapped from the profile in `ContextScopes.cs:54` and copied to `ContextRepo` in `ScopeRules.cs:74`. Pin edges carry evidence `profile:index.concepts.<id>.…` (`PinEdges.cs:25-39`), which `StructureCut.cs:50` uses to know them. Pages take the same path.
- **Export** reads `MapAccess.View(...).Graph` (`ExportMap/ExportMapService.cs:37,44`); import stores the file's nodes as map nodes.
- **Enums are strict on disk.** `NodeKind` and `EdgeType` read through `StrictEnumConverter` (`EdgeType.cs:6`); the map file's schema version is 2 only for `tags` or counted edges (`MapAccess.cs:213-214`).

## 3. Design

### 3.1 Where named pages live: the profile, beside pins

```yaml
index:
  pages:
    - id: schedule-week
      name: Schedule week
      repo: web                                        # optional when the surface is in one repo, or the profile has one
      surface: "surface:route/GET:/schedule/week"      # optional: the map page it names
      actions:
        - id: submit-schedule
          name: Submit a schedule
          action: "action:SchedulePage.submit-schedule"   # optional: the map action it names
          concepts: [self-scheduling, approval-workflow]
        - name: Request a swap
          concepts: [self-scheduling]
```

- **Why the profile, not overrides.** Pins are the owner's concept list there (#1667), so pages sit next to the concepts they point at: one writer, one approval rule, one review diff. Overrides correct single nodes and carry no `on` or concept edges; widening them would make a second owner-input path. Import replaces the map and is not a list.
- **Settings and the engine type.** `PageSettings` / `PageActionSettings` in `Aw.Sindri.Core/Profile/` (unmapped members refused, like `ConceptSettings`). The engine gets a Context-side `PagePin` (`Aw.Context.Core/Engine/PagePin.cs`: id, name, repo, surface key, actions with id, name, action key, concept ids), carried as `ProfileRepoEntry.Pages`, mapped in `ContextScopes` like `Concepts` and copied to `ContextRepo.Pages` in `ScopeRules.cs:74`, with `PublicAPI.Unshipped.txt` lines for both.
- **Repo.** `repo:` when given. Else, with a surface key: the repos of the `repo:<repo>/…` evidence on the `implements` edges into that surface; one repo is the page's, several is refused (`SND-CONTEXT-252`, "this page is in web and admin: say which"). Else the profile's only repo; with several repos and no key, refused (`SND-CONTEXT-252`).
- **`ProfileDiff.IndexPages`** shows each change by id. A page's row value holds every field: name, repo, surface key, and each action's id, name, key and concept ids, so any edit, even one concept on one action, is a row.
- **Rules, two times.** At **write**: `PageRules.Check(pages, facts)` in `Aw.Context.Core` (pure: the pages and the current map's concept ids, surface and action keys and their repos), called by the CLI, the sheet and Add several pages alike. Names 1 to 80 characters after control and format characters are stripped, no leading or trailing space (the alias rules, `SND-CONTEXT-255`); at most 200 pages, 40 actions per page, 20 concepts per action (`SND-CONTEXT-253`); page ids unique and action ids unique within a page (`SND-CONTEXT-254`); `concepts` name pin ids or derived `concept:<id>` in the current map (`SND-CONTEXT-250`); `surface` and `action` keys exist in the current map (`SND-CONTEXT-251`). At **read** of an approved profile nothing is refused: a key the map no longer has falls back to an owner node and a concept it no longer has drops that edge (§3.3), and `sindri doctor` shows a `context:pages` warning row per stale key or concept (and per cap a hand-edited profile breaks: the overlay reads the first 200 pages, 40 actions and 20 concepts and ignores the rest), so a rebuild after a route rename never makes the approved profile fail.
- **Ids** when absent: the name lower-cased, words joined by `-` (`Schedule week` -> `schedule-week`); a clash with another page's id takes `-2`, `-3`. Kept on rename (the #1667 rule, its §3.1).
- **The writer** is #1667's `IProfileConceptWriter`, generalized to `IProfileIndexWriter` with an entry kind (concept or page): the same text edit, comments kept, re-reading and comparing the profile hash under its one lock. Every write carries the hash it read: the sheet's Save, Add several pages' Save all and `page set --if-hash` alike (`SND-PROFILE-003` on a clash).
- **Where in the profile.** `index` sits at the top and under each repo (`ProfileDiff.cs:43,129`), so `index.pages` does too; a page under `repos.<name>.index.pages` belongs to that repo, and its `repo:` must be absent or equal it.
- **Who approves: as today** (owner, 2026-10-29, on #1667's Open question 7). A page change goes through the existing `ApproveProfile` with q93's behavior; there is no approve rule over `index.pages.`, so a session the owner started can approve a page change as it can any profile change.

### 3.2 The new edge: `expresses`

- `EdgeType.Expresses`, action or surface -> concept, many per source (`EdgeRules` gains `[Action, Surface] -> [Concept]`). It says "this page or action is how a person meets this concept". Not `belongs-to`: a feature has one home concept, but an action relates to several, and pins' `HoldsNode` must not put an action inside every concept it touches (#1667 §3.1 keeps homes single).
- **Strict on disk.** A map holding `expresses` can't be read by a binary from before P1a (`StrictEnumConverter`): no rollback past P1a, said in the context doc. Every binary on the box upgrades together through `install-cs.sh`, so this only bites a hand rollback. The map file says so too: `SchemaVersionFor` picks a new version 3 when the map holds an `expresses` edge (a `MapDocumentValidator.SchemaVersions` entry), written as explicit cases (3 for `expresses`, else 2 for `tags` or counts, else 1) rather than `SchemaVersions[^1]`, so a tags-only map keeps writing 2; so an older reader refuses the file by version, not with a parse error.
- **Owner edges** carry `Provenance.Human`. **Suggested edges** are built: after `namers.Concepts.Run` (`BuildMapService.cs:122`), a page or action gets `expresses concept:<id>` for each concept its implementing code's home features `belongs-to` (a pin or a derived concept, so a fresh map with no pins still gets suggestions): at most 3 per source, the ones with the most implementing files first, then by id. `Provenance.Deterministic`, reason `code in Self-scheduling` (or `code in Self-scheduling and Billing`). Because the build makes them, P1a bumps `MapRuleset.RulesVersion` (`MapRuleset.cs:14-15`; today `"1"`, and whichever of #1667 and this lands second takes the next number): every box rebuilds once after the upgrade (a guess of a few minutes here, 0 model calls; P1a's PR states the measured time). Owner pages are not in the hash.
- **What counts as a page.** `PageKinds.IsPage(node, edges)`, judged from the node's kind and the paths of the `implements` edges into it, nothing else: `screen` and `dialog` always; `route` when one of those paths ends in `.tsx` or `.jsx` (a server route in a `.ts` or `.cs` file is not a page in v1); never `pane` (every other component and every test id is a `pane`, `Recognizers.cs:117-125`), `api`, `command`, `keybinding`, `setting`, `menu`, `job`, `notification`, `permission`, `report` or `flag`. The suggestions, the drill and `context_pages` all use it.
- **Names offered.** A route's path segments to words (`GET:/schedule/week` -> `Schedule week`, parameters dropped); a component name split at capitals with `Page`/`Screen`/`Dialog`/`Modal` dropped (`ScheduleWeekPage` -> `Schedule week`); else the raw label. An action offers its node's label as `ActionPass` made it (`Submit schedule`). Two pages may share a name (a route and its component both give `Schedule week`): ids keep them apart, and the sheet says `Another page is called Schedule week` without blocking the save.

### 3.3 Applied at read time, so nothing rebuilds

A named page is an overlay, `PageOverlay.Apply(graph, pages)`, applied inside `MapAccess` **right after the deny cut and before the overrides** (between `MapAccess.cs:82` and `:83`), to the cut graph from which both `view.Graph` and `view.Applied` come. After the cut, because the cut never judges a Human node (`DenyCut.cs:216`): a relabel can't rescue a surface a deny glob hid, and the overrides can still act on owner nodes.
- **Marks without changing provenance.** Every node and edge the overlay makes or touches carries the evidence ref `page:<repo>/<page-id>` (one scheme, so `RepoScope`, the marks and export all read the same thing). A node with a `page:` ref is `Set by a person`.
- A page with a `surface:` key relabels that node: the key, provenance and evidence stay, the owner's name shows, and it gains the `page:` ref. Without one the overlay adds `surface:screen/owner-<id>`, `Provenance.Human` (`screen` is a valid kind; the `owner-` prefix keeps it apart from extracted screens).
- Each action the same way: an `action:` key relabels; otherwise `action:owner/<page-id>/<id>`, `on` its page.
- Each action's `concepts` become `expresses` edges, `Provenance.Human`; for a named node they replace its suggested `expresses` edges. A concept the map no longer has makes no edge, and the action row says `Linked to a concept the map no longer has: Edit page to pick another.`
- A key the current map doesn't have (renamed, or cut by a deny glob) falls back to an owner node, and the page row says `This page's route isn't on the map any more: Edit page to pick its new one.`; `sindri doctor`'s row says which of the two it is.
- **Repo scope for sessions.** `RepoScope.NodesFor` gains one clause: a node with a `page:<repo>/…` ref is in `<repo>`'s set. Owner actions then join through `on` (`WithProduct`, unchanged), and a relabeled page is in scope by its `implements` edges as today. Because the repo is on the graph, every `ScopedTo` caller (`MapAccess`, `ConceptReader`, `SearchMap`, `GetWholeMap`) gets it with no new argument. `WithConcepts` also keeps a concept that a kept node `expresses`, so a session in repo web opening Self-scheduling sees web's pages for it and none of admin's.
- **Publish, export, import.** Publish holds and releases a stored version and never touches the graph (`MapPublishing.cs:12-31`); since the overlay runs on every read, a published version shows the same pages as a draft. Export reads through `MapAccess` **with the scope's pages left out**, so a file holds the map and never the overlay: the profile is the only source of owner pages. Import drops any node or edge with a `page:` ref, in case a file was made by hand. So a removed page stays removed after any export and import, and a box whose profile never had the page never shows it.
- **Shared keys.** Surface and action keys carry no repo, so one `surface:route/GET:/health` can be evidenced in web and admin. A page relabels its surface (or an action its action) only when every `repo:` ref on the node's `implements` edges is the page's repo. Otherwise the overlay makes the owner node instead and copies onto it the `implements` edges whose evidence is in the page's repo, with that real evidence and their own provenance, so the page still shows its code and an admin session never sees web's name for the shared node. The sheet says `This route is in web and admin: the name is for web only`.
- **The view cache.** `MapViewCache` keys a view by the settings, each repo's name, path and deny paths and the caller (`MapViewCache.cs:76-79`); the key gains a digest of each repo's `Pages`, so an approved edit misses the cache and shows on the next read. Export's read without pages is its own cache mode. P1c's test reads through `MapReader`, the cache in front: `An_approved_page_edit_shows_on_the_next_cached_read`.
- An approved page edit shows on the next read: no refresh, 0 model calls. Cost: one overlay pass per read over at most 200 pages x 40 actions; P1c's PR adds a unit-lane timing line for that worst case.

### 3.4 Reaching them from Why (G2)

The Why band stays records and decisions (the issue: keep the taxonomy). What changes is what you can reach from it:
- **A concept's Why drill** gains **Pages and actions**, after its decisions and records: the concept's pages, each with its actions that `expresses` the concept; owner-named first in profile order, then `Suggested` by number of actions; at most 20 pages, then `Show all 34`. A row: `Submit a schedule · on Schedule week · also in Approval workflow · 2 decisions · code: SubmitButton.tsx`.
- **Pages in no concept**: a link on the Context page's head, `Pages in no concept (12)`, shown when the count isn't 0. It opens the same section over the pages (`IsPage`) that relate to no concept: no `expresses` edge from the page or from any action `on` it (owner links sit on actions, so a page leaves the list as soon as one of its actions is linked; a page saved with no linked action stays, which is the settings-page case). Same rows, same Name this page and Accept all suggested; empty state `Every page relates to a concept.` P2a's test: `A_page_leaves_pages_in_no_concept_when_one_of_its_actions_is_linked`.
- **An action's node panel**: its page; `Relates to` (every concept it expresses, each a link); `Why` (decisions that `decides` it or its page, records that `mentions` either: `LensScope.IntoScope` gains surfaces and actions); `Code` (`implements` edges, with #1716's source links), or `No code found yet`.
- **A page's node panel**: its actions, the union of their concepts, its Why, its code.
- **Server.**
  - `ConceptScope.Nodes` gains a second root set: the nodes with an `expresses` edge to the concept and the surfaces those actions are `on`, added after the closure and not counted against `ClosureCap`, with its own cap of 200 pages (the section's order) and their actions. So an action linked only by `expresses` is in the drill, and a big concept's pages aren't cut by its product closure.
  - `WholeMapBuilder` serves `expresses` edges (`OpenedConcept` keeps them, both ends now shown).
  - `AreaAssigner` gains one rule: a page with no `part-of` takes the area of its first concept (its own `expresses`, else its first linked action's). A suggested page with no code home has neither and stays in Unassigned, where Pages in no concept finds it.

**States.**
| Where | Loading | Couldn't read | Empty |
|---|---|---|---|
| Pages and actions | `Loading pages…` | `Couldn't read this concept's pages just now. Try again.` with **Try again** | `No pages or actions found for this concept yet. Name one with New page.` |
| Pages in no concept | `Loading pages…` | `Couldn't read the pages just now. Try again.` | `Every page relates to a concept.` |
| Action panel, Relates to | (with the panel) | (the panel's own error) | `Not linked to a concept yet. Edit page to pick one.` |
| Action panel, Why | (with the panel) | (the panel's own error) | `No decisions or records about this yet.` |
| Page panel | (with the panel) | (the panel's own error) | actions: `No actions on this page yet. Edit page to add one.`; concepts: `None of its actions relates to a concept yet.` |

### 3.5 Owner's, suggested, code-backed (G3)

| Mark | Means |
|---|---|
| `Set by a person` | from `index.pages` (a `page:` ref) |
| `Suggested` | found by the map, not yet named (Deterministic) |
| `Waiting for approval` | named in the profile, not approved yet (read from the waiting profile diff the needs-you bar already reads) |
| `Code found` / `No code found yet` | whether an `implements` edge reaches it |

Always words, never color alone. A concept card shows `2 new suggested pages` when a rebuild found pages that weren't there before, until the owner opens the section; a later rebuild that finds more starts the count again.

### 3.6 The sheet: Name this page, Edit page, New page, Add several pages

- **Fields.** Name (pre-filled per §3.2); actions, one row each (name pre-filled; concepts as ticks, pre-ticked from the suggested `expresses` edges; **Add an action**; **Remove** per row); the page's map surface and each action's map action as read-only `From the map: GET /schedule/week` lines, with **Pick another** when one is gone. A page with no actions saves; its panel says so (§3.4).
- **Pick another**: a search box over `IsPage` surfaces (or the page's actions), up to 50 rows by name; `Searching…` while it reads, `No page matches "schedul"` when none does, the panel's error line when the read fails.
- **States** as #1667's sheet: `Saving…`, a clash keeps every typed field and says `The profile changed since you opened this. Your changes are kept: Save again to apply them on top.` (`SND-PROFILE-003`), Esc with `Discard your changes?`. A refused write shows its reason as a sentence under the field it names (`No concept called "aproval"`, `Names are at most 80 characters`); the CLI prints the id (§5).
- **Keyboard and screen readers.** Focus starts on Name; Tab runs Name, each action row (name, its concept ticks, Remove), Add an action, the buttons. Each concept tick is a check box named `Relates to Approval workflow`; each row reads `Action 2 of 3, Submit a schedule, relates to 2 concepts`. The section's rows are a list; each row is one tab stop, its name the link, read with its marks, page, other concepts, decision count and code file; those are separate links on the node panel. After **Show all 34** focus goes to the first newly shown row; after **Try again**, to the section head. Closing returns focus to the button that opened it. The section's status line is a polite live region (a `TextBlock` whose `Text` changes, `docs/csharp/sindri-ui.md:228-231`; not yet confirmed by ear until the owner's VoiceOver pass, as that doc says).
- **Add several pages.** A text box named `Pages to add`, described by `One page per block: its name, then one action per line, optionally -> concept, concept`. Blocks separated by blank lines; bullets and numbers dropped; control characters stripped; at most 64 KB pasted. **Read the list** (parsing is local and instant) shows a preview row per block before anything is written, then moves focus to the first row with a problem, else to Save all:
  | Case | Row |
  |---|---|
  | nothing pasted | `Paste a list first: one page per block.` under the box |
  | matched to a suggested page | `Matches GET /schedule/week (4 actions found)`, changeable, or `New page` |
  | a second block matching the same page | `Same page as block 2`, unticked |
  | a concept name that matches none | that name in the row with `No concept called "aproval": pick one`, a picker beside it in the row's tab order; the block isn't written until it is picked or removed |
  | an action line matching one the map found | `Matches Submit schedule` |
  | a name over 80, over 40 actions or 20 concepts | the reason in the row, the block unticked (an unticked block reads `Not added: <reason>`) |
  Matching: the suggested page whose name words (§3.2) equal the block's name ignoring case and plural, else the one sharing most words, ties to the one with more actions. Concepts here are names: a pin or concept id first, then an exact name ignoring case, else the row above. **Save all and approve** / **Save all**: one write, one approval. It ships as its own PR after the single-page sheet (P2c).
- **Accept all suggested.** A button on the Pages and actions section and on Pages in no concept, shown when the list holds a suggested page: `Accept all suggested (14)`. It opens Add several pages' preview (above) already filled, one row per suggested page there, each ticked, with its pre-filled name (§3.2), its actions and its pre-ticked concepts; a row opens into the single-page fields to rename or retick. Owner pages aren't listed. On **Pages in no concept** the rows have no suggested concept to tick (that is why they're on the list), so each row gets a concept picker instead: pre-filled with the concept whose name shares the most words with the page's name (§3.2's words, ignoring case and plural; ties to the concept with more pages), else empty with `Pick a concept`; **None** in the picker skips that row (it reads `Not added: no concept picked`, unticked), and a row with an empty picker is unticked too. So every page that's saved links a concept and leaves the list, and the button does what it says; a page that really has no concept (the settings-page case) is named one at a time with Name this page. Over 200 in one list, the first 200 by the section's order and `186 more: accept these first`; the profile's 200-page cap still holds, so rows past it say `The profile holds at most 200 pages` and stay unticked. **Save all and approve** / **Save all**: one write (one profile hash, `SND-PROFILE-003` on a clash keeps every tick and name), one approval. Esc with `Discard your changes?`. It ships with the single-page sheet in P2b, since it needs no paste parser.
- **Remove page**: `Remove the page Schedule week? Its actions and their concept links go too; the map's own page stays as a suggestion.` Focus lands on Cancel; Esc cancels.
- **CLI.**
  - `sindri context page set <name> [--id ID] [--repo R] [--surface KEY] [--action "Name[=KEY][->c1,c2]"]... [--if-hash H] [--dry-run] [--json]`: **replaces** the page with that id (or the id made from the name) by what the flags say. `--action` takes names without `=`, `->` or `,`; anything else goes through `page set --from-file F` (the YAML above). Concepts resolve as in Add several pages. `--dry-run` prints the `ProfileDiff.IndexPages` rows and writes nothing.
  - `page remove <id>`, `page list [--json]` (the owner's pages), `page list --suggested [--concept C] [--json]` (the map's pages with their keys and suggested concepts, for finding a `--surface` key).
  - Exit codes: 0 written (the output says `waiting for approval` when it is), 1 an unknown name or a stale key (attention), 2 refused or error. Follow, `--detach` and `--no-start` as `context pin` (#1667 §3.6). An agent may write; approving is the existing `ApproveProfile`, unchanged.

### 3.7 MCP

- **`context_pages`** (read): args `concept` (optional, a pin or concept id), `page` (optional, a page id), `repo` (optional), `limit` (default 50, max 200), `offset` (default 0). Returns `{ pages: [{ id, key, name, repo, mark: "owner"|"suggested", code: bool, actions: [{ id, key, name, mark, code, concepts: [id] }] }], total, truncated }`, ordered as §3.4 (owner pages in profile order, then suggested by number of actions), at most 40 actions a page, suggested pages included, names fenced as untrusted text, a session's repo scope applied. An unknown `concept` or `repo` is `SND-CONTEXT-258`, an unknown `page` `SND-CONTEXT-257`.
- `context_concepts` rows gain `pages` (count). `context_query` with an action or page anchor works as for any node; `concept=` (#1667) keeps an action when it `expresses` the concept.

## 4. Tests

- **Core.** `PageOverlay`: relabel by key keeps provenance and evidence and adds the `page:` ref; an owner node `surface:screen/owner-<id>` when no key; fallback when the key is gone; `expresses` edges, Human; owner edges replace suggested ones for a named node; a stale concept makes no edge; idempotent on a second apply. `PageKinds.IsPage`: one row per `SurfaceKindList` kind, plus a `pane` from a test id (no), a `route` from a `.ts` file (no) and from a `.tsx` file (yes). The name rules. The suggestion rule after concept naming, on a derived concept, on a pin, and on an action with two homes (both, at most 3). `EdgeRules`: `expresses` from action and surface to concept only. `PageRules.Check` at write (`A_page_action_naming_an_unknown_concept_is_refused_at_write`, the caps, the ids, a surface key in two repos without `--repo`); the read-time fallback (`An_approved_page_whose_route_vanished_still_reads`). `RepoScope`: an owner page in repo web is in web's set and not api's, with no surface key; `WithConcepts` through `expresses`. `ConceptScope`: `A_concept_whose_only_link_to_an_action_is_expresses_lists_it`, and a concept with a 200-node closure still lists its pages. `SchemaVersionFor` gives 3 with an `expresses` edge, and a version-2 reader refuses that file by version. A session's `propose` override can't make an `expresses` edge that reads as Human.
- **Features / integration.** Name a suggested page, approve, read: the page and its actions show with the owner's names and `expresses` edges to two concepts, and no refresh job is queued. Survival: rebuild, publish, export, import of the export, read: the same pages, ids, names and links, nothing doubled. `A_removed_page_stays_gone_after_an_import_of_an_older_export`. A session of repo `api` sees none of repo `web`'s owner pages, through `MapAccess`, `ConceptReader` and `GetWholeMap`; a page naming a surface evidenced in web and admin shows its name to web's session and the map's own label to admin's (`A_shared_surface_named_for_web_keeps_its_own_label_for_admin`). A session's approve of a diff touching only `repos.web.index.pages` goes through, as q93 has it. A deny glob over a named surface's only file: the surface is cut, the page falls back to an owner node, and doctor says hidden by a deny rule. `context_pages` (with `offset`) and `context_concepts` shapes; goldens.
- **Ui.** The Pages and actions section (order, cap, states, focus after Show all and Try again); Pages in no concept; the action and page panels (Relates to, Why, Code or `No code found yet`, their empties); the sheet pre-fill, add an action, tick a second concept, a page with no actions, Pick another, the states, keyboard order and names; Accept all suggested (all ticked, untick one, rename one, the 200 cap, one write and one approval; on Pages in no concept: the name-match pre-fill, an empty picker and None leave the row unticked, every saved page leaves the list); Add several pages' preview cases; Remove page with focus on Cancel; `Waiting for approval`. Rendered frames through `aw-ui-driver` of: concept -> page -> action -> second concept -> its Why -> code (the issue's "navigate both ways" proof), on a fixture with a feature, a screen, a button action and a decision about it.

## 5. Texts and error ids

| When | Text |
|---|---|
| section head | `Pages and actions` |
| row | `Submit a schedule · on Schedule week · also in Approval workflow · 2 decisions · code: SubmitButton.tsx` |
| marks | `Set by a person` / `Suggested` / `Waiting for approval` / `Code found` / `No code found yet` |
| suggestion reason | `code in Self-scheduling` |
| new suggestions | `2 new suggested pages` |
| no concept | `Pages in no concept (12)` / `Every page relates to a concept.` |
| route gone | `This page's route isn't on the map any more: Edit page to pick its new one.` |
| concept gone | `Linked to a concept the map no longer has: Edit page to pick another.` |
| same name | `Another page is called Schedule week` |
| section states | `Loading pages…` / `Couldn't read this concept's pages just now. Try again.` / `No pages or actions found for this concept yet. Name one with New page.` |
| panel empties | `Not linked to a concept yet. Edit page to pick one.` / `No decisions or records about this yet.` / `No actions on this page yet. Edit page to add one.` / `None of its actions relates to a concept yet.` |
| remove confirm | `Remove the page Schedule week? Its actions and their concept links go too; the map's own page stays as a suggestion.` |
| accept all | `Accept all suggested (14)` / `186 more: accept these first` |
| Add several | `Paste a list first: one page per block.` / `Same page as block 2` / `No concept called "aproval": pick one` / `Not added: <reason>` |
| `SND-CONTEXT-250` | `index.pages[0].actions[1].concepts[0]: 'aproval' is no concept in map v41 (fix: try approval-workflow)` |
| `SND-CONTEXT-251` | `index.pages[0].surface: 'surface:route/GET:/schedule/wk' is not in map v41 (fix: sindri context page list --suggested, or pick it in the app)` |
| `SND-CONTEXT-252` | `index.pages[0]: say which repo this page is in, web or admin (fix: --repo)` |
| `SND-CONTEXT-253` | `index.pages[0]: 41 actions; a page holds at most 40` / `index.pages: 201 pages; at most 200` / `index.pages[0].actions[2]: 21 concepts; at most 20` |
| `SND-CONTEXT-254` | `index.pages[3].id: 'schedule-week' is already index.pages[0]'s` / `index.pages[0].actions[2].id: 'submit' is already actions[0]'s` |
| `SND-CONTEXT-255` | `index.pages[0].name: 81 characters; at most 80` / `… is empty` / `… starts or ends with a space` |
| `SND-CONTEXT-256` | `--action "Submit->": no concept after '->'` |
| `SND-CONTEXT-257` | `no page is named 'schedul-week' (fix: try schedule-week)` (remove, `context_pages`) |
| `SND-CONTEXT-258` | `context_pages: no concept 'self-schedul' (fix: try self-scheduling)` / `… no repo 'wbe'` |

**Range.** This spec books `SND-CONTEXT-250..259` (250-258 used). Not 180-194: the explain spec owns 180-189 (`2026-10-20-sindri-context-explain-design.md:56`) and the code-index spec 190-194 (`2026-10-19-sindri-code-index-design.md:555`); 200-209 is the deployable-context spec's, 230-232 and 240-241 are on main or booked. The range goes into `ContextErrors.cs`'s class summary in P1b-1, and each id into `docs/csharp/sindri-context.md` in the PR that adds it; query-side ones join `ContextErrors.Attention` where an unknown name exits 1 today.

## 6. Phases and parallel plan

| Phase | What | Depends on | The owner can check |
|---|---|---|---|
| P1a | `EdgeType.Expresses`, `EdgeRules`, schema version 3, `PageKinds.IsPage`, the name rules, the suggestion rule after concept naming, the `RulesVersion` bump. First red test: `An_action_whose_code_is_homed_in_billing_suggests_expressing_billing` | none | nothing yet (a rebuild, no new screen) |
| P1b-1 | `index.pages` settings, `PagePin`, `ProfileRepoEntry.Pages` and `ContextRepo.Pages`, `ProfileDiff.IndexPages`, `PageRules.Check`, the read-time rules, the error range, the profile docs. No CLI or UI. First red test: `A_page_action_naming_an_unknown_concept_is_refused_at_write` (a unit test of `PageRules.Check`) | none | `index.pages` in a hand-edited profile shows in the review diff |
| P1b-2 | `IProfileIndexWriter` (the #1667 writer generalized to `index.pages` entries). First red test: `Writing_a_page_keeps_every_other_key_and_comment` | P1b-1, #1667 Q7a merged | a page write shows in Review and approve |
| P1c | `PageOverlay` in `MapAccess`, the `page:` ref, `RepoScope` and `WithConcepts`, export without the overlay, import dropping `page:` nodes. First red test: `A_named_page_relabels_its_surface_and_links_its_action_to_two_concepts` | P1a, P1b-1 | nothing on screen yet |
| P1d-1 | The reads: `page list` (and `--suggested`), the read route, `context_pages`, `context_concepts` counts, doctor's `context:pages` row (`src/Sindri/aot-smoke/expected.txt`). First red test: `Context_pages_lists_an_owner_page_with_its_actions_and_concepts` | P1c | hand-add a page to the profile, approve it, then `page list` and an agent's `context_pages` show it |
| P1d-2 | The writes: `page set` (with `--from-file`, `--dry-run`), `page remove`, the write route, through `IProfileIndexWriter`. First red test: `Page_set_replaces_the_page_and_waits_for_approval` | P1d-1, P1b-2 | `sindri context page set "Schedule week" --surface …`, approve, `page list` |
| P2m | Mockups (ui lane): 2-3 variants of the Pages and actions section, Pages in no concept, the action and page panels, and the page sheet with Add several pages' preview, with real numbers from this repo's map (and the count for the owner's steps: how many pages land under a concept, how many in no concept); the owner picks. No code. **Done:** section A, Name this page A, Accept all B, Pages in no concept A (#1744, §0) | none | done |
| P2a | Server for the app: `ConceptScope`'s second root, `WholeMapBuilder` serves `expresses`, `LensScope` gains surfaces and actions, the `AreaAssigner` rule, the Pages in no concept read, goldens. First red test: `A_concept_whose_only_link_to_an_action_is_expresses_lists_it` | P1c | nothing on screen yet |
| P2b | Ui: the section, Pages in no concept, the panels, the sheet, Accept all suggested (on the preview rows P2c then reuses), Remove page, `Waiting for approval`, the new-suggestions line, built to the picked mockups (§0). First red test: `Concept_why_drill_lists_its_owner_page_first_then_suggested` | P2m, P2a, P1b-2 | the owner's steps (§0): 2 clicks for one page, 2 for every suggested page under a concept |
| P2c | Add several pages. First red test: `Two_blocks_naming_the_same_page_flag_the_second` | P2b | paste a list, see the preview, save once |

**Parallel plan.** P1a, P1b-1 and P2m start at once: two builders plus the ui lane's mockups. P1c starts when P1a and P1b-1 merge; P1b-2 when #1667's Q7a has; P1d-1 and P2a run side by side once P1c has; P1d-2 once P1d-1 and P1b-2 have; P2b when the owner has picked and P2a and P1b-2 have merged; P2c after P2b. P1a, P1b-1 and P1c change `Aw.Context.Core`'s public API (`PublicAPI.Unshipped.txt`; appended lines merge by keeping both). Every phase that adds a `using Aw.` or a namespace folder runs `Aw.Architecture.Tests`; P2a, P2b and P2c run `Aw.Sindri.Ui.Tests`; P1d-1 runs `src/Sindri/aot-smoke/run.sh` for the doctor row. The owner's flow (§0) is proven once P2b merges, with the rendered frames in §4.

**Docs per phase.** P1a: `docs/csharp/sindri-context.md` (the edge, schema version 3, the page kinds, no rollback past it). P1b-1: `docs/csharp/sindri-profile.md` and `docs/sindri/profile.md` (`index.pages`), the error range in `ContextErrors.cs`. P1b-2: `docs/csharp/sindri.md` (the approve row). P1d-1 and P1d-2: `Usage.cs` (the `context page` lines, each with its verbs), `docs/csharp/sindri-context.md` (`context_pages`, the route, ids 250-258), `docs/sindri/context.md` (the marks, what `Suggested` means and why a page may be missing, the CLI with a worked example on real keys), AGENTS.md's command list. P2b: `docs/csharp/sindri-ui.md`.

## 7. Security

Page and action names are owner profile input, fenced as untrusted text wherever they render (the drill, the panels, the sheet, the review diff, `context_pages`); control and format characters are stripped before the length rule. Approval is unchanged (q93; the owner kept it): a session the owner started can approve a page change as it can any profile change, and a page's diff row holds every field, so **Review and approve** shows all of it. The overlay runs after the deny cut, so a deny glob still hides the map's own surface even when the owner named it (the owner's name stays, as an owner node with no file evidence); owner nodes have no file evidence, so deny globs never apply to them. A session sees only its repo's owner pages, read from the `page:` ref (§3.3). The overlay only adds or relabels over the approved profile and never removes a map node; export never carries it, so a page can't travel to another box through a file.

## 8. Open questions for the owner

Owner, 2026-10-29: the spec is approved, so 1 to 3 go as recommended; 4 is answered below.

1. **Pages in the profile, beside pins?** *Recommend yes* (§3.1): one place for your company's words, one approval, one review diff.
2. **Suggested links from where the code lives?** *Recommend yes, marked `Suggested`*: without them a fresh map shows no pages under any concept until you name one. A suggestion becomes yours when you save the page.
3. **Owner actions as flow steps** (`Submit a schedule` is step 2 of "Publish next week")? *Recommend not now*; add when a flow view needs it.
4. **Recognize Avalonia `.axaml` views as pages** (this repo's own UI: 11 `*Page` and 3 `*Dialog` views, 0 pages today)? N2 rules out new extraction here, so *recommend a follow-up issue, not this spec*: a recognizer that makes `screen` and `dialog` surfaces from `.axaml` views, after which this spec's suggestions and drill work on them unchanged. **Owner, 2026-10-29: later, as its own issue** (#1752).
