# Conditions

The `condition` block reads one expression in its `when` field and leaves by `true` or `false`. This page is the expression language.

## Parts

- References: `check.urgent`, `trigger.scheduledFor`. You may wrap one as `{{check.urgent}}`.
- Literals: text in double quotes, numbers, `true`, `false` and `null`.
- Comparisons: `==`, `!=`, `<`, `<=`, `>`, `>=`.
- Logic: `and`, `or`, `not`, and parentheses.
- `exists(check.urgent)`: true when the value is there and is not null.

## Order

Tightest first: a comparison, then `not`, then `and`, then `or`. So `not a == b` means `not (a == b)`.

## Edge cases

- Two values of different types are never equal, and the comparison is false. It never raises an error.
- Any ordering with a null (`<`, `<=`, `>`, `>=`) is false.

## Example

```json
{
  "version": 1,
  "name": "Branch on a flag",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "ask", "type": "prompt", "config": { "prompt": "Is this urgent? {{trigger.payload}}", "output": { "type": "object", "required": ["urgent"], "properties": { "urgent": { "type": "boolean" } } } } },
    { "id": "gate", "type": "condition", "config": { "when": "ask.urgent == true and exists(trigger.payload)" } },
    { "id": "page", "type": "notify", "config": { "title": "Urgent" } }
  ],
  "edges": [
    { "from": "start", "to": "ask" },
    { "from": "ask", "to": "gate" },
    { "from": "gate", "output": "true", "to": "page" }
  ]
}
```

A condition that does not parse is refused when you save or activate, with the column and the fix.
