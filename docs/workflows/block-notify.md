# Send notification

Group: Outputs. Type: `notify`. Sends the owner a notification: it writes a row on the Needs-you page.

## Config

| Field | Type | Needed | Meaning |
|---|---|---|---|
| `title` | text | yes | The row's title. It must not be empty. |
| `body` | text | no | The row's text. It may hold references. |

## Hands on

Nothing. Later nodes cannot read from a notify node.

## Notes

- A notify node that fails (an empty title, or the row could not be written) fails the run unless the node has `"onFailure": "continue"`.
- Put what the owner needs to decide in `body`. A reference such as `{{summarize.summary}}` works there.

## Example

```json
{
  "version": 1,
  "name": "Summarize and notify",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "summarize", "type": "prompt", "config": { "prompt": "Summarize this in one line: {{trigger.payload}}", "output": { "type": "object", "required": ["summary"], "properties": { "summary": { "type": "string" } } } } },
    { "id": "tell", "type": "notify", "config": { "title": "Summary ready", "body": "{{summarize.summary}}" } }
  ],
  "edges": [
    { "from": "start", "to": "summarize" },
    { "from": "summarize", "to": "tell" }
  ]
}
```
