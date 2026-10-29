# Sindri context: named pages and their actions, linked to concepts (#1701, pages half)

**Status:** draft for the owner's read. Not ready until the owner has read it. Review rounds and their findings are in the PR body.
**Lane:** ideas. **Builds on:** `2026-10-29-sindri-concept-pins-query-design.md` (#1667: pins, `index.concepts`, the pin sheet, the profile writer and the pin approve rule; P1b-2 waits for its Q7a and Q7c), `2026-10-27-sindri-context-map-structure-design.md` (homes, roll-up, §11 records). **Coordinates with:** #1565 (the What band; whether its part B still blocks surfaces and actions in `map/whole` is checked with the context lane first, §6 P0), #1702 / #1716 (source links on the node panel).

## The owner's steps: install -> works

0 lines of YAML, 0 commands. On the app's Context page, after the map has been built once:

1. Open a concept (say Self-scheduling, a pin or a concept the map found). Its Why drill has a **Pages and actions** section: the screens and frontend routes the map found whose code sits in this concept, each marked `Suggested`, and any you've already named, marked `Set by a person`.
2. On a suggested page, press **Name this page**. A sheet opens filled in from the map: a name made from the route or component (`/schedule/week` -> `Schedule week`, `ScheduleWeekPage` -> `Schedule week page`), its actions (the click and submit handlers the map found, `onSubmitSchedule` -> `Submit schedule`), and for each action the concepts it relates to, pre-ticked from where its code lives. Rename anything into your team's words, tick a second concept on an action (`Submit a schedule` relates to Self-scheduling **and** Approval workflow), add an action the map missed, or leave it.
3. Press **Save and approve** (the pin rule: one click when this is the only waiting change, else **Save** and the needs-you bar's **Review and approve**). The page shows on the next read of the map: nothing rebuilds (§3.3).

That is **2 clicks** for a page the map found. **New page** does it for a page the map didn't find. **Add several pages** takes a pasted list: one page per block, its first line the name, then one action per line, optionally `-> Self-scheduling, Approval workflow`; it shows each block as a preview row before anything is written.

From then on, from a concept you find its pages and actions; from an action, its page, every concept it relates to, the decisions and records about it, and the code that implements it when the map knows it. What each screen looks like is the owner's pick from 2-3 mockup variants before any of it is built (phase P2m).

## 0. Why

#1701 asks for two things. The concept list is #1667. This is the other: "This is a page called 'XYZ' and it has these 'actions' like 'submit a schedule' that relates to this other concept." Today:

1. The map has the node kinds: `Surface` (keys `surface:<kind>/<name>`, kinds in `SurfaceKindList.cs:9`: screen, pane, dialog, route, api, command, keybinding, setting, …) and `Action` (`NodeKind.cs:14-19`); an action is `on` a surface (`EdgeRules.cs:16`), and code `implements` either (`:22`). `ActionPass` derives actions from named click and submit handlers, state-changing routes and titled commands at confidence 0.8 (`Extract/ActionPass.cs:9-15`). Route surfaces are keyed `surface:route/{Method}:{Path}` and labelled `{Method} {Path}` (`Recognizers.cs:106,110`).
2. Nothing links an action or a page to a concept: `belongs-to` runs only from features, records and decisions (`EdgeRules.cs:27`).
3. The owner can't name or correct them: an `add` override makes a node with one `part-of` parent and nothing else (`Overrides/OverrideApplier.Working.cs:119-137`), and no app screen makes overrides.
4. They don't reach the Why view: the Why band holds records and decisions only (`Ui/ViewModels/Context/Whole/NodeBands.cs:12-13`); surfaces and actions are in the What band (`:14-19`).
5. So in the issue's screenshot a migration's table shows where a page should: the map has no page to show.

## 1. Goals and non-goals

**Goals**
- G1. The owner names pages and their actions, and links each action to one or more concepts, from the app in at most 3 clicks for a page the map found.
- G2. From a concept: its pages and actions. From an action: its page, its concepts, its Why (decisions that `decides` it or its page, records that `mentions` either) and the code that `implements` it. Both ways, in the Why drill and the node panel.
- G3. Owner-named, suggested and code-backed are three visibly different things, in words.
- G4. Named pages survive every rebuild, publish and import, with stable ids; a page whose route vanished stays, and says so.
- G5. Missing evidence stays explicit: an owner action with no code says `No code found for this yet`, never a guess.

**Non-goals**
- N1. No directory tree: an action relates to any number of concepts, a page to none or many.
- N2. No new extraction: suggestions come from what `ActionPass` and the recognizers already find (`Recognizers.cs:19-26`). Better extraction is #1565's.
- N3. No product nodes made up from tables or migrations: a `thing` stays a `thing`.
- N4. Flows are not authored here (Open question 3).
- N5. Source links are #1702's; this spec shows the ones the node panel already has (#1716).

## 2. What the code does today (main 7ad84c8cf)

- **The read path.** `MapAccess` is the one function from a stored snapshot to what a caller sees (`MapAccess.cs:15-30`): the intent layer is merged, the deny cut is decided and applied, the overrides are cut and applied, then a session's view is cut to its repo (`ScopedTo`, keeping nodes reachable from that repo's code, `RepoScope.cs`). Lenses, status, export and the review queue all read through it.
- **Bands are a UI decision** (`NodeBands.cs:11-21`): Intent ("Why") = record, decision; Product = feature, surface, action, thing, flow, concept; Code = code.
- **The Why drill** is `WholeMapBandDrill.cs` and `NodePanelViewModel.cs`; the server side is `WholeMapBuilder` and `AreaAssigner` (surfaces placed by `part-of`, actions by `on`, `AreaAssigner.cs:84-85`); the per-node intent lists are `LensIntent.Build` over `LensScope.IntoScope` (`LensIntent.cs:12-44`, `LensScope.cs:42`).
- **Owner input in Context.** Pins reach the engine as `ProfileRepoEntry.Concepts` (`ProfileRepoEntry.cs:9`), mapped from the profile in `ContextScopes.cs:54`. Pages need the same path.
- **Enums are strict on disk.** `NodeKind` and `EdgeType` read through `StrictEnumConverter` (`EdgeType.cs:6`), so a stored map holding a new edge type can't be read by an older binary.

## 3. Design

### 3.1 Where named pages live: the profile, beside pins

```yaml
index:
  pages:
    - id: schedule-week
      name: Schedule week
      repo: web                                        # optional when surface is given
      surface: "surface:route/GET:/schedule/week"      # optional: the map page it names
      actions:
        - id: submit-schedule
          name: Submit a schedule
          action: "action:web/SchedulePage.onSubmitSchedule"   # optional: the map action it names
          concepts: [self-scheduling, approval-workflow]
        - name: Request a swap
          concepts: [self-scheduling]
```

- **Why the profile, not overrides.** Pins are the owner's concept list there (#1667), so pages sit next to the concepts they point at: one writer, one approval rule, one review diff. Overrides correct single nodes and carry no `on` or concept edges; widening them would make a second owner-input path. Import replaces the map and is not a list.
- **Settings and the engine type.** `PageSettings` / `PageActionSettings` in `Aw.Sindri.Core/Profile/` (unmapped members refused, like `ConceptSettings`). The engine gets a Context-side `PagePin` (`Aw.Context.Core/Engine/PagePin.cs`: id, name, repo, surface key, actions with id, name, action key, concept ids), carried as `ProfileRepoEntry.Pages` and mapped in `ContextScopes` like `Concepts`, with its `PublicAPI.Unshipped.txt` lines. **Repo:** a page belongs to its `repo:`, else the repo of its surface key, else the profile's only repo; with several repos and neither, the write is refused (`SND-CONTEXT-185`).
- **`ProfileDiff.IndexPages`** shows each change by id.
- **Rules, two times.** At **write** (sheet, CLI): names 1 to 80 chars with the alias character rules; at most 200 pages, 40 actions per page; ids unique; `concepts` name pin ids or derived `concept:<id>` in the current map; `surface` and `action` keys exist in the current map. At **read** of an approved profile, a key or concept the map no longer has is never a refusal: the overlay falls back (§3.3) and `sindri doctor` shows a `context:pages` warning row per stale key, so a rebuild after a route rename never makes the approved profile fail.
- **Ids** are derived from the name when absent and kept on rename (the #1667 rule).
- **The writer** is #1667's `IProfileConceptWriter`, generalized to `IProfileIndexWriter` with an entry kind (concept or page): the same text edit, comments kept.
- **The approve rule** triggers on any diff row of `ProfileDiff.IndexConcepts` or `IndexPages`: while one waits, `ApproveProfile` needs the app or a terminal. P1b-2's first red test is that a session's approve of a pages-only diff is refused.

### 3.2 The new edge: `expresses`

- `EdgeType.Expresses`, action or surface -> concept, many per source (`EdgeRules` gains `[Action, Surface] -> [Concept]`). It says "this page or action is how a person meets this concept". Not `belongs-to`: a feature has one home concept, but an action relates to several, and pins' `HoldsNode` must not put an action inside every concept it touches (#1667 §3.1 keeps homes single).
- **Strict on disk.** A map holding `expresses` can't be read by a binary from before P1a (`StrictEnumConverter`): no rollback past P1a, said in the context doc. Every binary on the box upgrades together through `install-cs.sh`, so this only bites a hand rollback.
- **Owner edges** carry `Provenance.Human`. **Suggested edges** are built: after `namers.Concepts.Run` (`BuildMapService.cs:122`), a page or action whose implementing code's home feature `belongs-to concept:<id>` (a pin or a derived concept, so a fresh map with no pins still gets suggestions) gets `expresses concept:<id>`, `Provenance.Deterministic`, reason `code in Self-scheduling`. Because the build makes them, P1a bumps `MapRuleset.RulesVersion` (`MapRuleset.cs:14-15`): every box rebuilds once after the upgrade (about 4 minutes here, 0 model calls). Owner pages are not in the hash.
- **What counts as a page.** Surfaces of kind `screen`, `pane`, `dialog`, and `route` when the routes recognizer found it in a frontend router file (`.tsx`, `.jsx`, or a router config imported by one); never `api`, `command`, `keybinding`, `setting`, `menu`, `job`, `notification`, `permission`, `report` or `flag`. The filter is one function, `PageKinds.IsPage(node)`, used by the suggestions, the drill and `context_pages`.
- **Names offered.** A route's path segments to words (`GET:/schedule/week` -> `Schedule week`, parameters dropped); a component name split at capitals with `Page`/`Screen`/`View` dropped (`ScheduleWeekPage` -> `Schedule week`); a handler name with `on`/`handle` dropped (`onSubmitSchedule` -> `Submit schedule`); else the raw label.

### 3.3 Applied at read time, so nothing rebuilds

A named page is an overlay applied inside `MapAccess`, **right after the intent layer is merged and before the deny cut** (`MapAccess.cs:18-22`), so the deny globs judge owner nodes like any other and the overrides can still act on them.
- A page with a `surface:` key relabels that node (the key stays, the owner's name shows, `Set by a person`); without one it adds `surface:screen/owner-<id>` with `Provenance.Human` (`screen` is a valid kind; the `owner-` prefix keeps it apart from extracted screens).
- Each action the same way: an `action:` key relabels; otherwise `action:owner/<page-id>/<id>`, `on` its page.
- Each action's `concepts` become `expresses` edges, `Provenance.Human`; for a named node they replace its suggested `expresses` edges.
- A key the current map no longer has falls back to an owner node, and the page row says `This page's route is gone from the map: Edit page to pick its new one.`
- **Repo scope for sessions.** `RepoScope` keeps an owner page and its owner actions in a session's view when the page's repo (§3.1) is the session's repo; a page naming a surface key is kept with that surface as today. So a session sees its repo's owner pages and never another repo's.
- **Publish, export, import.** Publish holds and releases a stored version and never touches the graph (`MapPublishing.cs:12-31`); since the overlay runs on every read, a published version shows the same pages as a draft. Export reads through `MapAccess`, so an exported file carries the owner nodes, marked Human. Importing that file stores them as map nodes; on the next read the overlay finds them by key and relabels them in place, so nothing doubles.
- An approved page edit shows on the next read: no refresh, 0 model calls.

### 3.4 Reaching them from Why (G2)

The Why band stays records and decisions (the issue: keep the taxonomy). What changes is what you can reach from it:
- **A concept's Why drill** gains **Pages and actions**, after its decisions and records: the concept's pages, each with its actions that `expresses` the concept; owner-named first in profile order, then `Suggested` by number of actions; at most 20 pages, then `Show all 34`. A row: `Submit a schedule · on Schedule week · also in Approval workflow · 2 decisions · code: SubmitButton.tsx`.
- **An action's node panel**: its page; `Relates to` (every concept it expresses, each a link); `Why` (decisions that `decides` it or its page, records that `mentions` either: `LensScope.IntoScope` gains surfaces and actions); `Code` (`implements` edges, with #1716's source links), or `No code found for this yet`.
- **A page's node panel**: its actions, the union of their concepts, its Why, its code.
- **Server.** `WholeMapBuilder` serves `expresses` edges; `AreaAssigner` gains one new rule: an owner page with no `part-of` takes the area of its first concept.

**States.**
| Where | Loading | Couldn't read | Empty |
|---|---|---|---|
| Pages and actions | `Loading pages…` | `Couldn't read this concept's pages just now. Try again.` with **Try again** | `No pages or actions found for this concept yet. Name one with New page.` |
| Action panel, Relates to | (with the panel) | (the panel's own error) | `Not linked to a concept yet. Edit page to pick one.` |
| Action panel, Why | (with the panel) | (the panel's own error) | `No decisions or records about this yet.` |

### 3.5 Owner's, suggested, code-backed (G3)

| Mark | Means |
|---|---|
| `Set by a person` | from `index.pages` (Human) |
| `Suggested` | found by the map, not yet named (Deterministic) |
| `Code found` / `No code found for this yet` | whether an `implements` edge reaches it |

Always words, never color alone. A concept card shows `2 new suggested pages` when a rebuild found pages that weren't there before, until the owner opens the section.

### 3.6 The sheet: Name this page, Edit page, New page, Add several pages

- **Fields.** Name (pre-filled per §3.2); actions, one row each (name pre-filled; concepts as ticks, pre-ticked from the suggested `expresses` edges; **Add an action**; **Remove** per row); the page's map surface and each action's map action as read-only `From the map: GET /schedule/week` lines, with **Pick another** (a search over `PageKinds.IsPage` surfaces, or the page's actions) when one is gone.
- **States** as #1667's sheet: `Saving…`, a clash keeps every typed field (`SND-PROFILE-003`), Esc with `Discard your changes?`.
- **Keyboard and screen readers.** Focus starts on Name; Tab runs Name, each action row (name, its concept ticks, Remove), Add an action, the buttons. Each concept tick is a check box named `Relates to Approval workflow`; each row reads `Action 2 of 3, Submit a schedule, relates to 2 concepts`. The section's rows are a list; each row's name is its link, read with its marks. Closing returns focus to the button that opened it. The section's status line is a polite live region (a `TextBlock` whose `Text` changes, `docs/csharp/sindri-ui.md:228-231`).
- **Add several pages.** Blocks separated by blank lines; a block's first line is the page name, each further line an action, optionally `-> c1, c2`; bullets and numbers dropped. **Read the list** shows a preview row per block before anything is written:
  | Case | Row |
  |---|---|
  | matched to a suggested page | `Matches GET /schedule/week (4 actions found)`, changeable, or `New page` |
  | a concept name that matches none | that name in the row with `No concept called "aproval": pick one`, a picker beside it; the block isn't written until it is picked or removed |
  | an action line matching one the map found | `Matches onSubmitSchedule` |
  | a name over 80, over 40 actions, a duplicate page name | the reason in the row, the block unticked |
  Matching: the suggested page whose name words (§3.2) equal the block's name ignoring case and plural, else the one sharing most words, ties to the one with more actions. **Save all and approve** / **Save all**: one write, one approval.
- **Remove page**: `Remove the page Schedule week? Its actions and their concept links go too; the map's own page stays as a suggestion.`
- **CLI.** `sindri context page set <name> [--id ID] [--repo R] [--surface KEY] [--action "Name[=KEY][->c1,c2]"]... [--clear-actions] [--if-hash H] [--json]`, `page set --from-file F`, `page remove <id>`, `page list [--json]`. Exit codes, follow, `--detach` and `--no-start` as `context pin` (#1667 §3.6). An agent may write; only the app or a terminal approves.

### 3.7 MCP

- **`context_pages`** (read): args `concept` (optional, a pin or concept id), `repo` (optional), `limit` (default 50, max 200). Returns `{ pages: [{ id, key, name, repo, mark: "owner"|"suggested", code: bool, actions: [{ id, key, name, mark, code, concepts: [id] }] }], truncated }`, names fenced as untrusted text, a session's repo scope applied. Error ids as §5.
- `context_concepts` rows gain `pages` (count). `context_query` with an action or page anchor works as for any node; `concept=` (#1667) keeps an action when it `expresses` the concept.

## 4. Tests

- **Core.** `PageOverlay`: relabel by key; an owner node `surface:screen/owner-<id>` when no key; fallback when the key is gone; `expresses` edges, Human; owner edges replace suggested ones for a named node; idempotent on a second apply and on an imported map that already holds the owner nodes. `PageKinds.IsPage` over each surface kind. The name rules. The suggestion rule after concept naming, on a derived concept and on a pin. `EdgeRules`: `expresses` from action and surface to concept only. `IndexRules` at write; the read-time fallback for a vanished key (`An_approved_page_whose_route_vanished_still_reads`).
- **Features / integration.** Name a suggested page, approve, read: the page and its actions show with the owner's names and `expresses` edges to two concepts, and no refresh job is queued. Survival: rebuild, publish, export, import of the export, read: the same pages, ids, names and links, nothing doubled. A session of repo `api` sees none of repo `web`'s owner pages; a deny glob over a page's surface file hides that page. `context_pages` and `context_concepts` shapes; goldens. The approve rule over a pages-only diff, through the real caller chain (a CLI with a readable token and no TTY is refused).
- **Ui.** The Pages and actions section (order, cap, states); the action and page panels (Relates to, Why, Code or `No code found for this yet`, their empties); the sheet pre-fill, add an action, tick a second concept, the states, keyboard order and names; Add several pages' preview cases; Remove page. Rendered frames through `aw-ui-driver` of: concept -> page -> action -> second concept -> its Why -> code (the issue's "navigate both ways" proof), on a fixture with a feature, a screen, a button action and a decision about it.

## 5. Texts and error ids

| When | Text |
|---|---|
| section head | `Pages and actions` |
| row | `Submit a schedule · on Schedule week · also in Approval workflow · 2 decisions · code: SubmitButton.tsx` |
| marks | `Set by a person` / `Suggested` / `Code found` / `No code found for this yet` |
| suggestion reason | `code in Self-scheduling` |
| new suggestions | `2 new suggested pages` |
| route gone | `This page's route is gone from the map: Edit page to pick its new one.` |
| section states | `Loading pages…` / `Couldn't read this concept's pages just now. Try again.` / `No pages or actions found for this concept yet. Name one with New page.` |
| panel empties | `Not linked to a concept yet. Edit page to pick one.` / `No decisions or records about this yet.` |
| remove confirm | `Remove the page Schedule week? Its actions and their concept links go too; the map's own page stays as a suggestion.` |
| Add several, unknown concept | `No concept called "aproval": pick one` |
| `SND-CONTEXT-183` | `index.pages[0].actions[1].concepts[0]: 'aproval' is no concept in map v41 (fix: try approval-workflow)` |
| `SND-CONTEXT-184` | `index.pages[0].surface: 'surface:route/GET:/schedule/wk' is not in map v41 (fix: sindri context page list, or pick it in the app)` |
| `SND-CONTEXT-185` | `index.pages[0]: say which repo this page is in (fix: --repo, or a surface key)` |
| `SND-CONTEXT-186` | `index.pages[0]: 41 actions; a page holds at most 40` / `index.pages: 201 pages; at most 200` |
| `SND-CONTEXT-187` | `index.pages[3].id: 'schedule-week' is already index.pages[0]'s` |
| `SND-CONTEXT-188` | `index.pages[0].name: <the character rule it breaks>` |
| `SND-CONTEXT-189` | `--action "Submit->": no concept after '->'` |
| `SND-CONTEXT-192` | `no page is named 'schedul-week' (fix: try schedule-week)` (remove, `context_pages`) |

183-189 and 192 are free in `src` and `docs` on main and after #1667's reservations (102-108, 171-179). Each goes into `docs/csharp/sindri-context.md` in the PR that adds it; query-side ones join `ContextErrors.Attention` where an unknown name exits 1 today.

## 6. Phases and parallel plan

| Phase | What | Depends on |
|---|---|---|
| P0 | Ask the context lane whether #1565 part B still keeps trusted surfaces and actions out of `map/whole` (the arch review found no kind filter in `WholeMapBuilder.cs:76-127`). If it does, its fix lands first; if not, P0 is dropped. | none |
| P1a | `EdgeType.Expresses`, `EdgeRules`, `PageKinds.IsPage`, the name rules, the suggestion rule after concept naming, the `RulesVersion` bump. First red test: `An_action_whose_code_is_homed_in_billing_suggests_expressing_billing` | none |
| P1b-1 | `index.pages` settings, `PagePin` and `ProfileRepoEntry.Pages` through `ContextScopes`, `ProfileDiff.IndexPages`, the write-time and read-time rules, the profile doc. First red test: `A_page_action_naming_an_unknown_concept_is_refused_at_write` | none |
| P1b-2 | `IProfileIndexWriter` (the #1667 writer generalized), the approve rule over `IndexPages`. First red test: `A_session_approving_a_pages_only_diff_is_refused` | P1b-1, #1667 Q7a and Q7c merged |
| P1c | `PageOverlay` in `MapAccess`, `RepoScope` for owner pages, the import idempotence. First red test: `A_named_page_relabels_its_surface_and_links_its_action_to_two_concepts` | P1a, P1b-1 |
| P2m | Mockups (ui lane): 2-3 variants of the Pages and actions section, the action and page panels, and the page sheet with Add several pages' preview, with real numbers from this repo's map; the owner picks. No code. | none (starts now) |
| P2a | Server: `WholeMapBuilder` serves `expresses`, `LensScope` gains surfaces and actions, the `AreaAssigner` rule, `context_pages`, `context_concepts` counts, the CLI verbs and route, goldens, doctor's `context:pages` row. First red test: `Context_pages_lists_an_owner_page_with_its_actions_and_concepts` | P1c (and P0 if it stays) |
| P2b | Ui: the section, the panels, the sheet, Add several pages, Remove page, the new-suggestions line, built to the P2m pick. First red test: `Concept_why_drill_lists_its_owner_page_first_then_suggested` | P2m, P2a, P1b-2 |

**Parallel plan.** P0 (a question to the context lane), P1a, P1b-1 and P2m start at once: two builders plus the ui lane's mockups. P1c starts when P1a and P1b-1 merge; P1b-2 when #1667's Q7a and Q7c have; P2a when P1c has; P2b when the owner has picked and P2a and P1b-2 have merged. P1a, P1b-1 and P1c change `Aw.Context.Core`'s public API (`PublicAPI.Unshipped.txt`; appended lines merge by keeping both). Every phase that adds a `using Aw.` or a namespace folder runs `Aw.Architecture.Tests`; P2a and P2b run `Aw.Sindri.Ui.Tests`. The owner's flow (§0) is proven once P2b merges, with the rendered frames in §4.

**Docs per phase.** P1a: `docs/csharp/sindri-context.md` (the edge, the page kinds, no rollback past it). P1b-1: `docs/csharp/sindri-profile.md` (`index.pages`). P1b-2: `docs/csharp/sindri.md` (the approve row). P2a: `Usage.cs` (the `context page` lines), `docs/csharp/sindri-context.md` (`context_pages`, the route, ids 183-192), AGENTS.md's command list. P2b: `docs/csharp/sindri-ui.md`.

## 7. Security

Page and action names are owner profile input, fenced as untrusted text wherever they render (the drill, the panels, the sheet, the review diff, `context_pages`). The approve rule covers any `index.pages` change (app or terminal while one waits), so a lane can draft a page list but not approve it; it is tested with a pages-only diff. The overlay runs before the deny cut, so deny globs hide owner nodes evidenced only by a denied file, and a session sees only its repo's owner pages (§3.3). It only adds Human nodes and edges over the approved profile and never removes a map node.

## 8. Open questions for the owner

1. **Pages in the profile, beside pins?** *Recommend yes* (§3.1): one place for your company's words, one approval, one review diff.
2. **Suggested links from where the code lives?** *Recommend yes, marked `Suggested`*: without them a fresh map shows no pages under any concept until you name one. A suggestion becomes yours when you save the page.
3. **Owner actions as flow steps** (`Submit a schedule` is step 2 of "Publish next week")? *Recommend not now*; add when a flow view needs it.
4. **Pages in no concept?** *Recommend allowed*: a settings page may relate to nothing; it shows under `Pages in no concept` on the layer's head.
