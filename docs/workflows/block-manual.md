# When called

Group: Triggers. Type: `manual` (the picker calls it When called; the type name stays `manual`). Starts a run by hand, and hands on the payload it was started with.

## When to use it

Use it for a workflow you start yourself, for a test run, or for a workflow another tool starts on demand. It takes no config.

## Config

None. Write `"config": {}`.

## Hands on

| Field | Type | Meaning |
|---|---|---|
| `payload` | object, may be missing | What the run was started with. Missing when the run was started with nothing. |

Read it as `{{trigger.payload}}`, or by the node's id.

## Example

```json
{
  "version": 1,
  "name": "Echo the payload",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "echo", "type": "notify", "config": { "title": "Started by hand", "body": "Payload: {{trigger.payload}}" } }
  ],
  "edges": [
    { "from": "start", "to": "echo" }
  ]
}
```
