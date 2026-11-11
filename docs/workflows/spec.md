# The workflow spec

A workflow is one JSON document: a name, a list of nodes and a list of edges. Sindri stores each saved copy as a numbered revision.
You never send positions. Sindri lays the graph out itself.

## Top-level keys

| Key | Needed | What it holds |
|---|---|---|
| `version` | yes | The spec format. Always `1`. |
| `name` | yes | The workflow's name for people. |
| `description` | no | What the workflow is for. |
| `concurrency` | no | How runs overlap: `one-per-workflow` (the default), `queue` or `unlimited`. See the concurrency page. |
| `resources` | no | A named `database`, and a default `sandbox` size (`memory`, `wall`) for agent blocks. |
| `nodes` | yes | The blocks the workflow uses. |
| `edges` | no | The wires between nodes. |

An unknown key anywhere in the document is refused.

## Nodes

A node has an `id`, a `type` and a `config`. It may also have a `title` (what the canvas shows) and `onFailure`.

- `id` is a lowercase letter, then lowercase letters, digits and `_`, at most 64 characters. It is the name references use.
- `trigger`, `item` and `workflow` are reserved. A node cannot use them as an id.
- `type` is a block type, such as `schedule`. Each block has its own page.
- `config` holds the block's fields. Any key the block does not list is refused.
- `onFailure` is `stop` (the default: the run halts when this node fails) or `continue` (the run keeps walking).

## Edges

An edge has a `from` node, a `to` node and an optional `output`. With no `output`, the edge leaves by `out`.
A branching block names its own outputs. A `condition` block leaves by `true` or `false`.

## Rules the validator checks

1. The workflow has exactly one trigger block.
2. Every node is reachable from the trigger.
3. There is no loop.
4. A node reads only from nodes upstream of it.
5. Every block type exists and its config fits.
6. A cron text parses.

A draft saves with wiring gaps (no trigger yet, a node not connected). Activation and a run take none.

## A whole spec

This workflow runs at 07:00 on weekdays and sends a notification.

```json
{
  "version": 1,
  "name": "Weekday hello",
  "description": "Says good morning on weekdays.",
  "concurrency": "one-per-workflow",
  "nodes": [
    { "id": "daily", "type": "schedule", "config": { "cron": "0 7 * * 1-5", "timezone": "America/Los_Angeles" } },
    { "id": "hello", "type": "notify", "title": "Say hello", "config": { "title": "Good morning", "body": "Tick for {{daily.scheduledFor}}" } }
  ],
  "edges": [
    { "from": "daily", "to": "hello" }
  ]
}
```
