---
name: planDevexReview
description: "Developer-experience review of a plan document. Maps friction in API ergonomics, setup, error messages, observability, docs, migration paths, and ramp-up cost."
argument-hint: "[plan-path] [--output <path>] [--slim]"
allowed-tools: Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Plan Devex Review — DX Lens on Plan Docs

Friction mapping for plan documents. Identifies severity-rated DX gaps that will hurt implementers or downstream contributors.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

## Overview

Reads a plan directory's engineering-facing docs (auto-discovers latest if no arg) plus the repo's conventions (`AGENTS.md`, `.agents/rules/*.md`). Identifies DX friction across six dimensions, rating each finding on the shared severity scale with a concrete fix. Writes the review to `plans/<feature>/devex-review.md` so `/autoplan` can consolidate.

## Inputs

- Plan — path arg, OR auto-discover per `_shared/plan-discovery.md`:
  ```bash
  SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
  source "$SHARED_DIR/repo-slug.sh"
  PLAN_DIR=$(ls -1dt "$AW_DIR/plans/"*/ 2>/dev/null | head -1)
  ```
- From the plan dir, per the `_shared/plan-layout.md` reader matrix: `plan.md` + **`engineering.md`** + **`TASKS.md`** — the DX lens must see the API surface and task breakdown it judges (legacy alias `design.md` → engineering.md only if the canonical file is absent)
- `--output <path>` (optional) — explicit output file path. `/autoplan` passes `--output <feature-dir>/devex-review.md`.
- `--slim` (optional) — compact output: findings tables and verdict only
- Repo conventions: `AGENTS.md` (or `CLAUDE.md` if that is all the repo has), every `.agents/rules/*.md` (fall back to `.claude/rules/*.md` or `.cursor/rules/*.mdc` in repos not yet migrated)

## Steps

1. Resolve the plan via the Inputs block. Resolve the output path: `--output` verbatim if provided; otherwise `$AW_DIR/plans/<feature>/devex-review.md`.
2. Read `plan.md` + `engineering.md` + `TASKS.md` + `AGENTS.md` + each rule file under `.agents/rules/` (or the legacy fallbacks above). Note any missing plan doc in the Summary.
3. For each of six dimensions, identify findings with severity per `_shared/severity.md` (CRITICAL / HIGH / MEDIUM / LOW):
   - **API surface ergonomics** — argument shapes, defaults, error responses, idempotency, discoverability (judge the actual API in engineering.md, not the TL;DR)
   - **Setup / install friction** — required pre-reqs, install steps, environment assumptions. **Must include a time-to-first-success estimate in minutes**, with the assumptions behind it.
   - **Error messages & observability** — log structure, error codes, debuggability, traceability, instrumentation
   - **Documentation completeness for this feature** — does the plan specify what docs will be written/updated? Are required reference docs already in place?
   - **Backwards compatibility / migration** — are existing users handled? Versioning? Deprecation path? Schema migration plan?
   - **Ramp-up cost for a new contributor** — **must include a named tribal-knowledge list**: each unwritten fact a new dev would need to be told, listed by name (or the literal `None.`)
4. For each finding write: dimension, severity, evidence (cite plan doc + line numbers), concrete fix recommendation. When the finding is that something is *missing* from the plan, use the evidence-of-absence format: `Evidence: absent — searched <terms/sections>`.
5. Derive the verdict per `_shared/severity.md`: any CRITICAL ⇒ BLOCKED; any HIGH ⇒ NEEDS_WORK; otherwise PASS.
6. Write to the resolved output path. Ensure the parent dir exists with `mkdir -p`. Structure:
   ```markdown
   # Plan Devex Review — <feature>
   
   **Plan:** <relative path>
   **Reviewed:** <ISO date>
   
   ## Summary
   <3–5 sentences. Top frictions, blockers. Time-to-first-success: ~N minutes.>
   
   ## Findings by severity
   <Severity definitions in `_shared/severity.md`. EVERY severity section below is
   mandatory and contains either findings or the literal line `None.` — an empty
   section is indistinguishable from a skipped one and is invalid.>
   ### CRITICAL
   - **<Dimension>:** <finding>
     - Evidence: plan line N — "<quote>"  (or: absent — searched <terms>)
     - Fix: <concrete recommendation>
   ### HIGH
   …
   ### MEDIUM
   …
   ### LOW
   …
   
   ## Per-dimension summary
   | Dimension | Worst severity | Findings |
   |---|---|---|
   | API surface ergonomics | … | N |
   | Setup / install (time-to-first-success: ~N min) | … | N |
   | Error messages & observability | … | N |
   | Docs completeness | … | N |
   | Backwards compatibility | … | N |
   | Ramp-up cost (tribal-knowledge items: N) | … | N |
   
   ## Verdict
   <One sentence citing the driving findings, then the literal final line:>
   verdict: <PASS|NEEDS_WORK|BLOCKED>
   ```

## Outputs

- Default: `$AW_DIR/plans/<feature>/devex-review.md`
- When `--output <path>` is supplied: that exact path (used by `/autoplan` for canonical placement inside the feature dir)

## Next steps

- `/autoplan` — if running standalone, fan out other lenses
- `/review` — if the plan is already implemented and you want a code-level DX check

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/planDevexReview-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) and print the same JSON as a fenced `aw-result` block before `## Next steps`.

Keys: `skill` (`planDevexReview`), `artifacts` (absolute paths: `$AW_DIR/plans/<feature>/devex-review.md`), `summary` (one line),
plus `verdict` (`PASS | NEEDS_WORK | BLOCKED`, copied from the report's last `verdict:` line).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
