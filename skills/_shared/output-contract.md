# Output contract

Every skill ends with a `## Result block` section that points here. The contract is what a caller (an eval, `bugFixOrchestrator`, another skill) can rely on when it runs a skill without reading the whole reply. It extends what a skill already prints (`rootCause`'s JSON tail, a lens's `verdict:` line, `## Next steps`) and never replaces it.

## The gate

Nothing below happens on a person's normal run. The Result block opens with this Bash line, and the skill runs it as written at the start of the run ([The opening](#the-opening)), not when it reaches the Result block:

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

Go by what it printed, not by what you expect. `AW_RESULT=0`: skip the rest of the Result block, write no result file and print no `aw-result` block (except where the skill's own section says it always prints one). `AW_RESULT=1`: do both.

The eval runner cannot pass `AW_*` variables into the agent's environment, so it rewrites the literal `${AW_RESULT:-0}` to `${AW_RESULT:-1}` in its staged copy of the skill. That is why the line must stay character for character as above; `scripts/check-skill-requires.sh` greps for it.

## The opening

The gate runs at the start of the run, not when the run reaches the Result block. A skill's last section is easy to never reach: a failed step, a FAIL verdict, a busy lock, a refusal or a question back to the person ends the run first, and a gate that never ran writes no file and prints no block (#2002: 7 of 9 misses). So every skill's opening, the text before its first `## ` heading, carries this line, next to its preamble or preflight:

```
Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.
```

A skill may word it its own way (`ui-evidence` and `verify-desktop` point at a Done checklist that ends with the Result block), but the line keeps the `(#result-block)` link and the words "gate now" and "opens with": `scripts/check-skill-requires.sh` looks for all three before the first `## ` heading. The reply order lives in the opening as well as here because the agent reads the skill, not this contract. Skills not swept yet are listed in `scripts/check-skill-requires.unswept`, and the list only shrinks.

Every exit is a finished run. Under `AW_RESULT=1`, a run that stops, fails or hands a question back still writes the result file and prints the block. Its `summary` says where it stopped and why, its `artifacts` lists what it did write (`[]` when nothing), and a skill key it could not fill gets the skill's own "not reached" value, or `null` when it has none. `skill`, `artifacts` and `summary` are never `null`.

## The result file

Under `AW_RESULT=1`, write one file:

```
$AW_DIR/results/<skill>-<timestamp>.json
```

- `$AW_DIR` is the per-repo output directory the preamble already resolved (`skills/_shared/repo-slug.sh` sets it; source it again in a new Bash call, shell state does not persist). Print it with `echo "$AW_DIR"` and put the absolute path in the Write call.
- Under an eval the file must land inside the workspace, the only place the Write tool can write and graders look. The eval runner (`scripts/skill-evals.sh`) does that in its staged copy of the toolkit: it rewrites `repo-slug.sh`'s `${AW_STATE_DIR:-$HOME/.agentic-workflow}` default to the workspace root's `.aw-state` (found by walking up from `$PWD` to the directory holding `.aw-eval/`, not `$PWD` itself, so a skill that `cd`s into a subdirectory writes to the same place), so `$AW_DIR` is `<workspace root>/.aw-state/<slug>`. Skills do nothing for this beyond using `$AW_DIR`, never a hand-built `~/.agentic-workflow/...` path.
- `<timestamp>` is `date +%Y%m%d-%H%M%S`.
- **Write it with the Write tool**, not a shell redirect. Under an eval, Bash cannot write anything a grader sees; the Write tool can, and it creates the parent directories itself. Do not put a `mkdir -p ... &&` chain in front of it.
- Nothing is written outside `AW_RESULT=1`, so a person's runs leave nothing new behind.

Required keys:

| Key | Value |
|---|---|
| `skill` | the skill's name, as in its frontmatter |
| `artifacts` | absolute paths of every file the run produced or changed on purpose (reports, plans, mockups, state files); `[]` when nothing was written |
| `summary` | one line, what happened |

Everything else is the skill's own. The skill's Result block names its extra keys; the keys a skill already has keep their meaning and spelling (`rootCause`'s `status` enum, a lens's `verdict`). No shared `status` enum is forced on skills that already have one. Skills that only talk (`judge`, `prismStatus`) use `artifacts: []`.

A lens or review skill adds `verdict`, copied from the last `verdict:` line of its report (or the verdict word the report prints, when the report has no such line).

## The reply

Under `AW_RESULT=1`, the reply carries the same JSON as one fenced block tagged `aw-result`:

````
```aw-result
{ "skill": "<name>", "artifacts": ["/abs/path"], "summary": "one line", ... }
```
````

- A skill that already prints its JSON in every reply (today `rootCause`) keeps printing it always, as an `aw-result` block with `skill`, `artifacts` and `summary` added to its existing keys. It moves to the top of the final reply like every other block; callers pick it by `skill`, never by position (`bugFixOrchestrator` reads `handoff_path` from it).
- Every other skill prints the block only when the gate printed `AW_RESULT=1`.
- Order: write the result file, and every other file the run writes, first. The final reply, the message after the last tool call, **opens** with the `aw-result` block, then the prose, then `## Next steps`. The block comes first because it was just written: the agent that saves the file and then writes the prose reply tends to report the file ("I also saved the result file at ...") and leave the block out (#2002). The result file never stands in for the block, and a reply that names the file without the block is unfinished. Callers and graders read only that final reply, so a block printed before a later tool call (a Write, a chained skill) is lost. A skill that chains another (canary to syncDocs, shipRelease to landAndDeploy) opens that reply with two or more blocks: the chained skill's first, the calling skill's last. A caller picks the block whose `skill` is the skill it ran, never by position. A lens's `verdict:` stays the last line of its report file.
