# Steering miner

A starter template. It reads human turns from past agent sessions and finds the mistakes you keep correcting. A label step marks each turn, and code groups the corrections. Each mistake you corrected at least twice comes back as one finding, with the refs of its turns.

Two templates share one graph from `shape` on:

- `steering-miner` reads your transcripts itself. `turns` (`transcript_turns`) lists the newest 400 human turns of the last 7 days as batches of refs. Inside the loop, `load` (`transcript_batch`) reads each batch's text.
- `steering-miner-payload` takes the turns from the run's payload instead (below). Use it for a hand-picked set or a test.

The text stays out of the list on purpose. 400 turns of 1,500 characters are far more than the 64 KB a step may hand on, while 400 refs are about 12 KB. Each batch's text is read inside the loop, under 64 KB per batch, so a turn keeps up to 1,500 characters.

## What it does

1. `start` (manual) starts the run.
2. `steering-miner` only: `turns` lists the turns, `each` loops over its batches (at most 20), and `load` reads one batch's text. `steering-miner-payload`: `each` loops over the payload's batches.
3. `shape` (code) checks one batch and cuts it to size.
4. `label` (prompt) gives each turn one label and, for a correction, a short mistake phrase.
5. `keep` (code) drops rows it cannot trust and keeps the corrections.
6. `gather` (collect) ends the loop.
7. `group` (code) puts corrections with the same label and phrase together and numbers the phrases `p1`, `p2`, and so on.
8. `merge` (prompt) says which phrases of one label name the same mistake in other words.
9. `rank` (code) joins those phrases, keeps each group with at least 2 different turns and ranks the findings.
10. `tell` (notify) shows the top 5 findings.

The model only labels and merges. Code applies the caps, the threshold of 2 and the order.

## The payload (`steering-miner-payload`)

`turns` is a list of batches. A batch is a list of turns. A turn has three fields:

- `ref`: the turn's opaque ref, such as `transcript:1a2b3c4d#120`. The part before `#` names the session.
- `text`: the human's text, already scrubbed.
- `before` (optional): the end of the assistant message just before the turn.

```text
{"turns": [
  [ {"ref": "transcript:1a2b3c4d#120", "text": "No, run it in CI, not locally.", "before": "I ran the suite on my machine."},
    {"ref": "transcript:1a2b3c4d#131", "text": "Thanks."} ],
  [ {"ref": "transcript:9f8e7d6c#44", "text": "Again: do not run it locally."} ]
]}
```

`load` hands on the same shape for one batch, so `shape` and every step after it are the same in both templates.

## Labels

| Label | The human... | Counts as a correction |
|---|---|---|
| `wrong_approach_design` | says the technical approach or design is wrong | yes |
| `wrong_approach_process` | corrects how or where the work is done (CI or local, which tool, the order of steps) | yes |
| `restate` | repeats an instruction they already gave | yes |
| `scope_surface` | points at places the assistant missed | yes |
| `rigor` | asks for evidence, checks or certainty | no |
| `defect_report` | reports a concrete bug in the work | no |
| `none` | asks something new, thanks, or answers | no |

## Caps

| Cap | Value | Why |
|---|---|---|
| Batches per run | 20 (`maxItems`) | A longer list fails `each` before any batch runs |
| Turns per batch | 20 | Extra turns are dropped and counted in `shape`'s `dropped` |
| Turns per run | 400 | 20 batches of 20 |
| `text` | first 1,500 characters, at most 2,000 bytes as JSON | 20 turns stay under the 64 KB step output cap |
| `before` | last 400 characters, at most 500 bytes as JSON | Same |
| `ref` | 80 characters, no spaces, once per batch | Other turns are dropped |
| Mistake phrase | 60 characters | Keeps the gathered corrections small |
| Phrases sent to `merge` | 200, most turns first | Keeps the merge question small |
| Findings | 50 | Keeps the result small |

## Rows that are dropped

- A turn with no `ref` or no `text`, or a `ref` seen before in the batch.
- A label row whose `ref` is not in its batch, repeats a `ref`, or names a label outside the table.
- A correction with an empty mistake phrase.
- A merge that names an unknown id or joins two labels.

## What you get

`rank` hands on `findings`, `count`, `summary`, `turns`, `labeled` and `corrections`. A finding has `label`, `mistake`, `count` (different turns), `sessions` (different sessions) and `refs`. Findings rank by `count`, then `sessions`, then label (table order), then phrase.

Read them with `sindri wf run-show <run>`, on the `rank` step. The run's own outputs stay empty: the steps of a full run hold more than 64 KB together.

## Run it by hand

1. `sindri wf create steering-miner --template steering-miner`
2. `sindri wf activate steering-miner`
3. `sindri wf run steering-miner`
4. `sindri wf run-show <run>`

To feed it by hand, create it from `--template steering-miner-payload` and run it with `--payload "$(cat turns.json)"`. To read another week or one repo, edit the `turns` step's `since` and `repo`.

## Limits

- One `label` or `merge` step that fails stops the whole run. The model fails a step when its answer is not JSON twice.
- Nothing caps the tokens of a run. Each label step takes at most 4,096 answer tokens.
- `gather` fails when the corrections of a run pass 64 KB. That happens only when nearly every turn is a correction with a long phrase.
