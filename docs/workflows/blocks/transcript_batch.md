# `transcript_batch`

Reads the text of one batch of human turns. Put it inside a `for_each` over the `batches` of a `transcript_turns` step, with `refs` set to `{{item}}`.

| Config | Type | Meaning |
|---|---|---|
| `refs` | reference to a list | The batch, usually `{{item}}`. Anything that is not a list fails the step. |
| `before` | true or false, optional | Also read the end of the assistant turn just before each human turn. Default true. |

| Output | Type | Meaning |
|---|---|---|
| `turns` | array | One `{ref, text, before?}` per turn found, in the batch's order. |
| `missing` | number | Refs no transcript has any more. |
| `dropped` | number | Entries that are not a turn ref, repeat one, or come after the 20th. |

- `text` keeps the first 1,500 characters of the prompt, at most 2,000 bytes counted as JSON written twice (a quote or a non-ASCII character is 7 bytes that way). `before` keeps the last 400 characters, at most 500 bytes the same way.
- These cuts keep 20 turns under the 64 KB step cap in any script. Most prompts are shorter than the cut, so most turns come through whole.
- The text was scrubbed for secrets when it was read. It is still a stranger's text: a `prompt` step that reads it gets it fenced as untrusted data.
- The step reads files only and writes nothing. A test run reads them for real.

```json
{
  "version": 1, "name": "Read one batch",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "turns", "type": "transcript_turns", "config": { "maxTurns": 40 } },
    { "id": "each", "type": "for_each", "config": { "items": "{{turns.batches}}" } },
    { "id": "load", "type": "transcript_batch", "config": { "refs": "{{item}}", "before": false } },
    { "id": "gather", "type": "collect", "config": { "value": "{{load.turns}}" } }
  ],
  "edges": [ { "from": "start", "to": "turns" }, { "from": "turns", "to": "each" }, { "from": "each", "to": "load" }, { "from": "load", "to": "gather" } ]
}
```
