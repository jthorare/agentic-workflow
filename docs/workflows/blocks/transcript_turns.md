# `transcript_turns`

Lists the newest human turns of your local agent sessions (Claude Code, Codex and Cursor). It hands on batches of turn refs, not the text. A `transcript_batch` step inside a `for_each` reads each batch's text.

| Config | Type | Meaning |
|---|---|---|
| `since` | text, optional | How far back, such as `7d` or `12h`. Default `7d`. |
| `providers` | list of text, optional | Only these agents: `claude-code`, `codex`, `cursor`. Default: every agent. |
| `repo` | text, optional | Only sessions of this main checkout, as a full path. Default: every repo. |
| `batchSize` | number, optional | Turns per batch, from 1 to 20. Default 20. |
| `maxTurns` | number, optional | The most turns, from 1 to 400. Default 400. The newest turns win. |

| Output | Type | Meaning |
|---|---|---|
| `batches` | array | A list of batches. Each batch is a list of turn refs, such as `transcript:0a1b2c3d#42`. Newest first. |
| `sessions` | number | How many sessions the turns come from. |
| `turns` | number | How many turns the batches hold. |
| `truncated` | boolean | True when `maxTurns` cut older turns off. |

- Only prompts a person typed count. Assistant turns and subagent sessions are left out.
- The step reads files only and writes nothing. A test run reads them for real.
- 400 refs take about 12 KB, so the output fits the 64 KB step cap. The text stays out of it on purpose: 400 turns of text don't fit.
- A ref names no repo or path. The part before `#` names the session.

```json
{
  "version": 1, "name": "Read last week's turns",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "turns", "type": "transcript_turns", "config": { "since": "7d", "batchSize": 20 } },
    { "id": "each", "type": "for_each", "config": { "items": "{{turns.batches}}", "maxItems": 20 } },
    { "id": "load", "type": "transcript_batch", "config": { "refs": "{{item}}" } },
    { "id": "gather", "type": "collect", "config": { "value": "{{load.missing}}" } }
  ],
  "edges": [ { "from": "start", "to": "turns" }, { "from": "turns", "to": "each" }, { "from": "each", "to": "load" }, { "from": "load", "to": "gather" } ]
}
```
