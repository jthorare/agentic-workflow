# For each

Group: Logic. Type: `for_each`. Runs the nodes after it once for every item of a list, in order, until the matching [`collect`](block-collect.md).

| Config | Type | Required | Meaning |
|---|---|---|---|
| `items` | reference | yes | The list, usually one whole reference such as `{{search.results}}`. Anything that is not a list fails the node |
| `maxItems` | number | no | The most items taken, 1 to 10,000. Default: the profile's `workflow.forEach.maxItems`, then 200. The profile value is the cap: a node can lower it and never raise it (the smaller of the two applies). A longer list fails the node before any item runs |
| `batchSize` | number | no | How many items one child workflow runs, 1 to 100. Default: the profile's `workflow.forEach.batchSize`, then 20 |

Outputs: `count` (number of items) and `batchSize`.

Inside the loop:

- `{{item}}` is the item, `{{itemIndex}}` its position counted from 1.
- A node of the loop reads another node of the loop at the same item. A node after the `collect` reads only `collect`'s `items` and `count`, never a node of the loop.
- Every node of the loop has one run per item (`workflow_run_get` lists them with their `itemIndex`). `for_each` and `collect` themselves have one run each, at index 0.
- A node that fails and says `stop` (the default) ends the loop: no later item runs, the `collect` fails with `node '<id>' failed on item N: <why>`, and the run follows the `collect`'s `onFailure`. Say `continue` on a node to let the loop go on without it.

The loop is what lies between the `for_each` and its one `collect`. Loops don't nest (start another workflow with `run_workflow`), and a node of the loop may be fed only from the loop. `sindri wf validate` reports both as `GRAPH-LOOP-001`. A `run_agent` node can't sit in the loop (an agent session doesn't run once per item): that is `GRAPH-LOOP-003`, and a run that reaches such a `for_each` fails it before any item runs. Run the agent in another workflow with `run_workflow`. A `wait` can sit in the loop and sleeps once per item; an `ask_owner` can too and asks once per item (answer it with `--item`, see the `ask_owner` page).

## Example

```json
{ "version": 1, "name": "Summarize new issues",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "each", "type": "for_each", "config": { "items": "{{trigger.payload}}", "maxItems": 50 } },
    { "id": "summary", "type": "prompt", "config": { "prompt": "Summarize in one line: {{item}}", "output": { "type": "object", "properties": { "line": { "type": "string" } }, "required": ["line"] } } },
    { "id": "gather", "type": "collect", "config": { "value": "{{summary.line}}" } },
    { "id": "tell", "type": "notify", "config": { "title": "Issues", "body": "{{gather.items}}" } } ],
  "edges": [ { "from": "start", "to": "each" }, { "from": "each", "to": "summary" }, { "from": "summary", "to": "gather" }, { "from": "gather", "to": "tell" } ] }
```
