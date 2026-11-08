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
| `GRAPH-REF-001` / `-002` / `-003` | a reference to a node that is not upstream / to a field the node does not output / to no node |
| `GRAPH-BLOCK-001`, `GRAPH-CONFIG-001` | unknown block type, config that fails the block's schema |
| `GRAPH-CRON-001`, `GRAPH-COND-001` | unreadable cron or timezone, unreadable condition |
| `GRAPH-REPO-001`, `GRAPH-NAME-001` | repo, connection or memory that does not exist (`-002` is the warning when nobody can answer yet) |
| `GRAPH-NODE-001`, `GRAPH-EDGE-001` / `-002` | repeated node id, edge to a missing node, edge by an output the block lacks |

A **draft** save keeps wiring gaps (`GRAPH-TRIGGER-002`, `GRAPH-WIRING-001`); **activation** refuses every error. Warnings never
block. `wf validate` always checks as for activation. The block catalog is empty in this build, so every node type reads as unknown
until the blocks arrive.

## Storage

`workflow` and `workflow_revision` in ledger.db (migration 0008). A save is a new immutable revision with its `spec_hash`;
saving on a revision that is no longer the latest is `SND-GRAPH-007`; `Activate` pins the revision triggers run.

## Error codes

`SND-GRAPH-001..009`, listed in `docs/sindri/errors.md`.
