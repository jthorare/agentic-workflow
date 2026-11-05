# The model check: `step.model.check` and the TLA+ specs

Phase **M-3** of the model-check design (`docs/superpowers/specs/2026-11-04-sindri-model-check-design.md`, §2, §3, §6). The spec says why; this page is what the step, the runner and the setup do. The step lives in `Aw.Sindri.Features/Blocks` and runs in the host worker as the `RunBlockStep` activity, not in the agent sandbox.

## What runs

One runner, one pinned jar. Nobody writes a second TLC script.

| Piece | Where | Job |
|---|---|---|
| `scripts/tlc-check.sh [--root D] [--timeout S] [--witnesses] [<spec-dir>...]` | repo, and `$AW_STATE_DIR/bin/cs/<stamp>/tlc/scripts/` | TLC on every `specs/tla/<Name>/` folder, once per `*.cfg`; one `tlc-check: <Name>/<cfg>: <result>` line per check |
| `skills/formal-verify/scripts/tlc.sh doctor\|fetch\|check\|translate` | repo, and `.../tlc/skills/formal-verify/scripts/` | java check, the pinned jar, one TLC run |
| `skills/formal-verify/tla2tools.lock` | repo, and `.../tlc/skills/formal-verify/` | the jar's version, sha256 and url |

`scripts/install-cs.sh` copies those three files next to the published `sindri`, keeping their repo-relative layout, because `tlc.sh` finds the lock one folder above itself. They are in `SINDRI_BUILD_PATHSPECS` (`scripts/lib/sindri-build-inputs.sh`), so a change to any of them makes a new build stamp. The step always runs that copy, never the workspace's or the owner's checkout's: a work repo has no `scripts/tlc-check.sh`, scoping's workspace is a folder outside any git tree, and the checkout can sit on an old commit. `install-cs.sh --dry-run` prints the copy.

`setup.sh` runs `scripts/install-deps.sh`, which reads `tlc.sh doctor`'s `java:` and `tla2tools:` lines (exit 3 when either is missing), installs JDK 21 when java is missing or older than 11 (`config/lib/sys-pkg.sh`, logical name `java`; under Homebrew `openjdk@21` is keg-only, so it is then linked with `brew link --force openjdk@21`), and runs `tlc.sh fetch` when the jar is missing. `AW_NO_SYSTEM_INSTALL=1` prints the install command instead; `AW_DRY_RUN=1` prints only.

| Package manager | Package for `java` |
|---|---|
| apt-get | `openjdk-21-jre-headless` |
| dnf, yum | `java-21-openjdk-headless` |
| brew | `openjdk@21` |
| pacman | `jre21-openjdk-headless` (not checked on a real box) |
| zypper | `java-21-openjdk-headless` (not checked on a real box) |

## The step

```yaml
- step: step.model.check
  with:
    specs: specs/tla/*/     # default; relative to fact:workspace
    scope: touched          # all | touched
    witnesses: true         # default true
```

Input is `fact:workspace` plus the `with`. `scope: touched` keeps the folders whose `models:` paths or own files the change (`fact:change`, `base..head` through `IBranchChanges.Between`) touches; with none left it ends `skipped`. `witnesses` also runs each folder's `gaps/*.cfg`, each of which must fail on the property its first line names.

What it does:

1. Lists the spec folders and checks each against the folder rules below. A folder that breaks them is `MDL-SPEC-001` and is not run.
2. Under `touched`, drops the folders the change doesn't touch.
3. Runs `tlc-check.sh --root <workspace> [--witnesses] --timeout 180 <abs folders>` through `RepoCommandRunner`: one heavy slot, `MaxDuration` 15 min (10 min for TLC after `BudgetFor`'s margin), folders in name order. The environment is `PATH`, `HOME`, `AW_STATE_DIR`, `TLC_SH`, `TLC_HEAP` and `TMPDIR` (the step's own temp dir, deleted after the run, timeouts included). No `JAVA_HOME`, and `--jar` is never passed.
4. Reads the `tlc-check:` lines. Their result word decides the outcome, not the exit code (the worst code hides a violation in one spec behind an error in another). A line with no result word it knows counts as `ERROR`. The one exception: exit 3 means only "java or the jar is missing".
5. Reads each passed cfg's `CONSTANT` lines and the README's `Bounds:` line, so a pass carries its bounds.
6. When time runs out, the cfgs that never ran are listed by name in `tooBig`; they are never counted as passed.

## Outcomes

Every result is a `step.completed` outcome, so a recipe routes it and the findings ride with it. An unexpected host error (temp dir, process start) still ends `step.failed`.

| Outcome | When | The instance gets |
|---|---|---|
| `pass` | every checked cfg printed `PASS` and every witness was caught | `fact:modelCheck`: per spec and cfg, distinct states, depth, bounds |
| `skipped` | `touched`, and the change touches no modelled code | `fact:modelCheck` with `skipped: true` and the files it looked for |
| `violation` | any cfg printed `VIOLATION` | `fact:findings`: `MDL-VIOLATION-001` per broken property; an `ERROR`, `TIMEOUT` or missed witness elsewhere rides along as its own finding |
| `vacuous` | no violation, and a witness printed `WITNESS-MISSED` or `WITNESS-WRONG` | `MDL-WITNESS-001`: which planted bug wasn't caught (or was caught on the wrong property: expected and actual names) |
| `specError` | no violation, and an `ERROR` that isn't out of memory, or `MDL-SPEC-001` / `MDL-SPEC-003` | `MDL-SPEC-ERROR-001` with tlc.sh's message and the line it names |
| `tooBig` | no violation, and a `TIMEOUT`, an `ERROR` naming `OutOfMemoryError`, or cfgs left unrun | `MDL-TOOBIG-001`: which spec, its bounds, how long it ran |
| `cantRun` | exit 3, or the runner copy is missing | `MDL-TOOL-001` with the fix for that case (java missing, older than 11, jar not fetched, or "reinstall: `scripts/install-cs.sh`"), and the `PATH` the step saw |

`cantRun` is the only outcome that asks the owner (a `step.model.desk` state with **Check again** plus **Stop** or **Skip the model**). The rest go back to whoever writes the code or the model. A pass means nothing past its bounds, and every report says so.

## Findings

| Code | Kind | Meaning |
|---|---|---|
| `MDL-VIOLATION-001` | finding | a property broke: the property in plain words (from the README), the steps that broke it, the scrubbed trace (first 5 and last 20 states when capped) |
| `MDL-WITNESS-001` | finding | a `gaps/` cfg's planted bug was not caught (`WITNESS-MISSED`) or tripped another property (`WITNESS-WRONG`): the model checks nothing there |
| `MDL-SPEC-ERROR-001` | finding | TLC could not run the spec: parse error, bad cfg, a gaps cfg with no `\* witness:` line |
| `MDL-TOOBIG-001` | finding | timeout, out of memory (`TLC_HEAP` raise it, or shrink the constants) or cfgs unrun |
| `MDL-TOOL-001` | finding | java or the jar is missing, or the runner copy is |
| `MDL-SPEC-001` | rule | the folder breaks the folder rules; not run |
| `MDL-SPEC-002` | warning | README has no `models:` path that exists, no `Bounds:` or a property without its line; shown on the run, not an outcome |
| `MDL-SPEC-003` | rule | build line: the packet's `model/<Name>/` was not moved to `specs/tla/<Name>/`, or the moved README's `models:` line names no path that exists in the head |

A trace is scrubbed with `Aw.Redaction` before it becomes a finding.

## Folder rules

- A spec folder holds only regular `.tla`, `.cfg` and `.md` files, plus a `gaps/` folder of `.cfg` files. TLC runs from the folder, so a stray `.class` or `.jar` could be loaded. A symlink or anything else is `MDL-SPEC-001`.
- Every module a spec pulls in is one of TLC's standard modules (`Naturals`, `Integers`, `Sequences`, `FiniteSets`, `Bags`, `TLC`) or another module in the same folder. The scan strips `\*` and `(* *)` comments, reads an `EXTENDS` list across lines, and checks `INSTANCE` and `LOCAL INSTANCE`. Anything else (`IOUtils` first) is `MDL-SPEC-001`. That is an honest-mistake guard, not a sandbox.
- TLC runs with one worker, a per-check timeout (the runner ends the check's whole process group, TERM then KILL after 5 s) and a heap cap (`-Xmx${TLC_HEAP:-2g}`).

## The README template

`specs/tla/<Name>/README.md` (and scoping's `model/<Name>/README.md`):

```
models: src/Sindri/Aw.Sindri.Core/Workflow/Engine/Rules/StateTimers.cs:57 src/Sindri/Aw.Sindri.Workflows/TimerScopes.cs
# Takeover
Bounds: 1 instance, 2 states, up to 2 takeovers, up to 2 parks.
- TimerStoppedWhileTakenOver: while you have an item taken over, its timer doesn't run.
- NoTimeGained: handing an item back never gives its timer more time than it had.
Vars: remaining = time left on the timer (none = no timer) · paused = the timer is paused
Actions: Takeover = TakeoverRequested · Handback = HandbackRequested · Fire = DeadlineFired · Elapse = (time passing, no C# trigger)
Leaves out: Temporal replay, the ledger mirror, desk timeouts, more than one instance.

## Known gaps (`gaps/*.cfg`)

| Config | Fails | What TLC shows |
|---|---|---|
| `gaps/FullTimeoutOnHandback.cfg` | `NoTimeGained` | The planted bug and its shortest trace. |
```

- Line 1 `models:` is read by the runner (a warning when it is missing or a path doesn't exist) and by the step (`touched`). Paths are repo-relative, space separated; matching ignores `:line`; a path ending in `/` is a folder and matches anything under it. No `models:` line: the folder is always checked under `touched`.
- `Bounds:`, the `- <Name>: <plain words>` property lines and `Vars:` are read for the owner's text. Missing ones fall back to the cfg's `CONSTANT` lines and the bare property name, with an `MDL-SPEC-002` warning. Without `Vars:` a trace step shows the changed variables as they are.
- `Actions:` and `Leaves out:` are read by `TlaSpecCoverageTests`: every trigger type under `Core/Workflow/Engine/Triggers/` and every `DeadlineKind` is named in some README, as an action or under `Leaves out:` with a reason, and every C# name a README lists still exists. A name check, not a proof the spec matches the code.
- `specs/tla/Takeover/README.md` is the worked example.

## Add a spec

1. `specs/tla/<Name>/<Name>.tla` and one or more `<cfg>.cfg`; `MC<Name>.tla` when TLC needs model values (the runner runs it instead of `<Name>.tla`).
2. `README.md` in the template above. List every C# trigger the model covers in `Actions:` and every one it doesn't under `Leaves out:`.
3. Keep it to the folder rules. Aim for under 30 s at the bounds; slower specs get smaller bounds, not a longer job.
4. Run `scripts/tlc-check.sh --witnesses specs/tla/<Name>` (java and the jar from setup).

## Add a witness

A witness proves the model can fail: a copy of the cfg with a constant that switches a planted bug on.

1. Put it in `specs/tla/<Name>/gaps/<Bug>.cfg`.
2. Line 1 is `\* witness: <PropertyName>`, the invariant or property it must break. A gaps cfg without that line is an `ERROR`.
3. `tlc-check.sh --witnesses` runs it and must see it fail on that property: `WITNESS-CAUGHT <kind> <Name>` (exit 0), `WITNESS-WRONG <kind> <actual> (expected <Name>)` or `WITNESS-MISSED <Name>` (exit 1). Without `--witnesses`, `gaps/` is not run.
4. Add a row to the README's Known gaps table.

## Environment

| Variable | Read by | Meaning |
|---|---|---|
| `AW_TLC_CHECK` | the step | path to the `tlc-check.sh` it runs; default the copy under `bin/cs/<stamp>/tlc/` (tests) |
| `TLC_SH` | `tlc-check.sh`, `install-deps.sh` | path to the `tlc.sh` it runs; default the sibling copy (tests point it at a stub) |
| `TLC_HEAP` | `tlc.sh check` | java heap cap, digits plus an optional k/m/g, default `2g`; anything else exits 2 before java starts |
| `AW_STATE_DIR` | `tlc.sh` | the jar is cached under `$AW_STATE_DIR/cache/bin/tla2tools-<version>/` |
| `AW_NO_SYSTEM_INSTALL`, `AW_DRY_RUN` | `install-deps.sh`, `sys-pkg.sh` | print the java install command instead of running it; print only |

Exit codes of `tlc-check.sh`: 0 every check passed, 1 a violation, 2 an error, 3 java or the jar is missing (the worst wins, 3 > 2 > 1 > 0).
