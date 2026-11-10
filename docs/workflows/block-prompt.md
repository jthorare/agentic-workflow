# Prompt

Group: AI. Type: `prompt`. Ask the model a question and get JSON back, checked against a schema you write.

## Config

| Field | Type | Needed | Meaning |
|---|---|---|---|
| `prompt` | text | yes | What to ask. It may hold references. |
| `output` | object | yes | The shape of the answer: a JSON schema with `type`, `required` and `properties`. |

## Hands on

One field for each key in `output.properties`. A property that is not in `required` may be missing, and the validator knows it.
A property type is `string`, `number`, `integer` (read as number), `boolean`, `object` or `array`.

## What counts as a good JSON answer from the model

1. The answer is a JSON object. A Markdown fence around it is fine.
2. Every `required` field is there.
3. Each field has the type its property names.

A bad answer fails the node with the reason. The reason names the field.

## Notes

- Text that came from a connection is marked untrusted when the prompt is sent. Do not ask the model to obey it.
- A `required` list must hold field names as text. A property `type` must be a type name, or a list of names.

## Example

```json
{
  "version": 1,
  "name": "Ask the model for a summary",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "summarize", "type": "prompt", "config": { "prompt": "Summarize this: {{trigger.payload}}", "output": { "type": "object", "required": ["summary"], "properties": { "summary": { "type": "string" }, "score": { "type": "number" } } } } },
    { "id": "tell", "type": "notify", "config": { "title": "Summary", "body": "{{summarize.summary}}" } }
  ],
  "edges": [
    { "from": "start", "to": "summarize" },
    { "from": "summarize", "to": "tell" }
  ]
}
```
