# Output contract

Every skill ends with a `## Result block` section that points here. The contract is what a caller (an eval, `bugFixOrchestrator`, another skill) can rely on when it runs a skill without reading the whole reply. It extends what a skill already prints (`rootCause`'s JSON tail, a lens's `verdict:` line, `## Next steps`) and never replaces it.

## The gate

Nothing below happens on a person's normal run. The Result block opens with this Bash line, run as written:

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

Go by what it printed, not by what you expect. `AW_RESULT=0`: skip the rest of the Result block, write no result file and print no `aw-result` block (except where the skill's own section says it always prints one). `AW_RESULT=1`: do both.

The eval runner cannot pass `AW_*` variables into the agent's environment, so it rewrites the literal `${AW_RESULT:-0}` to `${AW_RESULT:-1}` in its staged copy of the skill. That is why the line must stay character for character as above; `scripts/check-skill-requires.sh` greps for it.

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

Everything else is the skill's own. The skill's Result block names its extra keys; the keys a skill already has keep their meaning and spelling (`rootCause`'s `status` enum, a lens's `verdict`). No shared `status` enum is forced on skills that already have one. Skills that only talk (`enhancePrompt`, `withInterview`, `judge`, `prismStatus`) use `artifacts: []`.

A lens or review skill adds `verdict`, copied from the last `verdict:` line of its report (or the verdict word the report prints, when the report has no such line).

## The reply

Under `AW_RESULT=1`, the reply carries the same JSON as one fenced block tagged `aw-result`:

````
```aw-result
{ "skill": "<name>", "artifacts": ["/abs/path"], "summary": "one line", ... }
```
````

- A skill that already prints a JSON tail in every reply (today `rootCause`) keeps printing it always, as an `aw-result` block with `skill`, `artifacts` and `summary` added to its existing keys.
- Every other skill prints the block only when the gate printed `AW_RESULT=1`.
- Order: write the result file, and every other file the run writes, first. The `aw-result` block goes in the final reply, the message after the last tool call, then `## Next steps`. Callers and graders read only that final reply, so a block printed before a later tool call (a Write, a chained skill) is lost. A skill that chains another (canary to syncDocs, shipRelease to landAndDeploy) ends with two or more blocks in that reply: the chained skill's first, the calling skill's last. A caller picks the block whose `skill` is the skill it ran, never by position. A lens's `verdict:` stays the last line of its report file.
