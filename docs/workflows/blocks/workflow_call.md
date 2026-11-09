# `workflow_call` (trigger)

The trigger a workflow declares to be started by another workflow's [`run_workflow`](run_workflow.md). A workflow has exactly one trigger, so it is this one or another, not both. It has no config.

| Output | Type | Meaning |
|---|---|---|
| `payload` | object | What the caller sent. |
| `parentRun` | string | The calling run's id. |
| `callDepth` | number | How deep this run is (1 for a run a top-level run called). |
| `by` | string | `workflow:<caller's slug>`. |

```json
{
  "version": 1, "name": "Triage", "concurrency": "unlimited",
  "nodes": [
    { "id": "start", "type": "workflow_call", "config": {} },
    { "id": "tell", "type": "notify", "config": { "title": "Triage for {{start.payload.source}}", "body": "called by {{start.parentRun}}" } }
  ],
  "edges": [ { "from": "start", "to": "tell" } ]
}
```
