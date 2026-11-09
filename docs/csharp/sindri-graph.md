# Workflow graphs (Sindri, issue 2258 PR 5)

A workflow is a JSON document Sindri stores as revisions. This page covers what PR 5 ships: the spec, the validator,
`{{...}}` interpolation and the revision store. Nothing runs a graph yet (PR 6).

## The spec

```json
{ "version": 1, "name": "Contract drift check", "concurrency": "one-per-workflow",
  "nodes": [ { "id": "daily", "type": "schedule", "config": { "cron": "0 7 * * 1-5", "timezone": "America/Los_Angeles" } },
             { "id": "tell", "type": "notify", "config": { "title": "Drifted" } } ],
  "edges": [ { "from": "daily", "to": "tell" } ] }
```

- `version` is 1. Unknown members are refused (`SND-GRAPH-001`).
- A node id is a lowercase letter, then lowercase letters, digits and `_` (at most 64), because references use it.
- An edge leaves by `output` (default `out`; a `condition` has `true` and `false`).
- Config strings take `{{node.field}}`, `{{trigger.*}}`, `{{item}}` and `{{itemIndex}}` (inside a `for_each`; `itemIndex` counts from 1) and `{{workflow.*}}`. A string that is exactly one
  reference keeps the referenced value's type; inside other text, objects and arrays render as compact JSON and null as nothing.

## Validation (`GraphValidator`, `sindri wf validate <file> [--json]`)

| Code | Finding |
|---|---|
| `GRAPH-TRIGGER-001` / `-002` | more than one trigger (names all) / none |
| `GRAPH-WIRING-001` | a node no trigger reaches |
| `GRAPH-CYCLE-001` | a loop, with its path in spec order (`a -> b -> c -> a`) |
| `GRAPH-REF-001` / `-002` / `-003` / `-004` / `-005` | a reference to a node that is not upstream / to an output name the node does not have, or a `{{workflow.<field>}}` a run does not have (`id`, `name`, `runId`, `revision`) / to no node / a `{{` that is never closed (a `{{` inside a string literal of a condition is text, not a reference) / an output whose type does not fit the field it fills |
| `GRAPH-BLOCK-001`, `GRAPH-CONFIG-001` | unknown block type, config that fails the block's schema |
| `GRAPH-CRON-001`, `GRAPH-COND-001` | unreadable cron or timezone, unreadable condition |
| `GRAPH-REPO-001`, `GRAPH-NAME-001` | repo, connection or memory that does not exist (`-002` is the warning when nobody can answer yet) |
| `GRAPH-LOOP-003` | a `run_agent` node in a `for_each` body (the per-item path has no agent loop); the run fails the `for_each` before any item |
| `GRAPH-LOOP-001` / `-002` | a `for_each` that can't run as a loop: no `collect` after it, more than one, a loop inside it, or a node of the loop also fed from outside it / a `collect` no `for_each` reaches |
| `GRAPH-NODE-001` / `-002`, `GRAPH-EDGE-001` / `-002` | repeated node id / a node id that is `trigger`, `item`, `itemIndex` or `workflow` (reserved reference roots), edge to a missing node, edge by an output the block lacks (an edge with no `output` leaves by `out`) |

`GRAPH-TRIGGER-001` / `-002`, `GRAPH-CYCLE-001` and `GRAPH-WIRING-001` are node-level findings: they name nodes and have no field.

The validator checks that a referenced output exists, and that a whole-value reference (`"{{a.n}}"` as the entire string) has a type the field takes (`GRAPH-REF-005`). A reference inside longer text is always text, so it is not type-checked.

A document with a missing or null node, edge, `id`, `type`, `config`, `from` or `to` is refused by `GraphJson.Parse` as `SND-GRAPH-001` naming the JSON path (such as `nodes[2].type`); neither `Parse` nor `wf validate` throws on any text.

`wf validate` reads `config.when` with `ConditionExpression` (references, comparisons, `and`/`or`/`not`, `exists(x)`); a parse error comes back with its column as `GRAPH-COND-001`, and the references it reads are checked for being upstream like any other.

A **draft** save keeps wiring gaps (`GRAPH-TRIGGER-002`, `GRAPH-WIRING-001`); **activation** refuses every error. Warnings never
block. `wf validate` always checks as for activation. The block catalog is empty in this build, so every node type reads as unknown
until the blocks arrive.

## Storage

`workflow` and `workflow_revision` in ledger.db (migration 0008). A save is a new immutable revision with its `spec_hash`;
saving on a revision that is no longer the latest is `SND-GRAPH-007`; `Activate` pins the revision triggers run.

## Error codes

`SND-GRAPH-001..011` (definitions) and `SND-GRAPH-100..108` (runs, below), listed in `docs/sindri/errors.md`.

## The `prompt` block's `output` schema

`prompt` asks the model once and checks the answer against its `output` JSON schema, with a hand-written subset (Core has no JSON-schema library). **It checks exactly this:** the answer is a JSON object (a ``` fence around it is removed); every name in `required` is present (names must be text); and every property that has a `type` has a value of that type, where `type` is `string`, `number`, `integer`, `boolean`, `object` or `array` (`integer` accepts any number, so `1.5` passes), or a list of type names, of which the first one other than `"null"` is used. **It does not check:** `enum`, nested `properties` or `items`, `additionalProperties`, `null`/union members beyond the first type, formats or ranges. An `output: {}` accepts any JSON object. A `type` that is not text or lists only `"null"`, or a non-text `required` entry, is a `GRAPH-CONFIG-001` finding when the workflow is validated and `SND-GRAPH-103` when the node runs (before the model is asked). Only the schema's `properties` become the node's outputs.

## Runs (PR 6)

A run is a row in `workflow_run` (ledger migration `0009_workflow_runs`; the number is renumbered at the final main merge). Its id is `<slug>@<revision>:<occurrence>`, the primary key, so a second insert of the same id is refused with `SND-GRAPH-100`. Statuses: `queued`, `running`, `waiting`, `succeeded`, `failed`, `cancelled`. `workflow_node_run` holds one row per node and item index; a node's input and output are each cut at 64 KB (UTF-8) and `truncated` is set.

`GraphRunStarter.Start` turns a trigger into a run by the workflow's `concurrency`: `one-per-workflow` (the default) skips and logs a trigger while another run is in flight, `queue` records it as `queued` without starting it, `unlimited` starts every trigger. A failed launch marks the run `failed`.

**Walking a run.** A started run is one Temporal workflow, `GraphWorkflow`, with the run id as its workflow id (`TemporalGraphRunLauncher`). It walks the revision's nodes in edge order through activities (`GraphRunActivities`), records every node in `workflow_node_run` and mirrors each transition into the ledger (`workflow.run.*` events; they are not public yet). Its end sets the run's status (`succeeded`, `failed` or `cancelled`).

**The queue moves on.** When a run ends, and when a launch fails, `IGraphQueueAdvance.StartNextQueued` starts the oldest `queued` run of the workflow, once nothing else of it is in flight. `IWorkflowRunStore.PromoteOldestQueued` checks and promotes in one transaction, so two runs ending together start one queued run, not two. A queued run whose own launch fails is marked `failed` and the next one gets the turn. Queued runs are not in `view_workflow_runs` until they start.

**By hand.** `sindri wf run <slug> [--payload JSON] [--json]` (`POST /sindri/workflows/{slug}/runs`) starts a run of the active revision with trigger `{"kind":"manual",...}`: it answers started, queued or skipped by the concurrency. `sindri wf cancel <run> [--json]` (`POST /sindri/workflow-runs/{run}/cancel`) asks a run in flight to stop (Temporal cancel); a queued run is cancelled in place and never starts. `sindri wf takeover <run> <node> [--item <index>] [--json]` (`POST /sindri/workflow-runs/{run}/nodes/{node}/takeover`) takes over the `run_agent` node the run waits on (its timeout pauses, only the human reports it) and `sindri wf handback ...` (`.../handback`) gives it back; both answer `{"run","node","itemIndex","agent"}` with `agent` `taken_over` or `waiting`, and both are open to a human and to an agent session alike. A node the run isn't waiting on is `SND-GRAPH-110`, a handback of a node nobody holds `SND-GRAPH-111`. `GET /sindri/workflow-runs/{run}` marks such a node row with `agent`: `waiting`, `taken_over` or null.

**Codes.** `SND-GRAPH-100` the run id is used, `101` no such run, `102` a block could not finish, `103` a prompt answer does not fit its schema, `105` the workflow is not active, `106` a manual payload is not a JSON object, `107` the run already ended, `108` a referenced node output was cut at 64 KB.

## The `run_agent` block (PR 7)

One node run is one agent session on the existing launcher and agent runtime. Config: `repo` and `prompt` (required); `readOnlyRepos`, `agent` (`claude-code`, `codex` or `cursor`; the profile's `workflow.agent.target` when left out), `outputs` (declared fields: `{"drifted":"boolean","summary":"string?"}`; a name is a lowercase letter then letters and digits, a type is `string`, `number`, `boolean`, `object` or `array`, and a trailing `?` makes it optional), `timeout` (`45m`; default `2h`), `connections` and `resources` (`wall`). The node's outputs are the declared fields plus `status`, `branch`, `commit`, `sessionId` and `usage` (`{tokens, emptyDiff}`).

**Launch.** `NodeRunAddress` (workflow, run, node, item index) replaces the Step's instance and run token in `LaunchRequest` (`Node`, `Task`; an attended open keeps both null). The launch id is `LCH-` and 32 hex digits of SHA-256 of `agent-launch:<run>:<node>:<index>`, so a replay reuses the session. A run id holds `:` and `@`, so the session name stays `snd-<launch id>` and the branch is `wf/<slug>/<revision>-<occurrence>-<node>` (`-<index>` after it for an item of a `for_each`): the node is in it because two agent nodes of one run need two branches. The worktree is `<repo>/.claude/worktrees/wf-<slug>-<short run>`, started from the repo's default branch; the checked-out branch is `wf/<slug>/<short run>/work` and the session may write refs under `wf/<slug>/<short run>/` only (its own node run; the sandbox makes that path a ref directory, so the branch sits one level inside it). The sandbox adds the host group `github` for a node that names a repo, plus each named connection's group once PR 10 supplies them (`GroupsInput.AddGroups`). A run no item started gets an item id made from its run id.

**Wait.** `GraphWorkflow` runs the node itself (`GraphWorkflow.Agent.cs`): `LaunchAgentNode`, then `WatchAgentNode` (a heartbeating activity) raced against the node's timeout, then `CloseAgentNode` (usage, diff signal, HEAD, stop). A takeover (`agent-takeover` update) stops the timeout clock, a handback (`agent-handback`) starts it again with what was left, and the idle sweep leaves a held session alone (`ILaunchFiles.MarkTakenOver`). A session that ends without a report fails the node with `exit`, `lost`, `idle` or `limit` (a limit, or a report the engine refused it for, never recovers); the timeout fails it with `timeout`. A cancel stops the session.

**Report.** The agent reports with `sindri workflow complete <run id> --run <token> --outcome done --output fact:<field>=<file>...` (the exact line is in its brief); the token is `<node position>.<item index + 1>` (`RunAgentWire`). `CompleteStepService` finds the node, decides who may report it (`AgentReportAuthority.DecideNode`: the launched session with its launch secret, or the human of a taken-over node; a wrong secret is `SND-AUTH-003`), checks the report against the declared outputs (`RunAgentReport`: a missing, undeclared or mistyped field is refused and named, and the agent may report again; a string output is the file's text, any other type is JSON) and sends the values to the run's workflow (`agent-report` update). A session that ended unreported completes the node from `.sindri-out` only when every declared output (except optional ones) is there (`AgentRecovery.DecideNode`).

## Flow blocks (agent-first PR 9, part C)

Five more blocks, each with an example spec page under `docs/workflows/blocks/`: [`only_new`](../workflows/blocks/only_new.md), [`create_item`](../workflows/blocks/create_item.md), [`run_workflow`](../workflows/blocks/run_workflow.md), the [`workflow_call`](../workflows/blocks/workflow_call.md) trigger and [`http`](../workflows/blocks/http.md). Registered by `GraphBlockRegistry.FlowBlocks`.

- **After-success hook.** `IGraphBlock.Commit` runs after a node's `Execute` succeeded (in `GraphBlockActivities`); a failure there fails the node. `only_new` records its keys there, so a node that fails leaves its items unseen. It runs at the node, not at the end of the run: a later node that fails does not give the items back.
- **`workflow.id`.** A node may read `{{workflow.id}}` (with `name`, `runId` and `revision`). `only_new` keys its memory by it.
- **Call guards** (`Core/Graph/Guards`). A run is depth 0, the run it calls is 1. `workflow.callDepth` (profile, default 3, 1 to 10): depth 3 is allowed, 4 is `SND-GRAPH-120`. A run may not start its own workflow, nor the workflow named by its trigger's `parentRun`: `SND-GRAPH-121`. The callee's trigger is `{"kind":"workflow_call","payload":...,"callDepth":N,"parentRun":"<run>","by":"workflow:<slug>"}` and its run id's occurrence is a hash of caller run, node and item index (from 100,000,000 up), so a node that runs twice starts one callee run. Seam: `CallDepth.Next(..., launchLineage)` is where PR 7's launch-based self-start check plugs in.
- **Profile keys.** `workflow.http.hosts` (bare lower-case host names, at most 50; none listed refuses every request) and `workflow.callDepth`; both are approval-relevant (`ProfileDiff`).
- **Seen memory.** `workflow_seen(workflow_id, key, first_seen_at)` in ledger.db (migration 0014; renumbered at the final main merge). Read only by `only_new`.
- **Codes.** `SND-GRAPH-120` call too deep, `121` self-start, `122` an http request refused before it was sent, `123` an http request that got no acceptable answer.

## Reading workflows and runs (PR 8a)

Four reads, each one slice (`Features/ListWorkflows`, `GetWorkflow`, `ListWorkflowRuns`, `GetWorkflowRun`) behind an HTTP route, an MCP tool and a `sindri wf` verb. They read the routes and never start the control plane; any caller may use them.

| Read | Route | MCP tool | CLI |
|---|---|---|---|
| The workflows that are not archived | `GET /sindri/workflows` | `workflow_list` | `sindri wf list [--json]` |
| One workflow: the revision it runs (the newest when off), the spec, the spec hash, who saved the newest revision, node positions | `GET /sindri/workflows/{slug}` | `workflow_get` (`slug`) | `sindri wf show <slug> [--json]` |
| A workflow's newest 50 runs, newest first, any status | `GET /sindri/workflows/{slug}/runs` | `workflow_runs` (`slug`) | `sindri wf runs <slug> [--json]` |
| One run with its trigger, outputs and node runs | `GET /sindri/workflow-runs/{run}` | `workflow_run_get` (`run`) | `sindri wf run-show <run> [--json]` |

An unknown slug is `SND-GRAPH-008` (404), an unknown run `SND-GRAPH-101`, a slug or run id of the wrong shape invalid input. "Who changed it last" is the `createdBy` of the newest revision (`owner` or `session:<id>`). Node payloads the store cut at its size limit come back as a string with `truncated: true` on the node run. Over MCP, names, descriptions, node titles and every string of a node config (prompts), failure text, trigger payloads, outputs and node inputs and outputs are fenced as untrusted. `IWorkflowRunStore.Recent` is the new store read behind the run list.

## Connection events

The built-in service `workflow-poll` (`Features/PollConnections`, every 30 seconds) starts runs for `connection_event` nodes. What the node takes, the intervals and what a catch-up does are in [workflows/blocks/poll.md](../workflows/blocks/poll.md).

## Schedules

The built-in service `workflow-schedule` (`Features/WorkflowSchedule`, every minute) starts runs for `schedule` nodes.

1. For each active workflow, take the `schedule` node of its active revision.
2. Compute the latest cron tick after the latest of the revision's save time, the last schedule run's tick and the last handled tick, up to now (`CronExpression.LastTick`, in the node's timezone).
3. Start one run through `IGraphRunStarter` with trigger `{"kind":"schedule","scheduledFor":"<tick>"}`. The starter decides start, queue or skip by the workflow's `concurrency`.

- Three missed days fire one run, for the latest tick only.
- **Occurrence** of a schedule run is the tick in whole minutes since the Unix epoch, so firing the same tick twice is SND-GRAPH-100 (one run row).
- **Handled ticks.** The store keeps the last handled tick per workflow (`IWorkflowRunStore.MarkTick` / `LastTick`, table `workflow_schedule_tick` in the run tables' migration; it only moves forward). The service marks a tick when the starter started it, queued it **or skipped it** (a run was in flight), and when the tick already has a run (SND-GRAPH-100). A skipped tick is therefore done: it is not retried each minute and does not fire late when the run in flight ends; the next cron tick fires on its own. The newest schedule run's occurrence (`IWorkflowRunStore.LatestOccurrence`) stays a second floor.
- **A failed launch does not use up its tick.** When the starter returns any other error (the engine is down, say), the tick is not marked, the pass logs a warning, and the next pass tries the same tick again (as long as it is still the latest). A pass keeps going over the other workflows after one fails and returns the first error at the end.
- **One schedule node.** A workflow has exactly one trigger (`GRAPH-TRIGGER-001`), so the service reads only the first `schedule` node (up to six crons inside it).
- **Known gap:** nothing stores when a revision was activated, so the floor is `WorkflowRevision.CreatedAt`. A tick between saving and activating a revision can fire once on activation.

## Loops (`for_each`, `collect`; PR 9)

Pages: `docs/workflows/blocks/for_each.md` and `collect.md`. A `for_each` node and the one `collect` it reaches bound a loop; the loop is every node between them (`LoopBody.Find`).

How it runs, in order:

1. `GraphRunBlock` runs the `for_each` like any block: it checks that `items` is a list of at most `maxItems` and outputs `count` and `batchSize`. A spec whose loop can't be found fails the `for_each` here, before any item.
2. `GraphWorkflow` starts one child workflow, `GraphIteration`, per batch of `batchSize` items (`GraphWorkflow.Loop.cs`), one after the other, so items run in order. The parent holds one entry per batch, never one per item, and the items themselves are never in its history: each activity re-reads the list from the stored output the `for_each` read.
3. `GraphIteration` (`GraphIterationWorkflow.cs`) loads the spec and, for each item of its batch, walks the loop's nodes with the same two rules as the run's walk (`GraphWalkRules`): one `GraphRunBlock` activity per node, a `workflow_node_run` row per node and item. The row's item index is the item's position counted from 1; the `for_each` and `collect` rows of the loop as a whole are at index 0. A failing `stop` node ends the batch and the nodes left for that item are `skipped`.
4. After the last batch the `GraphCollect` activity reads the `collect` rows of items 1 to N, in item order, and writes the `collect` node's own row (`items`, `count`). Values never pass through workflow history.

Limits: `forEach.maxItems` (profile `workflow.forEach.maxItems`, default 200, at most 10,000; this is the cap, not a default: a node's own `maxItems` can lower it and never raise it, so the effective limit is the smaller of the two, and a list over it fails the `for_each` naming the profile key) and `batchSize` (default 20, at most 100). Temporal stops a workflow at 51,200 events; the parent adds about 12 events per batch (child initiated, started, completed, and the parent's own workflow task events), so 10,000 items at the default batch size are 500 batches, about 6,000 events. A batch's own history grows with its items times the loop's nodes (about 9 events per node per item). `ForEachWorkflowTests.Five_hundred_items_stay_under_the_history_budget` asserts the parent stays under 500 events for 500 items; it measured 298 (25 batches, 2026-11-09).

`GraphRunBlock` reads `IWorkflowRunStore.NodeRunsForItem` (every index 0 row plus the current item's rows), not the whole run, so a loop of N items reads O(N) rows in all. It still re-interpolates the `for_each` config (the whole `items` list) once per call, which costs CPU, not row reads.

**Sizes.** A block's input and output are cut at 64 KB before they enter workflow history (`GraphBlockActivities.Fit`): an activity result over Temporal's 2 MB limit fails the workflow task, which Temporal retries without end. A cut value keeps one extra character so the store still flags it `truncated`. The `collect` node's own output (all the values together) must also fit in 64 KB: when it does not, `GraphCollect` fails the `collect` and the message names the collected size and the cap (for example 200 items of 400 bytes are about 80 KB).

**Failures in a body node.** A failing body node (item 25 of a batch of 20 is in the second batch) stops the loop and lands on the `collect` node: the `collect` row is `failed` and the run's error names the item. Only the `collect`'s `onFailure` decides what happens next (`continue` lets the walk go on to the nodes after the loop; anything else fails the run). `ForEachLaterBatchTests` covers item 25 of 30 with the default batch size, both ways.
