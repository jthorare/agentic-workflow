# `memory_forget`

Forgets facts of a Sindri memory (picker label **Forget**), as `service:workflow`. Give exactly one of `fact` and `tag`; a memory that does not exist fails the node with the handler's error.

| Config | Type | Meaning |
|---|---|---|
| `memory` | text | The memory's name. |
| `fact` | text, optional | One fact's id (what `memory_write` hands on as `id`). |
| `tag` | text, optional | Every fact with this tag. |

| Output | Type | Meaning |
|---|---|---|
| `count` | number | How many facts were forgotten; 0 when none matched. |

```json
{
  "version": 1, "name": "Forget stale notes", "concurrency": "unlimited",
  "nodes": [
    { "id": "start", "type": "workflow_call", "config": {} },
    { "id": "forget", "type": "memory_forget", "config": { "memory": "drift-check", "tag": "stale" } }
  ],
  "edges": [ { "from": "start", "to": "forget" } ]
}
```
