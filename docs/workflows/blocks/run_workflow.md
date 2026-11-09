# `run_workflow`

Starts another workflow's active revision, with a payload. The callee must declare a [`workflow_call`](workflow_call.md) trigger.

| Config | Type | Meaning |
|---|---|---|
| `workflow` | text | The callee's slug. |
| `payload` | object, optional | What the callee's trigger hands on as `payload`. |
| `wait` | true or false, optional | `true` waits until the called run ends (polling, up to `waitSeconds`); the node fails when that run fails or is cancelled. Default: go on at once. A callee that was skipped (it runs one at a time and was busy) is not waited for. |
| `waitSeconds` | number, optional | The longest to wait, 1 to 3500; default 600. When it runs out the node fails and the called run keeps going. |

| Output | Type | Meaning |
|---|---|---|
| `runId` | string | The called run. |
| `status` | string | `started`, `queued` or `skipped`; with `wait`, `succeeded`. |
| `outputs` | any | With `wait`, the called run's outputs: an object keyed by node id, holding what each node that succeeded gave (`{{call.outputs.fetch.items}}`). A node that ran once gives its outputs object; a node that ran per item gives an array of them in item order. Nodes that failed, were skipped or whose output was cut at 64 KB are left out; when the whole object would pass 64 KB it is `null`. |

**Guards.** A run is depth 0 and the run it calls is depth 1. `workflow.callDepth` (profile, default 3): depth 3 is allowed, a call that would be depth 4 is `SND-GRAPH-120`. A run can't start its own workflow, nor the workflow that called it: `SND-GRAPH-121`. The callee's run id is made from the caller's run, node and item index, so a node that runs twice starts one callee run.

```json
{
  "version": 1, "name": "Nightly", "concurrency": "one-per-workflow",
  "nodes": [
    { "id": "daily", "type": "schedule", "config": { "cron": "0 2 * * *" } },
    { "id": "triage", "type": "run_workflow", "config": { "workflow": "triage", "payload": { "source": "nightly" }, "wait": true, "waitSeconds": 300 } },
    { "id": "tell", "type": "notify", "config": { "title": "Triage {{triage.status}}" } }
  ],
  "edges": [ { "from": "daily", "to": "triage" }, { "from": "triage", "to": "tell" } ]
}
```
