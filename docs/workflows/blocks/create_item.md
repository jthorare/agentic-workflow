# `create_item`

Makes a work item through the same create-item handler as `sindri item add` and the app's New item form, as `service:workflow`.

| Config | Type | Meaning |
|---|---|---|
| `title` | text | 1 to 200 characters, one line. |
| `repo` | text, optional | A repo of the profile. Left out, the item has no repo. A repo the profile doesn't have fails the node with the handler's own error. |
| `body` | text, optional | Up to 4,000 characters. |
| `linearIssue` | text, optional | A Linear issue id (`abc-1`) or a whole key (`linear:issue/abc-1`); it is recorded as the item's link. |

| Output | Type | Meaning |
|---|---|---|
| `itemId` | string | The new item's id. |

Inside a `for_each`, every pass makes an item, but the run is linked to the first one made; later items keep their own ids in the node outputs.

```json
{
  "version": 1, "name": "File follow-ups", "concurrency": "unlimited",
  "nodes": [
    { "id": "start", "type": "workflow_call", "config": {} },
    { "id": "make", "type": "create_item", "config": { "title": "Follow up: {{trigger.payload.subject}}", "repo": "web", "body": "{{trigger.payload.details}}" } }
  ],
  "edges": [ { "from": "start", "to": "make" } ]
}
```
