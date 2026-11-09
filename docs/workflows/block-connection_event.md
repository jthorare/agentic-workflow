# Connection event

Group: Triggers. Type: `connection_event`. Starts a run when a connection's service reports an event, such as a merged GitHub pull request.

## Run when something happens on a connection

Name the connection (`<service>:<label>`, such as `github:work`) and one event kind from the catalog (`github.pr.merged`). Sindri polls the service once per connection and event kind, however many workflows watch it, then starts a run for each event that passes your `filter`. One provider event starts one run: the event's delivery id is the run's occurrence, so a poll that sees the same event twice does not start a second run. A workflow has exactly one trigger.

`workflow_blocks` lists the event kinds each connection offers. An event kind that is not in the catalog is refused when you save.

## Config

| Field | Type | Needed | Meaning |
|---|---|---|---|
| `connection` | text | yes | The connection, such as `github:work`. |
| `event` | text | yes | The event kind, such as `github.pr.merged`. |
| `filter` | text | no | A condition over the event's fields, read as `trigger.<field>`, such as `trigger.repo == "acme/api"`. An event that fails it starts no run. |

## Hands on

| Field | Type | Meaning |
|---|---|---|
| `deliveryId` | text | The service's id for this event. |
| `occurredAt` | text | When the event happened, as a timestamp. |
| `connection` | text | The connection the event came from. |
| `event` | text | The event kind. |
| `repo`, `number`, `title`, `url`, `author`, `ref`, `identifier`, `label`, `to`, `channel`, `ts`, `user`, `text`, `start` | text | Only the ones this kind of event has. Which kind has which is in the catalog. |

Read them as `{{trigger.title}}`, or by the node's id. The values are text from the service, so anything you send to a model should treat them as untrusted.

## Example

```json
{
  "version": 1,
  "name": "Tell me when an API pull request merges",
  "nodes": [
    { "id": "merged", "type": "connection_event", "config": { "connection": "github:work", "event": "github.pr.merged", "filter": "trigger.repo == \"acme/api\"" } },
    { "id": "hello", "type": "notify", "config": { "title": "Merged", "body": "{{merged.title}} ({{merged.url}})" } }
  ],
  "edges": [
    { "from": "merged", "to": "hello" }
  ]
}
```
