# Skill proposer

A starter template. It takes the findings of a steering-miner run, the mistakes you corrected more than once. For each of the top 5 it drafts an edit to the toolkit skill that should stop that mistake.

Template name: `skill-proposer` (issue 3787, piece 4). This first version ends at a notification that lists the drafts. Saving and staging them as proposals comes with the proposal store.

## What it does

1. `start` (manual) takes the payload below, and `check` (code) stops the run when it has no `repo` or no `findings` list, naming what is missing.
2. `skills` (`skill_catalog`) lists every skill with its description.
3. `each` loops over the findings, at most 5. The first ones are the top ranked.
4. `match` (prompt) picks the skill whose instructions should change, or `none`.
5. `pick` (code) keeps the pick only when the list has that exact name. Anything else becomes `none`.
6. `has_skill` (condition) ends the item when there is no skill.
7. `draft` (`run_agent`) edits that skill's files on its own branch and checks them with the free eval modes.
8. `entry` (code) notes the skill, the agent's report, the branch and the commit.
9. `gather` (collect) ends the loop, and `summary` (code) writes one line per draft.
10. `tell` (notify) shows the drafts and how many findings had no skill.

## The payload

The payload needs both fields. A run without `repo` fails at `check` before any model or agent is asked.

- `findings`: the `findings` of a steering-miner run's `rank` step, as `wf run-show` prints them.
- `repo`: the name of the toolkit checkout in your profile. The agent works in it.

## What the agent may do

- Change only files under `skills/<skill>/`.
- Keep the change general: no names of people, companies, repos or products, and no text quoted from a session.
- Run `scripts/skill-evals.sh --skills <skill>` with `--selftest`, `--lint-descriptions` or `--dry-run` only. Those cost nothing. A real eval run spends shared usage, so the agent never starts one.
- Commit on its own branch. It never pushes and never opens a pull request.

The agent sees the mistake's label and its short generic phrase. It never sees the turns behind it: the turn refs stay in the payload.

## Run it by hand

1. Run `steering-miner` and read its `rank` step with `sindri wf run-show <run>`.
2. `sindri wf create skill-proposer --template skill-proposer`
3. `sindri wf activate skill-proposer`
4. `sindri wf run skill-proposer --payload '{"repo": "<toolkit repo>", "findings": [...]}'`
5. Each draft's branch is in the notification. Review it there.

## Limits

- Each draft is one agent session, up to 45 minutes. That is why only the top 5 findings get one.
- A draft whose agent fails stops the run.
