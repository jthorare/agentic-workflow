# Schedule

Group: Triggers. Type: `schedule`. Starts a run on a cron: a timer that fires at set times.

## Run every weekday morning

Give it a five-field cron (minute, hour, day of month, month, weekday) and a timezone name. A workflow has exactly one trigger.
Without a `timezone`, the cron is read in UTC. When both a day of month and a weekday are set, either one matching fires the run.

## Config

| Field | Type | Needed | Meaning |
|---|---|---|---|
| `cron` | text | yes | Five fields, such as `0 7 * * 1-5` (07:00, Monday to Friday). |
| `timezone` | text | no | A zone name, such as `America/Los_Angeles`. |

## Hands on

| Field | Type | Meaning |
|---|---|---|
| `scheduledFor` | text | The tick this run fired for, as a timestamp. |

Read it as `{{trigger.scheduledFor}}`, or by the node's id.

A cron that does not parse is refused when you save, with the reason. If a tick fires while the last run is still going, the workflow's `concurrency` decides what happens.

## Example

```json
{
  "version": 1,
  "name": "Weekday morning ping",
  "nodes": [
    { "id": "daily", "type": "schedule", "config": { "cron": "0 7 * * 1-5", "timezone": "America/Los_Angeles" } },
    { "id": "hello", "type": "notify", "config": { "title": "Good morning", "body": "Tick for {{daily.scheduledFor}}" } }
  ],
  "edges": [
    { "from": "daily", "to": "hello" }
  ]
}
```
