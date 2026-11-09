# `http`

One request to a host on the profile's `workflow.http.hosts`. Until connections exist (PR 10) a request is always unauthenticated.

| Config | Type | Meaning |
|---|---|---|
| `url` | text | A whole `https://` address, no user name or password. |
| `method` | text, optional | `GET` (default), `POST`, `PUT`, `PATCH` or `DELETE`. |
| `headers` | object, optional | Any header except a credential: `Authorization`, `Proxy-Authorization`, `Cookie`, `X-Api-Key`, `Api-Key` and any name with `token`, `secret` or `password` are refused. |
| `body` | object, optional | Sent as JSON. |
| `expect` | text, optional | The statuses that count as success, such as `200,201`, `200-299` or `2xx`; default any 2xx. Another status fails the node. |

| Output | Type | Meaning |
|---|---|---|
| `status` | number | The status code. |
| `body` | any | The answer parsed as JSON, or null when it is not JSON. |
| `text` | string | The answer as text. |

**What is refused** (`SND-GRAPH-122`, before anything is sent): a host not on `workflow.http.hosts`; `localhost`, any IP address (the list holds names only); plain `http`; a credential header. **What fails** (`SND-GRAPH-123`): a status outside `expect`; a redirect to another host (refused, never followed) or more than three redirects to the same one; an answer over 1 MB; a network error or a 30 s timeout. When the name resolves only to a loopback or link-local address, the connection itself is refused.

```json
{
  "version": 1, "name": "Ping status page", "concurrency": "one-per-workflow",
  "nodes": [
    { "id": "hourly", "type": "schedule", "config": { "cron": "0 * * * *" } },
    { "id": "ask", "type": "http", "config": { "url": "https://status.example.com/api/v2/status.json", "expect": "200" } },
    { "id": "tell", "type": "notify", "config": { "title": "Status: {{ask.status}}" } }
  ],
  "edges": [ { "from": "hourly", "to": "ask" }, { "from": "ask", "to": "tell" } ]
}
```

Profile:

```yaml
workflow:
  http:
    hosts: [status.example.com]
```
