# Sindri model check: a TLC step for recipes, and TLA+ specs of the engine

Status: draft for the owner's read (owner decision 2026-11-04, routed by meta). Lane: engine. Related: the formal-verify skill (#2052, ideas), the shared TLC runner and CI job (#2057, its `--root` / `--timeout` follow-up #2067, api), the projector and heavy-slot specs (api).

## 1. What this is

Two things the owner asked for:

1. **A model-check step any recipe can use.** Scoping runs it when the item has a state machine, a protocol or concurrency. The build line runs it as a check on a change that touches modelled code.
2. **TLA+ specs of Sindri's own engine**: the recipe instance's transitions, and takeover / handback (the state timer pauses on takeover and resumes with what was left on handback). TLC checks them in CI.

Both use one runner and one pinned jar. Nobody writes a second TLC script.

**How we'll know it's worth it.**
- Before it's wired in: the first spec (Takeover, M-1) must catch a real timer bug planted on purpose, the handback arming the full timeout instead of what was left. The bug is one the engine really had to get right (`StateTimers.cs:79-83`), not one invented to be caught. If TLC doesn't catch it at the spec's bounds, we stop before wiring the step into the build line and scoping (M-4, M-5). §7 has the gate.
- After it ships: every run leaves its result in the ledger (the step's facts). 30 days after M-4 merges, engine counts the runs, violations, false alarms and minutes spent, and brings the owner one line: keep it on, narrow it, or turn the build-line state off.

## 2. What already exists (read at main 8c86c54)

- **formal-verify skill** (#2052): `skills/formal-verify/scripts/tlc.sh doctor|fetch|check|translate`. `fetch` downloads the jar pinned in `skills/formal-verify/tla2tools.lock` (v1.7.4, sha256 checked, a mismatch refused) into `$AW_STATE_DIR/cache/bin/tla2tools-<v>/`. `check <Spec.tla> [--config F]` runs TLC with its state files in a temp dir and ends on one line: `tlc: PASS distinct=N depth=N`, `tlc: VIOLATION <invariant|property|deadlock|assumption> <name>` or `tlc: ERROR ...`. Exit 0 pass, 1 violation, 2 spec or tool error, 3 java or jar missing. `doctor` wants java 11 or newer and prints a fix naming JDK 21. `--jar` points at another jar and skips the pin; the step never passes it.
- **Layout and runner, agreed with api and ideas:**
  - One folder per spec: `specs/tla/<Name>/<Name>.tla` and one or more `*.cfg`, plus `MC<Name>.tla` when TLC needs model values, plus a `README.md` (§3, "The README").
  - Configs that are meant to fail (witnesses: "this planted bug is caught") live in `specs/tla/<Name>/gaps/`. A plain run skips them.
  - `scripts/tlc-check.sh [--root DIR] [--timeout S] [<spec-dir>...]` (#2057, #2067) runs `tlc.sh check` on every folder and cfg, one worker, and prints one line per check: `tlc-check: <Name>/<cfg>: PASS ...|VIOLATION ...|ERROR ...|TIMEOUT after S s`, then tlc.sh's full output indented by `  | ` when a check isn't PASS. The line format is pinned by a test row. `--timeout` ends a check's whole process group, java included (TERM, then KILL after 5 s). Its exit code is the worst seen (3 > 2 > 1 > 0). Its test is `scripts/tests/tlc-check.test.sh` (stub java). A `tla` CI job installs a JDK and runs the real specs.
  - No `binaries.lock` row: its format is per-platform `.tar.gz` and the jar is neither.
- **How a recipe runs a local tool today.** A deterministic step is a C# block step (`runtime: block`, `IBlockStep`, registered in `Aw.Sindri.Features/Blocks/BlockRegistrations.cs`). It runs commands through `RepoCommandRunner` (`Core/Checks/RepoCommandRunner.cs`: one heavy slot, one shared time budget, `BudgetFor` keeps 5 min of the step's `MaxDuration` back for the report) over the `ICheckCommands` port and `ProcessToolRunner`. Block steps run in the host worker as the `RunBlockStep` activity, not in the agent sandbox. The step maps the result to an outcome itself, as `step.build.checks` does (`Features/Build/BuildChecksStep.cs`, `BLD-COMMAND-001`).
- **How a recipe reacts.** A `step.completed` outcome must match a transition. A `step.failed` with no transition is retried twice by the engine, then parks as `unavailable` (`TransitionDecision.cs:135`, `Rules/Unavailables.cs`), which is a health item, not a question in the needs-you list, and carries no findings. So everything this step wants the owner or a builder to act on is an outcome, never a failure. A question for the owner is a desk state with buttons, like the build line's `scope-desk`.
- **The build line already runs repo commands.** `step.build.checks` runs every `workflow.build.commands` entry of the profile on the head, under a heavy slot. A failure routes back to `green` (`recipes/sindri/workflow.build.yaml`: `checks` → `green` on `failed`, inside the `revisions` loop, cap 6). A file outside the phase's allowed files goes to `scope-desk` first (`BranchRules.OutOfScope`).
- **Scoping** branches only on step outcomes, loop caps and fact latches. Nothing classifies an item as having a state machine. The gap-search detour, a guarded transition out of `check` on `pass` with its own loop (`gapsearch`, cap 3), is the pattern for an optional sub-step (`recipes/sindri/workflow.scoping.yaml`).
- **The installed toolkit** (`~/.agentic-workflow/toolkit`, `AW_TOOLKIT`) is a link to the owner's checkout, at whatever commit it's on. It's not a safe place to run a versioned tool from.
- **CI has no java** (`.github/workflows/ci.yml`) until #2057's `tla` job.

## 3. The step: `step.model.check`

One block step. Any recipe can name it, and nothing about it is specific to scoping or build.

**Input.** `fact:workspace` (a directory), plus `with: { specs: <glob>, scope: all | touched, witnesses: true | false }`:
- `specs` is the spec folders to check, relative to the workspace. Default `specs/tla/*/`.
- `scope: touched` checks only the folders whose modelled code or spec files the change touches (`fact:change`).
- `witnesses` (default true) also runs each folder's `gaps/*.cfg` and expects each to fail. A model whose planted bug isn't caught checks nothing.

**Which runner.** `scripts/install-cs.sh` copies `scripts/tlc-check.sh`, `skills/formal-verify/scripts/tlc.sh` and `tla2tools.lock` next to the published `sindri` (`$AW_STATE_DIR/bin/cs/<stamp>/tlc/`), with the runner's `TLC_SH` pointed at the copy. The step runs that copy, never the workspace's or the owner's checkout's: a work repo has no `scripts/tlc-check.sh`, scoping's workspace is a draft folder outside any git tree, and the checkout can sit on an old commit. The runner and the step then always come from the same commit, so a flag the step passes is a flag the runner knows. `AW_TLC_CHECK` overrides the path (tests). The step passes `--root <workspace>` and absolute folder paths.

**What it does.**
1. Lists the spec folders.
2. Checks each folder's files (rules below). A folder that breaks them is `MDL-SPEC-001` and isn't run.
3. Under `touched`, keeps the folders whose `models:` paths or own files the change touches. With none left, it ends `skipped`.
4. Runs the runner on those folders through `RepoCommandRunner`: one heavy slot (TLC is CPU-bound), `MaxDuration` 15 min (10 min for TLC after `BudgetFor`'s margin), `--timeout 180` per check, `--witnesses` when on (ask to api, the next follow-up after #2067: run `gaps/*.cfg` too and print `WITNESS-CAUGHT <property>` or `WITNESS-MISSED`). It runs folders in name order, and the environment it passes TLC is only `PATH`, `HOME`, `JAVA_HOME`, `TLC_HEAP` and `TMPDIR` (the step's own temp dir, deleted after the run, timeouts included).
5. Reads the `tlc-check:` lines, one per spec and cfg. Their result word decides the outcome, never the exit code, because the worst code hides a violation in one spec behind an error in another. The one exception: exit 3 means only "java or the jar is missing", and its line carries tlc.sh's fix line and no result word.
6. Reads each passed cfg's `CONSTANT` lines and the README's bounds line, so a pass carries its bounds whatever the runner prints.
7. When the step's time runs out, the cfgs that never ran are listed by name in `tooBig`. They're never counted as passed.

**Outcomes.** Every result is a `step.completed` outcome, so the recipe routes it and the findings ride with it.

| Outcome | When | What the instance gets |
|---|---|---|
| `pass` | every checked cfg printed `PASS`, and every witness was caught | `fact:modelCheck`: per spec and cfg, distinct states, depth, bounds |
| `skipped` | `touched`, and the change touches no modelled code | `fact:modelCheck` with `skipped: true` and the files it looked for |
| `violation` | any cfg printed `VIOLATION`, whatever the others printed | `fact:findings`: `MDL-VIOLATION-001` per broken property (shape in §4.3); an `ERROR`, `TIMEOUT` or missed witness elsewhere rides along as its own finding |
| `vacuous` | no violation, and a witness printed `WITNESS-MISSED` | `MDL-WITNESS-001`: which planted bug the model didn't catch, so the model checks nothing there |
| `specError` | no violation, and an `ERROR` that isn't out of memory (the spec doesn't parse, a bad cfg), or `MDL-SPEC-001` / `-003` | `MDL-SPEC-ERROR-001` with tlc.sh's message and the line it names |
| `tooBig` | no violation, and a `TIMEOUT`, an `ERROR ... OutOfMemoryError`, or cfgs left unrun | `MDL-TOOBIG-001`: which spec, its bounds, how long it ran, "make the bounds smaller" |
| `cantRun` | exit 3, or the runner copy is missing | `MDL-TOOL-001` with the fix for that case: java missing, java older than 11, jar not fetched, or "reinstall: `scripts/install-cs.sh`" |

A missing java isn't a broken model, so it's `cantRun`, the only outcome that asks the owner. The others go back to whoever writes the code or the model.

**Rules.**
- The step never calls a model checked unless TLC printed `PASS` for it in this run. A pass always carries its bounds, and every report says a pass means nothing past them.
- A spec folder holds only regular `.tla`, `.cfg` and `.md` files, plus a `gaps/` folder of `.cfg` files. TLC runs from the folder, so a stray `.class` or `.jar` could be loaded. A symlink or anything else is `MDL-SPEC-001`.
- Every module a spec pulls in names one of TLC's standard modules (`Naturals`, `Integers`, `Sequences`, `FiniteSets`, `Bags`, `TLC`) or another module in the same folder. The scan strips `\*` and `(* *)` comments first, reads an `EXTENDS` list across lines, and checks `INSTANCE` and `LOCAL INSTANCE` too. Anything else, `IOUtils` first, is `MDL-SPEC-001`, so a spec in a worktree can't run a process on the host. That's an honest-mistake guard, not a sandbox.
- TLC gets a heap cap (ideas, a small PR after #2062/#2063: `tlc.sh check` passes `-Xmx${TLC_HEAP:-2g}`, a full heap ends as `tlc: ERROR ... OutOfMemoryError`, and killing tlc.sh kills java). With the heap, the per-check timeout and one worker, a spec with loose bounds runs out of time or memory instead of filling the disk.
- A trace is scrubbed with `Aw.Redaction` before it becomes a finding.
- M-3's tests cover each rule: the scan cases above, a symlink, `--jar` never on the command line, a stub java that ignores SIGTERM is gone after the timeout, and the temp dir is gone after a timeout.

**The README.** One template for every `specs/tla/<Name>/README.md` (and every scoping `model/<Name>/README.md`):

```
models: src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/StateTimers.cs:57 src/Sindri/Aw.Sindri.Workflows/TimerScopes.cs
# Takeover
Bounds: 1 instance, 2 states, up to 2 takeovers, up to 2 parks.
- TimerStoppedWhileTakenOver: while you have an item taken over, its timer doesn't run.
- NoTimeGained: handing an item back never gives its timer more time than it had.
Actions: Takeover = TakeoverRequested · Handback = HandbackRequested · Fire = DeadlineFired
Leaves out: Temporal replay, the ledger mirror, desk timeouts, more than one instance.
```

- Line 1, `models:`, is read by the runner (warnings) and the step (`touched`). Paths are repo-relative and separated by spaces. Matching is on the path only and ignores `:line`, since line numbers drift with every edit. A path ending in `/` names a folder and matches anything under it.
- `Bounds:` and the property lines (`- <Name>: <plain words>`) are read by the step for the owner's text (§4.3).
- `Actions:` and `Leaves out:` are read by `TlaSpecCoverageTests` (§5.2).
- No `models:` line: the folder is always checked under `touched`. It never silently drops out.
- A path that doesn't exist: the step adds an `MDL-SPEC-002` finding to the run (not an outcome), so a stale path shows on the page. The folder is still matched on its own files. Scoping's models have no repo yet, so the step skips this check under `specs: model/*/`.

## 4. Where recipes use it

### 4.1 The build line (`workflow.build`)

Add two states after `checks`: `model` (this step, `scope: touched`) and `model-desk` (the owner's buttons when it can't run). `checks` → `model` on `pass`, and `model` routes:
- `pass` / `skipped` → `built`
- `violation`, `vacuous`, `specError`, `tooBig` → `green`, with the findings. The builder fixes the code or, when the design changed on purpose, the spec or its bounds. This is inside the existing `revisions` loop (cap 6), so a fix that keeps breaking the model parks `rounds` like any other failing check.
- `cantRun` → `model-desk`, whose answers are **Check again** (→ `model`) and **Stop** (→ `stopped`).

**The specs are in the plan.** When scoping wrote a model, the scoping packet lists `specs/tla/<Name>/` in the phase's allowed files, so moving it there doesn't stop at `scope-desk`. The step checks the move: when the active packet has `model/<Name>/` and the head has no `specs/tla/<Name>/`, that's `MDL-SPEC-003`, a `specError`, back to `green`.

**The PR says what was checked.** The PR body gets one line from `fact:modelCheck`: "Model checked: Takeover (1,204 states at up to 2 takeovers and 2 parks)", or "Model check skipped: no modelled code changed".

The `with` is `scope: touched`, so a change that touches no modelled code costs one directory listing. This is a recipe version bump (6 → 7) with new eval cases: pass, skipped, violation-then-fixed, violation at the revisions cap, vacuous, tooBig, cantRun then Check again, cantRun then Stop. M-4 also checks what an instance started on v6 does at the bump, under the engine's pin rules, and adds an eval for it.

**A repo that already lists `scripts/tlc-check.sh` in `workflow.build.commands`** would run every spec twice. `sindri doctor` warns on that profile entry once v7 ships.

**Alternative, zero engine code:** the repo adds the runner to `workflow.build.commands` in its profile, and `step.build.checks` runs it like any other command. That works today. But it runs every spec on every change, a violation comes back as an untyped `BLD-COMMAND-001`, and a missing java fails the build instead of asking. I recommend the step. The alternative is a fine stopgap until the step ships.

### 4.2 Scoping (`workflow.scoping`)

Scoping is before code exists, so there's nothing in `specs/tla/` to check yet. The model has to be written first. That needs four states:

1. `model-gate`: a `step.judge.choose` classifier that asks one question of the draft. Does this item change a state machine, a protocol, a lock or lease, a queue, a retry, or anything where order and timing matter? It's a cheap model call, like `size`.
2. When the answer is yes:
   - `model-write`, an agent step, runs the formal-verify skill on the draft's design: the README (template above), the model, the cfg, and at least one `gaps/` cfg with a planted bug the model must catch. It writes them into the scoping packet as `model/<Name>/`, the same layout as `specs/tla/<Name>/`. It's in a new `models` loop (cap 2).
   - Then `model-check` (this step, `specs: model/*/`, `scope: all`, `witnesses: true`) runs TLC on what the agent wrote. The agent's own "pass" is never taken as the result.
   - `violation` → `draft` with the findings. The design has a real hole, and that's the point of running it here.
   - `vacuous`, `specError`, `tooBig` → `model-write` with the findings: the model is wrong, not the design.
   - `cantRun` → `model-desk`, with **Check again** and **Skip the model** (the scope goes on without one, and the packet says so).
   - Past the `models` cap, the next round parks `rounds`, and the owner reads the trace.

This is a detour out of `check` on `pass`, guarded by a fact latch (`fact:modelClosed`), the same shape as the gap search. When the build happens, the build agent moves `model/<Name>/` into `specs/tla/<Name>/` in the PR (§4.1 checks it). From then on the build line's `touched` check keeps it true.

**The packet changes** (M-5): the scoping kind allows `model/<Name>/` files (today `PKT-SCP-002` refuses them), hashes them by relative path in sorted order like the other files, and `freeze` re-checks the folder rules above. That's a change to `schemas/scoping/` and `aw-packets`, with the packet docs.

### 4.3 What the owner sees

Everything below is text. No result is shown by color alone, and the trace is a plain text block the keyboard reaches like any finding.

**The instance view's step line** (the state's `displayName` and the outcome's words). Bounds come from the README's `Bounds:` line, in plain words:

| Result | Line |
|---|---|
| running | "Checking the design model: Takeover, RecipeWorkflow (up to 10 min)" |
| `pass` | "Model checked: Takeover passed, 1,204 states, at up to 2 takeovers and 2 parks. Nothing past that is checked." |
| `skipped` | "Model check skipped: this change touches none of the modelled code (Takeover, RecipeWorkflow)." |
| `violation` | "The model found a hole: NoTimeGained — handing an item back gave its timer more time than it had." |
| `vacuous` | "The model missed the bug planted in it (gaps/FullTimeoutOnHandback), so it checks nothing there. Sent back to fix the model." |
| `specError` | "The model doesn't parse: Takeover.tla line 42, unknown operator Remain. Sent back to fix it." |
| `tooBig` | "The model is too big to check: Takeover ran 3 min at its bounds. Sent back to make the bounds smaller." |
| `cantRun` | "The model check can't run: java isn't installed. Install java 21 (`brew install openjdk@21` or `sudo apt-get install -y openjdk-21-jre-headless`), then press Check again." |

**`model-desk`** is a desk state like `scope-desk`, so it shows in the needs-you list with its buttons: the question is the `cantRun` line, and the buttons are **Check again** and **Stop** (build) or **Skip the model** (scoping).

**Scoping's new states** have these display names: `model-gate` "Does this need a design model?", `model-write` "Write the design model", `model-check` "Check the design model", `model-desk` "The model check can't run". Each has a `why` line like the other scoping states.

**A violation's finding** (`MDL-VIOLATION-001`), in this order:
1. The property in plain words, from the README's line for it.
2. The steps that broke it, one line per TLC state: the action's name and the variables that changed. "1. Start · 2. Takeover (timer paused, 2 left) · 3. Park arrives · 4. Handback (timer resumed, 3 left)".
3. The full scrubbed trace behind an expander. When it's capped, it keeps the first 5 states and the last 20, never only the tail: a counterexample is read from its start.

The app has no viewer for this today: a finding shows as a Facts row with a View button. M-3 adds a findings view for `MDL-VIOLATION-001` (ui).

**No java on the box.** `sindri doctor` gets one line from `tlc.sh doctor`:
- `ok   model check: java 21, tla2tools 1.7.4`
- `warn model check: java isn't installed; the model check stops for you until it is. fix: brew install openjdk@21`

The app's Health page gets a Tools section with the same line (the server's status gains the tool check; api + ui, part of M-3). Setup installs java (§6), so this should only show on a box where that failed.

## 5. TLA+ specs of the engine

Two folders, both run by the `tla` CI job.

### 5.1 `specs/tla/Takeover/`

**What it models:**
- `Core/Workflow/Engine/Rules/StateTimers.cs` (`Takeovers`, `HandedBack`)
- `Core/Workflow/Engine/Rules/Suspensions.cs` (takeover, `EndCurrent`, the queued parks)
- `TransitionDecision.EngineTriggers.cs` (takeover, handback, cancel)
- `Aw.Sindri.Workflows/TimerScopes.cs` (`Pause`, `Resume`)

**Actors:**
- the owner: takeover, handback, cancel, answer
- the step: completes
- the state timer: fires
- the engine: a park arrives

The timer's remaining time is `none` or a small counter (0..3). `none` is a takeover that kept no timer, a start taken over (`StateTimers.cs:54`), and it's different from a timer that ran out.

**A handback has three results** (`StateTimers.cs:79-83`), and the spec has all three:
- back to the park the takeover covered: no timer (the park has none);
- the takeover paused a running timer: resume it with what was left;
- the takeover kept no timer: arm the state's full timeout.

**Properties, in plain words:**
- `TimerStoppedWhileTakenOver`: while the instance is taken over, no state timer is running.
- `NoTimeGained`: when a takeover paused a running timer, the time left after the handback is never more than the time left when it paused. A takeover that kept no timer arms the full timeout on handback, and so does a new state entry or an answer to a park.
- `ParkSurvivesTakeover`: a park that arrives during a takeover is the suspension after the handback. The instance never goes back to active with that park lost.
- `NoTimerOnPark`: a handback back to a park runs no state timer.
- `AtMostOneSuspension`, plus `TypeOK`.
- `EndedIsFinal`: after a cancel or a terminal state, no timer runs and nothing changes.
- `IdleNeverHandsBack`: the 2 h `takeoverIdle` reminder never ends a takeover.
- Liveness, `HandbackResumes`: with the step and the timer weakly fair, a handback with time left is eventually followed by the step completing or the timer firing.

**Bounds:** 1 instance, 2 recipe states, up to 2 takeovers, up to 2 parks, remaining time none or 0..3.

**The planted bug** (`gaps/FullTimeoutOnHandback.cfg`, the go/no-go in §7): a constant switches the handback to arm the full timeout even when the takeover paused a running timer. TLC must report `VIOLATION invariant NoTimeGained` with a trace of at most 6 states. In this repo the runner's `--witnesses` runs it in the `tla` CI job.

### 5.2 `specs/tla/RecipeWorkflow/`

**What it models:** the pure core, `TransitionDecision.Decide`, on a fixed small recipe shaped like `workflow.build`: active → active with a loop cap of 2 → terminal, one wait state, one gate on one transition.

**Triggers modelled**, by their C# types in `Core/Workflow/Engine/Triggers/`: `StepCompleted`, `StepFailed`, `WaitSatisfied`, `AnswerGiven`, `CancelRequested`, `RewindRequested`, `TakeoverRequested`, `HandbackRequested`, `CommandFired`, `DeadlineFired`. Parks and escalations aren't triggers: they're what `Decide` does with these, and the spec models them as results.

**Properties:**
- `TypeOK`
- `LoopCapHolds`: a loop never runs past its cap. At the cap the instance parks `rounds`.
- `GateRefusalNeverFallsThrough`: a refused gate never takes the transition.
- `EndedIsFinal`, as above.
- `NoStaleTimer`: a timer from an earlier entry never moves the instance.
- `UnmatchedFailureRetriesThenParks`: a `StepFailed` with no transition runs the step again at most twice, then parks `unavailable`; an unmatched `StepCompleted` never moves the instance.
- Liveness, `EventuallySettles`: with fair steps and an owner who eventually answers, every instance reaches a terminal state or a park waiting on a person.

**Planted bug:** `gaps/LoopCapOffByOne.cfg` lets a loop run one round past its cap; TLC must report `LoopCapHolds`.

**Keeping a spec true to the code.** A spec drifts silently when the code changes. A unit test in `Aw.Sindri.Tests`, `TlaSpecCoverageTests`, reads every README's `Actions:` and `Leaves out:` lines and checks two things:
- every trigger type in `Core/Workflow/Engine/Triggers/` that derives from `Trigger`, and every handler in `TransitionDecision.Timers.cs`, is named in some spec's README, as an action or under `Leaves out:` with a reason (`SessionLost`, `LaunchRefused`, `OpenRequested`, `OutboxRefused`, `RecipeWake`, `RuntimeUnavailable` and `ChildChanged` start there);
- every C# name a README lists still exists.

So a new trigger fails the build until someone adds it to a spec, or says why it's out of scope. It's a name check, not a proof that the spec matches the code. The spec stays an abstraction, and its README says what it leaves out.

## 6. Cost, setup and limits

- TLC runs on the host. Each spec here should check in under 30 s at its bounds, so the `tla` CI job stays under 2 min. Any spec slower than that gets smaller bounds, not a longer job. A change to a spec's bounds shows in the pass line and the PR body, so a shrink is never silent.
- A pass says nothing beyond its bounds, and every report says so.
- **Setup installs java and the jar.** `config/lib/sys-pkg.sh` gets the JDK 21 package names (`openjdk-21-jre-headless` on apt, `java-21-openjdk-headless` on dnf/yum, `openjdk@21` on brew, linked onto `PATH` since brew keeps it off by default; the pacman and zypper names are checked on a real box in M-3, with a row in `config/lib/tests/sys-pkg.test.sh`). `scripts/install-deps.sh` installs it when `tlc.sh doctor` says java is missing or older than 11, then runs `tlc.sh fetch`. `AW_NO_SYSTEM_INSTALL=1` prints the command instead, as it does for every other package.
- `sindri doctor` gets the line in §4.3: a warning, not a failure, since the step asks when java is missing. Per the team rule, that means updating aot-smoke's `expected.txt`.
- Docs: `docs/csharp/sindri-model-check.md` (the step, its `with`, the README template, the outcomes and findings, how to add a spec and a witness) ships with M-3; `AGENTS.md` gets the runner's line from #2057 and a pointer to the doc. New env vars (`AW_TLC_CHECK`, and `TLC_HEAP` from ideas) go in `.env.example`.

## 7. Phases and parallel plan

| Phase | What | Depends on | Owner |
|---|---|---|---|
| M-0 | This note (docs PR) | — | engine |
| M-1 | `specs/tla/Takeover/`: spec, cfg, README, the planted-bug `gaps/` cfg; `TlaSpecCoverageTests` | #2057 (stacks on it while open), api's `--witnesses` | engine |
| M-2 | `specs/tla/RecipeWorkflow/`: same shape | M-1's coverage test | engine |
| M-3 | `step.model.check`: red test first, then the step, the `MDL-*` findings, registration, the runner copy in install-cs.sh, the doctor line, sys-pkg.sh + install-deps.sh, the doc | #2067 (`--root`, `--timeout`), api's `--witnesses`, ideas' `TLC_HEAP` PR: all three merged before M-3 merges | engine |
| M-3u | The findings view for `MDL-VIOLATION-001`; the Health page's Tools section | M-3's finding shape; the status field (api) | ui, api |
| M-4 | `workflow.build` v7: `model`, `model-desk`, the PR body line, the plan's spec files, evals, the doctor's double-run warning | M-3, and M-1's go/no-go | engine |
| M-5 | `workflow.scoping`: `model-gate`, `model-write`, `model-check`, `model-desk`, the `models` loop, the packet's `model/` folder, evals | M-3, M-1's go/no-go | engine (+ context/ui for the scoping view's new states) |

**Go/no-go after M-1.** M-1's PR shows TLC catching the planted handback bug (`gaps/FullTimeoutOnHandback.cfg`): the violation line, its trace, and the run time at the spec's bounds. If it's caught, M-4 and M-5 go. If not, they wait, and the note comes back to the owner with what the spec missed.

Parallel: M-1 and M-3 start together, with separate builders and trees. M-1 needs only TLA+ and the runner, M-3 only C#. M-2 follows M-1. M-3u follows M-3's finding shape. M-4 and M-5 follow M-3 and the go/no-go, and can run side by side. A recipe edit means the Ui tests run in that PR's gate (team rule).

## 8. Open questions for the owner

1. **Build-line check: the step or the profile command?** I recommend the step (§4.1): it checks only specs whose code changed, reports a typed violation, and asks when java is missing. The profile command works today with no code, but runs every spec on every change.
2. **What happens when a box has no java?** I recommend stopping at the desk with the install line and a Check again button (§4.3), so nothing is ever called checked without TLC running. Setup installs java, so this should be rare. The other option is to skip with a warning on the run, which means a PR can merge with its model unchecked on that box (CI still checks it).
3. **Scoping: always ask, or only for some sizes?** I recommend asking the one-question classifier on every scoping run. It's one cheap model call, and the model write only runs on yes. The other option is to ask only for items sized M or bigger.
4. **When the scoping model finds a hole, who decides?** I recommend going back to `draft` with the trace, like any other gap, with two tries before it stops for you (§4.2). The other option is a desk stop where you read the trace first, every time.
