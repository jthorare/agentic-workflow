# `rule_catalog`

Lists the repo's rule records, each with its text and the checks that enforce it. A `prompt` step can then see which rules and check ids already exist.

It takes no config. Write `"config": {}`.

| Output | Type | Meaning |
|---|---|---|
| `rules` | array | One `{id, kind, section, text, status, checks}` per record, sorted by `id`. `checks` is a list of `{type, id}`. |
| `count` | number | How many rules `rules` holds. |
| `skipped` | number | How many record files the step could not use. |
| `sections` | array | The distinct `section` values of `rules`, sorted. A later step can check a proposed section against them. |

- The step reads `rules/<kind>/<language>/*.yaml`, for example `rules/engineering/csharp/x.yaml`. The folder is `AW_SINDRI_RULES_DIR`, else the checkout's `rules/` when Sindri runs in a toolkit checkout, else the installed toolkit's.
- A record that fails to parse, or has no `id` or no `text`, is skipped and counted in `skipped`. It never fails the step.
- A record with no `kind`, `section`, `status` or `checks` is still listed, with empty ones.
- The step keeps the first 500 records by `id`, and the first 600 characters of each `text`, so the list stays under the 64 KB step cap.
- The step reads files only and writes nothing. A test run reads them for real.
- A missing rules folder is an empty list, not an error.
- The folder is Sindri's, not the repo a workflow's payload names. When the two differ, the list can miss rules that repo has (the same gap as `skill_catalog`, issue 4107).

```json
{
  "version": 1, "name": "List the rules",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "rules", "type": "rule_catalog", "config": {} },
    { "id": "tell", "type": "notify", "config": { "title": "{{rules.count}} rules" } }
  ],
  "edges": [ { "from": "start", "to": "rules" }, { "from": "rules", "to": "tell" } ]
}
```
