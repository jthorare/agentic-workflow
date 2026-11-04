---
name: planDesignReview
description: "Use when a plan or feature spec needs a UX check before any building: screens, flows, loading, empty and error states, accessibility, copy tone. Rates five design dimensions 1-5 and fills a surface-by-state coverage table from the plan text. Not for a running UI, and not for API or developer-experience review."
argument-hint: "[plan-path] [--output <path>] [--slim]"
allowed-tools: Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Plan Design Review — Design Lens on Plan Docs

Rates a plan document across five design dimensions with specific gaps and recommended changes. Complements `/productReview` and `/archReview`.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Overview

Reads a plan directory's design-relevant docs (auto-discovers latest if no arg) along with the design language artifacts (`.impeccable.md`, `design-tokens.json`). Rates each of five dimensions on the anchored 1–5 scale from `_shared/severity.md`, with a mandatory surface×state coverage table. Writes the review to `plans/<feature>/design-review.md` so `/autoplan` can consolidate alongside other lenses.

## Inputs

- Plan — path arg, OR auto-discover per `_shared/plan-discovery.md`:
  ```bash
  SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
  source "$SHARED_DIR/repo-slug.sh"
  PLAN_DIR=$(ls -1dt "$AW_DIR/plans/"*/ 2>/dev/null | head -1)
  ```
  (falls back to the newest legacy single-file plan — see the shared snippet)
- From the plan dir, per the `_shared/plan-layout.md` reader matrix: `plan.md` + **`design-brief.md`** (the doc written for the design team — the primary source for this lens) + `product.md` (legacy alias `requirements.md` only if product.md is absent)
- `--output <path>` (optional) — explicit output file path. `/autoplan` passes `--output <feature-dir>/design-review.md`.
- `--slim` (optional) — compact output: Inputs available, Ratings, coverage tables, and verdict only
- `.impeccable.md` from project root (if missing: Brand/voice dimension is N/A)
- `design-tokens.json` from project root (if missing: note in report)

## Steps

1. Resolve the plan path via the Inputs block. Resolve the output path: `--output` verbatim if provided; otherwise `$AW_DIR/plans/<feature>/design-review.md` (where `<feature>` is the parent dir name of the resolved plan.md).
2. Read `plan.md`, `design-brief.md`, `product.md`, `.impeccable.md` (if present), `design-tokens.json` (if present). Record presence/absence of each in the `## Inputs available` table — **an absent input makes its dependent dimension `N/A — missing <input>`, never a number.**
3. Review across five dimensions, each rated on the anchored 1–5 scale in `_shared/severity.md` (5 = no findings above LOW … 1 = CRITICAL or unaddressed):
   - **Information architecture** — top-level structure, grouping, naming, prioritization
   - **Interaction states** — loading / error / empty / success — verified via the surface×state table below, not free-form prose
   - **User flow completeness** — entry points, success paths, failure paths, edge cases
   - **Accessibility** — keyboard nav, screen reader semantics, color contrast, focus management — verified via the a11y sub-table
   - **Brand/voice consistency** — alignment with `.impeccable.md` brand personality; voice/tone of user-facing copy (requires `.impeccable.md` — else N/A)
4. For each scored dimension, write 2–4 bullet evidence lines (quote plan lines + cite line numbers) + a 1–3 bullet "Recommended changes" list.
5. Derive the verdict — rule-based per `_shared/severity.md`: any dimension ≤2 or any CRITICAL finding ⇒ BLOCKED; any dimension = 3 or any HIGH finding ⇒ NEEDS_WORK; otherwise PASS. N/A dimensions don't score but are listed.
6. Write to the resolved output path. Ensure the parent dir exists with `mkdir -p`. Structure:
   ```markdown
   # Plan Design Review — <feature>
   
   **Plan:** <relative path>
   **Reviewed:** <ISO date>
   
   ## Inputs available
   | Input | Present? |
   |---|---|
   | plan.md | ✓/✗ |
   | design-brief.md | ✓/✗ |
   | product.md | ✓/✗ |
   | .impeccable.md | ✓/✗ |
   | design-tokens.json | ✓/✗ |
   <Absent input ⇒ dependent dimension is N/A — not scored.>
   
   ## Summary
   <3–5 sentences. Top tensions, top wins.>
   
   ## Ratings
   <Anchors per `_shared/severity.md`.>
   | Dimension | Rating | Top gap |
   |---|---|---|
   | Information architecture | N/5 | … |
   | Interaction states | N/5 | … |
   | User flow completeness | N/5 | … |
   | Accessibility | N/5 | … |
   | Brand/voice consistency | N/5 or N/A — missing .impeccable.md | … |
   
   ## Surface × state coverage
   <One row per interactive surface named in the plan/design-brief. Each cell cites
   the plan line addressing that state, or the literal `GAP`. GAPs feed the
   Interaction-states rating.>
   | Surface | Loading | Error | Empty | Success |
   |---|---|---|---|---|
   
   ### Accessibility sub-table
   | Surface | Keyboard | Screen reader | Contrast | Focus mgmt |
   |---|---|---|---|---|
   
   ## Detailed findings
   ### Information architecture (N/5)
   - Evidence: …
   - Recommended changes: …
   <etc. for each dimension>
   
   ## Verdict
   <One sentence citing the driving dimension/finding, then the literal final line:>
   verdict: <PASS|NEEDS_WORK|BLOCKED>
   ```

## Outputs

- Default: `$AW_DIR/plans/<feature>/design-review.md`
- When `--output <path>` is supplied: that exact path (used by `/autoplan` for canonical placement inside the feature dir)

## Next steps

- `/autoplan` — if running standalone, fan out to other plan lenses (product, arch, devex, security)
- `/design-shotgun` — if design ratings are low, regenerate visuals from variants instead of patching the plan

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/planDesignReview-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`planDesignReview`), `artifacts` (absolute paths: `$AW_DIR/plans/<feature>/design-review.md`), `summary` (one line),
plus `verdict` (`PASS | NEEDS_WORK | BLOCKED`, copied from the report's last `verdict:` line).

The block is the result file's JSON copied unchanged, never retyped from memory: the same keys and the same absolute paths (Read the file back if it is no longer in view). Its shape:

```aw-result
{ "skill": "planDesignReview", "artifacts": ["<absolute design-review.md path>"], "summary": "<one line>", "verdict": "PASS | NEEDS_WORK | BLOCKED" }
```

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
