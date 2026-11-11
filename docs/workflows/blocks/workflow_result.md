# `workflow_result`

Reads what a finished run of a workflow gave and starts nothing. Use it to pick up the result of a run that was started elsewhere (a schedule, an agent, `run_workflow` without `wait`).

| Config | Type | Meaning |
|---|---|---|
| `workflow` | text | The workflow's slug. |
| `run` | text, optional | `latest-succeeded` (the default): the newest run of that workflow that succeeded, looked for among its 1,000 newest runs. Or a run id such as `triage@1:7`: a finished run of that workflow, with any final status. |

| Output | Type | Meaning |
|---|---|---|
| `runId` | string | The run that was read. |
| `status` | string | `succeeded`, `failed` or `cancelled`. |
| `finishedAt` | string, optional | When the run ended (ISO 8601). |
| `outputs` | any | The run's outputs: an object keyed by node id, holding what each node that succeeded gave (`{{result.outputs.fetch.items}}`). `null` for a run that did not succeed, or when the object would pass 64 KB. |

**Refusals.** A run id that is not one of that workflow's runs, a run that has not finished, and a workflow with no succeeded run fail the node with `SND-GRAPH-124`. A workflow that does not exist is `SND-GRAPH-008`.

```json
{
  "version": 1, "name": "Report", "concurrency": "one-per-workflow",
  "nodes": [
    { "id": "daily", "type": "schedule", "config": { "cron": "0 8 * * *" } },
    { "id": "result", "type": "workflow_result", "config": { "workflow": "triage", "run": "latest-succeeded" } },
    { "id": "tell", "type": "notify", "config": { "title": "Triage last ran {{result.finishedAt}}" } }
  ],
  "edges": [ { "from": "daily", "to": "result" }, { "from": "result", "to": "tell" } ]
}
```
