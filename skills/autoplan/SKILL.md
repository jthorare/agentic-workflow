---
name: autoplan
description: "Use when a plan should be vetted from every angle before anyone builds it: product, architecture, design, developer experience and security in one pass. Runs the five review lenses in parallel and merges them into one verdict with the places they disagree. Not for a request about a single lens; that goes to the lens skill."
argument-hint: "[plan-path] [--mode mvp|growth|scale|pivot] [--slim]"
allowed-tools: Bash(ls *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Autoplan — Plan Meta-Orchestrator

Fans out five review lenses in parallel against a plan document, then consolidates findings into a single decision-ready report.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

## Overview

Dispatches five subagents in parallel — product, architecture, design, devex, security — each writing its own review file directly into the feature dir. After all return, consolidates: dedupes findings into one normalized severity table, surfaces cross-lens tensions (e.g., "product wants X but security blocks Y"), derives an overall verdict, recommends top 3 next actions. One command for full plan vetting.

## Inputs

- Plan doc path (arg), OR auto-discover per `_shared/plan-discovery.md`:
  ```bash
  SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
  source "$SHARED_DIR/repo-slug.sh"
  PLAN_DIR=$(ls -1dt "$AW_DIR/plans/"*/ 2>/dev/null | head -1)
  ```
  Resolve files inside the plan dir per `_shared/plan-layout.md`. If nothing is found, stop and ask the user for the plan path.
- `--mode mvp|growth|scale|pivot` (optional) — forwarded verbatim to `productReview` (its default is `mvp`, which under-reviews growth/scale-stage plans).
- `--slim` (optional) — forwarded verbatim to every lens: write compact reviews (tables and verdict only, minimal prose).

## Steps

1. Resolve the plan doc path via the Inputs block above. Derive `<feature>` as the parent dir name of the plan.md. Compute the absolute feature dir path: `$AW_DIR/plans/<feature>/`.

2. **Dispatch in parallel** 5 subagents per `_shared/parallel-dispatch.md` (all five launched together — one batch, never sequential). Every agent receives the absolute plan.md path and an explicit `--output` path inside the feature dir — no agent invents its own output location, and nothing needs moving afterward. Append `--slim` to every invocation when given.

   - **`productReview` agent** → "Invoke the `productReview` skill with `--plan <plan-path> --output <feature-dir>/product-review.md`" — append `--mode <mode>` when `--mode` was given.
   - **`archReview` agent** → "Invoke the `archReview` skill with `--plan <plan-path> --output <feature-dir>/arch-review.md`."
   - **`planDesignReview` agent** → "Invoke the `planDesignReview` skill with `--plan <plan-path> --output <feature-dir>/design-review.md`."
   - **`planDevexReview` agent** → "Invoke the `planDevexReview` skill with `--plan <plan-path> --output <feature-dir>/devex-review.md`."
   - **`cso --plan` agent** → "Invoke the `cso` skill with `--plan <plan-path> --output <feature-dir>/security-review.md`."

3. **Existence-check before consolidation** (per `_shared/parallel-dispatch.md`). After all 5 agents return, verify each expected output exists and is non-empty:

   ```bash
   SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
   source "$SHARED_DIR/repo-slug.sh"
   FEATURE_DIR="$AW_DIR/plans/<feature>"
   for f in product-review arch-review design-review devex-review security-review; do
     [ -s "$FEATURE_DIR/$f.md" ] && echo "OK: $f.md" || echo "MISSING OUTPUT: $f.md"
   done
   ```

   A missing or empty output is a **named failure**: record it in the Lens status table and in the summary. Never fill in findings from memory for a lens whose file is missing.

4. Read every present review file. Fill the Lens status table from the Step 3 check output plus each file's own `verdict:` line — never from memory.

5. **Consolidate:** produce `plans/<feature>/consolidated-review.md`:
   ```markdown
   # Consolidated Plan Review — <feature>
   
   **Plan:** <relative path>
   **Reviewed:** <ISO date>
   **Lenses applied:** product · arch · design · devex · security
   
   ## Normalized findings
   <Schema per `_shared/severity.md`. Map each lens's vocabulary via its legacy table
   (P0→CRITICAL, P1→HIGH, etc.). Dedupe overlapping findings across lenses: keep one
   row, list every raising lens in the lens column. Empty ⇒ literal `None.`>
   | id | severity | lens | location | finding | recommended fix |
   |---|---|---|---|---|---|
   
   ## Top tensions (cross-lens)
   <Numbered list. Each item: short title, the lenses in tension, the two finding ids
   in tension, the trade-off, recommended resolution.
   Floor: at least 1 tension for every pair of lenses that both produced HIGH-or-worse
   findings. If a HIGH-producing pair genuinely has no tension, state it explicitly:
   "No tension between <lens A> and <lens B>: <reason>." An empty section without
   these statements is invalid.>
   
   ## Per-lens highlights
   ### Product
   - Top wins: <bullets>
   - Top gaps: <bullets>
   - File: `plans/<feature>/product-review.md`
   ### Architecture / Design / Devex / Security
   <same shape each>
   
   ## Recommended next actions
   1. <action> — <why> — <which lens raised it>
   2. <action> — <why> — <which lens raised it>
   3. <action> — <why> — <which lens raised it>
   
   ## Lens status
   | Lens | File | Status |
   |---|---|---|
   | Product | product-review.md | ✓ / partial / MISSING — from Step 3 output |
   | Architecture | arch-review.md | … |
   | Design | design-review.md | … |
   | Devex | devex-review.md | … |
   | Security | security-review.md | … |
   
   ## Overall verdict
   <Rule-derived per `_shared/severity.md` (CD9) from the Normalized findings table —
   never judgment-called: any CRITICAL ⇒ BLOCKED; any HIGH ⇒ NEEDS_WORK; only
   MEDIUM/LOW or lenses skipped-with-reason ⇒ PASS. A MISSING lens caps the verdict
   at NEEDS_WORK. One sentence citing the driving finding ids, then end the file with
   the literal final line:>
   verdict: <PASS|NEEDS_WORK|BLOCKED>
   ```

6. Print a short summary to stdout (5-line lens status table + overall verdict + top 3 next actions) so the user sees the result without opening the file.

## Outputs

All under `$AW_DIR/plans/<feature>/`: `product-review.md` · `arch-review.md` · `design-review.md` · `devex-review.md` · `security-review.md` · `consolidated-review.md`

## Next steps

- If the overall verdict is **not BLOCKED** — `/specToProvenPR <feature-dir>/plan.md` — hand the vetted plan to staged implementation; the consolidated review and lens files ride along in the feature dir
- If **BLOCKED** — address the CRITICAL findings (edit the plan or re-run `/officeHours`), then re-run `/autoplan`
- `/design-shotgun` — if visual exploration is recommended by the design lens

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/autoplan-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) and print the same JSON as a fenced `aw-result` block before `## Next steps`.

Keys: `skill` (`autoplan`), `artifacts` (absolute paths: `consolidated-review.md` and each lens's review file in the feature dir), `summary` (one line),
plus `feature_dir` (absolute path of `$AW_DIR/plans/<feature>`); `verdict` (`PASS | NEEDS_WORK | BLOCKED`, copied from the report's last `verdict:` line); `lenses` (object: lens name to that lens's verdict, or `MISSING`).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
