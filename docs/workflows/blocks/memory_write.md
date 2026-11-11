# `memory_write`

Writes a fact to a Sindri memory (picker label **Remember**), as `service:workflow`. A fact very similar to an existing one with the same tags refreshes that fact instead of adding another. The fact's provenance is the run's workflow, the run and the run's item.

| Config | Type | Meaning |
|---|---|---|
| `memory` | text | The memory's name. A memory that does not exist fails the node with the handler's error. |
| `fact` | text | The fact, 1 to 2,000 characters. |
| `tags` | text list, optional | Tags. The same text under other tags is a different fact. |
| `expires` | text, optional | How long the fact lives from now: `30d`, `12h`, `45m`. Left out, it never expires. |

| Output | Type | Meaning |
|---|---|---|
| `id` | string | The fact's id; the existing fact's when it was refreshed. |
| `action` | string | `added`, or `refreshed` when a near duplicate with the same tags was found. |

```json
{
  "version": 1, "name": "Remember a check", "concurrency": "unlimited",
  "nodes": [
    { "id": "start", "type": "workflow_call", "config": {} },
    { "id": "remember", "type": "memory_write", "config": { "memory": "drift-check", "fact": "Checked api-repo at {{trigger.payload.commit}}", "tags": ["checks"], "expires": "30d" } }
  ],
  "edges": [ { "from": "start", "to": "remember" } ]
}
```
