---
name: bugHunt
description: Fix-and-verify loop with atomic commits and regression test generation. Three tiers — quick (lint+typecheck), standard (unit+integration), exhaustive (full suite + edge cases).
argument-hint: "[--tier quick|standard|exhaustive] [--from-report <path> --item N] [--from-investigation <path>] [--depth N] [bug-description-or-test-command]"
allowed-tools: Bash(git *), Bash(npm *), Bash(npx *), Bash(pytest *), Bash(cargo *), Bash(go *), Bash(bundle *), Bash(coverage *), Bash(SHARED_DIR=*), Bash(source *), Bash(cat *), Bash(echo *), Bash(mkdir *), Bash(ls *), Agent, Read, Write, Edit, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff, mcp__prism-mcp__session_task_route, mcp__prism-mcp__prism_infer
---

# Bug Hunt

Fix-and-verify loop with atomic commits, regression test generation, and tiered verification.

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

## Step 1: Parse Arguments

Parse the argument string for:
- **Tier flag:** `--tier quick`, `--tier standard`, or `--tier exhaustive`. Default: `standard`.
- **`--from-report <path> --item N`** — entry from a `/bugReport` report: Read `<path>` and take row N of its `## Bugs` table as the bug description (severity, file, line, description). No manual re-description needed.
- **`--from-investigation <path>`** — entry from a `/rootCause` handoff (`investigations/<slug>/handoff.md`): Read `<path>` and adopt its root cause, module boundary, and repro command/journey. Skip re-deriving anything the handoff already answers in Steps 2–3.
- **`--depth N`** — dispatch-chain depth guard (default 0). If N ≥ 2, do not dispatch any sub-skill from this run — report instead. When dispatching, pass `--depth N+1`.
- **Bug description or test command:** Everything after the flags (or the entire argument if no flag).

Tier definitions:
| Tier | Verification scope |
|------|-------------------|
| `quick` | Lint + typecheck only |
| `standard` | Specific test file + related test files |
| `exhaustive` | Full test suite + additional edge case tests |

## Step 2: Reproduce

Confirm the bug exists before attempting a fix.

1. **Detect the runner** per `skills/_shared/test-runner-detection.md` (sets `TEST_CMD`). An undetected runner is "n/a" — fall back to the app path below; missing tooling is never itself the bug.
2. **If argument contains a test command** (e.g., `npm test -- path/to/test`), run it directly.
3. **If argument is a description**, search files to find related test files and **list every candidate test first** (file + why it matched) before running any. Then run the candidates, best match first.
4. **Capture the failure output.** If no candidate fails, do NOT conclude non-reproduction yet:
   - Show the full candidate list you tried.
   - For user-facing symptoms, try the app path: **Invoke skill `verify-app`** with args `--yes --journey <repro steps from the description>` (for a desktop app it runs the real app on seeded data).
   - Only after both routes pass, **Ask the user** whether they can provide more detail / a failing command, or wants to stop.

## Step 3: Locate

Find the bug in the source code.

1. Search files for code related to the failure — error messages, function names from stack traces, relevant module paths.
2. **Spawn a subagent** (explore) to search the codebase if the initial search is insufficient.
3. Read all relevant source files. Trace the logic to identify the defect.

## Step 3.6: Prevent the Class

Once the defect is located and before choosing a fix, read `$SHARED_DIR/prevent-the-class.md` and ask its question:

> How can we prevent this class of bugs from happening again—not just fix this instance? Consider enforceable constraints such as lint rules, analyzers, type/API design, shared primitives, state-machine or architecture changes, and checks that catch other instances of the same failure.

Follow steps 1-4 of that file (name the class and invariant, search for the other instances, pick the smallest mechanism, keep mitigation apart from prevention) and write the handoff block (Failure class, Invariant, Affected scope, Prevention decision, Mitigation, Proof). With `--from-investigation`, adopt the handoff's block instead of redoing it; if it has none, write it now. A dark-factory objective (Step 3.5) carries this block too.

## Step 3.5: Dark Factory (optional)

Follow `skills/_shared/dark-factory.md` — the gate (CD12), the route/infer/verify flow, and the **bugHunt fix objective template** are all preserved there. Fill the template with the root cause from Step 3 and the failing command from Step 2:

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
cat "$SHARED_DIR/dark-factory.md"
```

- Gate disabled or config absent ⇒ skip silently to Step 4 (don't mention dark factory).
- Delegated fix **verified by the host** (re-run `TEST_CMD` per the shared flow) ⇒ commit it as in Step 4.2, then **continue at Step 5 and Step 6** — a dark-factory fix never skips the regression test or tier verification.
- Unavailable, refused, or failed verification ⇒ manual Step 4.

## Step 4: Fix

Implement the fix and commit atomically.

1. **Edit the source file(s)** to fix the bug, shaped by the Step 3.6 decision: the chosen mechanism beside the instance fix, or the instance fix plus the recorded follow-up. Keep changes minimal — fix the identified defect and build only the mechanism that decision names; label a stopgap as mitigation.
2. **Commit the fix:**
   ```bash
   git add <changed-files>
   git commit -m "fix: <short description of what was fixed>"
   ```

## Step 5: Generate Regression Test

Write a test that guards against this bug recurring.

1. **Identify the correct test file.** If a related test file exists, add the test there. Otherwise, create a new test file following the project's test conventions.
2. **Write a test** that:
   - Would **fail** on the original buggy code
   - **Passes** on the fixed code
   - Tests the specific edge case or condition that triggered the bug

   A test with one fixture is the instance's proof, not the class's. The mechanism's own proof (Step 3.6: a second representative bad instance rejected, or more than one case run) is separate and goes in the report, unless the decision is "existing guard" or "deferred: <link>".
3. **Commit the test:**
   ```bash
   git add <test-files>
   git commit -m "test: regression test for <short description>"
   ```

## Step 6: Verify by Tier

Detect `TEST_CMD` per `skills/_shared/test-runner-detection.md` (re-detect in each bash block — shell state does not persist). Undetected tooling reports "n/a", never a failure.

### Tier: quick
Lint + typecheck only, using the project's configured tools (JS example: `npm run lint || npx eslint .`, `npm run typecheck || npx tsc --noEmit`; use the detected stack's equivalents otherwise). Unconfigured ⇒ "n/a".

### Tier: standard
Run the originally failing test, plus related test files (same directory or importing the fixed module): `$TEST_CMD <test-file>` then `$TEST_CMD <related-test-files>`.

### Tier: exhaustive
Run the full suite (`$TEST_CMD`), write and run any additional edge-case tests you identify, and **Step 6.5 is mandatory for user-facing fixes**.

### Step 6.5: Verify in the running app (user-facing fixes)

If the fix touches user-facing code (routes, components, views, templates, styles):

> **Invoke skill `verify-app`** with args `--yes --journey <the repro steps from Step 2, as the journey>`

- Recommended at `standard` tier; **mandated** at `exhaustive` tier — do not report `fixed` there without it.
- For a desktop UI fix this is the desktop evidence pack (`verify-desktop`: the real app on seeded data, screens judged against their mockups).
- Record the resulting evidence pack (`skills/_shared/evidence-pack.md`) for the report:
  ```bash
  SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
  source "$SHARED_DIR/repo-slug.sh"
  ls -1dt "$AW_DIR/verification/"*/ 2>/dev/null | head -1
  ```
- A `FAIL` verdict in the pack counts as verification failure → Step 7.

## Step 7: Loop on Failure

If verification fails:

1. **Persist and check the iteration counter** (survives context loss — never track it only in your head):
   ```bash
   SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
   source "$SHARED_DIR/repo-slug.sh"
   mkdir -p "$AW_DIR/qa"
   COUNTER_FILE="$AW_DIR/qa/.bughunt-attempts-<slug>"
   ATTEMPT=$(( $(cat "$COUNTER_FILE" 2>/dev/null || echo 0) + 1 ))
   echo "$ATTEMPT" > "$COUNTER_FILE"
   echo "attempt $ATTEMPT/3"
   ```
   If `ATTEMPT` ≥ 3 (max), skip to Step 8 with status `unfixed`.
2. **Analyze the new failure.** Read the output, determine if it is the same bug or a new issue introduced by the fix.
3. **Go back to Step 4.** Revert the broken fix if necessary (`git revert HEAD` or edit), then re-implement.

## Step 8: Write QA Report

Write the report to `$HOME/.agentic-workflow/$REPO_SLUG/qa/{timestamp}-{slug}.md` where:
- `{timestamp}` is `YYYYMMDD-HHmmss` format
- `{slug}` is a short kebab-case summary of the bug (max 40 chars)

Report format:

```markdown
# Bug Hunt Report: {short description}

**Date:** {ISO timestamp}
**Tier:** {quick | standard | exhaustive}
**Status:** {fixed | unfixed}
**Attempts:** {n}/3

## Bug Description

{Original bug description or failing command}

## Root Cause

{What was actually wrong and why}

## Fix Summary

{Description of the fix}

### Changed Files
- `{file}:{line}` — {what changed}

### Commits
- `{sha}` — fix: {description}
- `{sha}` — test: regression test for {description}

## Regression Test

**File:** `{test-file-path}`
**Test name:** `{test name or describe block}`

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
- Original failure: {the failing check, red before, green after}
- Prevention: {the mechanism's own proof, or "deferred"}

## Verification Results

**Tier:** {tier}
**Result:** {pass | fail}

{Command output summary}

## Evidence

- **Runner:** {TEST_CMD or "n/a — no runner detected"}
- **Verification output:** {key lines from the tier run}
- **Evidence pack:** {`~/.agentic-workflow/<repo-slug>/verification/<run-id>/pack.json` from Step 6.5, or "n/a — not a user-facing fix"}
- **Pack verdict:** {PASS | WARN | FAIL | n/a}
```

## Step 9: Report to User

```
Bug hunt complete.

Status: {fixed | unfixed}
Tier: {tier}
Attempts: {n}/3
Root cause: {one-line summary}
Fix: {commit sha} — fix: {description}
Test: {commit sha} — test: regression test for {description}
Original bug: {the check's before and after}
Class prevention: {what was built and its proof | existing guard <which> | none built}
Deferred: {follow-ups with links | none}
Report: ~/.agentic-workflow/<repo-slug>/qa/{filename}
Evidence: {pack path or "n/a"}
```

### Sub-skill Dispatch

If the fix-and-verify loop ends with status `unfixed` (all hypotheses exhausted) **and** the depth guard allows (`--depth` < 2):
> **Invoke skill `bugReport`** with args `--depth <N+1> <scope>`

Do not invoke bugReport on success — bugHunt's own Step 8 report is sufficient. At depth ≥ 2, report only.

## Next steps

- `/review` — verify fixes pass code review
- `/shipRelease` — if all bugs are fixed and tests pass
- `/weeklyRetro` — if this was part of a larger debugging push
