# Runs

A run is one walk through a workflow, started by its trigger or by hand. A run keeps the revision it started with. Saving a new revision never changes a run in flight.

## Run states

| State | Meaning |
|---|---|
| `queued` | Recorded, not started. A `queue` workflow waits for the run before it. |
| `running` | Walking its nodes. |
| `waiting` | Waiting on a person, a timer or an agent. |
| `succeeded` | Every node finished or was skipped. |
| `failed` | A node failed and its `onFailure` was `stop`. |
| `cancelled` | Someone cancelled it. |

## Node states

A node is `running`, `waiting`, `succeeded`, `failed`, `skipped` or `cancelled`.

- A node is `skipped` when it never ran: a branch was not taken, or a failure halted the walk.
- A node with `onFailure` set to `continue` fails without halting the run. Its own outputs are missing, so later nodes read nothing from it.

## What a node run stores

Its input, its output and its error. Each of input and output is stored up to 64 KB. The rest is cut and the row says it was truncated. Free text is scrubbed of secrets before it is stored.

## How the walk goes

1. The trigger node runs and hands on its fields.
2. A node runs once every edge into it comes from a node that has run or been skipped.
3. A branching block says which outputs it took. Edges on other outputs lead to skipped nodes.
4. Cancelling a run marks the running node `cancelled` and the rest `skipped`.
