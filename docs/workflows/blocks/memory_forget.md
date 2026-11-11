# `memory_forget`

Forgets facts of a Sindri memory (picker label **Forget**), as `service:workflow`. Give exactly one of `fact` and `tag`; a memory that does not exist fails the node with the handler's error. An empty `fact` (for example a reference that came out empty) fails the node; it never falls back to the `tag`.

**Who it can forget.** A workflow forgets as `service:workflow`, and forgetting has no service grants to check. So any active workflow can forget any fact in any memory, by id or by tag, including facts a person wrote. The same holds for `create_item`. Read a workflow that has this node as you would read a person with write access to every memory.

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
