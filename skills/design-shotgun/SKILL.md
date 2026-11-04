---
name: design-shotgun
description: "Use when you want to compare several looks for one screen or feature before committing to one. Builds 4-6 mockup variants in parallel with distinct aesthetic directions, screenshots them onto one contact sheet and hands the pick to design-mockup. Not for building a single mockup (design-mockup)."
argument-hint: "[feature-name] [--variants N]"
allowed-tools: Bash(ls *), Bash(mkdir *), Bash(cat *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__plugin_playwright_playwright__browser_navigate, mcp__plugin_playwright_playwright__browser_take_screenshot, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Design Shotgun — Parallel Mockup Variants

Generates 4–6 mockup variants in parallel with distinct aesthetic directions. Contact sheet for side-by-side comparison; user picks winner → `/design-mockup` takes it as seed.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Overview

Sits BEFORE `/design-mockup` in the design pipeline. Spawns N parallel subagents, each with a distinct aesthetic direction (minimalist, brutalist, editorial, glassmorphism, neo-skeuomorphic, swiss-grid). Each subagent invokes the `compound-engineering:frontend-design` plugin skill with the direction baked into the brief. Outputs all N variants as HTML, plus a captioned contact-sheet HTML embedding them side-by-side, Playwright screenshots of each, and a `picked.json` handoff for `/design-mockup`.

**External pack dependency:** design-shotgun delegates variant generation to the `frontend-design` skill provided by the **compound-engineering plugin** (skill name `compound-engineering:frontend-design`). If that plugin skill is not available, the skill falls back to constructing the mockup HTML inline using design tokens + the chosen aesthetic direction's prompt template (see `skills/design-shotgun/aesthetics.md` for fallback templates). This makes design-shotgun self-sufficient even in environments without the plugin.

## Inputs

- Feature name (positional arg). If omitted, auto-discover from the latest `plans/*` dir.
- `--variants N` (optional, default 4, max 6).
- `design-tokens.json` from project root (required for token-aware generation).
- `.impeccable.md` from project root (required for brand alignment).
- Feature brief from `$AW_DIR/plans/<feature>/plan.md`.

## Steps

1. **Resolve the output dir** — this skill has no design preamble, so derive the slug explicitly. Single Bash invocation:
   ```bash
   SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
   source "$SHARED_DIR/repo-slug.sh"
   mkdir -p "$AW_DIR/design/shotgun"
   echo "shotgun-dir: $AW_DIR/design/shotgun"
   ```
   Use the echoed **absolute** `shotgun-dir` everywhere below — never `~` or an unexpanded `$AW_DIR` in subagent prompts or Playwright calls.
2. **Prerequisite gate** — this skill has no design preamble, so check explicitly: `design-tokens.json` AND `.impeccable.md` must exist at the project root. If either is missing, stop:
   > "Design language not established — run `/design-language [url1 url2...]` first, then re-run `/design-shotgun`. Generating variants without tokens produces token-free mockups that can't seed the pipeline."
3. Resolve feature name (arg, or `ls -t "$AW_DIR"/plans/*/plan.md | head -1` — re-source repo-slug.sh in the same invocation — and use its dirname).
4. Resolve N (`--variants` flag or 4). Clamp to [2, 6]. If N < 4, print a one-line note "generating fewer than 4 variants reduces aesthetic diversity — consider --variants 4+" but proceed.
5. Read `design-tokens.json` and `.impeccable.md`. Read the plan doc for context.
6. **Pick N aesthetic directions** from this seed list, choosing the N that most diverge from each other given the brand personality in `.impeccable.md`:
   - `minimalist` — restrained typography, generous whitespace, monochrome accents
   - `brutalist` — raw monospace, hard edges, system fonts, minimal styling
   - `editorial` — serif headlines, magazine-style hierarchy, drop caps
   - `glassmorphism` — translucent surfaces, backdrop blur, layered depth
   - `neo-skeuomorphic` — soft shadows, subtle gradients, tactile metaphors
   - `swiss-grid` — strict 12-column grid, Helvetica, asymmetric balance
7. **Dispatch N subagents in parallel.** **Dispatch in parallel** — all N subagents at once (one **Spawn a subagent** per variant; see `_shared/parallel-dispatch.md`). Before dispatching, read `skills/design-shotgun/aesthetics.md` and extract the section matching each chosen aesthetic direction — these are the inline fallback templates. Each subagent receives:
   - Feature brief from the plan
   - `design-tokens.json` content
   - `.impeccable.md` content
   - ONE aesthetic direction (its own; no overlap)
   - **Fallback template** from `skills/design-shotgun/aesthetics.md` for that direction (the entire matching section verbatim)
   - Output path: the **expanded absolute path** `<shotgun-dir>/variant-{i}.html` where `i` is the variant number 1..N (expand `<shotgun-dir>` from Step 1 before dispatch — subagents get fresh shells and cannot resolve `$AW_DIR`)
   - **Instruction:** "Probe for the compound-engineering plugin's frontend-design skill by attempting to **Invoke skill `compound-engineering:frontend-design`** (a Claude Code plugin skill; on other hosts it is normally absent) — do NOT probe a provider skills directory for a `frontend-design/` folder (such a directory may exist but be empty; the real skill lives in the plugin). If the plugin skill is unavailable, use the inline aesthetic template below as system context to produce a single self-contained HTML mockup, and say which path was taken. Either way, the output must respect the project's design tokens and brand language, biased toward the chosen aesthetic direction." Include the relevant aesthetics.md section verbatim in the subagent prompt as the fallback template.
8. Wait for all N subagents. Verify each `<shotgun-dir>/variant-{i}.html` exists and is non-empty; a missing output is a named failure — report it, never silently skip.
9. Generate `<shotgun-dir>/contact-sheet.html` — a single HTML page with each variant in an `<iframe>` (or `<details>`-wrapped block). **Each variant carries a caption block** containing:
   - **Direction** — the aesthetic direction name
   - **Rationale** — one line: why this direction was chosen given the brand personality in `.impeccable.md`
   - **Token deviation** — one line: which design tokens the variant bends or breaks (e.g. "swaps accent #6366f1 → mono", "spacing scale intact"), or "on-token" if fully compliant
   Mobile-friendly grid layout, max 3 columns on desktop.
10. **Screenshot each variant** using Playwright MCP at 1280×800 viewport, holding the browser lock:
    - Acquire before the first navigation (single Bash invocation):
      ```bash
      SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
      LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; acquire_lock
      ```
    - For each variant: navigate to `file://<shotgun-dir>/variant-{i}.html` (absolute path), then `mcp: playwright/browser_take_screenshot` with `{ filename: "<shotgun-dir>/variant-{i}.png" }` (absolute path)
    - Release after the last screenshot (single invocation): re-run the block above with `release_lock` in place of `acquire_lock`
11. **Pick a winner.** Ask via **Ask the user**: "Which variant should seed /design-mockup?" with one option per variant (label = `variant-{i} — <direction>`, description = the caption rationale) plus "None — discard and re-run". On a pick, write `<shotgun-dir>/picked.json`:
    ```json
    {
      "variant": 3,
      "direction": "editorial",
      "html_path": "<shotgun-dir>/variant-3.html",
      "feature": "<feature-name>"
    }
    ```
    (`html_path` fully expanded and absolute.) On "None", write nothing and report.
12. Print summary to stdout: list of variants with directions and captions, contact-sheet path, picked variant (if any), and the `picked.json` path.

## Outputs

All under `$AW_DIR/design/shotgun/`:
- `variant-{1..N}.html` — full-page HTML mockups
- `variant-{1..N}.png` — Playwright screenshots
- `contact-sheet.html` — captioned side-by-side comparison page
- `picked.json` — `{variant, direction, html_path, feature}` handoff consumed by `/design-mockup-web` Step 0

## Next steps

- `/design-mockup <screen>` — reads `design/shotgun/picked.json` and takes the picked variant's HTML/styling as the seed for a fully-built mockup
- `/design-refine` — if you want to iterate within the chosen direction without rebuilding from scratch

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-shotgun-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`design-shotgun`), `artifacts` (absolute paths: the variant files, the contact sheet under `$AW_DIR/design/shotgun/` and `picked.json` when a variant was picked), `summary` (one line),
plus `variants` (count of variants generated); `picked` (the picked variant, or null).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
