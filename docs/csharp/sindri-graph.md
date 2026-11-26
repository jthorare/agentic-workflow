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
| `GRAPH-REF-001` / `-002` / `-003` / `-004` / `-005` | a reference that can be empty at run time: to a node that is not upstream, to a node that can be skipped on a path to the reader (it sits on one branch of a condition that later joins), to a loop-body node from outside its for_each body (read the collect), or `{{item}}` / `{{itemIndex}}` outside a for_each body / to an output name the node does not have, or a `{{workflow.<field>}}` a run does not have (`id`, `name`, `runId`, `revision`) / to no node / a `{{` that is never closed (a `{{` inside a string literal of a condition is text, not a reference) / an output whose type does not fit the field it fills |
| `GRAPH-BLOCK-001`, `GRAPH-CONFIG-001` | unknown block type, config that fails the block's schema |
| `GRAPH-CRON-001`, `GRAPH-COND-001` | unreadable cron or timezone (a timezone or cron that is not text too), unreadable condition |
| `GRAPH-REPO-001`, `GRAPH-NAME-001` | repo, connection or memory that does not exist (`-002` is the warning when nobody can answer yet) |
| `GRAPH-LOOP-003` | retired (#2470): a `run_agent` node in a `for_each` body runs one agent per item; only a run that started on the old walk still fails it |
| `GRAPH-LOOP-001` / `-002` | a `for_each` that can't run as a loop: no `collect` after it, more than one, a loop inside it, or a node of the loop also fed from outside it / a `collect` no `for_each` reaches |
| `GRAPH-NODE-001` / `-002`, `GRAPH-EDGE-001` / `-002` | repeated node id / a node id that is `trigger`, `item`, `itemIndex` or `workflow` (reserved reference roots), edge to a missing node, edge by an output the block lacks (an edge with no `output` leaves by `out`) |
| `GRAPH-NODE-003` | an `onFailure` value other than `stop`, `continue` or `skip-item`; write `stop` (halt the run when the node fails, the default), `continue` (keep walking) or `skip-item` (on a for_each body node: drop the item and go on with the next) |
| `GRAPH-NODE-004` | `onFailure: continue` on a node with no single `out` output, such as a condition. A failed node takes no named branch, so the nodes after it never run. Put the condition's work in a step with `onFailure: continue`, or remove `onFailure` |
| `GRAPH-NODE-005` | `onFailure: skip-item` on a node that is not in a for_each body: outside every loop, on the `for_each` node, or on the `collect` itself. Only a body node has an item to drop. Put it on the body node that can fail, or write `stop` or `continue` |
| `GRAPH-RESULT-001` | the top-level `result` names no node of the graph; name the node whose output is the run's result, or clear `result` with `set_meta` and `"result": ""` (a `remove_node` of the result node needs that op in the same batch) (#3807) |
| `GRAPH-PROMPT-001` | warning: a `prompt` whose question (`config.prompt`) has no `{{...}}` reference and whose only parents are triggers, so the model answers about no data. Add a step before it and read its output with `{{step.field}}` |

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

`SND-GRAPH-001..011` (definitions) and `SND-GRAPH-100..111` (runs, below), listed in `docs/sindri/errors.md`.

## The `prompt` block's `output` schema

`prompt` asks the model once and checks the answer against its `output` JSON schema, with a hand-written subset (Core has no JSON-schema library). **It checks exactly this:** the answer is a JSON object (a ``` fence around it is removed); every name in `required` is present (names must be text); and every property that has a `type` has a value of that type, where `type` is `string`, `number`, `integer`, `boolean`, `object` or `array` (`integer` accepts any number, so `1.5` passes), or a list of type names, of which the first one other than `"null"` is used. **It does not check:** `enum`, nested `properties` or `items`, `additionalProperties`, `null`/union members beyond the first type, formats or ranges. An `output: {}` accepts any JSON object. A `type` that is not text or lists only `"null"`, or a non-text `required` entry, is a `GRAPH-CONFIG-001` finding when the workflow is validated and `SND-GRAPH-103` when the node runs (before the model is asked). Only the schema's `properties` become the node's outputs. The model is also asked for two implicit fields, `couldNotAnswer` (boolean) and `couldNotAnswerWhy` (text); when `couldNotAnswer` is `true` the node fails with `SND-GRAPH-133` and `couldNotAnswerWhy` (cut at 200 characters) in the message, before the shape is checked. A missing `couldNotAnswer` counts as false, and neither field is an output, and a field of your own named `why` is not touched. Without `output`, the answer isn't read and the node has no outputs.

## Runs (PR 6)

A run is a row in `workflow_run` (ledger migration `0009_workflow_runs`; the number is renumbered at the final main merge). Its id is `<slug>@<revision>:<occurrence>`, the primary key, so a second insert of the same id is refused with `SND-GRAPH-100`. Statuses: `queued`, `running`, `waiting`, `succeeded`, `failed`, `cancelled`. `workflow_node_run` holds one row per node and item index; a node's input and output are each cut at 64 KB (UTF-8). `truncated` is set only when the output was cut: a cut input is kept for display and never hides a whole output (#3896).

`GraphRunStarter.Start` turns a trigger into a run by the workflow's `concurrency`: `one-per-workflow` (the default) skips and logs a trigger while another run is in flight, `queue` records it as `queued` without starting it, `unlimited` starts every trigger. A failed launch marks the run `failed`.

**Walking a run.** A started run is one Temporal workflow, `GraphWorkflow`, with the run id as its workflow id (`TemporalGraphRunLauncher`). It walks the revision's nodes in edge order through activities (`GraphRunActivities`), records every node in `workflow_node_run` and mirrors each transition into the ledger (`workflow.run.started`, `.finished` and `.cancelled` and `workflow.node.started`, `.finished` and `.failed` are public and carry ids only; `workflow.node.taken-over`, `workflow.node.handed-back` and `workflow.notify` stay private). Its end sets the run's status (`succeeded`, `failed` or `cancelled`). The run's own writes (load, begin, the re-run's read of the earlier steps, each node's `running` and final row, the block commit and the finish) are local activities under the patch `graph-local-activities-1`: they run in the worker without a task-queue round trip, with the same timeout and retry as before (#3685); a retry wait over one second is a workflow timer, not a sleep in the worker. Once a patched run exists, an older build can't replay it (it does not know the marker), so don't roll back past this change while such a run is in flight. Block runs stay ordinary activities, and a run that started before the patch replays the old calls.

**The run's result (#3807).** A succeeded run's `outputs` (what `run_workflow` with `wait` and `workflow_result` read) are built by `GraphResultOutputs.Build` in `GraphRunActivities.FinishAsync`: an object keyed by node id holding the node the spec's top-level `result` (`GraphSpec.Result`) names, or, with no `result`, every node no edge leaves, in spec order, that succeeded. A for_each body node (rows at items 1 to N, none at 0) gives an array in item order. A `collect` gives its own row at item 0 only (`items`, `count`, `skipped`), never its item rows (#4093; before, it gave `[own result, item 1, item 2, ...]`). The 64 KB cap (`NodeRunLimits.PayloadBytes`) applies to that result on its own, not to the sum of every node row. A result node whose row is `truncated`, or a result over the cap, is stored as `RunResultMarker` (`{"truncated":true,"bytes":N}`; `RunResultMarker.IsMarker` matches only that shape, `truncated` true with an optional `bytes` and no other key, so a step output such as GitHub's tree body `{"truncated":true,"tree":[...]}` is not one, #4089), and both blocks fail with `SND-GRAPH-108` (`RunResultMarker.Refusal`) instead of handing it on. Before #3807 the outputs held every succeeded node and were `null` past 64 KB together; runs already stored keep that shape. The run read (`GET /sindri/workflow-runs/{run}`) still lists every node row. An edit sets `result` with the `set_meta` op (`{"op":"set_meta","result":"rank"}`; `""` clears it), and `rename` carries it along.

**The queue moves on.** When a run ends, and when a launch fails, `IGraphQueueAdvance.StartNextQueued` starts the oldest `queued` run of the workflow, once nothing else of it is in flight. `IWorkflowRunStore.PromoteOldestQueued` checks and promotes in one transaction, so two runs ending together start one queued run, not two. A queued run whose own launch fails is marked `failed` and the next one gets the turn. Queued runs are not in `view_workflow_runs` until they start.

**By hand.** `sindri wf run <slug> [--payload JSON] [--json]` (`POST /sindri/workflows/{slug}/runs`) starts a run of the active revision with trigger `{"kind":"manual",...}`: it answers started, queued or skipped by the concurrency. `sindri wf cancel <run> [--json]` (`POST /sindri/workflow-runs/{run}/cancel`) asks a run in flight to stop (Temporal cancel); a queued run is cancelled in place and never starts. `sindri wf takeover <run> <node> [--item <index>] [--json]` (`POST /sindri/workflow-runs/{run}/nodes/{node}/takeover`) takes over the `run_agent` node the run waits on (its timeout pauses, only the human reports it) and `sindri wf handback ...` (`.../handback`) gives it back; both answer `{"run","node","itemIndex","agent"}` with `agent` `taken_over` or `waiting`, and both are open to a human and to an agent session alike. A node the run isn't waiting on is `SND-GRAPH-110`, a handback of a node nobody holds `SND-GRAPH-111`. Each takeover that changed the node and each handback appends a ledger event about the run's item (`workflow.node.taken-over`, `workflow.node.handed-back`: run, node, itemIndex, `by` the caller's actor; not public), which the item's timeline shows; a takeover of a node already held answers the same and appends nothing (#2497). `GET /sindri/workflow-runs/{run}` marks such a node row with `agent`: `waiting`, `taken_over` or null.

**Codes.** `SND-GRAPH-100` the run id is used, `101` no such run, `102` a block could not finish, `103` a prompt answer does not fit its schema, `105` the workflow is not active, `106` a manual payload is not a JSON object, `107` the run already ended, `108` a referenced node output was cut at 64 KB, `109` a run of this workflow can't start the workflow again, `110` the run is not waiting on an agent at that node, `111` that agent node is not taken over.

## Re-run from a step (Workflows page P7, #3204)

A run that ended can start again from one of its steps. The steps before it keep their stored output, so nothing that already ran runs a second time.

- **Route, CLI, tool.** `POST /sindri/workflow-runs/{run}/rerun` with `{"fromNode": "<node id>", "input": {...}}` (`input` is a JSON object, as in the MCP tool; before, the route took it as a string holding JSON, #4205; a value that is not an object is `SND-GRAPH-106`); `sindri wf rerun <run> --from <node> [--input JSON] [--json]`; MCP `workflow_rerun` (`run`, `from`, `input`). An agent session may run all three. The answer is `started`, `queued` or `skipped` like `wf run`, plus the source run, the steps restored and the steps that run again.
- **What happens.** `RerunWorkflowRunService` splits the graph with `RerunPlan.Restored`: the chosen step and every step reachable from it run again. Every other step is restored when the steps it reads from are restored and its rows say it is done: it succeeded, or it was skipped on a branch the restored steps did not take. The walk also writes `skipped` for every step still pending when a failure or a cancel ends the run; such a step runs again. A restored step's branch comes from its own stored row (`RerunPlan.Branches`): a condition's `result`, an ask_owner's `answeredBy` (`timeout` or answered), `out` for every other step. A branching step whose whole row does not say runs again. A restored branching step whose row was cut at 64 KB can't say its branch either, and running it again would ask an ask_owner's question a second time, so the re-run is refused with SND-GRAPH-108; re-run from that step instead. The walk's `GraphRestored` reads the branch the same way from the copied rows, so it never counts a branch as taken because the step after it has no row. A `for_each` is restored only with every body and collect row succeeded or skipped, and the walk then decides its body and collect with it; otherwise the whole loop runs again. A step after a step that runs again runs again too. The restored node runs (every item row of a repeated step) are copied to the new run, and `GraphRunStarter` stores them right after it admits the run, before the walk starts or the run leaves the queue. The walk's patched `GraphRestored` activity marks those steps decided and gives each the branches it took, so the steps that run again read their output as before.
- **The new run.** It walks the source run's revision, not the active one. Its trigger has kind `rerun`, `of` (the source run id), `fromNode`, `by`, the source trigger's fields at the top level (so `{{trigger.payload.repo}}` still resolves) and the first run's trigger as `original`: for a re-run of a re-run it is the source's own `original`, so it stays one level deep (`RerunWorkflowRunService.First`). Manual runs and re-runs share one run number count (`WorkflowRunNumbers.Next`: one more than the highest manual run or re-run of the workflow), so neither starts inside the other's numbers. Hashed connection and call runs start at 100,000,000, so they never hold a counted number. Only two starts at once can read the same number: on a used number the re-run tries the next, up to 50 times, and a manual run tries once more.
- **Refusals.** `SND-GRAPH-101` no such run; `008` the workflow was deleted; `001` the revision has no such step, or the step sits inside a `for_each` body; `125` the run has not ended; `126` a step before the chosen one did not succeed (re-run from that step instead); `127` every step the chosen one reads from was skipped on a branch nobody took (see below); `108` a step to restore had its output cut at 64 KB (a cut step before the chosen one says to re-run from it; a cut step that is not before the chosen one says so, because re-running from it would not run the chosen step); `106` the input is not a JSON object.
- **Re-running a skipped step.** Re-running from a step the source run skipped, on a branch it did not take, runs that step on purpose: the restored steps before it count as having taken their edges into it (`RerunPlan.WithChosen`, from the trigger's `fromNode`), because the owner asked for the step. That holds only while one parent of the chosen step ran. When every parent was skipped (graph: `c` says true to `a` and false to `p`, `p` -> `x`; `c` said true; re-run from `x`), `p` has no output and `{{p.*}}` would resolve empty, so the re-run is refused with `SND-GRAPH-127` (`RerunWorkflowRunService.BranchRefusal`). The refusal names the skipped parent to re-run from instead (`p`: it was skipped because the branch to it was not taken, and re-running from it runs that branch on purpose, since its own parent `c` ran). When the dead branch is nested (`p1` -> `p2` -> `x`, every one skipped), it names the first skipped ancestor whose parent ran (`p1`); when no single ancestor qualifies, the line stays generic: re-run from a step on the branch that ran.
- **Known limits.** `input` is stored in the trigger and nothing in the engine reads it yet. A step inside a `for_each` body can't be the start: re-run from the `for_each` step. Restored steps write no `workflow.node.finished` events, so the Runs view's last-step field does not move for them. The mode of a test run is not carried over until the test-mode work (P4a) lands.

## The `run_agent` block (PR 7)

One node run is one agent session on the existing launcher and agent runtime. Config: `repo` and `prompt` (required); `readOnlyRepos`, `agent` (`claude-code`, `codex` or `cursor`; the profile's `workflow.agent.target` when left out), `outputs` (declared fields: `{"drifted":"boolean","summary":"string?"}`; a name is a lowercase letter then letters and digits, a type is `string`, `number`, `boolean`, `object` or `array`, and a trailing `?` makes it optional), `timeout` (`45m`; default `2h`), `connections` and `resources` (`wall`). The node's outputs are the declared fields plus `status`, `branch`, `commit`, `sessionId` and `usage` (`{tokens, emptyDiff}`).

**Launch.** `NodeRunAddress` (workflow, run, node, item index) replaces the Step's instance and run token in `LaunchRequest` (`Node`, `Task`; an attended open keeps both null). The launch id is `LCH-` and 32 hex digits of SHA-256 of `agent-launch:<run>:<node>:<index>`, so a replay reuses the session. A run id holds `:` and `@`, so the session name stays `snd-<launch id>` and the branch is `wf/<slug>/<revision>-<occurrence>-<node>` (`-<index>` after it for an item of a `for_each`): the node is in it because two agent nodes of one run need two branches. The worktree is `<repo>/.claude/worktrees/wf-<slug>-<short run>`, started from the repo's default branch; the checked-out branch is `wf/<slug>/<short run>/work` and the session may write refs under `wf/<slug>/<short run>/` only (its own node run; the sandbox makes that path a ref directory, so the branch sits one level inside it). The sandbox adds the host group `github` for a node that names a repo, plus each named connection's group once PR 10 supplies them (`GroupsInput.AddGroups`). A run no item started gets an item id made from its run id.

**Wait.** `GraphWorkflow` runs the node itself (`GraphWorkflow.Agent.cs`): `LaunchAgentNode`, then `WatchAgentNode` (a heartbeating activity) raced against the node's timeout, then `CloseAgentNode` (usage, diff signal, HEAD, stop). A takeover (`agent-takeover` update) stops the timeout clock, a handback (`agent-handback`) starts it again with what was left, and the idle sweep leaves a held session alone (`ILaunchFiles.MarkTakenOver`). The sandbox wall stops too: the session's watchdog counts only unsuspended time, and `LedgerRunSuspensions` reads the node's `workflow.node.taken-over` and `workflow.node.handed-back` rows (about the run's item) as a suspension, so a takeover longer than the wall is not killed as `limit`. A session that ends without a report fails the node with `exit`, `lost`, `idle` or `limit` (a limit, or a report the engine refused it for, never recovers); the timeout fails it with `timeout`. A cancel stops the session.

**Report.** The agent reports with `sindri workflow complete <run id> --run <token> --outcome done --output fact:<field>=<file>...` (the exact line is in its brief); the token is `<node position>.<item index + 1>` (`RunAgentWire`). `CompleteStepService` finds the node, decides who may report it (`AgentReportAuthority.DecideNode`: the launched session with its launch secret, or the human of a taken-over node; a wrong secret is `SND-AUTH-003`), checks the report against the declared outputs (`RunAgentReport`: a missing, undeclared or mistyped field is refused and named, and the agent may report again; a string output is the file's text, any other type is JSON) and sends the values to the run's workflow (`agent-report` update). A session that ended unreported completes the node from `.sindri-out` only when every declared output (except optional ones) is there (`AgentRecovery.DecideNode`).

**Agent state events (#3151).** Each `run_agent` node run writes a `workflow.node.agent.changed` ledger event about the run's item every time its session changes state, waiting-on text or last tool (`NodeAgentRecorder`). The Agents page reads these to show a workflow session as a row. The public shape carries only `runId`, `nodeId` and `itemIndex`; the full payload (`WorkflowNodeAgentChangedPayload`) is private. The private payload has these fields: `runId`, `nodeId`, `itemIndex` (0 outside a loop), `state` (below), `waitingOn` (text or null), `lastTool` and `lastToolAt` (or null), `launchId` (null before launch), `branch`, `deadline` (null before launch) and `by` (who recorded it). The idempotency key is `workflow.node.agent.changed:<run>:<node>:<item index>:<sequence>`. The sequence is counted in the workflow, so a replay writes the same keys and the ledger keeps each event once. A history recorded before the events (patch `node-agent-events-1`) writes none.

| `state` | When | `waitingOn` |
|---|---|---|
| `waiting-for-slot` | the launch cap is full; the node's timeout has not started | `launch slot (waiting)` (no queue position yet) |
| `launching` | the launch is under way | null |
| `working` | the session runs | null |
| `waiting-on-you` | the session's aw:fleet state file says it asks for an approval | `permission prompt` |
| `ended-succeeded`, `ended-failed`, `ended-timed-out` | the node finished; the failure text picks one (`timeout:` gives timed out) | null |
| `limited` | defined, not recorded yet | |

The workflow watches the permission prompt with one long call at a time (activity `AskingAgentNode`, patch `node-agent-ask-1`), beside the wait on the session. The activity finds the session through the launch's row and reads that session's aw:fleet state file every 2 s, heartbeating on each read. It takes the ask the workflow last saw (tool and time, or none) and returns as soon as the file says something different, or after a 10 minute cap with nothing new. The workflow records the change and starts the next call. It stops the call when the wait ends (report, session end, timeout) and while a person holds the node. One call per cap, not one per few seconds, keeps a 2 h node to about a dozen history events for the ask, far from Temporal's 51,200 limit. A read that fails counts as no ask. A call that fails ends the watching and the node goes on as working. The node's timeout is exact: it is not cut into slices. `lastTool` and `lastToolAt` are the tool that approval asks for and when the hook saw it. They stay on the events after the person answers the prompt. The tool name is dropped from the event when it does not match the hook's own pattern (letters, digits, `_ . : -`, at most 64 characters), and the event writer scrubs it like `waitingOn`. They are null until the first approval, because the hook records no tool for a working session: PostToolUse writes nothing unless the file says asking (`config/hooks/fleet-state.sh`). A lane row has the same rule, so a lane row and a workflow row never disagree (Agents page spec §3.1 note, open question 3). Each event links the session: `launchId` (from launch on), `branch` (the branch the session may push) and `deadline`. The deadline is launch time plus the node's timeout. The clock stops while a person holds the node, so a handback moves the deadline to now plus the time left and writes a new `working` event that carries it.

A cancelled run writes `ended-failed` as its last event for the node (written detached, so the cancel does not cancel it).

## Flow blocks (agent-first PR 9, part C)

Five more blocks, each with an example spec page under `docs/workflows/blocks/`: [`only_new`](../workflows/blocks/only_new.md), [`create_item`](../workflows/blocks/create_item.md), [`run_workflow`](../workflows/blocks/run_workflow.md), the [`workflow_call`](../workflows/blocks/workflow_call.md) trigger and [`http`](../workflows/blocks/http.md). Registered by `GraphBlockRegistry.FlowBlocks`.

**`workflow_result`** ([page](../workflows/blocks/workflow_result.md)) reads a finished run and starts nothing: config `workflow` and `run` (`latest-succeeded`, the default, or a run id of that workflow); outputs `runId`, `status`, `finishedAt`, `outputs` (the run's result, see "The run's result"). `WorkflowResultBlock` (Core) reads through `IGraphResultPort`; `GraphRunResults` (Workflows) answers it from the workflow and run stores, looking for the latest succeeded run among the workflow's 1,000 newest. A run id that isn't that workflow's, a run not finished, or a workflow with no succeeded run is `SND-GRAPH-124`. Registered by `GraphBlockRegistry.ResultBlocks`.

- **After-success hook.** `IGraphBlock.Commit` runs after a node's `Execute` succeeded (in `GraphBlockActivities`); a failure there fails the node. `only_new` records its keys there, so a node that fails leaves its items unseen. It runs at the node, not at the end of the run: a later node that fails does not give the items back.
- **`workflow.id`.** A node may read `{{workflow.id}}` (with `name`, `runId` and `revision`). `only_new` keys its memory by it.
- **Call guards** (`Core/Graph/Guards`). A run is depth 0, the run it calls is 1. `workflow.callDepth` (profile, default 3, 1 to 10): depth 3 is allowed, 4 is `SND-GRAPH-120`. A run may not start its own workflow, nor the workflow named by its trigger's `parentRun`: `SND-GRAPH-121`. The callee's trigger is `{"kind":"workflow_call","payload":...,"callDepth":N,"parentRun":"<run>","by":"workflow:<slug>"}` and its run id's occurrence is a hash of caller run, node and item index (from 100,000,000 up), so a node that runs twice starts one callee run. Seam: `CallDepth.Next(..., launchLineage)` is where PR 7's launch-based self-start check plugs in.
- **Profile keys.** `workflow.http.hosts` (bare lower-case host names, at most 50; none listed refuses every request) and `workflow.callDepth`; both are approval-relevant (`ProfileDiff`).
- **Seen memory.** `workflow_seen(workflow_id, key, first_seen_at)` in ledger.db (migration 0014; renumbered at the final main merge). Read only by `only_new`.
- **Codes.** `SND-GRAPH-120` call too deep, `121` self-start, `122` an http request refused before it was sent, `123` an http request that got no acceptable answer.

## Code block (agent-first PR 9c)

The [`code`](../workflows/blocks/code.md) block runs a C# method body (`GraphBlockRegistry.CodeBlocks`). `Core/Graph/Blocks/CodeBlock` reads the config and calls `ICodeRunPort`; `Infrastructure/Workflow/Code` holds the adapter: `CodeCompiler` (Roslyn, fixed references), `CompiledCodeCache` (`<home>/code-cache/<sha256>.dll`; the validator's `CodeChecker` checks through it too, so a validated body is already compiled when it runs, and a failing body's errors stay in memory, up to 256 bodies; every compile takes the caller's token, #2727; a hit marks the file used, and each new assembly removes the ones unused for 30 days, the least recently used past 1000 files and `.tmp` leftovers older than an hour, never a file used in the last hour, #2747), `CodeRunSandbox` (the `ProcessSandboxRequest`: no host group, the repo in `ReadOnlyPaths`) and `CodeRunnerProcess`, which starts the `sindri-code-runner` binary that sits beside `sindri` (the framework-dependent `Aw.Sindri.CodeRunner` project, argv: assembly path and `--memory-mb`; stdin the input, stdout the result; exit 2 unusable, 3 memory, 4 the code threw). The assembly is copied into the run's private `/tmp`, so the sandbox needs no other path to it.

- **Validation.** `GraphValidationContext.CodeCheck` (set from `ICodeCheck` by `ProfileWorkflowChecker` and `sindri wf validate`) compiles every `code` node: `GRAPH-CODE-001` with line and column. The `code` field is not scanned for `{{ }}` references and is read from the raw config at run time.
- **Memory.** `memoryMb` is a .NET heap cap the runner sets on itself (`GCHeapHardLimit`), not a cgroup limit.
- **Smoke.** `src/Sindri/aot-smoke/run.sh` publishes `Aw.Sindri.CodeRunner` into the folder of the published host, makes and runs a one-node Code workflow (`return new JsonObject { ["sum"] = 1 + 2 };`) with `wf create`, `activate`, `run` and `run-show`, and pins the line `code-node: {"sum":3}` in `expected.txt`.
- **Codes.** `SND-CODE-001` does not compile, `002` timeout, `003` memory, `004` threw or not JSON, `005` runner could not start.

## Reading workflows and runs (PR 8a)

Four reads, each one slice (`Features/ListWorkflows`, `GetWorkflow`, `ListWorkflowRuns`, `GetWorkflowRun`) behind an HTTP route, an MCP tool and a `sindri wf` verb. They read the routes and never start the control plane; any caller may use them.

| Read | Route | MCP tool | CLI |
|---|---|---|---|
| The workflows that are not archived, each with its trigger in words and `nextRunAt` | `GET /sindri/workflows` | `workflow_list` | `sindri wf list [--json]` |
| One workflow: the revision it runs (the newest when off), the spec, the spec hash, who saved the newest revision, node positions, the next run time of an active schedule (`nextRunAt`) | `GET /sindri/workflows/{slug}` | `workflow_get` (`slug`) | `sindri wf show <slug> [--json]` |
| A workflow's runs, newest first, 50 a page, filtered by status and start time, paged by `before` | `GET /sindri/workflows/{slug}/runs[?status=&since=&until=&before=]` | `workflow_runs` (`slug`, `status`, `since`, `until`, `before`) | `sindri wf runs <slug> [--status S] [--since T] [--until T] [--before R] [--json]` |
| One run with its trigger, outputs and node runs | `GET /sindri/workflow-runs/{run}` | `workflow_run_get` (`run`) | `sindri wf run-show <run> [--json]` |

`nextRunAt` is the soonest tick of the active revision's schedule trigger after now, computed by one Core helper (`Core/Graph/Schedule/WorkflowNextRun.cs`), so the list row and the single read always agree. It is null when the workflow is off, archived, run by hand or by an event, or when its schedule has no tick (#3756); `sindri wf show` prints a `next run` line only when it is set.

An unknown slug is `SND-GRAPH-008` (404), an unknown run `SND-GRAPH-101`, a slug or run id of the wrong shape invalid input. "Who changed it last" is the `createdBy` of the newest revision (`owner` or `session:<id>`). Node payloads the store cut at its size limit come back as a string. `truncated: true` on the node run means its output was cut; a cut input alone leaves it false. Over MCP, names, descriptions, node titles and every string of a node config (prompts), failure text, trigger payloads, outputs and node inputs and outputs are fenced as untrusted. `IWorkflowRunStore.Recent` is the new store read behind the run list.

### Filtering and paging the runs read (#4334)

The runs read takes four optional values, the same on the route's query, the MCP arguments and the CLI flags. The store applies them in its SQL (`IWorkflowRunStore.Find`), before the page cap, so a filter reaches runs older than the newest 50.

- `status`: comma-separated words, or the parameter repeated (`?status=failed&status=cancelled` reads like `failed,cancelled`). `running` keeps queued, running and waiting runs; `ended` and `succeeded` keep succeeded runs; `failed`, `cancelled`, `queued` and `waiting` keep that status.
- `since` and `until`: ISO-8601 instants on the start time, `since` inclusive and `until` exclusive. A date alone is its midnight UTC. A `+` offset may be sent unencoded (`since=2026-11-25T10:00:00+01:00`): the query string turns it into a space, and a single space right before an `hh:mm` offset at the end is read as the `+`.
- `before`: a run id. The page holds the runs after it in the list order. The answer's `next` is the last run of the page while `more` is true, so `before=<next>` reads the older page.

A value the read can't parse is `SND-GRAPH-134` (400), and so is a `before` run of another workflow; an unknown `before` run is `SND-GRAPH-101`. `sindri wf runs` sends the flags unchecked and prints the next page's command under a page with `more`.

### The fields the list row and the runs table read (#3683, #3756)

The list row, the run list and the run read carry the fields below on top of the ones above. The MCP tools answer the same JSON as the routes. The MCP tools also fence `workflows[].trigger` and `runs[].result` as untrusted (`Host/Mcp/FencePlan.cs`): a cron with no words comes back as written, and a result line is error or output text an agent or a service wrote.

**`GET /sindri/workflows`, `workflow_list`: each row** (`Features/ListWorkflows/WorkflowSummary.cs`).

| Field | Type | What it holds | When it is empty |
|---|---|---|---|
| `trigger` | string | The trigger in words (`TriggerWords.Of`): `By hand`, `Every 20 min`, `Every day 7:00`, `Every weekday 7:00` (weekdays as `1-5` or `MON-FRI`), `N schedules` for a node with more than one cron, `On a schedule` for a schedule node with no cron. A cron it has no words for comes back as its text; any other trigger as its node type (`connection_event`, `workflow_call`). The trigger node is the first node no edge leads into, read from the active revision, else the newest | `""` when the graph has no trigger node |
| `paused` | bool | True when the workflow was on once and is off now (`workflow.was_activated` set by an activate, kept by a pause). A workflow never turned on is off, not paused | Always present. A workflow that was off before migration 0023 reads `false` |
| `nextRunAt` | timestamp or null | The soonest tick of the active schedule, the same value as `GET /sindri/workflows/{slug}` | Null when the workflow is off, archived, run by hand or by an event, or its schedule has no tick |
| `lastRun` | object or null | The newest run of any status: `run` (the run id), `status`, `at` (when the store recorded the run) | Null when the workflow never ran |
| `recent` | array | The newest 10 runs of any status, newest first: `run`, `status`, `startedAt`, `seconds` (null while the run has not ended) | `[]` when the workflow never ran |

```json
{
  "workflows": [
    {
      "slug": "morning-check",
      "trigger": "Every weekday 7:00",
      "paused": false,
      "nextRunAt": "2026-01-02T07:00:00.0000000Z",
      "lastRun": { "run": "morning-check@1:2", "status": "running", "at": "2026-01-01T07:00:00.0000000Z" },
      "recent": [
        { "run": "morning-check@1:2", "status": "running", "startedAt": "2026-01-01T07:00:00.0000000Z", "seconds": null },
        { "run": "morning-check@1:1", "status": "succeeded", "startedAt": "2025-12-31T07:00:00.0000000Z", "seconds": 42.5 }
      ]
    }
  ]
}
```

**`GET /sindri/workflows/{slug}`, `workflow_get`** (`Features/GetWorkflow/GetWorkflowResponse.cs`).

| Field | Type | What it holds | When it is empty |
|---|---|---|---|
| `nextRunAt` | timestamp or null | The soonest tick of the active revision's schedule, whichever revision the read shows (`?revision=latest` or a number) | Null in the same cases as the list row |

```json
{ "slug": "morning-check", "revision": 1, "activeRevision": 1, "nextRunAt": "2026-01-01T07:00:00.0000000Z" }
```

**`GET /sindri/workflows/{slug}/runs`, `workflow_runs`: each run** (`Features/ListWorkflowRuns/WorkflowRunSummary.cs`).

| Field | Type | What it holds | When it is empty |
|---|---|---|---|
| `trigger` | string | What started the run (`RunWords.TriggerWord`): `manual` (the app or HTTP), `cli` (`sindri wf run`, stored as `by: "human:cli"`), `agent` (a stored `manual` trigger by `session:<id>`) or `schedule`. Any other stored kind (`workflow_call`, `connection_event`) comes back as stored | `unknown` when the stored trigger has no `kind` |
| `seconds` | number or null | `endedAt` minus `startedAt`, in seconds | Null while the run has not ended |
| `result` | string or null | One line for the table (`RunWords.Result`): the first line of `error` for a failed or cancelled run, the outputs as compact JSON for a succeeded run, cut at 120 characters with a trailing `…` | Null for a queued, running or waiting run, a failed run with no error, or a succeeded run with no outputs |

```json
{
  "slug": "morning-check",
  "runs": [
    { "id": "morning-check@1:3", "status": "running", "trigger": "agent", "seconds": null, "result": null },
    { "id": "morning-check@1:2", "status": "failed", "trigger": "schedule", "seconds": 12.0, "result": "node 'agent' failed" },
    { "id": "morning-check@1:1", "status": "succeeded", "trigger": "cli", "seconds": 42.5, "result": "{\"summary\":\"no drift\"}" }
  ],
  "more": false
}
```

**`GET /sindri/workflow-runs/{run}`, `workflow_run_get`** (`Features/GetWorkflowRun/GetWorkflowRunResponse.cs`, `NodeRunView.cs`). The `trigger` object stays as it was.

| Field | Type | What it holds | When it is empty |
|---|---|---|---|
| `triggerKind` | string | The same word as the run list's `trigger` | Always present |
| `nodes[].seconds` | number or null | How long the node row ran: `ended_at` minus `started_at` of `workflow_node_run` (migration 0023). The store stamps `started_at` when it writes a `running` row and `ended_at` on the final write, with its own clock; a later write keeps the first `started_at` (`Core/Graph/Runs/NodeRunTimes.cs`) | Null while the node runs, for a row written before migration 0023, and for a row written once at its end (`skipped` rows, `collect` rows) |

```json
{
  "id": "morning-check@1:1",
  "status": "succeeded",
  "trigger": { "kind": "manual", "by": "human:cli" },
  "triggerKind": "cli",
  "nodes": [
    { "node": "start", "itemIndex": 0, "status": "succeeded", "seconds": null },
    { "node": "agent", "itemIndex": 0, "status": "succeeded", "seconds": 41.8 }
  ]
}
```

## Writing workflows (PR 8b)

Seven writes, each one slice (`Features/CreateWorkflow`, `ApplyWorkflowOps`, `ReplaceWorkflow`, `ValidateWorkflow`, `ActivateWorkflow`, `PauseWorkflow`, `DeleteWorkflow`) behind a route, an MCP tool and a `sindri wf` verb. `workflow_run` and `workflow_cancel` are the existing run and cancel slices (PR 6) as tools. Every write is a catalog row (`Host/Catalog/WorkflowRows.cs`) an agent session may run, except activate and pause: those are the owner's, and a session is refused with `SND-AUTH-002` (the check sits in `ActivateWorkflowService` and `PauseWorkflowService`, so the route, the MCP tool and the CLI share it; the rows have `SessionOk: false`). The CLI verbs start the control plane first, like `wf run`.

Create, edit by ops and replace are registered with `AddUngatedCommand` (#3571): the draft check, which compiles every code node (seconds on a cold start), runs before the command gate, and each service takes the gate (`ICommandGate`) only around the write. Inside the gate it reads the workflow again, so a revision that moved meanwhile is `SND-GRAPH-007` with the current revision, and a gate busy for 5 s is `SND-API-009`. The audit row is the same as for a gated command.

| Write | Route | MCP tool | CLI |
|---|---|---|---|
| Make a workflow (off, revision 1) from a spec or a starter template | `POST /sindri/workflows` | `workflow_create` | `sindri wf create <slug> (--template <name> \| --file <spec.json>)` |
| Edit by ops against `baseRevision` | `POST /sindri/workflows/{slug}/ops` | `workflow_apply_ops` | `sindri wf edit <slug> --base <n> (--ops <json> \| --ops-file <file>)` |
| Replace the whole graph against `baseRevision` | `PUT /sindri/workflows/{slug}` | `workflow_replace` | `sindri wf edit <slug> --base <n> --file <spec.json>` |
| Check as for activation (writes nothing) | `POST /sindri/workflows/validate` | `workflow_validate` | `sindri wf validate --slug <slug> [--revision <n>]` (a file still runs in process) |
| Turn triggers on at a revision (newest by default) | `POST /sindri/workflows/{slug}/activate` | `workflow_activate` | `sindri wf activate <slug> [--revision <n>]` |
| Turn triggers off; revisions stay | `POST /sindri/workflows/{slug}/pause` | `workflow_pause` | `sindri wf pause <slug>` |
| Archive (hidden, off for good; revisions and runs stay) | `DELETE /sindri/workflows/{slug}` | `workflow_delete` | `sindri wf delete <slug>` |
| The block types, grouped as the picker shows them (Triggers, Data, AI, Logic, Outputs), with titles, config fields, outputs, branches and examples | `GET /sindri/workflows/blocks` | `workflow_blocks` | `sindri wf blocks` |
| The workflow docs: rank a question, read a page (`--page block-schedule`) or list the pages | `GET /sindri/workflows/docs?question=&page=` | `workflow_docs` | `sindri wf docs [<words>] [--page <slug>]` |
| Start / stop a run | `POST /sindri/workflows/{slug}/runs`, `POST /sindri/workflow-runs/{run}/cancel` | `workflow_run`, `workflow_cancel` | `sindri wf run`, `sindri wf cancel` |

**Docs and blocks.** The pages in `docs/workflows/*.md` are embedded in the binary (`IWorkflowDocs`) and searched by `WorkflowDocSearch`; `blocks` and `docs` are reserved, since the literal routes win over `/workflows/{slug}`. Each block says its picker `Group` and `Title` (`IGraphBlockType`; a block that says nothing is Logic, shown by its type), and the block list is read from the block definitions only. A config field that names a connection carries `services` in the list: the services whose connections fit it (`connection_event`'s `connection`: every service with pollable events, `github`, `linear`, `slack`, `google`), or `[]` for any connection (`call_tool`'s); other fields leave it out. The step sheet's connection picker reads it, so no service is hard-wired in the app (#3704).

**Conflicts.** An edit or replace names the newest revision it read. If a newer one exists, nothing is written and the answer is `409 SND-GRAPH-007` with `currentRevision`; the caller reads the workflow again and retries. A batch of ops applies all or none (`SND-GRAPH-012` names the failing op). Create, edit and replace check the graph in draft mode and refuse blocking findings (`SND-GRAPH` findings); activate checks in activate mode. The checks use the running Sindri's block catalog and the repos of the approved profile (`IWorkflowChecker`, `Host/Commands/ProfileWorkflowChecker.cs`).

**Events.** `workflow.created`, `workflow.revised`, `workflow.activated`, `workflow.paused` and `workflow.archived` (payload `WorkflowChangedPayload`: workflow, slug, revision, spec hash, `By`). The write slices check the lease before they touch the store, then append the event through `WorkflowChangeLog` once the store accepts the change; `By` is `owner` or `session:<id>`. The app's "changed by session X" tag reads them.

**Loop guard.** `RunWorkflowService` refuses with `SND-GRAPH-109` (naming the run) on HTTP, CLI and MCP when the calling session was launched by a run of the same workflow (`WorkflowLoopRule`, `IGraphLaunchRuns`). Until PR 9's agent blocks record which launch belongs to which run, `NoGraphLaunchRuns` answers "none", so the guard cannot fire in production yet.

## Connection events

The built-in service `workflow-poll` (`Features/PollConnections`, every 30 seconds) starts runs for `connection_event` nodes. What the node takes, the intervals and what a catch-up does are in [workflows/blocks/poll.md](../workflows/blocks/poll.md).

## Schedules

The built-in service `workflow-schedule` (`Features/WorkflowSchedule`, every minute) starts runs for `schedule` nodes.

1. For each active workflow, take the `schedule` node of its active revision.
2. Compute the latest cron tick after the latest of the revision's save time, the last schedule run's tick and the last marked tick, up to now (`CronExpression.LastTick`, in the node's timezone).
3. Start one run through `IGraphRunStarter` with trigger `{"kind":"schedule","scheduledFor":"<tick>"}`. The starter decides start, queue or skip by the workflow's `concurrency`.

- Three missed days fire one run, for the latest tick only.
- **Occurrence** of a schedule run is the tick in whole minutes since the Unix epoch, so firing the same tick twice is SND-GRAPH-100 (one run row).
- **Marked ticks.** The store keeps the last marked tick per workflow (`IWorkflowRunStore.MarkTick` / `LastTick`, table `workflow_schedule_tick` in the run tables' migration; it only moves forward). The service marks a tick when the starter started it, queued it **or skipped it** (a run was in flight), and when the tick already has a run (SND-GRAPH-100). A skipped tick is therefore done: it is not retried each minute and does not fire late when the run in flight ends; the next cron tick fires on its own. The newest schedule run's occurrence (`IWorkflowRunStore.LatestOccurrence`) stays a second floor.
- **A failed launch does not use up its tick.** When the starter returns any other error (the engine is down, say), the tick is not marked, the pass logs a warning, and the next pass tries the same tick again (as long as it is still the latest). A pass keeps going over the other workflows after one fails and returns the first error at the end.
- **One schedule node.** A workflow has exactly one trigger (`GRAPH-TRIGGER-001`), so the service reads only the first `schedule` node (up to six crons inside it).
- **Known gap:** nothing stores when a revision was activated, so the floor is `WorkflowRevision.CreatedAt`. A tick between saving and activating a revision can fire once on activation.

## Memory blocks (agent-first PR 13 follow-up)

`memory_search` (picker label Recall, group Data), `memory_write` (Remember, Logic) and `memory_forget` (Forget, Logic) reach the Sindri memory stores through `IGraphMemoryPort` (Core), the way `create_item` reaches the item handler. `WorkflowMemoryPort` (Features root, beside the slices because it joins three of them) calls the `SearchMemory`, `WriteMemoryFact` and `ForgetMemoryFact` handlers directly as `service:workflow`; `ScopedMemoryPort` is the engine's singleton and opens a scope per call. Pages: `docs/workflows/blocks/memory_search.md`, `memory_write.md`, `memory_forget.md`. `memory_search` outputs `facts` (the fact texts, an array, so `{{recall.facts}}` renders compact JSON and `prompt` fences it as untrusted) and `mode` (`semantic` or `keyword`, the slice's own words). `memory_write` takes `tags` and `expires` (a length such as `30d`, counted from the write), writes the run's workflow, run and item as the fact's provenance, and outputs `id` and `action` (`added` or `refreshed`). `memory_forget` takes exactly one of `fact` (an id) and `tag` and outputs `count`. Not built yet: the validator does not check that the memory name exists (a missing memory fails the node at run time with the slice's error), and `prompt` / `run_agent` have no `memory` field.

## Parking blocks (PR 9)

`wait` and `ask_owner` park a run on a Temporal timer or wait; they never poll (pages: `docs/workflows/blocks/wait.md`, `docs/workflows/blocks/ask_owner.md`). The block runs as an activity first (`wait` works out its wake-up time, `ask_owner` writes its question), then `GraphWorkflow` parks in `GraphWorkflow.Wait.cs` while the node is still `running`, so a cancel ends the node `cancelled`. `ask_owner` takes its answer as a Temporal Update named `Answer`: its validator refuses an answer when the node is not waiting (`SND-GRAPH-130`) or the answer is not one of the node's choices (`SND-CLI-003`). `AnswerQuestionService` delivers it through `IGraphRunAnswerer` and then records `workflow.answered` with who gave it. The question is a `workflow.question` event and a Needs-you row of kind `question`, cleared when it is answered, when its node ends or when its run ends. Both events are public on the event stream (`GET /sindri/events`) with the payload `{run, node, kind}` and no question, choices, default, answer or answerer (`WorkflowQuestionPublic`, `WorkflowAnsweredPublic`); `kind` is always `question`, the Needs-you row kind the app re-reads, so the row shows and clears live. The ledger keeps the whole payload. New code: `SND-GRAPH-130`.

## Loops (`for_each`, `collect`; PR 9)

Pages: `docs/workflows/block-for_each.md` and `block-collect.md` (top level, so they are embedded and searched like the other `block-*` pages). A `for_each` node and the one `collect` it reaches bound a loop; the loop is every node between them (`LoopBody.Find`).

How it runs, in order:

1. `GraphRunBlock` runs the `for_each` like any block: it checks that `items` is a list of at most `maxItems` and outputs `count` and `batchSize`. A spec whose loop can't be found fails the `for_each` here, before any item.
2. `GraphWorkflow` starts one child workflow, `GraphIteration`, per batch of `batchSize` items (`GraphWorkflow.Loop.cs`), one after the other, so items run in order. The parent holds one entry per batch, never one per item, and the items themselves are never in its history: each activity re-reads the list from the stored output the `for_each` read.
3. `GraphIteration` (`GraphIterationWorkflow.cs`) loads the spec and, for each item of its batch, walks the loop's nodes with the same two rules as the run's walk (`GraphWalkRules`): one `GraphRunBlock` activity per node, a `workflow_node_run` row per node and item. The row's item index is the item's position counted from 1; the `for_each` and `collect` rows of the loop as a whole are at index 0. A failing `stop` node ends the batch and the nodes left for that item are `skipped`. A failing `skip-item` node ends only its item: the nodes left for that item are `skipped`, the batch goes on with its next item, and the batch's result counts the item in `Skipped`.
4. After the last batch the `GraphCollect` activity reads the `collect` rows of items 1 to N, in item order, and writes the `collect` node's own row (`items`, `count`, `skipped`). `skipped` is the sum of the batches' `Skipped` counts, passed in `GraphCollect`; an item a `skip-item` dropped has a `skipped` collect row, so it is not in `items`. Values never pass through workflow history.

Limits: `forEach.maxItems` (profile `workflow.forEach.maxItems`, default 200, at most 10,000; this is the cap, not a default: a node's own `maxItems` can lower it and never raise it, so the effective limit is the smaller of the two, and a list over it fails the `for_each` naming the profile key) and `batchSize` (default 20, at most 100). Temporal stops a workflow at 51,200 events; the parent adds about 12 events per batch (child initiated, started, completed, and the parent's own workflow task events), so 10,000 items at the default batch size are 500 batches, about 6,000 events. A batch's own history grows with its items times the loop's nodes (about 9 events per node per item). `ForEachWorkflowTests.Five_hundred_items_stay_under_the_history_budget` asserts the parent stays under 500 events for 500 items; it measured 298 (25 batches, 2026-11-09).

`GraphRunBlock` reads `IWorkflowRunStore.NodeRunsForItem` (every index 0 row plus the current item's rows), not the whole run, so a loop of N items reads O(N) rows in all. It still re-interpolates the `for_each` config (the whole `items` list) once per call, which costs CPU, not row reads.

**Sizes.** A block's input and output are cut at 64 KB before they enter workflow history (`GraphBlockActivities.Fit`): an activity result over Temporal's 2 MB limit fails the workflow task, which Temporal retries without end. A cut output keeps one extra character so the store still flags it `truncated`; a cut input sets no flag. The `collect` node's own output (all the values together) must also fit in 64 KB: when it does not, `GraphCollect` fails the `collect` and the message names the collected size and the cap (for example 200 items of 400 bytes are about 80 KB).

**Failures in a body node.** A failing body node whose `onFailure` is not `skip-item` (item 25 of a batch of 20 is in the second batch) stops the loop and lands on the `collect` node: the `collect` row is `failed` and the run's error names the item. Only the `collect`'s `onFailure` decides what happens next (`continue` lets the walk go on to the nodes after the loop; anything else fails the run). `ForEachLaterBatchTests` covers item 25 of 30 with the default batch size, both ways.

**Dropping one item (`skip-item`, #3806).** A body node with `onFailure: skip-item` that fails ends only its item (`GraphIterationWorkflow.ItemFailure.cs` `Drops`). Its row is `failed` with its error, the item's later nodes are `skipped`, and no failure goes up: the run's `error` stays empty and the run's status does not change. Batches run one after the other, so a dropped item never cancels another. The `collect` gives the kept items in input order and `skipped: N`; when all N items are dropped, `items` is empty and the walk goes on after the `collect`. `SkipItemInLoopTests` covers one dropped item with one batch and with one batch per item, and every item dropped.

**What a failed node stores.** A failed node's stored output is for the run page, and most blocks store none. A failed `run_agent` stores `{lastRefusedReport, files}` when it has them. `lastRefusedReport` is the last report the engine refused. `files` holds the declared output files the agent left: a file that parses is stored as its JSON value, else as text. The engine reads them when the session exited, went idle or stopped heartbeating before its outputs were complete. A sandbox limit, a lost watch and a timeout store no output.

**Reading a node that failed and continued.** A later node can't read a node that failed with `onFailure: continue`, whatever it stored. When its config reads `{{a.x}}` from such a node, it fails with `SND-GRAPH-131` (`'a' failed and continued, so '{{a.x}}' has no value`), and a condition that reads it fails the same way. The run fails at that reader, not later on a null. A reference to a node that succeeded, or to one a branch skipped, reads as before.
