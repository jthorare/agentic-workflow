---
name: rootCause
description: Use BEFORE fixing any bug — a failing or flaky test, a bug report or customer complaint, an error, crash or stack trace, a regression, or anything described as broken, wrong or not working, even when the fix looks small. Finds and proves the root cause first with scope frozen to the module boundary. Not for new features, refactors, or explaining how code works.
argument-hint: "[--depth N] [--investigate-only] [error-message-or-issue-description | canary incident JSON]"
allowed-tools: Bash(git *), Bash(npm *), Bash(npx *), Bash(pytest *), Bash(cargo *), Bash(go *), Bash(bundle *), Bash(SHARED_DIR=*), Bash(source *), Bash(mkdir *), Bash(cat *), Agent, Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Root Cause Analysis

4-phase systematic debugging with automatic scope freeze at the module boundary.

A passing test does not end the run. Every run, fixed or not, ends with the **Done checklist** at the end of Phase 6: the report, the handoff, the result file (under `AW_RESULT=1`) and the `aw-result` block.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

Every run ends with the [Result block](#result-block), whatever the outcome: fixed, unfixed, diagnosed, a scope breach or a stop. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Your final reply always opens with the `aw-result` block (callers parse it, whatever `AW_RESULT` is); the result file never stands in for it.

## Phase 0: Parse Input

Extract the starting point for the investigation from the argument:

- **`--depth N`** — dispatch-chain depth guard (default 0). If N ≥ 2, this run must NOT dispatch any sub-skill (Sub-skill Dispatch is disabled) — report findings only. When dispatching, always pass `--depth N+1`.
- **`--investigate-only`** — diagnose without fixing (used by `/bugFixOrchestrator`, whose implementers write the fix). Run Phases 0–3.6, skip Phase 4, write the Phase 5 report + handoff — status `diagnosed` when a hypothesis is confirmed, `unfixed` when all were ruled out — and never dispatch a sub-skill. The working tree must end exactly as it started.
- **Error message or issue description** — the normal entry.
- **Production-incident entry** — if the argument is a JSON object of the shape `{merge_sha, release_id, symptom, logs_excerpt}` (as passed by `/canary` on an UNHEALTHY verdict), treat this as a production incident:
  - `logs_excerpt` is the primary evidence; `symptom` is the failure description.
  - Scope the first suspect set to `git diff <merge_sha>^..<merge_sha>` (the released change).
  - There is usually **no local failing command** — plan for the verify-app journey branch in Phases 1 and 4.

## Phase 1: Investigate

Reproduce the error and collect evidence.

1. **Run the failing command or test.** If the argument contains a runnable command (e.g., `npm test`, a file path with a stack trace hint), execute it to reproduce the failure. Capture the full output including stack traces.
2. **If no runnable command** (production/UI symptom), search files for the error message in the codebase, and write down concrete **repro steps** (navigate/click/fill sequence) from the symptom — these become the verify-app journey used for verification in Phase 4.
3. **Collect artifacts:** full error output / stack trace / logs excerpt; affected files (from the trace, logs, diff, or search); related test files or the repro-steps journey.

## Phase 2: Analyze

Read the relevant source and map the call chain.

1. **Read every file** identified in Phase 1. Follow imports and function calls from the error site back to the root cause.
2. **Map the call chain** — document the sequence of function calls from the entry point to the error site.
3. **Identify the module boundary** — the **nearest common ancestor directory** of (a) the error site and (b) the hypothesized cause site. The repo root and generic top-level source dirs (`src/`, `lib/`, `app/`) are **rejected** as boundaries: if the ancestor resolves to one of those, narrow to the most specific package/directory that still contains both sites, or declare the cause-side directory as the boundary and treat the error site as read-only context. Declare it explicitly:

> **SCOPE FREEZE:** Module boundary is `<path-or-package>` (nearest common ancestor of `<error-site>` and `<cause-site>`). All fixes in Phase 4 must stay within this boundary.

## Phase 3: Hypothesize

Generate 2-3 hypotheses ranked by likelihood. Document each as: **Hypothesis** (one-sentence suspected cause) · **Confirms if** (evidence that would confirm it) · **Rules out if** (evidence that would eliminate it) · **Likelihood** (High / Medium / Low).

For a race or ordering bug (two or more actors that can interleave: locks, leases, retries with idempotency, queues and workers, multi-step writes across actors, failover or leader election, ordering between processes), a hypothesis may be checked with **Invoke skill `formal-verify`** on the mechanism inside the module boundary: model the real atomic steps and the property the report says broke. The bad case: two workers both claim a job because the check and the set are separate statements. Skip it for anything that is not concurrency or a state machine.

## Phase 3.5: Confirm Before Editing

**Run the "Confirms if" check for hypothesis #1 BEFORE editing any code** — add the log line, run the narrowed test, inspect the state, whatever the check specifies.

- **Confirmed** → proceed to Phase 3.6 with hypothesis #1.
- **Ruled out** → record the result in the hypotheses table, promote hypothesis #2, and run its check.
- A TLC counterexample whose steps reproduce the reported symptom counts as the "Confirms if" check for that hypothesis; record the `report.md` path in the hypotheses table. A `pass` within its bounds rules out only what the model covers.
- Never implement a fix whose hypothesis has not been confirmed by its own check. "Confirms if / Rules out if" is an executable contract, not documentation.

**With `--investigate-only`:** run the confirm check for each hypothesis in order until one is confirmed or all are ruled out, recording every result. Revert any temporary instrumentation (log lines, narrowed test edits) — `git status --porcelain` must show nothing you added. Then run Phase 3.6 for the confirmed cause (skip it when every hypothesis was ruled out), skip Phase 4 and go to Phase 5 with status `diagnosed` if a hypothesis was confirmed, or `unfixed` if every one was ruled out (the caller must not fix an unconfirmed cause). In the report, write `n/a — investigate-only` under **Fix Applied** and **Verification**.

## Phase 3.6: Prevent the Class

After the cause is confirmed and before a fix is chosen, read `$SHARED_DIR/prevent-the-class.md` and ask its question:

> How can we prevent this class of bugs from happening again—not just fix this instance? Consider enforceable constraints such as lint rules, analyzers, type/API design, shared primitives, state-machine or architecture changes, and checks that catch other instances of the same failure.

Follow steps 1-4 of that file: name the failure class and the invariant, search for the other instances (file:line each, or how you searched and found none), pick the smallest mechanism that holds the invariant, and keep mitigation apart from prevention. Output the handoff block (Failure class, Invariant, Affected scope, Prevention decision, Mitigation, Proof) before Phase 4. The answer shapes the fix; it is not a follow-up note written after a local patch. The scope freeze still holds: a mechanism that needs files outside the boundary becomes a recorded follow-up (an issue with the class, the instances and the proposed mechanism), not an edit.

## Phase 4: Implement

Fix the confirmed cause.

1. **Implement the fix** for the confirmed hypothesis, shaped by the Phase 3.6 decision: the chosen mechanism, or the instance fix plus the recorded follow-up. Label any stopgap as mitigation. All changes MUST stay within the declared module boundary.
2. **Verify:**
   - If a local failing command exists, re-run it, and run the mechanism's own proof from the handoff block (it must reject a second representative bad instance, or be shown working across more than one case).
   - If the symptom is production/UI-only (no local failing command), run the app-path check instead: **Invoke skill `verify-app`** with args `--yes --journey <repro steps from Phase 1>`. The fix is verified only on a PASS verdict in the resulting evidence pack. For a desktop app that pack comes from `verify-desktop` (the real app on seeded data, screens judged against their mockups).
   - If a TLC model confirmed the cause, run `formal-verify --rerun <slug>` on the same properties after the fix; it must pass (the properties stay unchanged).
3. **If verification still fails**, revert the change, return to Phase 3.5 for the next hypothesis. Repeat up to hypothesis #3.
4. **If a fix requires changes outside the module boundary**, do NOT make the change. Instead, flag it:

> **SCOPE BREACH:** Fixing this requires changes in `<outside-path>`. Asking the user for permission before proceeding.

Then **Ask the user** whether to expand the scope or stop.

5. **Green is not done.** Once the fix verifies, go straight on to Phase 5 and Phase 6 before you reply.

## Phase 5: Write Investigation Report + Handoff

Write the report to `$AW_DIR/investigations/{timestamp}-{slug}.md` where:
- `{timestamp}` is `YYYYMMDD-HHmmss` format
- `{slug}` is a short kebab-case summary of the error (max 40 chars)

(Re-derive the paths in bash via the shared helper — shell state does not persist between blocks:)
```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
echo "$AW_DIR"
```

Write the report and the handoff with the **Write tool** at absolute paths under that `$AW_DIR`; it creates the missing directories. Don't `mkdir` first, and don't skip the files because the shell can't write there: a shell that is read-only or sandboxed is not a reason to skip them. Send the report, the handoff and (under `AW_RESULT=1`) the result file as Write calls in the same turn, so none of them waits on the others.

Report format:

```markdown
# Investigation: {short description}

**Date:** {ISO timestamp}
**Status:** {fixed | unfixed | scope-breach | diagnosed}

## Error Description
{Original error message and context}

## Module Boundary
`{declared boundary path}`

## Call Chain
{entry-point} -> {fn1} -> {fn2} -> {error-site}

## Hypotheses

| # | Hypothesis | Likelihood | Result |
|---|-----------|------------|--------|
| 1 | {description} | High | {confirmed/ruled-out/untested} |
| 2 | {description} | Medium | {confirmed/ruled-out/untested} |
| 3 | {description} | Low | {confirmed/ruled-out/untested} |

## Root Cause
{Confirmed root cause explanation}

## Fix Applied
{Description of the fix}
- `{file}:{line}` — {what changed}

## Verification
{Command run and its output — pass/fail; or the verify-app journey + evidence pack path and verdict}

## Failure class
{one sentence: the mistake that can recur}

## Invariant
{one sentence: what should always hold}

## Affected scope
{other instances, file:line each, or "searched <how>, none found"}

## Prevention decision
{existing guard <which> | new mechanism <which> | deferred: <follow-up link>}, and why it covers the class

## Mitigation
{the instance fix and any stopgap, or "none"}

## Proof
- Original failure: {the check, red before, green after; or n/a — investigate-only}
- Prevention: {the mechanism's own proof, or "deferred"}
```

**Also write the handoff file** to `$AW_DIR/investigations/<slug>/handoff.md` — this is the structured input `/bugHunt --from-investigation <path>` consumes, so the full investigation is never discarded:

```markdown
# Handoff: {slug}

status: {fixed | unfixed | scope-breach | diagnosed}
report: {absolute path to the investigation report}
boundary: {declared module boundary path}
repro: {failing command, or the verify-app journey steps}

## Root Cause
{confirmed or best-supported root cause, one paragraph}

## Hypotheses

| # | Hypothesis | Cause-site files | Likelihood | Result |
|---|-----------|------------------|------------|--------|
| 1 | {description} | `{file}`, `{file}` | High | {confirmed/ruled-out/untested} |
| 2 | {description} | `{file}` | Medium | {confirmed/ruled-out/untested} |

## Ruled Out
- {hypothesis} — {evidence that ruled it out}

## Suggested Fix
{concrete fix direction, file paths, constraints}

## Failure class
{one sentence: the mistake that can recur}

## Invariant
{one sentence: what should always hold}

## Affected scope
{other instances, file:line each, or "searched <how>, none found"}

## Prevention decision
{existing guard <which> | new mechanism <which> | deferred: <follow-up link>}, and why it covers the class

## Mitigation
{the instance fix and any stopgap, or "none"}

## Proof
- Original failure: {the check, red before, green after; or "not run"}
- Prevention: {the mechanism's own proof, or "deferred"}
```

## Phase 6: Report to User

```
Root cause analysis complete.

Status: {fixed | unfixed | scope-breach | diagnosed}
Module boundary: {path}
Root cause: {one-line summary}
Original bug: {the check's before and after}
Class prevention: {what was built and its proof | existing guard <which> | none built}
Deferred: {follow-ups with links | none}
Report: $AW_DIR/investigations/{filename}
Handoff: $AW_DIR/investigations/{slug}/handoff.md
```

**Open your final reply with exactly one fenced `aw-result` block**, before the report text and `## Next steps` (`/review`, `bugFixOrchestrator` and other callers parse it and pick it by `skill`; see the Result block below). A run that stops before Phase 5 (the person says stop, or it refuses before any report) sets `report_path` and `handoff_path` to `null`, reports status `unfixed` and lists only what it wrote in `artifacts`; a caller stops there instead of guessing a path. A scope breach is not that stop: it writes both, and the handoff goes on to `bugHunt`:

```aw-result
{ "skill": "rootCause", "artifacts": ["<absolute report path>", "<absolute handoff.md path>"], "summary": "<one line>", "status": "fixed | unfixed | scope-breach | diagnosed", "report_path": "<absolute report path>", "handoff_path": "<absolute handoff.md path>", "boundary": "<boundary path>" }
```

**Done checklist.** Do every item before you send the reply; a run that skips one is not finished, even when the fix passed:

1. The report exists at `$AW_DIR/investigations/<timestamp>-<slug>.md` (Phase 5, Write tool), or the run stopped before Phase 5 and `report_path` is `null`.
2. The handoff exists at `$AW_DIR/investigations/<slug>/handoff.md` (Phase 5, Write tool), or the run stopped before Phase 5 and `handoff_path` is `null`.
3. Under `AW_RESULT=1`, the result file exists at `$AW_DIR/results/rootCause-<timestamp>.json` (Result block, Write tool). Print the gate (`echo "AW_RESULT=${AW_RESULT:-0}"`) to know.
4. Your final reply, sent after the last tool call (every Write above done first), opens with the `aw-result` block above, before the report text and `## Next steps`. A block printed before a later tool call does not count: callers read only the final reply. It is never optional: callers parse it, and it goes in even when the rest of the reply is short.

If the Write tool itself refuses a path, say so in the reply and still print the block, with the paths you could not write left as `""`.

### Sub-skill Dispatch

Never with `--investigate-only` (status `diagnosed` is the intended end — the caller fixes). Otherwise, if Phase 4 ends with status `unfixed` or `scope-breach`, **and** the depth guard allows (`--depth` < 2):
> **Invoke skill `bugHunt`** with args `--from-investigation <handoff.md path> --depth <N+1>`

Do not invoke bugHunt if the fix was verified — rootCause's own report is sufficient on success. At depth ≥ 2, stop the chain and report only.

## Next steps

- `/bugHunt --from-investigation <handoff path>` — auto-fix the bug using the full diagnosis
- `/review` — re-review the codebase after applying fixes

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

The `aw-result` block in Phase 6 is always printed, as a block tagged `aw-result` (not `json`) that carries `skill`, `artifacts` and `summary` besides its four keys, whatever the gate printed. `AW_RESULT=0`: write no result file. `AW_RESULT=1`: also write `$AW_DIR/results/rootCause-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) with the same JSON, before you print the block; the block opens your final reply, after the last tool call, before the report text and `## Next steps`.

Keys: `skill` (`rootCause`), `artifacts` (absolute paths: the report `$AW_DIR/investigations/<timestamp>-<slug>.md` and the handoff), `summary` (one line),
plus `status` (`fixed | unfixed | scope-breach | diagnosed`, as today); `report_path` (as today, or `null` when the run stopped before Phase 5); `handoff_path` (as today, or `null` when the run stopped before Phase 5); `boundary` (as today).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
