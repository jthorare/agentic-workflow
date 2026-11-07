# Sindri skill blocks — `block.yaml`, the result file and how a skill finishes

A repo skill becomes a **block** in Sindri when it has a `skills/<name>/block.yaml` beside its `SKILL.md`. Recipes then
use it as a Step, `step.skill.<id>`, and the session that runs it ends when the skill writes its result file. This page
is the author guide. The design and its reasons are in `docs/superpowers/specs/2026-11-05-sindri-skill-flows-design.md`
(§3); where this page summarizes, the spec wins. Release 1a ships three blocks, the bug chain: `bugFixOrchestrator`,
`review` and `addressReview`.

## The manifest

```yaml
# skills/review/block.yaml
schema: 1
blocks:
  - id: review                   # kebab-case, written out, unique across all manifests
    sentence: Reviews a pull request with domain reviewers and says pass or needs changes.
    inputs:  { pr: { type: text, label: Pull request (number or link), required: true } }
    args: ["{pr}"]               # joined with single spaces; { flag: --mode, input: mode } drops itself when mode is empty
    outputs: { findings: { type: path, from: review_file } }
    outcomes:
      from: verdict              # a key of the result; with no `from` the only outcome is `success`
      map: { PASS: pass, NEEDS_CHANGES: needsChanges }
      success: pass
    pushes: false                # true = a request for the repo's agent-app push grant, shown in the approval preview
    maxDuration: 2h              # m, h or d; the recipe state's timeout must be at least this (RCP-SKILL-001)
```

- **Inputs** are `text` or `path` (an absolute path the session can read). `values:` on a `text` input is a non-empty list
  of strings and makes the form a pick list.
- **Quote a label that has a comma.** A flow-map entry (`{ ... }`) splits at every comma, so
  `label: The bug (a Linear id, a link or the report)` turns into extra keys. Write `label: "The bug (a Linear id, a link or the report)"`.
- **Outputs** read the result JSON: a top-level key (`feature_dir`, `review_file`, `pr`) or one artifact by file name,
  `artifacts[resolution.md]` (the one path in `artifacts` that ends in `/resolution.md`). A path output must be absolute,
  exist and hold no newline.
- **Fact keys** are derived, `fact:<block id in camelCase><Output capitalised>`: `fact:reviewFindings`,
  `fact:bugFixOrchestratorPr`. They must be unique across all manifests and at most 64 characters.
- **`pushes: true`** pins the skill folder's hash: editing any file in it needs a re-approval before the run finishes
  that block.

## The `aw-result` block

The SKILL.md's `## Result block` section holds exactly one fenced ` ```aw-result ` block: a JSON example of the result the
skill writes. Its top-level keys are the names an output's `from` (and `outcomes.from`) may use, and the file names in its
`artifacts` list are the names `artifacts[<name>]` may use. The example's `skill` is the skill's own name.

The skill writes the **same keys** to the result file as it prints in the block (a run that dropped `artifacts`, `summary`
or `branch` failed its outputs in the S-0 trial, SND-SKILL-004).

## Checking a manifest

`node scripts/check-skill-blocks.mjs` (and `bash scripts/tests/check-skill-blocks.test.sh`) checks every manifest:
schema (`schemas/skill-block.schema.json`), unique ids, every `{name}` slot and flag input in `args` is a declared input
and every required input appears, `success` is a mapped outcome, `values` sit on a `text` input, every output's `from`
names a key or artifact file of the `aw-result` example, and derived fact keys are unique and short enough.
`scripts/check-skill-requires.sh` also requires `AW_RESULT_FILE` and `waiting` in the Result block of every skill that has
a `block.yaml`. Exit 0 ok, 1 findings (one `file: reason` each), 2 when Ajv or `yaml` is missing (`npm ci --prefix scripts`).

## The author loop

1. Edit `skills/<name>/block.yaml` (and the Result block).
2. `node scripts/check-skill-blocks.mjs`.
3. `sindri skill run <id> --skills ./skills --input k=v --follow`. It prints the outcome, each fact and the manifest hash it
   ran. A `--skills` trial seeds the session by path (`Read <dir>/<name>/SKILL.md and follow it with arguments: ...`) and
   skips the SND-SKILL-007 sha check, since the folder is not the installed one.
4. Repeat 1-3 until it's right.
5. Restart serve to load it for chains, then `sindri recipe approve` any recipe that uses it.

`sindri skills [--json]` lists the loaded blocks, the path each came from and the reason for any that didn't load. The
skills folder is, in order: `--skills <dir>` on the in-process verbs, `AW_SINDRI_SKILLS_DIR`, the checkout's `skills/`
inside an agentic-workflow checkout, else `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills`.

## The session and the result file

Sindri launches the skill in an agent Step with `AW_RESULT=1`, `AW_RESULT_SKILL=<skill name>`, `AW_RESULT_FILE=<run dir>/.sindri-out/result-<launch>.json`
(a new name per launch) and `AW_DIR` set to the repo's state folder. The seed is only the skill's invocation in the target
agent's form (`/review 412` for Claude Code, the `$review` form for Codex). In a chain run the slash form stays; a trial
uses the path form above.

In the skill: when `AW_RESULT_SKILL` equals its own name it writes the result to `AW_RESULT_FILE` instead of
`$AW_DIR/results/<skill>-<ts>.json`, and a question goes in `"waiting": "<one line>"` (a non-empty string; `null` and `""`
mean no question), printed, turn ended, never AskUserQuestion. Unset, nothing changes
(`skills/_shared/output-contract.md`, "Under Sindri"). Under `AW_RESULT_SKILL` a failing prism-mcp call is a one-line
note, never a stop (the sandbox has no route to it).

Sindri reads the file at each turn end, inside the Step's own watch. `waiting` is checked before the outcome map, so a
skill that also wrote an unmapped `status` is still a waiting row.

| What it finds | What happens | Code |
|---|---|---|
| No `waiting`, `skill` matching, verdict mapped, every output present | Completes: outputs to facts, verdict to outcome, each path output's sha256 on the run; the step's `workflow.transition` row carries the result file's own sha256 as `resultSha256` (next to `agent` and `model`) | - |
| `waiting` (a non-empty string) | Waiting on you; the row shows the question | - |
| No file, or one written before this launch started | Waiting on you: "stopped without a result" | - |
| The file is older than the last prompt (`UserPromptSubmit`) | Working, no row: a background subagent's notice is not a new turn needing you. The settle rule: a turn end counts after the marker has stood about 30 s with no tool running | - |
| Not valid JSON | Waiting on you, with the parse error on the row | SND-SKILL-001 |
| `skill` names another skill | Waiting on you, "the result came from <other>" | SND-SKILL-002 |
| A verdict missing from the map | `step.failed` | SND-SKILL-003 |
| A mapped output missing, or a path output that is relative, missing on disk or holds a newline | `step.failed` | SND-SKILL-004 |
| (before launch) `requires.yaml` names a bin, MCP server, skill or agent the agent home lacks | `step.failed` before any session starts, naming each | SND-SKILL-005 |
| (at the start) a `pr` input that is not a PR number or a link to one | `step.failed` before launch | SND-SKILL-006 |
| (before launch) the agent home's SKILL.md differs by sha from the loaded one's | `step.failed` before launch; fix: re-run `setup.sh`, or point `AW_SINDRI_SKILLS_DIR` at the folder the agent home links. A toolkit checkout that is behind fails every run until it is updated, on purpose | SND-SKILL-007 |

## Where the bug chain runs

- **Branches.** The sandbox only lets a Step write `refs/heads/sindri/<instance>/<run>/`. `bugFixOrchestrator` names its fix
  and candidate branches under `${AW_SINDRI_BRANCH%/work}`, runs one implementer at a time in the Step's own worktree (no
  `git worktree add`, `git fetch` or `git branch -D`: the sandbox refuses them), pushes the winner as `$AW_SINDRI_BRANCH`,
  opens a **draft** PR only when `status` is `resolved`, and ends with `git checkout --detach` so the next Step's worktree
  can take the branch. `addressReview` works detached at that branch tip and pushes `HEAD:refs/heads/$AW_SINDRI_BRANCH`.
- **The PR fetch.** `review` is `pushes: false`, so its Step has no GitHub network. The host fetches the PR into a local
  ref before the launch and sets `AW_SINDRI_PR_REF`; `review` (and `addressReview`) read the PR from that ref, diffing it
  against the merge base with the default branch, and make no `gh` call. `review` never posts: `/postReview` stays yours.
- **`AW_DIR`.** The host computes the repo's slug and sets `AW_DIR`; `skills/_shared/repo-slug.sh` keeps a preset value
  only when `AW_RESULT_SKILL` is set (a skill Step), and recomputes it anywhere else.
  `bugfix-state` keeps its own folder, `$AW_STATE_DIR/bugfix/<slug>/`, which the sandbox binds read-write.
- **Turn hooks.** The "turn ended" marker comes from the Stop, UserPromptSubmit and PreToolUse hooks. Claude Code gets them
  from the Step's policy file. For a Codex Step the launcher writes them (plus PostToolUse) to the worktree's
  `.codex/hooks.json`, git-excluded, and starts Codex with `--dangerously-bypass-hook-trust`; a repo that has its own
  `.codex/hooks.json` refuses the launch.

## Known gaps

- A skill that is `waiting` on you is still ended by the 120 s idle sweep like any idle Step; skipping waiting skills is F-2a.
- The PR fetch's local branch `sindri/<instance>/<run>/pr` is never deleted.
- `providers/install-skills.sh` links only the toolkit's own skills into a separate login's home, not the external packs
  (impeccable, emil-design-eng, taste-skill), so a skill whose `requires.yaml` names one fails SND-SKILL-005 there.
- The seed fills every `{input}` slot of a text item in turn, so an input whose value itself contains `{other}` gets that
  slot filled too.
- `POST /sindri/skills/{id}/runs` has no API golden yet.
- The Codex `.codex/hooks.json` and its `info/exclude` line stay in the worktree after the run (the next Codex Step rewrites
  the same file).
- A launch with sandboxing off skips SND-SKILL-005 and SND-SKILL-007 and gets no `AW_DIR` from the host.

## Evals

`scripts/skill-evals.sh` sets `AW_RESULT_SKILL` and `AW_RESULT_FILE` in its staged copy from `.aw-eval/result-skill` and
`.aw-eval/result-file`, which only a Sindri case's `fixture.sh` writes. Each block has two cases under
`evals/<skill>/behaviour/`: `<skill>-sindri-done` (the file carries the same keys as the block; check the keys, not just
that a file exists) and `<skill>-sindri-waiting` (a non-empty `waiting`, no AskUserQuestion).
