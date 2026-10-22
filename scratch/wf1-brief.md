# Builder brief: LN-WF1, the line dispatcher (phases run in parallel and stacked)

Worktree: /home/thor/repos/aw-wt/engine-mainchk (reused), branch feat/sindri-line-dispatcher from main 08e1f40. Overnight: never block; take
the recommended reversible default and note it in the PR body. You may run scoped builds and tests yourself (as separate --filter-class
calls; never combine --filter-namespace with --filter-class) and push. Read .agents/rules/csharp.md first.

Spec: docs/superpowers/specs/2026-10-15-sindri-spec-to-proven-pr-line-design.md, LN-WF1 and the line's phase scheduling (search `start-phase`,
`phaseChanged`, `stacked`, `parallel`), and recipes/sindri/workflow.line.yaml. Gap (from the survey, /home/thor/repos/aw-wt/engine-lch1/scratch/gap-survey.md):
neither `effect.line.start-phase` nor a `line.phaseChanged` event exists in src, so workflow.line runs one phase at a time. "Four phases, two in parallel,
one stacked" can't happen.
The ideas lane owns `effect.line.report` (a phase tells the line it shipped or merged); coordinate through its shape and don't build it.

## Scope
1. `effect.line.start-phase`: starts a workflow.phase child for a phase whose dependencies are satisfied (the plan's phase graph: parallel = no
   dependency, stacked = depends on its parent's PR existing), with `fact:phasePacket` and the stack parent (base branch) as inputs. It's idempotent per
   (line, phase). Find how child workflows start today (P7 child workflows, RecipeWorkflow children) and use that.
2. The `line.phaseChanged` event: what a phase emits on its state changes (started, shipped, merged, failed). Wire it as an event descriptor (W25 pattern)
   that the line recipe can wait on, so the line wakes and starts the next eligible phases.
3. workflow.line: a dispatch loop. Start every eligible phase, then wait on line.phaseChanged and repeat until all phases are terminal, under a cap
   (the profile's parallel-phases limit if it exists; default 2 and say so). Version = main + 1, re-pin StatePathOrderTests, and add evals:
   two parallel plus one stacked; a failure stops its dependents.
4. Red tests first. Then the ShippedBlocks and descriptor pins and docs (sindri-workflow.md, the line section).
5. Push to jthorare feat/sindri-line-dispatcher, write scratch/wf1-pr-body.md (with the footer lines), and report the sha.
This is a big unit: commit and push after each green step, so a stop loses nothing.
