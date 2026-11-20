# Scoping benchmark answer keys

The scoping benchmark of the context scope spec (§7, epic #2959): plain questions about a real repo, each with the files a right answer
names. `scripts/scoping-bench.mjs` asks each question of a running Sindri and scores the files its answer names.

One folder per repo, with `questions.yaml` in it. A folder may hold more key files for one issue's questions, each with its own `commit` (`agentic-workflow/ranked-query.yaml`: the three ranked-query questions of #3673, ids `rq-01` to `rq-03`). Every file has the same fields:

| Field | Meaning |
|---|---|
| `repo`, `commit`, `source` | The repo and the commit the paths hold at. Clone `source` at `commit` to map and `--check` it. |
| `questions[].id` | A stable id (`effect-01`, `aw-01`). |
| `questions[].plain` | The question as a person asks it, with no file or symbol names. The runner asks this by default. |
| `questions[].question` | The same change said precisely (`--field question` asks this one). |
| `touch` | Files a correct change edits, or for a "where is X" question the files that do X. Touch recall is counted on its own: a scope that finds every read file and misses the file to edit has failed. |
| `read` | Files needed to understand the change, not edited. |
| `decoys` | Files whose words match the question but which a correct change leaves alone. |
| `issue`, `links_followed` | Optional: the issue the question came from, and the searches that found the key. |

Each entry is `{path, why}`; the why names the symbol or line that puts the file there. A file a change would create is never a key:
list the file it is modelled on as `read`.

```bash
node scripts/scoping-bench.mjs --questions tests/fixtures/scoping/agentic-workflow/questions.yaml --check
AW_SINDRI_HOME=... AW_SINDRI_PORT=... node scripts/scoping-bench.mjs --questions tests/fixtures/scoping/effect/questions.yaml --out /tmp/run
```

The runner measures one answer per question. Follow-ups needed (spec §7) are counted by the person and agent walk of phase Q1, not here.
