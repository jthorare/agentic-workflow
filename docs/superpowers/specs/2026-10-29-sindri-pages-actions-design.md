# Sindri context: named pages and their actions, linked to concepts (#1701, pages half)

**Status:** draft for the owner's read. Not ready until the owner has read it. Review rounds and their findings are in the PR body.
**Lane:** ideas. **Builds on:** `2026-10-29-sindri-concept-pins-query-design.md` (#1667: pins, `index.concepts`, the pin sheet, the profile writer and the pin approve rule), `2026-10-27-sindri-context-map-structure-design.md` (homes, roll-up, §11 records). **Coordinates with:** #1565 (the What band: derived actions and flows; its part B, trusted things and actions never served by `map/whole`, is a prerequisite here, §6 P0), #1702 / #1716 (source links on the node panel).

## The owner's steps: install -> works

0 lines of YAML, 0 commands. On the app's Context page, after the map has been built once:

1. Open a concept (say Self-scheduling). Its Why drill now has a **Pages and actions** section: pages and actions the map found whose code sits in this concept, each marked `Suggested`, and any you've already named, marked `Set by a person`.
2. On a suggested page, press **Name this page**. A sheet opens filled in from the map: the page's name (from its route or component, `Schedule week`), its actions (the handlers the map found, `Submit a schedule`, `Request a swap`), and for each action the concepts it relates to (pre-ticked from where its code lives). Rename anything into the words your team uses, tick a second concept on an action (`Submit a schedule` relates to Self-scheduling **and** Approval workflow), add an action the map missed, or leave it.
3. Press **Save and approve** (the same rule as pins: approve in one click when this is the only waiting change). The page shows at once: nothing rebuilds, since a named page only adds owner nodes and edges over the built map (§3.3).

That is **2 clicks** for a page the map found. **New page** does it for a page the map didn't find (typing, so more clicks by nature). **Add several pages** takes a pasted list the way Add several pins does: one page per block, its first line the name, then one action per line, `Submit a schedule -> Self-scheduling, Approval workflow` or just `Submit a schedule` (concepts picked in the row).

From then on, from a concept you find its pages and actions; from an action you find its page, every concept it relates to, the decisions and records about it (its Why), and the code that implements it when the map knows it. What each looks like is the owner's pick from 2-3 mockup variants before any of it is built (phase P2m).

## 0. Why

#1701 asks for two things. The concept list is #1667. This is the other: "This is a page called 'XYZ' and it has these 'actions' like 'submit a schedule' that relates to this other concept." Today:

1. The map has the node kinds: `Surface` (a screen is `surface:<kind>/<name>`) and `Action` (`NodeKind.cs:7-45`), an action is `on` a surface (`EdgeRules.cs:16`), and code `implements` either (`:22`). `ActionPass` derives actions from named click and submit handlers, state-changing routes and titled commands at confidence 0.8 (`Extract/ActionPass.cs:8-13`, #1565 part A).
2. Nothing links an action or a page to a concept. `belongs-to` runs only from features, records and decisions (`EdgeRules.cs:27`), so "Submit a schedule relates to Self-scheduling and Approval workflow" can't be said.
3. The owner can't name or correct them. An `add` override makes a node with one `part-of` parent and nothing else (`Overrides/OverrideApplier.Working.cs:119-137`), and no app screen makes overrides.
4. They don't reach the Why view. The Why band holds records and decisions only (`Ui/ViewModels/Context/Whole/NodeBands.cs:12-13`); surfaces and actions are in the What band (`:14-19`), and #1565 part B (trusted actions stored but not served by `map/whole`) is still open.
5. A migration file shows up where a page should (the issue's `staffing_goal` screenshot): the map has no page to show, so the nearest evidence is a table.

## 1. Goals and non-goals

**Goals**
- G1. The owner names pages and their actions, and links each action to one or more concepts, from the app in at most 3 clicks for a page the map found (§0).
- G2. From a concept: its pages and actions. From an action: its page, its concepts, its Why (decisions that `decides` it, records that `mentions` it) and the code that `implements` it. Both ways, in the Why drill and the node panel.
- G3. Owner-named, suggested (derived by the map) and code-backed are three visibly different things, in words.
- G4. Named pages survive every rebuild, publish and import, with stable ids.
- G5. Missing evidence stays explicit: an owner action with no code says `No code found for this yet`, never a guess.

**Non-goals**
- N1. No directory tree: an action can relate to any number of concepts, and a page to none.
- N2. No new extraction. Suggestions come from what `ActionPass`, `FlowPass` and the recognizers already find (`Recognizers.cs:19-26`). Better extraction is #1565's.
- N3. No product nodes made up from tables or migrations. A `thing` stays a `thing`.
- N4. Flows (`FlowPass`) are not authored here; an owner action may later be a step of a flow (Open question 3).
- N5. Source links are #1702's; this spec only shows the ones the node panel already has (#1716).

## 2. What the code does today (main 7ad84c8cf)

- **Node and edge kinds** as in §0 items 1-2. `step-of` runs surface or action to flow (`EdgeRules.cs:21`); `decides` runs decision to any kind (`:24`); `mentions` record to any.
- **Bands are a UI decision** (`NodeBands.cs:11-21`): Intent ("Why") = record, decision; Product = feature, surface, action, thing, flow, concept; Code = code.
- **The Why drill** is `WholeMapBandDrill.cs` (own or cross links) and `NodePanelViewModel.cs`; the server side is `WholeMapBuilder` with `AreaAssigner.AssignIntent` (`WholeMap/AreaAssigner.cs:186`), and the per-node lens intent band is `Lenses/IntentBand.cs` (records that `mentions` scope nodes, decisions that `decides` them, at most 100 each).
- **Owner input that survives rebuilds**: pins in the profile (`index.concepts`, #1667) and overrides (`map_override`, re-applied on every read, `BuildMapService.cs:20`). Import replaces the version (`ImportMapService.cs:12-50`).

## 3. Design

### 3.1 Where named pages live: the profile, beside pins

```yaml
index:
  pages:
    - id: schedule-week
      name: Schedule week
      surface: "surface:route/schedule/week"     # optional: the map page it names
      actions:
        - id: submit-schedule
          name: Submit a schedule
          action: "action:schedule/submit"       # optional: the map action it names
          concepts: [self-scheduling, approval-workflow]
        - name: Request a swap
          concepts: [self-scheduling]
```

- **Why the profile, not overrides.** Pins are the owner's concept list there (#1667), so pages sit next to the concepts they point at; one writer, one approval rule, one review diff. Overrides correct single nodes and carry no `on` or concept edges (§0 item 3); widening them would add a second owner-input path for the same thing. Import replaces the map and is not a list.
- **Shape.** `PageSettings` and `PageActionSettings` in `Aw.Sindri.Core/Profile/` (unmapped members refused, like `ConceptSettings`); `ProfileDiff.IndexPages` shows each change by id; `IndexRules` checks: names 1 to 80 chars with the alias character rules, at most 200 pages and 40 actions per page, `concepts` name pin ids or `concept:<id>` of derived concepts present in the current map, `surface` and `action` keys exist in the current map when given, ids unique. Ids are derived from the name when absent and kept on rename (the #1667 rule).
- **The writer.** #1667's `IProfileConceptWriter` becomes `IProfileIndexWriter` with an entry kind (concept or page), the same text edit, comments kept. **The approve rule** is #1667's: while the diff touches `index.concepts` or `index.pages`, `ApproveProfile` needs the app or a terminal.

### 3.2 The new edge: `expresses`

- `EdgeType.Expresses`, action or surface -> concept, many per source (`EdgeRules` gains `[Action, Surface] -> [Concept]`). It says "this page or action is how a person meets this concept". Not `belongs-to`: a feature has one home concept, but an action relates to several, and pins' `HoldsNode` must not treat an action as inside every concept it touches (#1667 §3.1 keeps homes single).
- **Owner edges** carry `Provenance.Human`. **Suggested edges** are derived at build: an action or surface whose implementing code's home feature `belongs-to concept:<id>` gets `expresses concept:<id>` with `Provenance.Deterministic` and the reason `code in Self-scheduling`. A suggestion never overrides an owner edge: when an owner page names a surface or action, its owner edges replace the suggested ones for that node.

### 3.3 Applied at read time, so nothing rebuilds

A named page is an overlay, like the intent overlay (`IntentOverlay.Apply`, `MapAccess.cs:77`): `PageOverlay.Apply(graph, pages)` runs on every map read, after overrides.
- A page with `surface:` relabels that node (the map's key stays, the owner's name shows) and marks it `Set by a person`; without one it adds `surface:owner/<id>` with `Provenance.Human`.
- Each action the same way: `action:` relabels; otherwise `action:owner/<page-id>/<id>`, `on` its page.
- Each action's `concepts` become `expresses` edges, `Provenance.Human`.
- A key that the current map no longer has (a route renamed in the code) falls back to an owner node, and the page card says `This page's route is gone from the map: Edit page to pick its new one.`
- Because it is read-time, an approved page edit shows on the next read: no `MapRuleset` change, no refresh, 0 model calls. Pages are not in the map hash. A published or exported map carries the overlay as of publish (`MapPublishing` applies it before writing), so a reader of the published file sees the same pages.

### 3.4 Reaching them from Why (G2)

The Why band stays records and decisions (the issue: keep the taxonomy). What changes is what you can reach from it:
- **A concept's Why drill** gains **Pages and actions**: the concept's pages, each with its actions that `expresses` the concept, owner-named first, then `Suggested`. Each row: `Submit a schedule · on Schedule week · also in Approval workflow · 2 decisions · code: SubmitButton.tsx`.
- **An action's node panel**: its page; `Relates to` (every concept it expresses, each a link); `Why` (decisions that `decides` it or its page, records that `mentions` either, through the existing `IntentBand`, which gains surfaces and actions in its scope); `Code` (`implements` edges, with #1716's source links), or `No code found for this yet` (G5).
- **A page's node panel**: its actions, the union of their concepts, its Why, its code.
- **The server side**: `WholeMapBuilder` serves surfaces and actions with their `expresses` edges (needs #1565 part B), and `AreaAssigner` places an owner page in the area of its first concept.

### 3.5 Owner's, suggested, code-backed (G3)

Each page, action and link shows one of three words, never color alone:
| Mark | Means |
|---|---|
| `Set by a person` | from `index.pages` (Human) |
| `Suggested` | derived by the map, not yet named (Deterministic, from `ActionPass` or the code-home rule) |
| `Code found` / `No code found for this yet` | whether an `implements` edge reaches it |

### 3.6 The sheet: Name this page, Edit page, New page, Add several pages

- **Fields.** Name (pre-filled from the surface's label); actions, one row each (name pre-filled from the action's label; its concepts as ticks, pre-ticked from the suggested `expresses` edges; **Add an action**; a row can be removed); the page's map surface and each action's map action as read-only `From the map: route /schedule/week` lines, with **Pick another** when the route is gone.
- **States** as #1667's sheet: counts load after, `Saving…`, a clash keeps every typed field (`SND-PROFILE-003`), Esc with `Discard your changes?`, focus starts on Name and returns to the button.
- **Add several pages**: blocks separated by blank lines; a block's first line is the page name, each further line an action; `->` then comma-separated concept names, optional (picked in the row otherwise); bullets and numbers dropped; each block matched to the suggested page whose name or route words fit best, ties to the one with more actions. One write, one approval.
- **Remove page**: confirm `Remove the page Schedule week? Its actions and their concept links go too; the map's own page stays as a suggestion.`
- **CLI.** `sindri context page set <name> [--id ID] [--surface KEY] [--action "Name[=action:KEY][->c1,c2]"]... [--clear-actions] [--if-hash H] [--json]`, `page set --from-file`, `page remove <id>`, `page list [--json]`. Same exit codes, follow, `--detach` and `--no-start` as `context pin` (#1667 §3.6). An agent may write; only the owner approves.

### 3.7 MCP and queries

- `context_concepts` rows gain `pages` (count) and the concept's page ids. A new `context_pages` tool lists pages and actions with their concepts, marks and code (`concept=` filters it). `context_query` with an action or page anchor gets the graph part from it as for any node; `concept=` (#1667) keeps an action when it `expresses` the concept.

## 4. Tests

- **Core.** `PageOverlay`: relabel by key, owner node when no key, fallback when the key is gone, `expresses` edges with Human provenance, owner edges replace suggested ones for a named node, idempotent on a second apply. The code-home suggestion rule: an action whose implementing file's home feature belongs to billing suggests `expresses concept:billing`, Deterministic. `EdgeRules`: `expresses` from action and surface to concept only. `IndexRules` for pages.
- **Features / integration.** Name a suggested page, approve, read: the page and its actions show with the owner's names, `expresses` edges to two concepts, no refresh job queued. Survival: rebuild, publish, import of the published file: the pages, ids, names and links are the same. A route renamed in the code: the owner page falls back and says so. `context_pages` and `context_concepts` shapes; goldens. The approve rule over `index.pages` through the real caller chain.
- **Ui.** The Why drill's Pages and actions section (owner first, Suggested after, the row line); the action panel (Relates to, Why, Code or `No code found for this yet`); the sheet pre-fill, add an action, tick a second concept, states; Add several pages with three blocks; Remove page. Rendered frames through `aw-ui-driver` of: concept -> page -> action -> second concept -> its Why -> code (the issue's "navigate both ways" proof), on a fixture with a feature, a screen, a button action and a decision about it.

## 5. Texts

| When | Text |
|---|---|
| section head | `Pages and actions` |
| row | `Submit a schedule · on Schedule week · also in Approval workflow · 2 decisions · code: SubmitButton.tsx` |
| marks | `Set by a person` / `Suggested` / `Code found` / `No code found for this yet` |
| suggestion reason | `code in Self-scheduling` |
| route gone | `This page's route is gone from the map: Edit page to pick its new one.` |
| empty concept | `No pages or actions found for this concept yet. Name one with New page.` |
| remove confirm | `Remove the page Schedule week? Its actions and their concept links go too; the map's own page stays as a suggestion.` |
| refused concept | `SND-CONTEXT-1xx: index.pages[0].actions[1].concepts[0]: 'aproval' is no concept in map v41 (fix: try approval-workflow)` |
| refused key | `SND-CONTEXT-1xx: index.pages[0].surface: 'surface:route/schedule/wk' is not in map v41` |

Error ids are reserved when this is approved, from the free numbers after #1667's.

## 6. Phases and parallel plan

| Phase | What | Depends on |
|---|---|---|
| P0 | #1565 part B: trusted surfaces and actions served by `map/whole` (the context lane's fix, tracked there) | none |
| P1a | `EdgeType.Expresses`, `EdgeRules`, the code-home suggestion rule in the build. First red test: `An_action_whose_code_is_homed_in_billing_suggests_expressing_billing` | none |
| P1b | `index.pages` in the profile: settings, `ProfileDiff.IndexPages`, `IndexRules`, the writer generalized to `IProfileIndexWriter`, the approve rule over pages, the profile doc. First red test: `A_page_action_naming_an_unknown_concept_is_refused` | #1667 Q7a, Q7c |
| P1c | `PageOverlay` on every map read and before publish. First red test: `A_named_page_relabels_its_surface_and_links_its_action_to_two_concepts` | P1a, P1b |
| P2m | Mockups (ui lane): 2-3 variants of the Why drill's Pages and actions section, the action and page panels, and the page sheet with Add several pages, with real numbers from this repo's map; the owner picks. No code. | none (starts now) |
| P2a | Server: `WholeMapBuilder` serves `expresses`, `IntentBand` scope gains surfaces and actions, `AreaAssigner` places owner pages, `context_pages`, `context_concepts` page counts, the CLI verbs, the route, goldens. First red test: `Context_pages_lists_an_owner_page_with_its_actions_and_concepts` | P0, P1c |
| P2b | Ui: the section, the panels, the sheet, Add several pages, Remove page, built to the P2m pick. First red test: `Concept_why_drill_lists_its_owner_page_first_then_suggested` | P2m, P2a |

**Parallel plan.** P0 (context lane), P1a, P1b and P2m run at once: three builders plus the ui lane's mockups. P1c starts when P1a and P1b merge; P2a when P0 and P1c have; P2b when the owner has picked and P2a merged. P1a and P1c touch `Aw.Context.Core`'s public API (`PublicAPI.Unshipped.txt`, appended lines merge by keeping both). P1b and #1667's Q7a/Q7c touch the same writer and approve check: P1b rebases after them. Every phase that adds a `using Aw.` or a namespace folder runs `Aw.Architecture.Tests`; P2a and P2b run `Aw.Sindri.Ui.Tests`. The owner's flow (§0) is proven once P2b merges, with the rendered frames in §4.

## 7. Security

Page names and action names are owner profile input, fenced as untrusted text wherever they render (the drill, the panels, the sheet, the review diff, `context_pages`). The approve rule is #1667's (app or terminal while page changes wait), so a lane can draft a page list but not approve it. The overlay only adds Human nodes and edges over the approved profile; it never removes a map node, and the audience and repo masks apply to owner nodes as to any other (an owner page with a surface key in a repo the caller can't see is not shown).

## 8. Open questions for the owner

1. **Pages in the profile, beside pins?** *Recommend yes* (§3.1): one place for your company's words, one approval, one review diff.
2. **Suggested `expresses` links from where the code lives?** *Recommend yes, marked `Suggested`*: without them a fresh map shows no pages under any concept until you name one. You turn a suggestion into yours by ticking it on the sheet.
3. **Owner actions as flow steps** (`Submit a schedule` is step 2 of "Publish next week")? *Recommend not now*; add when a flow view needs it.
4. **Pages without a concept?** *Recommend allowed*: a settings page may relate to nothing; it shows under `Pages in no concept` on the layer's head.
