---
name: bugReport
description: Report-only variant of bugHunt — produces a structured bug report with health scores but does NOT fix the bugs. Use for triage and prioritization.
argument-hint: "[--runtime] [--depth N] [area-or-module-to-audit]"
allowed-tools: Bash(git *), Bash(npm *), Bash(npx *), Bash(pytest *), Bash(cargo *), Bash(go *), Bash(bundle *), Bash(coverage *), Bash(SHARED_DIR=*), Bash(source *), Bash(cat *), Agent, Read, Glob, Grep, Write, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Bug Report

Read-only audit that produces a structured bug report with health scores. This skill **never modifies source code** — it is purely diagnostic.

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

**IMPORTANT: This skill is read-only. Do NOT modify any source code, test files, or configuration. Only write the output report.**

> **Read-only exception:** running the detected test suite (`$TEST_CMD`) may itself write build artifacts, coverage output, or test snapshots as side effects. That is tolerated — but never edit source, tests, or config yourself.

## Step 1: Parse Scope

Parse the argument:

- **Area/module** — scope the audit to that directory, package, or module. No argument ⇒ audit the entire project.
- **`--runtime`** — additionally verify behavior in the running app (Step 3.5). Without it, behavior claims stay static-only.
- **`--depth N`** — dispatch-chain depth guard (default 0). At N ≥ 2, do not suggest or dispatch further skills — report only.

## Step 2: Scan

Detect the project's toolchain per `skills/_shared/test-runner-detection.md` (sets `TEST_CMD` / `COVERAGE_CMD`), then run each **detected** tool and collect output. Do not stop on failure — collect everything.

- **Tests:** run `$TEST_CMD` (scoped where supported, e.g. `npm test -- <path>`, `pytest <path>`). No runner detected ⇒ record test dimension as **"n/a — excluded from scoring"**.
- **Typecheck:** the stack's checker if configured (JS: `npm run typecheck` / `npx tsc --noEmit`; mypy/`cargo check`/`go vet` equivalents). Not configured ⇒ **"n/a — excluded from scoring"**.
- **Lint:** the stack's linter if configured (JS: `npm run lint` / `npx eslint <path>`; ruff/clippy/golangci-lint equivalents). Not configured ⇒ **"n/a — excluded from scoring"**.

**Never score a missing toolchain as a failure** — a healthy non-JS repo must not score catastrophically because npm commands don't apply (see the scoring rule in the shared file).

Capture and store:
- **Test output:** pass count, fail count, skip count, coverage percentage (if reported)
- **Typecheck output:** error count, list of errors with file and line
- **Lint output:** error count, warning count, list of issues with file and line

## Step 3: Investigate

For each failure, warning, or error found in Step 2:

1. **Read the relevant source file** at the reported line.
2. **Understand the context** — what is the code doing, why is it failing?
3. **Classify** each item as one of:

| Classification | Meaning |
|---------------|---------|
| `bug` | Actual defect that causes incorrect behavior |
| `tech-debt` | Code smell, complexity issue, or pattern violation — not currently broken |
| `test-gap` | Missing test coverage for an important code path |
| `false-positive` | Tool is wrong — the code is correct |

Rate each `bug` with the shared severity scale from `skills/_shared/severity.md`: **CRITICAL / HIGH / MEDIUM / LOW** (same vocabulary as `/review` and `/cso`).

For each `bug`, read `$SHARED_DIR/prevent-the-class.md` and answer its question (how to prevent this class, not just this instance) without fixing anything: name the **Failure class** and the **Invariant**, search for the **Affected scope** (other instances, file:line, or how you searched and found none), and recommend the smallest prevention mechanism (an existing guard, a lint or analyzer rule, a type or API change, a shared primitive, a reusable check). Don't invent defects you haven't seen.

Static tool output alone cannot confirm runtime behavior — without `--runtime`, mark suspected behavior defects as `bug (suspected — static evidence only)`.

## Step 3.5: Runtime Verification (only with `--runtime`)

Observe actual behavior instead of inferring it from static output:

> **Invoke skill `verify-app`** with args `--yes`

For a desktop app `verify-app` runs `verify-desktop`: each screen that mismatches its mockup becomes a row too.

Fold the resulting evidence pack (`skills/_shared/evidence-pack.md`) into the audit: each FAIL check/journey becomes a Bugs-table row with source `runtime`, and the pack path is cited in the report. Skip this step entirely without the flag.

## Step 4: Score

Compute health scores on a 0-100 scale — **for detected dimensions only**.

**Unknown-dimension rule:** a dimension whose toolchain is not configured ("n/a" from Step 2) reports `—` in the dashboard — never a substitute number like 50 — and its weight is redistributed proportionally across the detected dimensions. Example: no typecheck configured ⇒ weights renormalize to test 40/70 ≈ 57%, lint 30/70 ≈ 43%.

### Test Health (base weight: 40%)
```
score = (pass_count / total_count) * 100
```
If coverage data is available, blend it: `score = (pass_rate * 0.6) + (coverage_pct * 0.4)`.
Runner detected but zero tests exist ⇒ score = 0. No runner detected ⇒ `—` (n/a, excluded).

### Type Health (base weight: 30%)
```
score = max(0, 100 - (error_count * 5))
```
Each typecheck error deducts 5 points. Floor at 0. Not configured ⇒ `—` (n/a, excluded).

### Lint Health (base weight: 30%)
```
score = max(0, 100 - (error_count * 3) - (warning_count * 1))
```
Each error deducts 3 points, each warning deducts 1. Floor at 0. Not configured ⇒ `—` (n/a, excluded).

### Overall Health
```
overall = Σ(score_i × weight_i) / Σ(weight_i)   over detected dimensions only
```
If no dimension is detectable, overall is `—` with the note "no toolchain detected — nothing to score".

## Step 5: Write Report

Write the report to `$HOME/.agentic-workflow/$REPO_SLUG/qa/{timestamp}-audit-{slug}.md` where:
- `{timestamp}` is `YYYYMMDD-HHmmss` format
- `{slug}` is the scoped module name in kebab-case, or `full-project` if no scope

Report format:

```markdown
# Bug Report: {scope or "Full Project Audit"}

**Date:** {ISO timestamp}
**Scope:** {module path or "entire project"}

## Health Score Dashboard

| Metric | Score | Weight | Details |
|--------|-------|--------|---------|
| Test Health | {score}/100 or — | {renormalized %} | {pass}/{total} tests passing, {coverage}% coverage — or "n/a — excluded from scoring" |
| Type Health | {score}/100 or — | {renormalized %} | {n} typecheck errors — or "n/a — excluded from scoring" |
| Lint Health | {score}/100 or — | {renormalized %} | {n} errors, {n} warnings — or "n/a — excluded from scoring" |
| **Overall** | **{score}/100 or —** | | weights renormalized over detected dimensions |

## Bugs

| # | Severity | Source | File | Line | Description |
|---|----------|--------|------|------|-------------|
| 1 | {CRITICAL/HIGH/MEDIUM/LOW} | {static \| runtime \| suspected} | `{file}` | {line} | {description} |

{If no bugs found: "No bugs detected."}

For each bug row, also:

| # | Failure class | Invariant | Affected scope | Recommended prevention |
|---|---------------|-----------|----------------|------------------------|
| 1 | {the mistake that can recur} | {what should always hold} | {other instances file:line, or "searched <how>, none found"} | {existing guard <which> \| mechanism <which> — why it covers the class} |

{If --runtime ran: "Runtime evidence pack: {path to verification/<run-id>/pack.json} — verdict {PASS|WARN|FAIL}"}

## Tech Debt

| # | File | Line | Description |
|---|------|------|-------------|
| 1 | `{file}` | {line} | {description} |

{If none: "No tech debt items identified."}

## Test Gaps

| # | Area | Description |
|---|------|-------------|
| 1 | `{file or module}` | {what is untested} |

{If none: "Test coverage appears adequate for the scoped area."}

## False Positives

Every suppressed item is listed — suppression is never invisible:

| # | Tool | File | Line | Reported issue | Justification for suppression |
|---|------|------|------|----------------|-------------------------------|
| 1 | {tool} | `{file}` | {line} | {what the tool claimed} | {why the code is actually correct} |

{If none: "No false positives."}

## Recommended Fix Priority

Based on severity and impact, address issues in this order:

1. {highest priority item — why}
2. {next item — why}
3. {next item — why}
...
```

## Step 6: Report to User

```
Bug report complete.

Scope: {module or "full project"}
Overall health: {score}/100 (— dimensions excluded from scoring)
  Test:  {score}/100 or —
  Type:  {score}/100 or —
  Lint:  {score}/100 or —

Found: {n} bugs, {n} tech debt items, {n} test gaps, {n} false positives (itemized in report)
Report: ~/.agentic-workflow/<repo-slug>/qa/{filename}

Fix a specific bug with full context: /bugHunt --from-report <report-path> --item N
```

## Next steps

- `/bugHunt --from-report <report-path> --item N` — auto-fix a numbered bug from this report with verification
- `/rootCause` — deep-dive a specific bug from the report
