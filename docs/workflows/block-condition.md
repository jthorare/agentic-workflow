# If

Group: Logic. Type: `condition`. Reads one expression and sends the run down the `true` branch or the `false` branch. Use it to branch on a flag.

## Config

| Field | Type | Needed | Meaning |
|---|---|---|---|
| `when` | text | yes | The expression, such as `ask.urgent`. The conditions page has the language. |

## Hands on

| Field | Type | Meaning |
|---|---|---|
| `result` | boolean | What the expression came to. |

## Outputs

An edge leaves a condition by `true` or `false`, never by `out`. Nodes on the branch not taken are `skipped`.
An edge with no `output` on a condition is refused.

## Example

```json
{
  "version": 1,
  "name": "Notify only when urgent",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "ask", "type": "prompt", "config": { "prompt": "Is this urgent? {{trigger.payload}}", "output": { "type": "object", "required": ["urgent"], "properties": { "urgent": { "type": "boolean" } } } } },
    { "id": "gate", "type": "condition", "config": { "when": "ask.urgent" } },
    { "id": "page", "type": "notify", "config": { "title": "Urgent", "body": "Look at this now." } }
  ],
  "edges": [
    { "from": "start", "to": "ask" },
    { "from": "ask", "to": "gate" },
    { "from": "gate", "output": "true", "to": "page" }
  ]
}
```
