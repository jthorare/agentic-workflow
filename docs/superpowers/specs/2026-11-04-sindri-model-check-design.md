# Sindri model check: a TLC step for recipes, and TLA+ specs of the engine

Status: draft for the owner's read (owner decision 2026-11-04, routed by meta). Lane: engine. Related: the formal-verify skill (#2052, ideas), the shared TLC runner and CI job (api), the projector and heavy-slot specs (api).

## 1. What this is

Two things the owner asked for:

1. **A model-check step any recipe can use.** Scoping runs it when the item has a state machine, a protocol or concurrency. The build line runs it as a check on a change that touches modelled code.
2. **TLA+ specs of Sindri's own engine**: the recipe instance's transitions, and takeover / handback (the state timer pauses on takeover and resumes with what was left on handback). TLC checks them in CI.

Both use one runner and one pinned jar. Nobody writes a second TLC script.

## 2. What already exists (read at main 4c49f7f)

- **formal-verify skill** (#2052): `skills/formal-verify/scripts/tlc.sh doctor|fetch|check|translate`. `fetch` downloads the jar pinned in `skills/formal-verify/tla2tools.lock` (v1.7.4, sha256 checked) into `$AW_STATE_DIR/cache/bin/tla2tools-<v>/`. `check <Spec.tla> [--config F]` runs TLC with its state files in a temp dir and ends on one line: `tlc: PASS distinct=N depth=N`, `tlc: VIOLATION <invariant|property|deadlock|assumption> <name>` or `tlc: ERROR ...`. Exit 0 pass, 1 violation, 2 spec or tool error, 3 java or jar missing. ideas keeps this CLI stable.
- **Layout and runner, agreed with api and ideas:**
  - One folder per spec: `specs/tla/<Name>/<Name>.tla` and `<Name>.cfg`, plus `MC<Name>.tla` when TLC needs model values, plus a 3-line `README.md`: the code it models (file:line), the properties, the cfg's bounds.
  - `scripts/tlc-check.sh [<spec-dir>...]` (api) runs `tlc.sh check` on every folder, `-workers 1`. Its test is `scripts/tests/tlc-check.test.sh` (stub java). A `tla` CI job installs a JDK and runs the real specs.
  - No `binaries.lock` row: its format is per-platform `.tar.gz` and the jar is neither.
- **How a recipe runs a local tool today.** A deterministic step is a C# block step (`runtime: block`, `IBlockStep`, registered in `Aw.Sindri.Features/Blocks/BlockRegistrations.cs`). It runs commands through `RepoCommandRunner` (`Core/Checks/RepoCommandRunner.cs`: one heavy slot, one shared time budget) over the `ICheckCommands` port and `ProcessToolRunner`. Block steps run in the host worker as the `RunBlockStep` activity, not in the agent sandbox. The step maps the result to an outcome itself, as `step.build.checks` does: a failing command is `failed` with a `BLD-COMMAND-001` finding carrying the output tail (`Features/Build/BuildChecksStep.cs`).
- **The build line already runs repo commands.** `step.build.checks` runs every `workflow.build.commands` entry of the profile on the head, under a heavy slot. A failure routes back to `green` with the findings (`recipes/sindri/workflow.build.yaml`, `checks` → `green` on `failed`).
- **Scoping** branches only on step outcomes, loop caps and fact latches. Nothing classifies an item as having a state machine. The gap-search detour, a guarded transition out of `check` on `pass`, is the pattern for an optional sub-step (`recipes/sindri/workflow.scoping.yaml`, the `check` transitions).
- **CI has no java** (`.github/workflows/ci.yml`: jobs `changes`, `bash-tests`, `gate`, `dotnet`, `scaffold`, `ci-result`).

## 3. The step: `step.model.check`

One block step. Any recipe can name it, and nothing about it is specific to scoping or build.

**Input.** `fact:workspace` (a directory), plus `with: { specs: <glob>, scope: all | touched }`:
- `specs` is the folders to check. Default `specs/tla/*/`.
- `scope: touched` checks only the folders whose modelled code or spec files the change touches (`fact:change`).

**What it does.**
1. Lists the spec folders.
2. Under `touched`, reads each folder's `README.md` `models:` line and keeps the folders the change touches. With none left, it ends `skipped`.
3. Runs `scripts/tlc-check.sh <dirs>` through `RepoCommandRunner`: one heavy slot (TLC is CPU-bound), the descriptor's `MaxDuration` (default 10 min) as the bound.
4. Reads the per-spec last lines.

**Outcomes.**

| Outcome | When | Facts |
|---|---|---|
| `pass` | every checked spec printed `tlc: PASS` | `fact:modelCheck`: per spec its name, distinct states, depth, bounds |
| `skipped` | `touched` and the change touches no modelled code | `fact:modelCheck` with `skipped: true` and the reason |
| `violation` | any spec printed `tlc: VIOLATION` | `fact:findings`: `MDL-VIOLATION-001` per spec, the property name and TLC's trace (scrubbed, tail-capped like `BLD-COMMAND-001`) |
| `blocked` | exit 3: no java or no jar | `fact:findings`: `MDL-TOOL-001` with tlc.sh's fix line |
| step failure | `tlc: ERROR` (the spec doesn't parse) or the runner itself errored | `step.failed`, so the engine's usual retry, park and escalation apply |

**Rules.**
- The step never calls a model checked unless TLC printed `PASS` in this run. A pass always carries its bounds.
- A spec may `EXTENDS` only TLC's standard modules. The step refuses (`MDL-SPEC-001`) a spec that extends `IOUtils` or any other module that can run commands, so a spec in a worktree can't run a process on the host. That's an honest-mistake guard, not a sandbox.
- `blocked` is its own outcome, not `failed`. A box without java isn't a broken model, and the recipe decides what to do (park with the fix line, below).

The `models:` line (agreed with api). Line 1 of every `specs/tla/<Name>/README.md` is exactly `models: <path>[:line] <path>[:line] ...`, and the `# <Name>` heading goes on line 2.
- Paths are repo-relative and separated by spaces.
- `scope: touched` matches on the path only and ignores `:line`. Line numbers drift with every edit, and a stale line must not drop a spec from the check.
- A path ending in `/` names a folder and matches anything under it.
- `scripts/tlc-check.sh` warns on a missing `models:` line or a path that doesn't exist, without changing its exit code.

## 4. Where recipes use it

### 4.1 The build line (`workflow.build`)

Add a `model` state after `checks`. `checks` → `model` on `pass`, and `model` routes:
- `pass` / `skipped` → `built`
- `violation` → `green`, with the findings (the builder fixes the code or, when the design changed on purpose, the spec)
- `blocked` → park `needs-answer`, with the fix line

The `with` is `scope: touched`, so a change that touches no modelled code costs one directory listing. This is a recipe version bump (6 → 7) with new eval cases: pass, skipped, violation-then-fixed, blocked.

**Alternative, zero engine code:** the repo adds `scripts/tlc-check.sh` to `workflow.build.commands` in its profile, and `step.build.checks` runs it like any other command. That works today. But it runs every spec on every change, a violation comes back as an untyped `BLD-COMMAND-001`, and a missing java fails the build instead of asking. I recommend the step. The alternative is a fine stopgap until the step ships.

### 4.2 Scoping (`workflow.scoping`)

Scoping is before code exists, so there's nothing in `specs/tla/` to check yet. The model has to be written first. That needs two states:

1. `model-gate`: a `step.judge.choose` classifier that asks one question of the draft. Does this item change a state machine, a protocol, a lock or lease, a queue, a retry, or anything where order and timing matter? It's a cheap model call, like `size`.
2. When the answer is yes:
   - `model-write`, an agent step, runs the formal-verify skill on the draft's design: the properties in plain words, the model, the cfg, and the witness checks. It writes them into the scoping packet's `model/` folder.
   - Then `model-check` (this step, `specs: model/`, `scope: all`) runs TLC on what the agent wrote. The agent's own "pass" is never taken as the result.
   - A `violation` goes back to `draft` with the trace as findings. The design has a real hole, and that's the point of running it here.

This is a detour out of `check` on `pass`, guarded by a fact latch (`fact:modelClosed`), the same shape as the gap search. When the build happens, the build agent moves `model/` into `specs/tla/<Name>/` in the PR. From then on the build line's `touched` check keeps it true.

## 5. TLA+ specs of the engine

Two folders, both run by the `tla` CI job.

### 5.1 `specs/tla/Takeover/`

**What it models:**
- `Core/Workflow/Engine/Rules/StateTimers.cs` (`Takeovers`, `HandedBack`)
- `Core/Workflow/Engine/Rules/Suspensions.cs` (takeover, `EndCurrent`, the queued parks)
- `TransitionDecision.EngineTriggers.cs` (takeover, handback, cancel)
- `Aw.Sindri.Workflows/TimerScopes.cs` (`Pause`, `Resume`)

**Actors:**
- the owner: takeover, handback, cancel
- the step: completes
- the state timer: fires
- the engine: a park arrives, an answer clears it

The timer's remaining time is a small counter (0..3).

**Properties, in plain words:**
- `TimerStoppedWhileTakenOver`: while the instance is taken over, no state timer is running.
- `NoTimeGained`: the time left after a handback is never more than the time left when the takeover started. Only a new state entry or an answer to a park arms the full timeout.
- `ParkSurvivesTakeover`: a park that arrives during a takeover is the suspension after the handback. The instance never goes back to active with that park lost.
- `AtMostOneSuspension`, plus `TypeOK`.
- `EndedIsFinal`: after a cancel or a terminal state, no timer runs and nothing changes.
- `IdleNeverHandsBack`: the 2 h `takeoverIdle` reminder never ends a takeover.
- Liveness, `HandbackResumes`: with the step and the timer weakly fair, a handback with time left is eventually followed by the step completing or the timer firing.

**Bounds:** 1 instance, 2 recipe states, up to 2 takeovers, up to 2 parks, remaining time 0..3.

### 5.2 `specs/tla/RecipeWorkflow/`

**What it models:** the pure core, `TransitionDecision.Decide`, on a fixed small recipe shaped like `workflow.build`: active → active with a loop cap of 2 → terminal, one wait state, one gate on one transition.

**Actors and triggers:**
- recipe triggers: `step.completed`, `step.failed`, `wait.satisfied`
- engine triggers: cancel, park, answer, escalate, rewind, takeover, handback, timer fire

**Properties:**
- `TypeOK`
- `LoopCapHolds`: a loop never runs past its cap. At the cap the instance parks `rounds`.
- `GateRefusalNeverFallsThrough`: a refused gate never takes the transition.
- `EndedIsFinal`, as above.
- `NoStaleTimer`: a timer from an earlier entry never moves the instance.
- Liveness, `EventuallySettles`: with fair steps and an owner who eventually answers, every instance reaches a terminal state or a park waiting on a person.

**Keeping a spec true to the code.** A spec drifts silently when the code changes. Each spec's README lists its actions next to the C# trigger or method each one models. A unit test in `Aw.Sindri.Tests`, `TlaSpecCoverageTests`, checks two things:
- the README names every engine trigger in the switch in `TransitionDecision.EngineTriggers.cs`;
- every C# name the README lists still exists.

So a new trigger fails the build until someone adds it to the spec, or says in the README why it's out of scope. It's a name check, not a proof that the spec matches the code. The spec stays an abstraction, and its README says what it leaves out: Temporal replay, the ledger mirror, desk timeouts, more than one instance.

## 6. Cost and limits

- TLC runs on the host. Each spec here should check in under 30 s at its bounds, so the `tla` CI job stays under 2 min. Any spec slower than that gets smaller bounds, not a longer job.
- A pass says nothing beyond its bounds, and every report says so.
- Java becomes a setup prerequisite for running the step on a box. `sindri doctor` gets one line from `tlc.sh doctor`: a warning, not a failure, since the step parks with the fix when java is missing. Per the team rule, that means updating aot-smoke's `expected.txt`.

## 7. Phases and parallel plan

| Phase | What | Depends on | Owner |
|---|---|---|---|
| M-0 | This note (docs PR) | — | engine |
| M-1 | `specs/tla/Takeover/`: spec, cfg, README, witness cfg; `TlaSpecCoverageTests` for it | api's runner PR (stacks on it while open) | engine |
| M-2 | `specs/tla/RecipeWorkflow/`: same shape | M-1's coverage test | engine |
| M-3 | `step.model.check`: red test first, then the step, the `MDL-*` rules, registration, the doctor line | api's runner PR | engine |
| M-4 | `workflow.build` v7: the `model` state, evals | M-3 | engine |
| M-5 | `workflow.scoping`: `model-gate`, `model-write`, `model-check`, evals | M-3, #2052 merged | engine (+ context/ui for the scoping view's new states) |

Parallel: M-1 and M-3 start together, with separate builders and trees. M-1 needs only TLA+ and the runner, M-3 only C#. M-2 follows M-1. M-4 and M-5 follow M-3 and can run side by side. A recipe edit means the Ui tests run in that PR's gate (team rule).

## 8. Open questions for the owner

1. **Build-line check: the step or the profile command?** I recommend the step (§4.1): it checks only specs whose code changed, reports a typed violation, and asks when java is missing. The profile command works today with no code, but runs every spec on every change.
2. **What happens when a box has no java?** I recommend parking with the fix line ("install java 11+, then `tlc.sh fetch`"), so nothing is ever called checked without TLC running. The other option is to skip with a warning on the run, which means a PR can merge with its model unchecked on that box (CI still checks it).
3. **Scoping: always ask, or only for some sizes?** I recommend asking the one-question classifier on every scoping run. It's one cheap model call, and the model write only runs on yes. The other option is to ask only for items sized M or bigger.
4. **When the scoping model finds a hole, who decides?** I recommend going back to `draft` with the trace, like any other gap. The other option is a desk stop where you read the trace first.
