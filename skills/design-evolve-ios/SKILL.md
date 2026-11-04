---
name: design-evolve-ios
description: Use when design-tokens.json exists and a local Swift file or Xcode project, such as a new theme, has changes to fold in without losing tokens the source lacks. Not for reference websites (design-evolve-web) or a first extraction (design-analyze-ios).
argument-hint: <path/to/Theme.swift or project dir>
allowed-tools: Bash(SHARED_DIR=*), Read, Write, Edit, Glob, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

---

# Design Evolve iOS — Merge Swift Reference into Design Language

Extracts design tokens from a local Swift reference file or Xcode project and selectively merges them into the existing `design-tokens.json`.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Validate Prerequisites

Both `.impeccable.md` and `design-tokens.json` must exist. If either is missing:
> "No existing design language found. Run `/design-analyze-ios` and `/design-language` first to establish a baseline."

## Step 2: Validate Argument

The argument must be a local path to a `.swift` file or a directory containing Swift files / an Xcode project. If no argument provided, **ask the user**:
> "Provide the path to a Theme.swift file or Xcode project directory to extract tokens from:"

Verify the path exists (read the file or glob for it). If not found:
> "Path not found: `<path>`. Check the path and retry."

## Step 3: Extract Tokens from Reference (shared procedure)

Resolve the shared dir from the stable toolkit path:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
echo "extraction: $SHARED_DIR/ios-token-extraction.md"
```

Locate the Swift sources at the given path:
- A single `.swift` file → read it directly
- A directory → `<path>/**/*.swift` and read files containing `Color(`, `Font.`, or spacing constants

Read `$SHARED_DIR/ios-token-extraction.md` and follow it exactly — colors (xcassets + Swift patterns), typography, the restricted spacing rule (CGFloat type + spacing/layout name pattern only), and the completeness gate (<3 colors or 0 typography ⇒ report shortfall + **Ask the user**).

## Step 4: Merge (shared algorithm)

Read `$SHARED_DIR/token-merge.md` and follow it exactly:

- **Step A** — category diff table (NEW / DIFFERENT / UNCHANGED) against current `design-tokens.json`, source = `<path>`
- **Step B** — **Ask the user** Adopt / Adapt / Ignore per group; **Adapt requires the follow-up **Ask the user** capturing the literal replacement value**
- **Step C** — write merged `design-tokens.json` (tokens not present in the reference are always preserved); **unconditionally** append the consultation to `.impeccable.md ## Sources`; set `baseline_stale` in `screens.json` when anything was adopted or adapted
- **Step D** — report counts and exact before→after values

## Next steps

- `/design-mockup-ios` — rebuild the SwiftUI mockup with the updated tokens (existing baselines are now marked stale)
- `/design-refine` — apply the evolved language to existing views

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-evolve-ios-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`design-evolve-ios`), `artifacts` (absolute paths: the updated `design-tokens.json`), `summary` (one line),
plus `adopted` (count of tokens adopted).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
