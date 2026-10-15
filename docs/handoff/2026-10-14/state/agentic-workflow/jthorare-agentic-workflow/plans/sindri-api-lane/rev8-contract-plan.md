# API P11 rev-8 contract unit (E-20, E-22, E-23): plan

Sources (origin/main 4a2389bd):
- **Screens spec** `docs/superpowers/specs/2026-10-13-sindri-ui-screens-design.md`: §4.1, §4.2, §5.1–§5.5, §6 (E-20…E-23), §8.1, §9 "The contract unit", and `…/fixture.md` "Root workflows".
  - The rev-8 E-numbers are in this file. They are not in `2026-10-11-sindri-ui-design.md`, which is rev 7.
- **API spec** `2026-10-10-sindri-api-surface-design.md`: §3.5 ownership, §3.6, §3.8 (the shapes are already written in: `InstanceList`, `InstanceSummary`, `StateCell`, `StatusFields`, `FactRow`, `ChildGroup`, `RecipeView`), and the Phases row P11.
- **WF spec**: §3.10 `StatePathOrder`, §6.3, §6.4 status layouts, and the Phases rows P2, P4, P5 and P7.
- Lane rules: the UI spec wins where texts differ (README X52, X62). Owner fields stay null (X61).

## 0. Goal

Land the wire shapes the UI's wave 1 (U3' and U8) builds against before the engine can serve them. That means:
- the C# records for E-20, E-22 and E-23;
- golden JSON for every scenario U3' and U8 render, recorded from those records filled from the screens fixture;
- stub slices and red tests that name the behaviour P11 must produce.

Every UI lane then codes against a fake `ISindriApi` that serves these files (screens §9). Later P11 units make the same files come out of the real routes, unchanged. If a file has to change, that is a reviewed contract change and the UI lane is told.

## 1. What can ship now and what waits

| Ships now (contract PR, branched off main) | Waits |
|---|---|
| Records and enums for E-20, E-22 and E-23, with JSON round-trip tests | The composer going green: needs `StatePathOrder` (engine **P2b**) and the instance Query and view (engine **P4**, #192) |
| Fixture builders in `Aw.Sindri.Testing` (screens fixture plus the two dev recipes' instances) | `ListInstances` filters (`root`, `recipe`, `subject`) and urgency ordering: need search attributes (engine P4) and `urgency` (engine P2b) |
| The golden files (§3), written by serializing the fixture through the host's own JSON options | Serving the files over HTTP: the routes are the engine's (`GetInstance` and `ListInstances` are mapped in #192), so this waits for #192 to merge |
| Stub `WorkflowViewComposer` (Core, pure) and stub `GetInstanceFact` slice (API-owned), both throwing `NotSupportedException` | `status.text` parity with `sindri workflow status`: needs the §6.4 renderer (engine P4e) |
| Red tests naming the P11 behaviours, skipped with a reason that names the unit that un-skips them (Q3) | Children with workstreams, agent and round: engine **P7** (children) and **P8** (agent runtime). P4 has only `ChildView(Address, State)` |
| Catalog: no rows. Queries have no catalog rows (EngineRows' header, API §3.3) | The item-state fold, needs-you `park` rows and `CloseItem` refusing: need API **P4** (the other half of P11) |

Base: main. The records are new files in a new folder, so the contract doesn't wait on #190, P3 or P4. The one shared hotspot is the JSON context (`SindriJsonContext` on main). Its additions sit in one block at the end of the attribute list, so the rebases with #190 and #192 stay mechanical.

## 2. Contents

**Folder.** `src/Sindri/Aw.Sindri.Core/Workflow/Views/` (namespace `Aw.Sindri.Core.Workflow.Views`).
- These are API-owned shapes over engine data (§3.5).
- The folder sits beside the engine's model and away from #190's `Core/Views/`.

**Enums** (kebab-case JSON):
- `StateGlyph`: `done`, `active`, `pending`.
- `EngineOwnedKind`: `parked`, `escalated`, `taken-over`.
- `FactType`: `text`, `file`, `item`, `link`, `scope-map`, `packet`, `evidence`, `decision`, `payload`. An unknown type maps to `payload`.
- `FactDirection`: `input`, `output`.
- `FactStatus`: `produced`, `in-progress`, `pending`.
- `PathOrdering`: `path`, `transition-list`.
- `AnswerKind`: `text`, `resume`, `grant`, `grant-tokens` (JSON `grantTokens`, as on `NeedsYouRow`), `accept`, `cancel-only`.
- `TimeoutKind`: `parks`, `escalates`.
- `RecipeUrgency`: `incident`, `triage`, `work`. This is the engine's P2b field; Q6 says which copy wins.

**Records.** Field names are exactly API §3.8. `?` means always present and null when empty; lists are never null.
- `InstanceList(instances, empty, asOf)`
- `InstanceSummary(address, itemId?, recipe: RecipeRef, state, since, engineOwned?, subject, inputs[], states[], now, actions[])`
  - `RecipeRef(id, hash, version, urgency)`
  - `EngineOwnedBadge(kind, reason)`
  - `SubjectRef(kind, key, label, href?)`
  - `InputPresence(key, label, kind, required, present)`
- `StateCell(id, order, branchOf?, glyph, badge?, round?, loops[], desk, child, children?, outcome?, terminal)`
  - `Round(n, max)`
  - `LoopPair(with, n, max)`
  - `Progress(done, total)`
- `WorkflowInstanceView`, the spec's `InstanceView`; the name is Q1.
  - Fields: `address, itemId?, title, recipe, state, since, engineOwned?, subject, orderedBy, states[], current, facts[], children[], counts, actions[], asOf`.
  - `CurrentState(state, step, gates[], desk?, status)`
  - `StepDeclaration(kind, ref, declaration)`
  - `GateLine(id, on, threshold?, description)`
  - `DeskForm(outputs[{key, type, required}], checks[], completeAction)`
  - `ViewCounts(timeline, decisions, requests, context)`. `context` is null until CTX serves lenses.
- `StatusFields(header, next, answer?, child?, drift?, inputs[], timeout?, budget?, paused?, commands[], text)` (E-23)
  - `StatusHeader(address, recipe, hash12, version, state, detail?, since, ageSeconds)`
  - `StatusAnswer(kind, actionId, class, cli, atTerminal)`
  - `StatusChild(address, state, reason?)`
  - `StatusDrift(block, needsReapproval)`
  - `StatusInput(key, name, hash?)`
  - `StatusTimeout(kind, inSeconds, on)`
  - `StatusBudget(scope, used, limit, instance{used, limit})`
  - `StatusPaused(timeoutLeftSeconds)`
  - `StatusCommand(verb, actionId, class, cli)`
- `FactRow(key, label, type, direction, from?, status, summary, at?, by?, ref?, progress?, pending?)`
  - `ref` is one `FactRef` record holding nullable per-type members: `name, size, hash, path` (file); `itemId`; `url, host, title`; `factKey`. Keeping one record avoids polymorphic JSON under AOT.
  - `FactPending(required, checks[], completeAction)`
  - `by` is an open actor string (`human:cli`, a Step id, an agent target), per X61(a). The app renders `human:*` as "you".
- `ChildGroup(state, recipe, done, total, workstreams[], rows[], more)`
  - `Workstream(id, label, rows[])`
  - `ChildRow(address, itemId?, title, glyph, state, round?, agent?, ageSeconds, engineOwned?, mirroredPark?, actions[])`
- `RecipeView` additions for E-22:
  - `RecipeInputView(key, label?, type, kind?, required)`
  - `RecipeOutputView(key, label?, type, from)`
  - `urgency`
  - `states[]` as `{id, order, branchOf?}` and `loops[]` as `{name?, from, to}` (WF §3.10).
  - These records only. Wiring them into `ShowRecipeResponse` waits on P2b (Q5).
- No `owner` field anywhere (X61). Times are ISO 8601 UTC; durations are whole seconds.

**Stubs.**
- `WorkflowViewComposer` (Core, pure). Its methods: `Summary(...)`, `View(...)`, `Status(...)`, `Facts(...)`, `Children(...)`. The inputs are the engine records named in §5. Every method throws `NotSupportedException`.
- `Features/GetInstanceFact/` holds `GetInstanceFactRequest(instance, key)`, `FactView(key, type, body, truncated)` and a stub service. Not mapped and not registered until its unit (U5).
- No stubs for `GetInstance`, `ListInstances` or `ShowRecipe`: those slices are the engine's (§3.5), and #192 already has the first two.

**Testing.**
- `WorkflowViewFixtures` in `Aw.Sindri.Testing` holds:
  - the screens fixture's root workflows (INC-14, the triage rows, PRJ-sync, PRJ-exports, PRJ-tabs, the two intake rows);
  - one instance each for `recipes/dev/triage-three-state.yaml` and `desk-round-trip.yaml`, the engine lane's item 4 (Q8).
- Each fixture is built with builders, never handwritten JSON.

**Golden test.** `tests/Aw.Sindri.Tests/ApiGoldens/WorkflowViewGoldenTests.cs`, in the integration lane.
- It resolves the host's `JsonOptions` from `GoldenHost`, serializes each fixture, and checks it through `ApiGolden` at `Golden/Api/<slug>/<scenario>.json`. `AW_GOLDEN_UPDATE=1` re-records.
- In U7 the body source changes from serialization to an HTTP GET on the real route, and the file paths stay the same.

**Docs.** `docs/csharp/sindri.md` lists the routes as `pending:API-P11`, adds a "golden contract" paragraph, and names the files.

## 3. Golden file list (`tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json`)

| # | slug/scenario | Edit | Shape | What it pins |
|---|---|---|---|---|
| 1 | `workflow-instances/root` | E-20 | `InstanceList` | `?root=true`, the fixture's 12 roots: urgency order (incident, triage, work), then oldest `since`; badges, `r2/3`, `desk`, `4/9`, `inputs[].present` |
| 2 | `workflow-instances/recipe-filter` | E-20 | `InstanceList` | `?root=true&recipe=triage.alert` |
| 3 | `workflow-instances/empty` | E-20 | `InstanceList` | `instances: []` and the `EmptyText` sentence |
| 4 | `workflow-instance/project-build` | E-20, E-23 | `WorkflowInstanceView` | PRJ-sync: children state 4/9, workstream groups, `scope-map`, `packet` and `evidence` facts (progress 4/9), status with budget and timeout |
| 5 | `workflow-instance/incident-loop` | E-20, E-23 | `WorkflowInstanceView` | INC-14: `loops` mitigate⇄verify, `round` 1/3, `outcome: sev2`, comms child, status `timeout: escalates` |
| 6 | `workflow-instance/triage-desk` | E-20, E-23 | `WorkflowInstanceView` | sentry #8812: `current.desk` form, `FactRow.pending`, `answer` of class `desk` |
| 7 | `workflow-instance/escalated-gate` | E-20, E-23 | `WorkflowInstanceView` | pagerduty P-2211: badge `escalated`, `answer.kind: accept`, `class: protected`, `atTerminal: true` |
| 8 | `workflow-instance/desk-round-trip` | E-20, E-23 | `WorkflowInstanceView` | The dev recipe, desk open (the P11 signal fixture) |
| 9 | `workflow-instance/triage-three-state` | E-20 | `WorkflowInstanceView` | The dev recipe: `order` follows the transition order |
| 10 | `workflow-instance/transition-list` | E-20 | `WorkflowInstanceView` | `orderedBy: transition-list`, a `branchOf` state |
| 11 | `workflow-instance/done` | E-20, E-23 | `WorkflowInstanceView` | sentry #8809 terminal: header-only status, `commands: []` |
| 12 | `workflow-instance/not-found` | E-20 | ProblemDetails | `SND-ITEM-404` (an address in another repo, or unknown) |
| 13 | `workflow-status/parked-child` | E-23 | `StatusFields` | WF §6.4 layout 1: `child`, `answer` forwarded `--grant 1` |
| 14 | `workflow-status/desk-task` | E-23 | `StatusFields` | Layout 2: `inputs`, `timeout: parks`, `budget` state and instance |
| 15 | `workflow-status/parked-budget` | E-23 | `StatusFields` | Layout 3: `answer.kind: grantTokens` |
| 16 | `workflow-status/escalated-gate` | E-23 | `StatusFields` | Layout 4: `accept` |
| 17 | `workflow-status/taken-over` | E-23 | `StatusFields` | Layout 5: `paused`, `commands` handback and cancel |
| 18 | `workflow-status/done` | E-23 | `StatusFields` | Layout 6: header only |
| 19 | `workflow-status/parked-drift` | E-23 | `StatusFields` | The `drift` line, `needsReapproval: true` |
| 20 | `workflow-status/needs-answer-wait` | E-23 | `StatusFields` | `answer.kind: resume` with text |
| 21 | `workflow-recipe/workflow-project` | E-22 | `RecipeView` additions | `inputs[].kind: linear-project`, `outputs[].from`, `urgency: work`, `states[].order` |
| 22 | `workflow-recipe/incident-response` | E-22 | `RecipeView` additions | `urgency: incident`, `loops[]` |
| 23 | `workflow-recipe/no-outputs` | E-22 | `RecipeView` additions | `outputs: []`, `kind` on the first input only |
| 24 | `workflow-instance-fact/scope-map` | E-20 (U8-API-8) | `FactView` | `body` is the scope-map table: surfaces, graph and items columns |
| 25 | `workflow-instance-fact/packet` | E-20 (U8-API-8) | `FactView` | `body` is tasks by workstream with `PKT-*` results, one failed check |
| 26 | `workflow-instance-fact/payload` | E-20 (U8-API-8) | `FactView` | `body` is a scrubbed JSON tree (the P-2211 alert) |
| 27 | `workflow-instance-fact/text` | E-20 (U8-API-8) | `FactView` | `body` is a string |
| 28 | `workflow-instance-fact/truncated` | E-20 (U8-API-8) | `FactView` | Body capped at 256 KiB, `truncated: true` |
| 29 | `workflow-instance-fact/not-found` | E-20 (U8-API-8) | ProblemDetails | An undeclared key |
| 30 | `workflow-instances/open` | U8-API-2 | `InstanceList` | `?root=true&open=true`: non-terminal rows only; `counts` over the whole root set |

- Files 13 to 20 are the `current.status` object alone. They are also what `sindri workflow status --json` prints (Q4).
- Files 21 to 23 are the E-22 part only, until P2b lands and decides how it joins `ShowRecipeResponse` (Q5). Then they become the full body and the UI is told.

## 4. Where E-23 lands: in this contract unit, not in #190

- E-23 is `StatusFields`: the WF §6.4 `workflow status` layout as fields under `InstanceView.current.status`, served by `GET /workflow/instances/{address}` (API §3.8, the P11 row).
- It does **not** touch `GET /status`, so it doesn't edit `GetStatusResponse`, which #190 unit E/F is extending with `views`, `views.text` and `health` (E-14).
- So there is no shared record and no reason to put E-23 into #190. #190 keeps `GetStatusResponse` to itself.
- The API work that does touch `/status` is E-29 (`dependencies`, assignment item 5). It waits until #190 merges.
- Side finding for the lead: #192 (engine P4) also edits `GetStatusResponse.cs`, adding `Views` and `Health`. It looks like it carries #190's change. That is #190 and #192's overlap to sequence, not this unit's.

## 5. Engine dependencies (what this unit reads; what isn't on main)

On main, usable now as the composer's inputs:
- `WorkflowInstance` (Address, Recipe as `PinnedRecipe`, Item, Parent, `Frame`, `Suspension` and `PendingPark`, `Loops`, `Spent`, `Facts`, `Outcome`)
- `Frame(State, Entry, Run, Child)`
- `Suspension` and `SuspensionKind`, `ParkReason`, `ResumeAction` and `ResumeKind`
- `LoopState`, `TokenSpend`
- The `Recipe` model (`RecipeState`, `RecipeTransition`, `RecipeInput(Key, Label, Type)`, `LoopSpec`, `TimeoutSpec`, `BudgetSpec`)
- `IWorkflowInstances.List(query, temporal)` returning `InstanceRow(Address, State, NeedsYou)`

Not on main. The engine lane must expose:
1. **P2b (assignment Engine item 1)**:
   - `Recipe.Urgency`, `Recipe.Outputs` with `from`, `RecipeInput.Kind`, and the nine fact types;
   - `StatePathOrder` in Core, returning order, `branchOf`, `loops{name?, from, to}` and `orderedBy`.
2. **P4 (#192)**:
   - the `RecipeWorkflow` Query returning its `InstanceView`;
   - `InstanceViews.Of`;
   - `StatusText`.
   - Gaps against E-23: #192's `InstanceView` has `Next`, `AllowedCommands`, `Question`, `Task`, `Run`, `Children(Address, State)` and `Outcome`. It lacks the step `inputs` line, `timeout` (`timeoutAt` is an argument to `Of`, not a field), `budget`, `paused`, `drift` and the child's reason.
   - **Ask:** the engine renders §6.4 from one structured `StatusLayout` record (header, then each labelled line as data), and `StatusText` prints that record. The API maps the record to `StatusFields` and copies the printed text into `text`. That gives "one renderer, both forms" (API §3.8).
3. **P4 search attributes** for `ListInstances`: `Root` (no parent), `RecipeId`, `Urgency`, `SubjectKey` and `Since`, alongside `NeedsYou`. Plus per-instance `since` (the state-entry time) on the Query.
4. **Fact provenance**: `FactValue` needs `at` and `by` (the actor string), and an output's producing state. Otherwise `FactRow.status`, `at` and `by` can't be derived.
5. **P5 (#209)**: the timeout deadline and remaining time on suspension (`timeout`, `paused.timeoutLeftSeconds`).
6. **P7 and P8**: child rows with state, round, agent and age, mirrored parks, and packet workstreams for `ChildGroup`. Until then P11 serves `ChildView` rows only (glyph, state), with `workstreams: []`.
7. **Engine item 4**: `recipes/dev/triage-three-state.yaml` and `recipes/dev/desk-round-trip.yaml`. They aren't on main; the contract's fixtures use their planned state lists.

## 6. Red-first units (about half a day each)

**Contract PR (ships now). K1, then K2, then K3, each one commit:**
- **K1 `feat:` vocabulary and records.** Enums, records, the JSON-context block. Round-trip tests in the unit lane: kebab enums; `grantTokens`; unknown `FactType` reads as `payload`; nulls present.
- **K2 `test:` fixtures and goldens.** `WorkflowViewFixtures`, `WorkflowViewGoldenTests`, the 23 files recorded. **Send §3 to the UI lane the moment K2's file list is fixed**; the scratchpad message is the draft.
- **K3 `test:` red behaviours and stubs.** The composer and `GetInstanceFact` stubs, plus skipped red tests:
  - `Summary_orders_by_urgency_then_oldest_since`
  - `StateCell_badge_replaces_glyph_on_interrupted_state`
  - `StateCell_carries_round_and_loop_pair`
  - `Children_state_reports_done_over_total`
  - `Facts_follow_declaration_order_inputs_then_outputs`
  - `Unknown_fact_type_is_served_as_payload`
  - `Now_is_next_shortened_to_80_chars`
  - `Status_text_equals_workflow_status_print`
  - `Status_answer_cli_matches_text_line`
  - `GetInstanceFact_caps_body_at_256KiB_with_truncated`

**P11 proper, after its dependencies (each unit un-skips its tests and goes green):**
- **U1 (after engine P2b).** The `RecipeView` additions in `ShowRecipe` (`{id}` and `{id}@{hash}`), with `recipe show` printing the same keys. Re-record goldens 21 to 23 as full bodies; the E-22 signal.
- **U2 (after #192).** Composer `states[]` from `StatePathOrder` and the Query: glyph, badge, round, loops, desk, child, children progress, `orderedBy`.
- **U3 (after #192 + `StatusLayout`).** `StatusFields` from the layout record, and the parity test against the `sindri workflow status` text for all eight layouts (the E-23 signal).
- **U4 (after #209).** Timeout and paused fields; `current` (step, gates, desk form); `counts` from views.db (P3/P4 tables).
- **U5.** `FactRow` from facts with provenance; `GetInstanceFact` green, mapped `GET …/facts/{key}`, 256 KiB cap.
- **U6.** `ListInstances`: `root`, `recipe` and `subject` filters over the search attributes; urgency ordering; `now`; session repo scoping (`SND-ITEM-404`).
- **U7.** HTTP: replace the bodies of #192's `GetInstanceResponse` and `ListInstancesResponse` with the §3.8 shapes on the engine's routes. Bind an address or an id. The golden tests switch to served bodies, with the same files; any diff is a contract change sent to UI.
- **U8 (after API P4).** The item projection folds `workflow.*` into item states; needs-you `park` rows carry the engine's action id; `CloseItem` refuses while an instance is active.
- **U9.** `ChildGroup` from P4's `ChildView`, plus workstreams once P7 and P8 land. Then the P11 proof on the fixture with the two dev recipes (Phases P11 signals).

## 7. Questions for the lead

1. **Name clash.** #192 has `Aw.Sindri.Core.Workflow.Engine.Runtime.InstanceView` (the engine's status view). The API spec calls its wire shape `InstanceView` too, and the composer reads one to build the other. Default: the API record is `WorkflowInstanceView`, and the JSON is unchanged. Or should the engine rename its record to `InstanceStatus`?
2. **Route bodies.** #192 maps `GET /sindri/workflow/instances` and `/{instanceId}` with its own response shapes. §3.5 gives the shapes to the API. Confirm that U7 replaces those bodies, and that #192 shouldn't pin its current JSON as a contract (golden or UI-facing) before then.
3. **Skipped red tests on main.** Can the contract merge with red tests skipped, each skip naming the unit that un-skips it? Otherwise the red tests stay on the P11 branch, and only the records, goldens and stubs merge.
4. **`workflow status --json`.** Default: it prints `StatusFields`, the same object as `current.status`, so files 13 to 20 are also the CLI contract. Is that the engine lane's call?
5. **E-22 body.** Main's `ShowRecipeResponse` is `{recipe: RecipeSummary, preview}`. API §3.8's `RecipeView` is "the engine's definition plus inputs and outputs". Only `{id}@{hash}` is mapped, not `{id}`. Who reconciles this: engine P2b or API U1? Until then, files 21 to 23 are the additions only.
6. **`RecipeUrgency` home.** Should the contract define it in `Core/Workflow/Recipes` for P2b to adopt, or wait for P2b's enum? Default: define it now, with the engine lane's agreement, so there is one copy.
7. **Children before P7.** Should P11 ship with `children[]` limited to P4's `ChildView` (no agent, round or workstreams)? Or should U9 move after engine P7?
8. **Dev recipes.** Goldens 8 and 9 use the planned dev recipes. If the engine lane's files differ in state names, the goldens re-record. Fine to record now?

## Lead answers (2026-10-14)
- No skipped or red tests on main. The contract unit ships green: records, fixture builders, and golden-writing serialisation tests that pass. Red slice tests stay on the branch until their units turn them green, as in P1–P3.
- `InstanceView` name clash with #192: whichever PR merges first keeps the name. Raised with the orchestrator and engine lane; until agreed, the API's record is `InstanceDetail` per API §3.8 if §3.8 differs from #192's.
- U7 replacing #192's bodies, and who reconciles recipe-show: the engine lane owns the routes #192 mapped. The API lane supplies the §3.8 records; swapping the bodies is agreed with the engine lane via the orchestrator.
- `workflow status --json` prints the StatusFields. Urgency enum lives in Core with the engine's recipe model (it's engine P2b's field); the API references it.
- Children may ship partial before engine P7 (marked in the golden scenario name). Goldens are recorded now from the planned dev recipes; they are re-recorded when engine P2b and the dev recipes land.

## 8. UI U8/U3' API edits (answered 2026-10-14)

Sources: `plans/sindri-ui/u8-plan.md` "API edits still needed" and `u3p-plan.md` "U3' asks".

Classes used in the table:
- **plan** means plan-only: the spec already covers it.
- **align** means the API spec is brought in line with the screens spec, `fixture.md` or `fixes.md`. The UI spec wins (X52, X62), so no approval is needed.
- **owner** means a spec edit beyond the UI spec. It needs owner approval.

Shapes added to §2 by this section:
- `InstanceSummary.chip`, `.answer?` and `.id`;
- `InstanceList.counts`;
- `ChildRow.chip`;
- `WorkflowInstanceView.id`, `.parent?` and `current.desk.run`;
- `StatusCommand.confirm?`;
- `FactView(key, type, body, truncated)`.

`id` is the engine's `WFI-…`. It is the id form the API §3.8 Address rule already accepts.

| # | Ask | Answer and citation | Owner | Class |
|---|---|---|---|---|
| U8-1 (= U3'-9) | Served state chip | **Taken.** `chip: {word, tone}` on `InstanceSummary`, `ChildRow` and `NeedsYouRow`. `tone` is one of `attn`, `fail`, `ok` or `neutral`, the class column of fixture.md "State chips (owner, 2026-10-14)"; R8-13 sets the one-mapping rule. `StateChip.For(state, engineOwned, deskOpen, takenOver)` is a pure function in Core. The map: `parked` → PARKED; `escalated` → ESCALATED; an open desk answer → DECISION; `taken-over` → WITH YOU; an agent run or a wait → RUNNING; `queued` → QUEUED; terminal → DONE. CHILD appears only in the P13 engine table. PRJ-exports' "with you: import" depends on whether a claimed desk step counts as taken over (Q9). | This contract unit (records and `StateChip` in K1). `NeedsYouRow.chip` lands in P4 (its record) | align |
| U8-2 (= U3'-10) | `open=true`, list counts | **Taken.** `GET /workflow/instances?open=true` keeps non-terminal rows. API §3.8 already defines the strip as "the first five rows with `state` not terminal", and screens §3 makes "open root instances" the tab count. `InstanceList.counts {open, needsYou, withYou, running, done}` is computed over the root set before `open` filters it, so every head count comes from rows (fixture.md "The human queue"). This covers #210's open question ("12 open includes four done") either way. | Contract unit (field, golden 30). P11 U6 (filter). The query binding is on the engine's route (U7) | align |
| U8-3 | `answer` on the list row | **Taken.** `InstanceSummary.answer?` is the same `StatusAnswer` record as `current.status.answer`, including `atTerminal` (screens §4.1 `[Answer]`, §4.2 key `A`). | Contract unit | align |
| U8-4 | Addressed nonce, command and complete routes | **Not added; served data covers it.** The POST routes are the engine's: WF §6.3, mapped id-only in #192. API §3.8's addressed rule covers GET only. The views now carry `id`, and `current.desk.run` carries the open run's token (WF §6.4 desk layout: `--run 1.1`), so the client posts to the id routes it already has. Addressed POST routes would be an engine-spec edit; not recommended. | Engine lane (#192) for `run` on the Query, already in its `InstanceView.Run`. Contract unit for the fields | align for the fields; **owner** only if addressed POSTs are still wanted |
| U8-5 | Instance timeline, decisions and requests | **Timeline exists under another name.** Engine #192 maps `GET /sindri/workflow/instances/{instanceId}/events?after=` (`GetInstanceEvents`; WF §6.3 `workflow events`, "the instance-level filter of the item timeline"). Screens §4.2 also needs the children's events ("the address and its children"), so that route gains `children=true`. **Decisions and requests** gain `?instance=<address>`: the instance's item plus its children's items (API §3.1 has `?item=` only). | Engine lane (`children=true` on `/events`). P11 U8 for the `?instance=` filters on P4's `ListDecisions` and `ListRequests` (resolving needs the engine) | align |
| U8-6 | `parent` on the view | **Taken.** `parent?: {address, title, task?, workstream?}` (screens §4.7: "packet task t3 of PRJ-sync (ws-1 UI)"). `address` comes from `WorkflowInstance.Parent`, which is on main. `task` and `workstream` stay null until engine P7 (packet children). | Contract unit (field). P11 U9 fills it | align |
| U8-7 | `items?parent=` | **Taken.** `GET /items?parent=<address>` returns the items whose instance's parent is that address. API §3.8 `ChildGroup.more` already says "Open all in Work follows the items filter", and §3.1 lacks the filter. The item projection needs the parent address from `workflow.*` events. | P11 U8 (P3's `ListItems` gains the filter). The engine lane emits the parent on the start event | align |
| U8-8 | Facts-route goldens | **Taken.** Goldens 24 to 29. The shape is `FactView{key, type, body, truncated}` (API §3.8 `GetInstanceFact`): `body` is a JSON value (an object for `scope-map`, `packet` and `payload`; a string for `text`). | Contract unit (K2) | plan |
| U8-9 | A served Cancel-confirm sentence | **Taken as a field.** `commands[].confirm?` holds one sentence on destructive verbs. R8-13 puts Cancel behind a one-line confirm, and fixes.md wave 2 gives "Cancel INC-14? The mitigation agent is stopped and the comms child closes." `ConfirmText` (Core, one table, like `EmptyText`) builds it from the engine's open run and open children. | Contract unit (field, `null` in goldens until wording is approved). P11 U3 fills it. The engine lane exposes the open run's runtime and the children on the Query | align for the field; **owner** for the sentence templates (copy) |
| U3'-11 | Drop "waiting on you" | **Taken.** P4's `EmptyText.NeedsYou` becomes "Nothing needs you. N running, M queued." (fixture.md "waiting on you is retired"; R8-13). The mockup's "Last cleared HH:MM" (fixes.md wave 1) would need a new last-cleared time, so it isn't taken unless the owner wants it. API §3.6's example sentence gets the same alignment edit. | P4 (p4-plan.md §1.5, golden re-record); u3-api-answers.md updated | align |
| U3'-12 | Served health sentence | **Not there yet under any name.** P2 unit E's `HealthSummary` serves `health{word, problems[{code, text}]}`. `text` is a fix sentence, not a clause. **Added:** `HealthProblem.clause` ("the profile changed since approval") and `health.sentence`, built once by `HealthSummary.For`: "Sindri is degraded: A, B, and C." (fixture.md "The health sentence"; R8-13 wave 6). "Updated 2s ago" is appended by the app from `asOf` (its freshness rule), not served. Code-index and Onyx clauses arrive with their health sources (collector §11, CTX). | **P2 unit E2 (#190)**: it already owns that record, so no second PR touches `/status` | align |
| U3'-ENG-1 | Incident dev recipe | Engine lane: `recipes/dev/incident-hold.yaml` (`urgency: incident`) with its eval. It is a fixture beside screens §8.2's two. | Engine lane | plan (fixture) |

**Questions for the lead from this section:**
- **Q9.** Is a desk step a person has started (PRJ-exports "with you: import the design output") `WITH YOU` or `DECISION`? fixture.md defines WITH YOU as taken over. If it's WITH YOU, the engine must say a desk task is claimed.
- **Q10 (owner).** The Cancel-confirm sentence templates: one per open-run runtime (agent, desk) plus a children clause.
- **Q11 (owner).** Keep the needs-you empty sentence's counts, or switch to "Last cleared HH:MM", which needs new data?

Units affected:
- K1 gains `StateChip`, `ConfirmText` (stub) and the new fields.
- K2 gains goldens 24 to 30 and re-records 1 to 12 with `chip`, `answer`, `id`, `parent` and `run`.
- U3 fills `confirm`.
- U6 adds `open` and `counts`.
- U8 adds `?instance=` and `items?parent=`.

## Lead answers to §8 (2026-10-14)
- Q9: a started desk step that wasn't taken over is DECISION. WITH YOU means taken over only, as the UI fixture defines it, so no "desk step claimed" engine signal is needed.
- Q10/Q11 and item 4's address routes go to the owner via the orchestrator. The fields ship null or off until approved, and item 4 needs no new routes (the views carry id and run).

## Owner decisions (2026-10-14, relayed by the orchestrator)
- Cancel confirm (Q10): `commands[].confirm` is "Cancel <address>? <N> running steps stop; <M> children are cancelled." It ships in this contract unit, with spec text, test-first. N and M come from the open run and children (engine P4b); until P4b, fill them from the fixture.
- Needs-you empty sentence (Q11): "Nothing needs you. N running, M queued." No "Last cleared" variant. It's P4's EmptyText; record it in the spec in the P4 PR (or here if the contract unit touches API §3.6 text).
- Engine P2b: every recipe hash changes; an absent `urgency` hashes the same as `work`. Dev recipe ids are workflow.dev-triage-three-state, workflow.dev-desk-round-trip and workflow.dev-incident-hold. Name goldens and fixtures after them.
