# `wait`

Sleeps for a length of time, or until a time of day, then lets the run go on. It runs as a Temporal timer: nothing polls, the run costs nothing while it sleeps, and cancelling the run ends the sleep at once.

## Config

| Field | Type | Meaning |
|---|---|---|
| `duration` | text | How long to wait: `90s`, `15m`, `2h`, `1d`, or a run of them such as `1h30m`. |
| `until` | text | A time to wait for (see below). |
| `timezone` | text, optional | An IANA name such as `America/Los_Angeles`. Reads a local `until`; UTC when missing. |

Write exactly one of `duration` and `until`. Both take `{{...}}` references.

`until` is one of:

- a local date and time, `2026-03-08T09:00` (seconds are allowed), read in `timezone`;
- a bare clock time, `07:00`, which means the next 07:00 in `timezone`;
- a time with an offset, `2026-03-08T09:00:00Z`, taken as written (`timezone` is ignored).

An `until` in the past wakes at once. At a daylight-saving change, a local time the clock skips (02:30 on the spring-forward day) wakes at the first instant after the gap (03:30), and one that happens twice (01:30 on the fall-back day) wakes at the first of the two.

## Outputs

| Field | Type | Meaning |
|---|---|---|
| `wakeAt` | string | The instant the wait ends, ISO 8601. |
| `waitedSeconds` | number | How long the node slept, in seconds. |

## Example

A workflow that reminds the owner at 07:00 Los Angeles time, whatever time the trigger fired:

```json
{ "version": 1, "name": "Morning nudge",
  "nodes": [ { "id": "start", "type": "manual", "config": {} },
             { "id": "hold", "type": "wait", "config": { "until": "07:00", "timezone": "America/Los_Angeles" } },
             { "id": "tell", "type": "notify", "config": { "title": "Good morning", "body": "Woke at {{hold.wakeAt}}" } } ],
  "edges": [ { "from": "start", "to": "hold" }, { "from": "hold", "to": "tell" } ] }
```

## Stops

- Cancelling the run (`sindri wf cancel <run>`) ends the wait; the node is `cancelled` and the rest `skipped`.
- A `duration` or `until` that can't be read fails the node with `SND-GRAPH-102` and the reason; `sindri wf validate` finds the same mistake before the run when it has no reference in it.

A `wait` node can't sit inside a `for_each` loop (`GRAPH-LOOP-004`): put the per-item part in its own workflow and call it with `run_workflow`.
