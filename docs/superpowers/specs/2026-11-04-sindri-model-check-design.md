# Sindri model check: a TLC step for recipes, and TLA+ specs of the engine

Status: draft for the owner's read (owner decision 2026-11-04, routed by meta). Lane: engine. Related: the formal-verify skill (#2052, ideas), the shared TLC runner and CI job (#2057, api), the projector and heavy-slot specs (api).

## 1. What this is

Two things the owner asked for:

1. **A model-check step any recipe can use.** Scoping runs it when the item has a state machine, a protocol or concurrency. The build line runs it as a check on a change that touches modelled code.
2. **TLA+ specs of Sindri's own engine**: the recipe instance's transitions, and takeover / handback (the state timer pauses on takeover and resumes with what was left on handback). TLC checks them in CI.

Both use one runner and one pinned jar. Nobody writes a second TLC script.

**How we'll know it's worth it.** The first spec (Takeover, M-1) must catch a real timer bug planted on purpose: the handback arming the full timeout instead of what was left. If TLC doesn't catch it at the spec's bounds, we stop before wiring the step into the build line and scoping (M-4, M-5) and rethink. §7 has the gate.

## 2. What already exists (read at main b7531c3)

- **formal-verify skill** (#2052): `skills/formal-verify/scripts/tlc.sh doctor|fetch|check|translate`. `fetch` downloads the jar pinned in `skills/formal-verify/tla2tools.lock` (v1.7.4, sha256 checked, a mismatch refused) into `$AW_STATE_DIR/cache/bin/tla2tools-<v>/`. `check <Spec.tla> [--config F]` runs TLC with its state files in a temp dir and ends on one line: `tlc: PASS distinct=N depth=N`, `tlc: VIOLATION <invariant|property|deadlock|assumption> <name>` or `tlc: ERROR ...`. Exit 0 pass, 1 violation, 2 spec or tool error, 3 java or jar missing. `--jar` points at another jar and skips the pin; the step never passes it.
- **Layout and runner, agreed with api and ideas:**
  - One folder per spec: `specs/tla/<Name>/<Name>.tla` and one or more `*.cfg`, plus `MC<Name>.tla` when TLC needs model values, plus a `README.md` (§3, "The README").
  - Configs that are meant to fail (witnesses: "this planted bug is caught") live in `specs/tla/<Name>/gaps/`. The runner doesn't run them; the spec's own test does, and expects `VIOLATION`.
  - `scripts/tlc-check.sh [<spec-dir>...]` (#2057) runs `tlc.sh check` on every folder and cfg, one worker, and prints one line per check: `tlc-check: <Name>/<cfg>: PASS ...|VIOLATION ...|ERROR ...`, then tlc.sh's full output indented by `  | ` when a check isn't PASS. Its exit code is the worst seen (3 > 2 > 1 > 0). Its test is `scripts/tests/tlc-check.test.sh` (stub java). A `tla` CI job installs a JDK and runs the real specs.
  - No `binaries.lock` row: its format is per-platform `.tar.gz` and the jar is neither.
- **How a recipe runs a local tool today.** A deterministic step is a C# block step (`runtime: block`, `IBlockStep`, registered in `Aw.Sindri.Features/Blocks/BlockRegistrations.cs`). It runs commands through `RepoCommandRunner` (`Core/Checks/RepoCommandRunner.cs`: one heavy slot, one shared time budget, `BudgetFor` keeps 5 min of the step's `MaxDuration` back for the report) over the `ICheckCommands` port and `ProcessToolRunner`. Block steps run in the host worker as the `RunBlockStep` activity, not in the agent sandbox, and never retry (RCP-TIME-003). The step maps the result to an outcome itself, as `step.build.checks` does (`Features/Build/BuildChecksStep.cs`, `BLD-COMMAND-001`).
- **How a recipe reacts.** A `step.completed` outcome must match a transition, or the instance is malformed. A `step.failed` with no transition parks the instance `needs-answer`, and the owner's answer re-runs the state.
- **The build line already runs repo commands.** `step.build.checks` runs every `workflow.build.commands` entry of the profile on the head, under a heavy slot. A failure routes back to `green` (`recipes/sindri/workflow.build.yaml`: `checks` → `green` on `failed`, inside the `revisions` loop, cap 6).
- **Scoping** branches only on step outcomes, loop caps and fact latches. Nothing classifies an item as having a state machine. The gap-search detour, a guarded transition out of `check` on `pass` with its own loop (`gapsearch`, cap 3), is the pattern for an optional sub-step (`recipes/sindri/workflow.scoping.yaml`).
- **CI has no java** (`.github/workflows/ci.yml`) until #2057's `tla` job.

## 3. The step: `step.model.check`

One block step. Any recipe can name it, and nothing about it is specific to scoping or build.

**Input.** `fact:workspace` (a directory), plus `with: { specs: <glob>, scope: all | touched }`:
- `specs` is the spec folders to check, relative to the workspace. Default `specs/tla/*/`.
- `scope: touched` checks only the folders whose modelled code or spec files the change touches (`fact:change`).

**Which runner.** The step runs the runner from the installed toolkit, `$AW_TOOLKIT/scripts/tlc-check.sh` (`AW_TOOLKIT` is the toolkit root the skills already read; unset, the `~/.agentic-workflow/toolkit` link setup.sh makes), never from the workspace: a work repo has no `scripts/tlc-check.sh`, and scoping's workspace is a draft folder outside any git tree. It passes `--root <workspace>` so the `models:` paths resolve against the repo being checked (ask to api: #2057 resolves them against its own checkout today). A missing runner is the same as missing java: `MDL-TOOL-001`.

**What it does.**
1. Lists the spec folders.
2. Checks each folder's files (rules below). A folder that breaks them is `MDL-SPEC-001` and isn't run.
3. Under `touched`, keeps the folders whose `models:` paths or own files the change touches. With none left, it ends `skipped`.
4. Runs the runner on those folders through `RepoCommandRunner`: one heavy slot (TLC is CPU-bound), `MaxDuration` 15 min (10 min for TLC after `BudgetFor`'s margin), and a per-spec limit `--timeout 180` (ask to api: the runner kills that check's whole process group, java included, and prints `TIMEOUT after 180 s`; its test uses a stub java that ignores SIGTERM).
5. Reads the `tlc-check:` lines, one per spec and cfg. It never reads the exit code, because the worst code hides a violation behind an error in another spec.
6. Reads each passed cfg's `CONSTANT` lines itself, so a pass carries its bounds whatever the runner prints.

**Outcomes and failures.**

| Result | When | What the instance gets |
|---|---|---|
| `pass` | every checked cfg printed `PASS` | `fact:modelCheck`: per spec and cfg, distinct states, depth, bounds |
| `skipped` | `touched`, and the change touches no modelled code | `fact:modelCheck` with `skipped: true` and which files it looked for |
| `violation` | any cfg printed `VIOLATION`, whatever the others printed | `fact:findings`: `MDL-VIOLATION-001` per broken property (shape in §4.3); an `ERROR` or `TIMEOUT` elsewhere rides along as its own finding |
| `step.failed` `tool-missing` | java missing, java too old, jar not fetched, or no runner (runner exit 3 or not found) | `MDL-TOOL-001` with the fix line for that case; parks `needs-answer` |
| `step.failed` `spec-error` | an `ERROR` line and no violation (the spec doesn't parse, a bad cfg), or `MDL-SPEC-001` | `MDL-SPEC-ERROR-001` with tlc.sh's message; parks `needs-answer` |
| `step.failed` `timeout` | a `TIMEOUT` line and no violation, or the step ran out of time | `MDL-TIMEOUT-001`: which spec, its bounds, "make the bounds smaller"; parks `needs-answer` |

Missing java isn't a broken model, so it isn't `violation`. It's a failure the owner fixes once, and the park says how. Block steps don't retry by themselves, so each failure parks, and the owner's answer re-runs the step.

**Rules.**
- The step never calls a model checked unless TLC printed `PASS` for it in this run. A pass always carries its bounds, and every report says a pass means nothing past them.
- A spec folder holds only `.tla`, `.cfg` and `.md` files, plus a `gaps/` folder of `.cfg` files. TLC runs from the folder, so a stray `.class` or `.jar` could be loaded. Anything else is `MDL-SPEC-001`.
- Every `EXTENDS` and `INSTANCE` names one of TLC's standard modules (`Naturals`, `Integers`, `Sequences`, `FiniteSets`, `Bags`, `TLC`) or another module in the same folder. Anything else, `IOUtils` first, is `MDL-SPEC-001`, so a spec in a worktree can't run a process on the host. That's an honest-mistake guard, not a sandbox.
- TLC gets a heap cap (ask to ideas: `tlc.sh check` passes `-Xmx` from `TLC_HEAP`, default 2 GB) and its state files go under the step's own temp dir, deleted after the run. With the heap, the per-spec time limit and one worker, a spec with loose bounds runs out of time instead of filling the disk.

**The README.** Line 1 of every `specs/tla/<Name>/README.md` is exactly `models: <path>[:line] <path>[:line] ...` (agreed with api), line 2 is the `# <Name>` heading, and the rest says the code it models (file:line), its properties in plain words, one line each, and the cfg's bounds.
- Paths are repo-relative and separated by spaces.
- `scope: touched` matches on the path only and ignores `:line`. Line numbers drift with every edit, and a stale line must not drop a spec from the check.
- A path ending in `/` names a folder and matches anything under it.
- No `models:` line: the folder is always checked under `touched`. It never silently drops out.
- A path that doesn't exist: the runner warns (exit code unchanged), and the step adds an `MDL-SPEC-002` finding to the run, so a stale path is on the page, not just in a log. The folder is still matched on its own files.

## 4. Where recipes use it

### 4.1 The build line (`workflow.build`)

Add a `model` state after `checks`. `checks` → `model` on `pass`, and `model` routes:
- `pass` / `skipped` → `built`
- `violation` → `green`, with the findings (the builder fixes the code or, when the design changed on purpose, the spec). This is inside the existing `revisions` loop (cap 6), so a fix that keeps breaking the model parks `rounds` like any other failing check.
- failures (`tool-missing`, `spec-error`, `timeout`) have no transition and park `needs-answer`.

The `with` is `scope: touched`, so a change that touches no modelled code costs one directory listing. This is a recipe version bump (6 → 7) with new eval cases: pass, skipped, violation-then-fixed, violation at the revisions cap, each failure parks.

**Alternative, zero engine code:** the repo adds `scripts/tlc-check.sh` to `workflow.build.commands` in its profile, and `step.build.checks` runs it like any other command. That works today. But it runs every spec on every change, a violation comes back as an untyped `BLD-COMMAND-001`, and a missing java fails the build instead of asking. I recommend the step. The alternative is a fine stopgap until the step ships.

### 4.2 Scoping (`workflow.scoping`)

Scoping is before code exists, so there's nothing in `specs/tla/` to check yet. The model has to be written first. That needs three states:

1. `model-gate`: a `step.judge.choose` classifier that asks one question of the draft. Does this item change a state machine, a protocol, a lock or lease, a queue, a retry, or anything where order and timing matter? It's a cheap model call, like `size`.
2. When the answer is yes:
   - `model-write`, an agent step, runs the formal-verify skill on the draft's design: the properties in plain words, the model, the cfg, and a `gaps/` cfg that shows a planted hole is caught. It writes them into the scoping packet as `model/<Name>/`, the same layout as `specs/tla/<Name>/`. This adds `model/` to the scoping packet's files and hash: a schema change in `schemas/scoping/` and `aw-packets`, part of M-5.
   - Then `model-check` (this step, `specs: model/*/`, `scope: all`) runs TLC on what the agent wrote. The agent's own "pass" is never taken as the result.
   - A `violation` goes back to `draft` with the findings. The design has a real hole, and that's the point of running it here. A new `models` loop (cap 2) bounds it: a third violation parks `rounds`, and the owner reads the trace.

This is a detour out of `check` on `pass`, guarded by a fact latch (`fact:modelClosed`), the same shape as the gap search. When the build happens, the build agent moves `model/<Name>/` into `specs/tla/<Name>/` in the PR. From then on the build line's `touched` check keeps it true.

### 4.3 What the owner sees

Everything below is text. No result is shown by color alone, and the trace is a plain text block the keyboard reaches like any finding.

**The instance view's step line** (the state's `displayName` and the outcome's words):

| Result | Line |
|---|---|
| running | "Checking the design model: Takeover, RecipeWorkflow (up to 10 min)" |
| `pass` | "Model checked: Takeover passed, 1,204 states, depth 9, at 2 takeovers, 2 parks, timer 0..3. Nothing is checked past these bounds." |
| `skipped` | "Model check skipped: this change touches none of the modelled code (Takeover, RecipeWorkflow)." |
| `violation` | "The model found a hole: NoTimeGained — after a handback the timer had more time than when the takeover began." |
| `tool-missing` | "The model check can't run: java isn't installed. Install java 11 or newer, run `tlc.sh fetch`, then press Retry." (one line per case: java missing, java too old, jar not fetched, toolkit runner missing) |
| `spec-error` | "The model doesn't parse: Takeover.tla line 42, unknown operator Remain." |
| `timeout` | "The model check ran out of time on Takeover at its bounds. Make the bounds smaller in Takeover.cfg." |

**A violation's finding** (`MDL-VIOLATION-001`), in this order:
1. The property in plain words, taken from the spec README's line for it.
2. The steps that broke it, one line per TLC state: the action's name and the variables that changed. "1. Start · 2. Takeover (timer paused, 2 left) · 3. Park arrives · 4. Handback (timer resumed, 3 left)".
3. The full scrubbed trace behind an expander. When it's capped, it keeps the first 5 states and the last 20, never only the tail: a counterexample is read from its start.

**No java on the box.** The park's question is the fix line above, and its one answer is Retry, which re-runs the step. `sindri doctor` gets a warning line from `tlc.sh doctor`, and the app's Health page gets a matching cell, "Model check: java missing", with the same fix (ui, part of M-3). Setup installs java (§6), so this park should only show on a box where that failed.

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

The timer's remaining time is `none` or a small counter (0..3). `none` is a takeover that kept no timer, a start taken over (`StateTimers.cs:54`), and it's different from a timer that ran out.

**Properties, in plain words:**
- `TimerStoppedWhileTakenOver`: while the instance is taken over, no state timer is running.
- `NoTimeGained`: when a takeover paused a running timer, the time left after the handback is never more than the time left when it paused. A takeover that kept no timer arms the state's full timeout on handback, and so does a new state entry or an answer to a park.
- `ParkSurvivesTakeover`: a park that arrives during a takeover is the suspension after the handback. The instance never goes back to active with that park lost.
- `AtMostOneSuspension`, plus `TypeOK`.
- `EndedIsFinal`: after a cancel or a terminal state, no timer runs and nothing changes.
- `IdleNeverHandsBack`: the 2 h `takeoverIdle` reminder never ends a takeover.
- Liveness, `HandbackResumes`: with the step and the timer weakly fair, a handback with time left is eventually followed by the step completing or the timer firing.

**Bounds:** 1 instance, 2 recipe states, up to 2 takeovers, up to 2 parks, remaining time none or 0..3.

**The planted bug** (`gaps/FullTimeoutOnHandback.cfg`, the go/no-go in §7): a constant switches the handback to arm the full timeout even when the takeover paused a running timer. TLC must report `VIOLATION invariant NoTimeGained` with a trace of at most 6 states.

### 5.2 `specs/tla/RecipeWorkflow/`

**What it models:** the pure core, `TransitionDecision.Decide`, on a fixed small recipe shaped like `workflow.build`: active → active with a loop cap of 2 → terminal, one wait state, one gate on one transition.

**Triggers modelled**, by their C# types in `Core/Workflow/Engine/Triggers/`: `StepCompleted`, `StepFailed`, `WaitSatisfied`, `CommandFired`, `DeadlineFired`, `TakeoverRequested`, `HandbackRequested`, plus cancel, park, answer, escalate and rewind under their own trigger types.

**Properties:**
- `TypeOK`
- `LoopCapHolds`: a loop never runs past its cap. At the cap the instance parks `rounds`.
- `GateRefusalNeverFallsThrough`: a refused gate never takes the transition.
- `EndedIsFinal`, as above.
- `NoStaleTimer`: a timer from an earlier entry never moves the instance.
- `UnmatchedFailureParks`: a `StepFailed` with no transition parks `needs-answer`; an unmatched `StepCompleted` never moves the instance.
- Liveness, `EventuallySettles`: with fair steps and an owner who eventually answers, every instance reaches a terminal state or a park waiting on a person.

**Planted bug:** `gaps/LoopCapOffByOne.cfg` lets a loop run one round past its cap; TLC must report `LoopCapHolds`.

**Keeping a spec true to the code.** A spec drifts silently when the code changes. Each spec's README lists its actions next to the C# type or method each one models. A unit test in `Aw.Sindri.Tests`, `TlaSpecCoverageTests`, checks two things:
- every trigger type in `Core/Workflow/Engine/Triggers/` (and every handler in `Timers.cs`) is named in some spec's README, as modelled or as out of scope with a reason;
- every C# name a README lists still exists.

So a new trigger fails the build until someone adds it to a spec, or says in the README why it's out of scope. It's a name check, not a proof that the spec matches the code. The spec stays an abstraction, and its README says what it leaves out: Temporal replay, the ledger mirror, desk timeouts, more than one instance.

## 6. Cost, setup and limits

- TLC runs on the host. Each spec here should check in under 30 s at its bounds, so the `tla` CI job stays under 2 min. Any spec slower than that gets smaller bounds, not a longer job.
- A pass says nothing beyond its bounds, and every report says so.
- **Setup installs java and the jar.** `scripts/install-deps.sh` installs a headless JDK 17 when java is missing (the same system-package path it uses today: brew on macOS, the box's package manager on Linux) and then runs `tlc.sh fetch`. `AW_NO_SYSTEM_INSTALL=1` prints the command instead, as it does for every other package.
- `sindri doctor` gets one line from `tlc.sh doctor`: a warning, not a failure, since the step parks with the fix when java is missing. Per the team rule, that means updating aot-smoke's `expected.txt`.
- Docs: `docs/csharp/sindri-model-check.md` (the step, its `with`, the README format, the findings, how to add a spec) ships with M-3; `AGENTS.md` gets the runner's line from #2057 and a pointer to the doc. The new env var `TLC_HEAP` goes in `.env.example`.

## 7. Phases and parallel plan

| Phase | What | Depends on | Owner |
|---|---|---|---|
| M-0 | This note (docs PR) | — | engine |
| M-1 | `specs/tla/Takeover/`: spec, cfg, README, the planted-bug `gaps/` cfg and its test; `TlaSpecCoverageTests` | #2057 (stacks on it while open) | engine |
| M-2 | `specs/tla/RecipeWorkflow/`: same shape | M-1's coverage test | engine |
| M-3 | `step.model.check`: red test first, then the step, the `MDL-*` findings, registration, the doctor line, install-deps.sh, the doc; the Health cell | #2057 with `--root` and `--timeout`; tlc.sh `TLC_HEAP` | engine (+ ui for the Health cell) |
| M-4 | `workflow.build` v7: the `model` state, evals | M-3, and M-1's go/no-go | engine |
| M-5 | `workflow.scoping`: `model-gate`, `model-write`, `model-check`, the `models` loop, the packet's `model/` folder, evals | M-3, M-1's go/no-go | engine (+ context/ui for the scoping view's new states) |

**Go/no-go after M-1.** M-1's PR shows TLC catching the planted handback bug (`gaps/FullTimeoutOnHandback.cfg`): the violation line, its trace, and the run time at the spec's bounds. If it's caught, M-4 and M-5 go. If not, they wait, and the note comes back to the owner with what the spec missed.

Parallel: M-1 and M-3 start together, with separate builders and trees. M-1 needs only TLA+ and the runner, M-3 only C#. M-2 follows M-1. M-4 and M-5 follow M-3 and the go/no-go, and can run side by side. A recipe edit means the Ui tests run in that PR's gate (team rule).

## 8. Open questions for the owner

1. **Build-line check: the step or the profile command?** I recommend the step (§4.1): it checks only specs whose code changed, reports a typed violation, and asks when java is missing. The profile command works today with no code, but runs every spec on every change.
2. **What happens when a box has no java?** I recommend parking with the fix line and a Retry (§4.3), so nothing is ever called checked without TLC running. Setup installs java, so this should be rare. The other option is to skip with a warning on the run, which means a PR can merge with its model unchecked on that box (CI still checks it).
3. **Scoping: always ask, or only for some sizes?** I recommend asking the one-question classifier on every scoping run. It's one cheap model call, and the model write only runs on yes. The other option is to ask only for items sized M or bigger.
4. **When the scoping model finds a hole, who decides?** I recommend going back to `draft` with the trace, like any other gap, with two tries before it stops for you (§4.2). The other option is a desk stop where you read the trace first, every time.
