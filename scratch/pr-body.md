## What and why

`workflow.line` ran one phase at a time. It now has a dispatcher (LN-WF1): after the plan is frozen it lists the phases on a board, starts every phase that is ready as its own `workflow.phase` run, and waits for any phase to move before starting the next, until every phase is merged or skipped. Two phases with no dependency run in parallel; a phase that depends on one whose pull request is open starts stacked on that branch.

- **Linked instances.** A phase run is a linked instance at `{line}/phase.<id>.<attempt>`. `effect.line.start-phase` starts it, idempotent by that address (a retry is the next attempt), pinned and approved with the line like a child recipe. It hands the phase its packet (with `base` and `stackParent`) and the line's `fact:lineContext`.
- **`line.phaseChanged`.** A new event on a new route, `parent`, delivered to the line the reporting phase is linked to (an effect the line runs itself, like the start, answers the line). Outcomes: `started`, `shipped`, `parked`, `resumed`, `merged`, `ended`, `failed`. `fact:phaseEvent` is `{ line, phase, attempt, status, instance, at }`; the attempt is read from the instance address when absent. The start effect emits `started`; the phase emits `failed` when the owner stops it; `effect.line.report` (separate PR) emits `shipped` and `merged`.
- **Reply clash.** `merged` is an outcome of three reply events. `EffectDone` now takes an optional `Event`, so an effect names its own reply event; with none, the one declaring event is used as before.
- **Dispatcher blocks.** `step.line.seed` (board), `step.line.dispatch` (pure: start, full, done, blocked; caps from `workflow.line.parallel`, default 2, and `workflow.line.maxOpenPrs`, default 4) and `step.line.decide` (owner: retry, skip with dependents, stop). `workflow.line` is version 4; `workflow.phase` is version 3 (its owner stop reports `failed`).
- **Evals.** `parallel-and-stacked`, `failure-stops-dependents` and `failure-stop`, plus the happy path rewritten for the dispatcher.

## Defaults taken (reversible)

- Parallel limit 2 and open-PR limit 4 when the profile sets neither.
- The phase's `failed` is reported only from its owner stop (every failing path ends at the owner). A cancelled phase reports nothing.
- `effect.line.report` must accept `status: failed` and name `line.phaseChanged` as its reply event (`EffectDone.Event`) for the failure report and for `merged`; until its PR lands the phase's owner-stop transition names an effect with no runtime.
- The engine-emitted `parked`, `resumed` and `ended` events and cancel cascade from parent to linked instances are not part of this PR.

## Tests

- `Aw.Sindri.Tests`: full project, heavy lane excluded (see the run below), including new `PhaseBoardTests`, `LineDispatchStepTests`, `StartPhaseEffectTests`, `LinePhaseChangedEventTests`, `RecipeChildrenTests`, `LinkedAddressTests` and the four new descriptor contracts.
- Not run: the whole-solution build and format, smokes, the heavy lane, a served instance end to end.

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_01FH7fi6qZLKrDbBRsYis5Ao
