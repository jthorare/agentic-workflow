# `memory_search`

Recalls facts from a Sindri memory by meaning (picker label **Recall**). It uses the same search as `sindri memory search` and the MCP tool, as `service:workflow`. Without the embedding model it ranks by keywords and says so in `mode`.

| Config | Type | Meaning |
|---|---|---|
| `memory` | text | The memory's name (`sindri memory create` makes one). A memory that does not exist fails the node with the handler's error. |
| `query` | text | What to recall; not blank. |
| `level` | text, optional | `loose`, `balanced` (the default) or `strict`: how close a fact has to be. |
| `limit` | number, optional | At most this many facts; the default is 10 and the most is 50. |

| Output | Type | Meaning |
|---|---|---|
| `facts` | array | The fact texts, best first. `{{recall.facts}}` renders them as compact JSON, and `prompt` and `run_agent` fence them as untrusted text. |
| `mode` | string | `semantic`, or `keyword` when the embedding model did not answer. |

```json
{
  "version": 1, "name": "Recall what we checked", "concurrency": "unlimited",
  "nodes": [
    { "id": "start", "type": "workflow_call", "config": {} },
    { "id": "recall", "type": "memory_search", "config": { "memory": "drift-check", "query": "last checked commit and open PR" } },
    { "id": "tell", "type": "notify", "config": { "title": "What we know", "body": "{{recall.facts}}" } }
  ],
  "edges": [ { "from": "start", "to": "recall" }, { "from": "recall", "to": "tell" } ]
}
```
