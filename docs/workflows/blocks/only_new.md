# `only_new`

Passes on the items of a list that this workflow has not passed on before.

| Config | Type | Meaning |
|---|---|---|
| `items` | reference to a list | The list, such as `{{search.results}}`. Anything else fails the node. |
| `key` | text, optional | The field of each item that names it (`id`, or `user.login` for a nested one). Without it the item itself is the key: text or a number as it is, an object as its compact JSON. |

| Output | Type | Meaning |
|---|---|---|
| `items` | array | The items not seen before, in their order. A key repeated inside the list counts once. |
| `count` | number | How many items that is. |
| `skipped` | number | How many were dropped (seen before, or repeated in this list). |

The keys are remembered **per workflow**, not per run (`workflow_seen` in ledger.db). They are written only when the whole run succeeded (`IGraphBlock.Commit`, called at the end of the run from each succeeded node's stored output), so a run that fails at any node leaves its items unseen and they come back on the next run. If this node's output was cut at 64 KB the keys are not written, so the items come back too (a repeat, never a loss). Inside a `for_each` body the node runs once per item, and each item's keys are written at the end of the run the same way.

```json
{
  "version": 1, "name": "New issues", "concurrency": "one-per-workflow",
  "nodes": [
    { "id": "start", "type": "workflow_call", "config": {} },
    { "id": "fresh", "type": "only_new", "config": { "items": "{{trigger.payload.issues}}", "key": "number" } },
    { "id": "tell", "type": "notify", "config": { "title": "{{fresh.count}} new issues" } }
  ],
  "edges": [ { "from": "start", "to": "fresh" }, { "from": "fresh", "to": "tell" } ]
}
```
