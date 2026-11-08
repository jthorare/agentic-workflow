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
- Config strings take `{{node.field}}`, `{{trigger.*}}`, `{{item}}` and `{{workflow.*}}`. A string that is exactly one
  reference keeps the referenced value's type; inside other text, objects and arrays render as compact JSON and null as nothing.

## Validation (`GraphValidator`, `sindri wf validate <file> [--json]`)

| Code | Finding |
|---|---|
| `GRAPH-TRIGGER-001` / `-002` | more than one trigger (names all) / none |
| `GRAPH-WIRING-001` | a node no trigger reaches |
| `GRAPH-CYCLE-001` | a loop, with its path in spec order (`a -> b -> c -> a`) |
| `GRAPH-REF-001` / `-002` / `-003` / `-004` | a reference to a node that is not upstream / to an output name the node does not have / to no node / a `{{` that is never closed |
| `GRAPH-BLOCK-001`, `GRAPH-CONFIG-001` | unknown block type, config that fails the block's schema |
| `GRAPH-CRON-001`, `GRAPH-COND-001` | unreadable cron or timezone, unreadable condition |
| `GRAPH-REPO-001`, `GRAPH-NAME-001` | repo, connection or memory that does not exist (`-002` is the warning when nobody can answer yet) |
| `GRAPH-NODE-001` / `-002`, `GRAPH-EDGE-001` / `-002` | repeated node id / a node id that is `trigger`, `item` or `workflow` (reserved reference roots), edge to a missing node, edge by an output the block lacks (an edge with no `output` leaves by `out`) |

`GRAPH-TRIGGER-001` / `-002`, `GRAPH-CYCLE-001` and `GRAPH-WIRING-001` are node-level findings: they name nodes and have no field.

The validator checks that a referenced output exists, not its type; type checks come with the typed block configs in PR 6.

A document with a missing or null node, edge, `id`, `type`, `config`, `from` or `to` is refused by `GraphJson.Parse` as `SND-GRAPH-001` naming the JSON path (such as `nodes[2].type`); neither `Parse` nor `wf validate` throws on any text.

`wf validate` reads `config.when` with `ConditionExpression` (references, comparisons, `and`/`or`/`not`, `exists(x)`); a parse error comes back with its column as `GRAPH-COND-001`, and the references it reads are checked for being upstream like any other.

A **draft** save keeps wiring gaps (`GRAPH-TRIGGER-002`, `GRAPH-WIRING-001`); **activation** refuses every error. Warnings never
block. `wf validate` always checks as for activation. The block catalog is empty in this build, so every node type reads as unknown
until the blocks arrive.

## Storage

`workflow` and `workflow_revision` in ledger.db (migration 0008). A save is a new immutable revision with its `spec_hash`;
saving on a revision that is no longer the latest is `SND-GRAPH-007`; `Activate` pins the revision triggers run.

## Error codes

`SND-GRAPH-001..009`, listed in `docs/sindri/errors.md`.

## Runs (PR 6)

A run is a row in `workflow_run` (ledger migration `0009_workflow_runs`; the number is renumbered at the final main merge). Its id is `<slug>@<revision>:<occurrence>`, the primary key, so a second insert of the same id is refused with `SND-GRAPH-100`. Statuses: `queued`, `running`, `waiting`, `succeeded`, `failed`, `cancelled`. `workflow_node_run` holds one row per node and item index; a node's input and output are each cut at 64 KB (UTF-8) and `truncated` is set.

`GraphRunStarter.Start` turns a trigger into a run by the workflow's `concurrency`: `one-per-workflow` (the default) skips and logs a trigger while another run is in flight, `queue` records it as `queued` without starting it, `unlimited` starts every trigger. A failed launch marks the run `failed`.
