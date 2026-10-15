# API lane → UI lane: rev-8 golden files (E-20, E-22, E-23)

This is the shape, and the record names may still change. The field names below are fixed: API spec §3.8.

## The files

They are in `tests/Aw.Sindri.Tests/Golden/Api/<slug>/<scenario>.json`. Each one is written by the API contract unit from the screens fixture.

- **E-20, the list** (`InstanceList`):
  - `workflow-instances/root`
  - `workflow-instances/recipe-filter`
  - `workflow-instances/empty`
- **E-20 and E-23, the view** (`WorkflowInstanceView`, which the spec calls `InstanceView`):
  - `workflow-instance/project-build`
  - `workflow-instance/incident-loop`
  - `workflow-instance/triage-desk`
  - `workflow-instance/escalated-gate`
  - `workflow-instance/desk-round-trip`
  - `workflow-instance/triage-three-state`
  - `workflow-instance/transition-list`
  - `workflow-instance/done`
  - `workflow-instance/not-found` (ProblemDetails, `SND-ITEM-404`)
- **E-23, the status fields alone** (`StatusFields`, also printed by `workflow status --json`):
  - `workflow-status/parked-child`
  - `workflow-status/desk-task`
  - `workflow-status/parked-budget`
  - `workflow-status/escalated-gate`
  - `workflow-status/taken-over`
  - `workflow-status/done`
  - `workflow-status/parked-drift`
  - `workflow-status/needs-answer-wait`
- **E-22, the recipe additions** (`RecipeView`):
  - `workflow-recipe/workflow-project`
  - `workflow-recipe/incident-response`
  - `workflow-recipe/no-outputs`

## Fields

`?` means the field is always present and holds null when empty. Lists are never null.

**`InstanceList`**: `instances[]`, `empty`, `asOf`.

**`InstanceSummary`**: `address`, `itemId?`, `recipe{id, hash, version, urgency}`, `state`, `since`, `engineOwned?{kind, reason}`, `subject{kind, key, label, href?}`, `inputs[]{key, label, kind, required, present}`, `states[]`, `now`, `actions[]`.

**`StateCell`**: `id`, `order`, `branchOf?`, `glyph`, `badge?{kind, reason}`, `round?{n, max}`, `loops[]{with, n, max}`, `desk`, `child`, `children?{done, total}`, `outcome?`, `terminal`.

**`WorkflowInstanceView`**: `address`, `itemId?`, `title`, `recipe`, `state`, `since`, `engineOwned?`, `subject`, `orderedBy`, `states[]`, `current`, `facts[]`, `children[]`, `counts{timeline, decisions, requests, context?}`, `actions[]`, `asOf`.
- `current`: `state`, `step{kind, ref, declaration}`, `gates[]{id, on, threshold?, description}`, `desk?{outputs[]{key, type, required}, checks[], completeAction}`, `status`.

**`StatusFields`**: `header{address, recipe, hash12, version, state, detail?, since, ageSeconds}`, `next`, `answer?{kind, actionId, class, cli, atTerminal}`, `child?{address, state, reason?}`, `drift?{block, needsReapproval}`, `inputs[]{key, name, hash?}`, `timeout?{kind, inSeconds, on}`, `budget?{scope, used, limit, instance{used, limit}}`, `paused?{timeoutLeftSeconds}`, `commands[]{verb, actionId, class, cli}`, `text`.

**`FactRow`**: `key`, `label`, `type`, `direction`, `from?`, `status`, `summary`, `at?`, `by?`, `ref?`, `progress?{done, total}`, `pending?{required, checks[], completeAction}`.
- `ref` is one object with nullable members: `name`, `size`, `hash`, `path`, `itemId`, `url`, `host`, `title`, `factKey`.
- `by` is an actor string (`human:cli`, a Step, an agent). Render `human:*` as "you".

**`ChildGroup`**: `state`, `recipe`, `done`, `total`, `workstreams[]{id, label, rows[]}`, `rows[]`, `more`.
- **`ChildRow`**: `address`, `itemId?`, `title`, `glyph`, `state`, `round?`, `agent?`, `ageSeconds`, `engineOwned?`, `mirroredPark?`, `actions[]`.

**`RecipeView` additions**: `urgency`, `inputs[]{key, label?, type, kind?, required}`, `outputs[]{key, label?, type, from}`, `states[]{id, order, branchOf?}`, `loops[]{name?, from, to}`.

## Values

- **Enums** (kebab-case): `glyph` is `done`, `active` or `pending`. `kind` on a badge or `engineOwned` is `parked`, `escalated` or `taken-over`.
- **Fact `type`**: `text`, `file`, `item`, `link`, `scope-map`, `packet`, `evidence`, `decision` or `payload`. An unknown type comes as `payload`.
- **Fact `status`**: `produced`, `in-progress` or `pending`.
- **`answer.kind`**: `text`, `resume`, `grant`, `grantTokens`, `accept` or `cancel-only`.
- **`urgency`**: `incident`, `triage` or `work`.
- **`orderedBy`**: `path` or `transition-list`.
- **Times** are ISO 8601 UTC. Durations are whole seconds.

## What may still change

- **Route prefix.** The routes are served under `/sindri` (`GET /sindri/workflow/instances…`).
- **The recipe files.** The `workflow-recipe/*` files become the full `ShowRecipe` body once engine P2b lands. Their field names stay; the outer body may change, and you will be told.
- **Children.** Before engine P7 and P8, `agent`, `round` and `workstreams` come as null or `[]`.
