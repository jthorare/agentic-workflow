# `for_each`

Runs the nodes after it once for every item of a list, in order, until the matching [`collect`](collect.md).

| Config | Type | Required | Meaning |
|---|---|---|---|
| `items` | reference | yes | The list, usually one whole reference such as `{{search.results}}`. Anything that is not a list fails the node |
| `maxItems` | number | no | The most items taken, 1 to 10,000. Default: the profile's `workflow.forEach.maxItems`, then 200. A longer list fails the node before any item runs |
| `batchSize` | number | no | How many items one child workflow runs, 1 to 100. Default: the profile's `workflow.forEach.batchSize`, then 20 |

Outputs: `count` (number of items) and `batchSize`.

Inside the loop:

- `{{item}}` is the item, `{{itemIndex}}` its position counted from 1.
- A node of the loop reads another node of the loop at the same item. A node after the `collect` reads only `collect`'s `items` and `count`, never a node of the loop.
- Every node of the loop has one run per item (`workflow_run_get` lists them with their `itemIndex`). `for_each` and `collect` themselves have one run each, at index 0.
- A node that fails and says `stop` (the default) ends the loop: no later item runs, the `collect` fails with `node '<id>' failed on item N: <why>`, and the run follows the `collect`'s `onFailure`. Say `continue` on a node to let the loop go on without it.

The loop is what lies between the `for_each` and its one `collect`. Loops don't nest (start another workflow with `run_workflow`), and a node of the loop may be fed only from the loop. `sindri wf validate` reports both as `GRAPH-LOOP-001`.

## Example

```json
{ "version": 1, "name": "Summarize new issues",
  "nodes": [
    { "id": "daily", "type": "schedule", "config": { "cron": "0 7 * * 1-5" } },
    { "id": "each", "type": "for_each", "config": { "items": "{{trigger.issues}}", "maxItems": 50 } },
    { "id": "summary", "type": "prompt", "config": { "prompt": "Summarize in one line: {{item}}", "output": { "type": "object", "properties": { "line": { "type": "string" } }, "required": ["line"] } } },
    { "id": "gather", "type": "collect", "config": { "value": "{{summary.line}}" } },
    { "id": "tell", "type": "notify", "config": { "title": "Issues", "body": "{{gather.items}}" } } ],
  "edges": [ { "from": "daily", "to": "each" }, { "from": "each", "to": "summary" }, { "from": "summary", "to": "gather" }, { "from": "gather", "to": "tell" } ] }
```
