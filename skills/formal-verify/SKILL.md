---
name: formal-verify
description: "Use to prove a design or a piece of concurrent logic correct with TLA+ before or after it ships: locks, retries, queues, leases, state machines, failover, protocols. Writes a TLA+ or PlusCal model with its safety and liveness properties, model-checks it with TLC and turns any counterexample into the real steps that break. Not for reproducing a live bug or reviewing a plan in prose."
argument-hint: "<file, design-doc section or described behavior> [--slug <name>] [--rerun <slug>]"
allowed-tools: Bash(bash *), Bash(java *), Bash(SHARED_DIR=*), Bash(source *), Bash(f=*), Bash([ *), Bash(date *), Bash(diff *), Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion
---

# Formal Verify — TLA+ model checking

Write the behavior as a small TLA+ model, say in plain words what must always and eventually hold, and let TLC try every interleaving within small bounds. A pass means no behavior of the model, within those bounds, breaks a property. A failure comes with the exact sequence of steps that breaks it.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

Every run ends with the [Result block](#result-block), whatever the outcome: pass, violation, a spec error, missing tools or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## When to use

- A mechanism where order and timing matter: a lock or lease, a retry with idempotency keys, a job queue with workers, a cache invalidation, a leader election, a two-step write, an approval state machine.
- A design-doc section that claims "never" or "eventually" ("a job never runs twice", "every request is eventually answered").
- After a fix to such a mechanism, to re-check the same properties (`--rerun`).

Not this skill: a bug you can reproduce (`/rootCause`), a prose review of a plan (`/archReview`), test coverage (`/testAudit`).

## Inputs

- The target: a file or function (`src/jobs/claim.py:40`), a design-doc section, or a behavior described in the request.
- `--slug <name>`: the output folder name (default: a short kebab-case name for the mechanism, e.g. `job-claim`).
- `--rerun <slug>`: re-check an existing model after a fix (see [Rerun](#rerun)).

## Tools

```bash
S="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/formal-verify"
bash "$S/scripts/tlc.sh" doctor
```

It prints one line for java (11 or newer) and one for the pinned `tla2tools.jar` (`tla2tools.lock`: version, sha256, url), each with its fix when missing. `bash "$S/scripts/tlc.sh" fetch` downloads the jar into `${AW_STATE_DIR:-~/.agentic-workflow}/cache/bin/` and refuses a file whose sha256 differs. Fetching is a download: **Ask the user** before running it. When java or the jar is missing and not fetched, still write the model, config and properties (steps 1 to 4), then stop with verdict `blocked` and the fix line. Never call a model verified that TLC did not run.

## Output

Everything goes in `$AW_DIR/formal-verify/<slug>/` (`skills/_shared/repo-slug.sh` sets `$AW_DIR`; source it in each Bash call that uses it). Write the files with the Write tool:

```
<Module>.tla           the model (module name = file name, CamelCase)
<Module>.cfg           the bounded constants and the properties TLC checks
<Module>.witness.cfg   the reachability checks (step 6), expected to fail
properties.md          each property in plain words, next to its TLA+ name; the list a rerun must keep
report.md              verdict, bounds, the trace in real steps, fixes (templates/report.md)
```

## Process

1. **Pin down the behavior.** Read the code or doc. List the actors (workers, clients, nodes), the shared state (rows, flags, queues, messages) and every step each actor takes. For each step, decide what is atomic in the real system: one SQL statement, one transaction, a compare-and-swap, one message send. A read followed by a write in separate statements is two steps.
   The rule: one TLA+ action (or one PlusCal label) per real atomic step. Merging two real steps into one action hides exactly the races this skill is for: a check-then-set lock written as one action passes, and the real lock lets two workers in.
2. **Write the properties in plain words first**, in `properties.md`, then as TLA+:
   - Safety, as `INVARIANT`s: what must never happen ("two workers never hold the job at once": `MutualExclusion`). Always add `TypeOK` (every variable stays in its set).
   - Liveness, as `PROPERTY`s: what must eventually happen ("every claimed job is eventually finished or released": `p ~> q`). A liveness property needs a fairness assumption in `Spec` (`WF_vars(A)`: a step that stays possible eventually happens; `SF_vars(A)`: a step that is possible again and again eventually happens). Write which one and why in plain words: fairness is a claim about the real scheduler, and a wrong one proves nothing.
   If the request gives no property, propose them and **Ask the user** to confirm before writing the model; with nobody to ask, take the ones the code or doc claims and list them in the report as assumed.
3. **Write the model** from `templates/Spec.tla`: TLA+ actions, or PlusCal (`--algorithm`) when the actors read like sequential code. Above each action or label, a comment with the `path:line` it models. Model only the mechanism: the rest of the system becomes a nondeterministic choice (a crash can happen at any step, a message can be lost or arrive twice), never a detail copied from the code. For PlusCal, `bash "$S/scripts/tlc.sh" translate "<dir>/<Module>.tla"` writes the TLA+ translation into the file.
4. **Write the config** with the smallest constants that still show the behavior: 2 or 3 actors, 2 jobs, a retry count of 2, a queue of length 2 or 3. Bound any counter that grows forever with a `CONSTRAINT`. Write the bounds and what they cover in the report ("every interleaving of 2 workers and 2 jobs, with up to 1 crash"). Most concurrency bugs show up with 2 or 3 actors; a pass still says nothing beyond its bounds.
5. **Run TLC:**
   ```bash
   S="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/formal-verify"
   f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; . "$f"
   bash "$S/scripts/tlc.sh" check "$AW_DIR/formal-verify/<slug>/<Module>.tla"
   ```
   Its last line is the result: `tlc: PASS distinct=<n> depth=<n>`, `tlc: VIOLATION <invariant|property|deadlock|assumption> <name>` (exit 1) or `tlc: ERROR <line>` (exit 2). TLC checks for deadlock by default: a model that is meant to stop gets a terminal step that loops (`Done == \A p : pc[p] = "Done" /\ UNCHANGED vars`), never `--no-deadlock` unless you say why in the report. An `ERROR` is a mistake in the model, not a finding: fix the spec and run again.
6. **Check that a pass means something.** A model that never reaches the interesting states passes every safety property. For each safety property, write a witness invariant that says the state it protects is never reached (`NobodyInCS == \A p \in Procs : pc[p] # "cs"`), put them in `<Module>.witness.cfg` (one `INVARIANT` each) and run `tlc.sh check <Module>.tla --config <Module>.witness.cfg`, one witness at a time. Each must come back `VIOLATION`. A witness that passes means the model never gets there: fix the model before you trust any pass. Write the witness results in the report.
7. **Read the result.**
   - `PASS`: the properties hold for every behavior within the bounds. Say the bounds and the distinct-state count.
   - `VIOLATION`: translate TLC's trace into the real system's steps, one row per state: the step, the actor, the `path:line` it models, and the values that matter. End with the state that breaks the property, in plain words ("both workers are now running job 7"). If the trace needs a step the real system can't take (an action that is atomic in reality, a crash where none can happen), the model is wrong: fix it and run again, and say so in the report.
8. **Suggest fixes.** For each violation, the smallest change to the real code or design that removes the trace (make the check and the set one statement, add a lease expiry, make the handler idempotent), with `path:line`. Then write the fix into the model and run the same config: report the before and after results. A fix that only passes with weaker properties is not a fix.
9. **Write `report.md`** from `templates/report.md`: the verdict, the properties in plain words with each one's result, the bounds, the witness results, the trace table, the fixes, and what the model leaves out.

## Rerun

`--rerun <slug>` re-checks `$AW_DIR/formal-verify/<slug>/` after a fix: read `properties.md` and the report, update the model's actions to the fixed code (same comments with `path:line`), keep `properties.md` and the `INVARIANT` and `PROPERTY` lines of `<Module>.cfg` exactly as they were, run steps 5 to 9 again and add a dated section to the report. Changing or dropping a property needs the user's say-so (**Ask the user**), and the report records it with their words.

## Rules

- Never say "verified" or "proven" without a `tlc: PASS` line from this run, and always with its bounds.
- Never weaken, delete or skip a property to make a check pass.
- The model is an abstraction: say in the report what it leaves out (timeouts, a third worker, network partitions), so nobody reads a pass as broader than it is.
- No app names, URLs or data from the repo go into the model beyond what the property needs: model names for actors and states, not customer data.

## Next steps

- `/rootCause` — when a violation trace matches a bug someone already sees in production
- `/archReview` — when the fix changes the design, not just one function
- `/formal-verify --rerun <slug>` — after the fix lands, to re-check the same properties

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/formal-verify-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`formal-verify`), `artifacts` (absolute paths of the model, configs, `properties.md` and `report.md`), `summary` (one line),
plus `verdict` (`pass` when TLC passed every property, `violation` when it found one, `error` when the model would not run, `blocked` when java or the jar is missing); `properties` (the TLA+ names checked, e.g. `["TypeOK", "MutualExclusion"]`); `violated` (the names TLC reported broken, `[]` when none); `bounds` (one line, e.g. `2 workers, 2 jobs, 1 crash`).

The block is the result file's JSON copied unchanged, never retyped from memory: the same keys and the same absolute paths (Read the file back if it is no longer in view). Its shape:

```aw-result
{ "skill": "formal-verify", "artifacts": ["<abs path>", "..."], "summary": "<one line>", "verdict": "pass | violation | error | blocked", "properties": ["<name>", "..."], "violated": ["<name>"], "bounds": "<one line>" }
```

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
