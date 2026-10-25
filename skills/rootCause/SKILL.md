---
name: rootCause
description: Use BEFORE fixing any bug — a failing or flaky test, a bug report or customer complaint, an error, crash or stack trace, a regression, or anything described as broken, wrong or not working, even when the fix looks small. Finds and proves the root cause first (investigate, analyze, hypothesize, then implement) with scope frozen to the module boundary. Not for new features, refactors, or explaining how code works.
argument-hint: "[--depth N] [--investigate-only] [error-message-or-issue-description | canary incident JSON]"
allowed-tools: Bash(git *), Bash(npm *), Bash(npx *), Bash(pytest *), Bash(cargo *), Bash(go *), Bash(bundle *), Bash(SHARED_DIR=*), Bash(source *), Bash(mkdir *), Bash(cat *), Agent, Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Root Cause Analysis

4-phase systematic debugging with automatic scope freeze at the module boundary.

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

## Phase 0: Parse Input

Extract the starting point for the investigation from the argument:

- **`--depth N`** — dispatch-chain depth guard (default 0). If N ≥ 2, this run must NOT dispatch any sub-skill (Sub-skill Dispatch is disabled) — report findings only. When dispatching, always pass `--depth N+1`.
- **`--investigate-only`** — diagnose without fixing (used by `/bugFixOrchestrator`, whose implementers write the fix). Run Phases 0–3.5, skip Phase 4, write the Phase 5 report + handoff — status `diagnosed` when a hypothesis is confirmed, `unfixed` when all were ruled out — and never dispatch a sub-skill. The working tree must end exactly as it started.
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

## Phase 3.5: Confirm Before Editing

**Run the "Confirms if" check for hypothesis #1 BEFORE editing any code** — add the log line, run the narrowed test, inspect the state, whatever the check specifies.

- **Confirmed** → proceed to Phase 4 with hypothesis #1.
- **Ruled out** → record the result in the hypotheses table, promote hypothesis #2, and run its check.
- Never implement a fix whose hypothesis has not been confirmed by its own check. "Confirms if / Rules out if" is an executable contract, not documentation.

**With `--investigate-only`:** run the confirm check for each hypothesis in order until one is confirmed or all are ruled out, recording every result. Revert any temporary instrumentation (log lines, narrowed test edits) — `git status --porcelain` must show nothing you added. Then skip Phase 4 and go to Phase 5 with status `diagnosed` if a hypothesis was confirmed, or `unfixed` if every one was ruled out (the caller must not fix an unconfirmed cause). In the report, write `n/a — investigate-only` under **Fix Applied** and **Verification**.

## Phase 4: Implement

Fix the confirmed cause.

1. **Implement the fix** for the confirmed hypothesis. All changes MUST stay within the declared module boundary.
2. **Verify:**
   - If a local failing command exists, re-run it.
   - If the symptom is production/UI-only (no local failing command), run the app-path check instead: **Invoke skill `verify-app`** with args `--yes --journey <repro steps from Phase 1>`. The fix is verified only on a PASS verdict in the resulting evidence pack. For a desktop app that pack comes from `verify-desktop` (the real app on seeded data, screens judged against their mockups).
3. **If verification still fails**, revert the change, return to Phase 3.5 for the next hypothesis. Repeat up to hypothesis #3.
4. **If a fix requires changes outside the module boundary**, do NOT make the change. Instead, flag it:

> **SCOPE BREACH:** Fixing this requires changes in `<outside-path>`. Asking the user for permission before proceeding.

Then **Ask the user** whether to expand the scope or stop.

## Phase 5: Write Investigation Report + Handoff

Write the report to `$HOME/.agentic-workflow/$REPO_SLUG/investigations/{timestamp}-{slug}.md` where:
- `{timestamp}` is `YYYYMMDD-HHmmss` format
- `{slug}` is a short kebab-case summary of the error (max 40 chars)

(Re-derive the paths in bash via the shared helper — shell state does not persist between blocks:)
```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/investigations/<slug>"
```

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
```

**Also write the handoff file** to `$HOME/.agentic-workflow/$REPO_SLUG/investigations/<slug>/handoff.md` — this is the structured input `/bugHunt --from-investigation <path>` consumes, so the full investigation is never discarded:

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
```

## Phase 6: Report to User

```
Root cause analysis complete.

Status: {fixed | unfixed | scope-breach | diagnosed}
Module boundary: {path}
Root cause: {one-line summary}
Report: ~/.agentic-workflow/<repo-slug>/investigations/{filename}
Handoff: ~/.agentic-workflow/<repo-slug>/investigations/{slug}/handoff.md
```

**End the response with exactly one fenced JSON block** (machine-readable tail — `/review` and other callers parse this):

```json
{ "status": "fixed | unfixed | scope-breach | diagnosed", "report_path": "<absolute report path>", "handoff_path": "<absolute handoff.md path>", "boundary": "<boundary path>" }
```

### Sub-skill Dispatch

Never with `--investigate-only` (status `diagnosed` is the intended end — the caller fixes). Otherwise, if Phase 4 ends with status `unfixed` or `scope-breach`, **and** the depth guard allows (`--depth` < 2):
> **Invoke skill `bugHunt`** with args `--from-investigation <handoff.md path> --depth <N+1>`

Do not invoke bugHunt if the fix was verified — rootCause's own report is sufficient on success. At depth ≥ 2, stop the chain and report only.

## Next steps

- `/bugHunt --from-investigation <handoff path>` — auto-fix the bug using the full diagnosis
- `/review` — re-review the codebase after applying fixes
