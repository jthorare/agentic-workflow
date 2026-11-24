# `skill_catalog`

Lists the toolkit's skills, each with the description from its SKILL.md. A `prompt` step can then pick the skill a finding is about.

It takes no config. Write `"config": {}`.

| Output | Type | Meaning |
|---|---|---|
| `skills` | array | One `{name, description}` per skill, sorted by name. `name` is the skill's folder under `skills/`. |
| `count` | number | How many skills that is. |

- The step reads the same skills folder as the skill blocks: `AW_SINDRI_SKILLS_DIR`, else the checkout's `skills/` when Sindri runs in a toolkit checkout, else the installed toolkit's.
- A folder whose name starts with `_` is shared text, not a skill. A folder with no SKILL.md is not a skill either.
- A SKILL.md with no `description` is still listed, with an empty one, so a prompt can match it by name.
- A description keeps its first 300 characters, so the list stays far under the 64 KB step cap.
- The step reads files only and writes nothing. A test run reads them for real.

```json
{
  "version": 1, "name": "List the skills",
  "nodes": [
    { "id": "start", "type": "manual", "config": {} },
    { "id": "skills", "type": "skill_catalog", "config": {} },
    { "id": "tell", "type": "notify", "config": { "title": "{{skills.count}} skills" } }
  ],
  "edges": [ { "from": "start", "to": "skills" }, { "from": "skills", "to": "tell" } ]
}
```
