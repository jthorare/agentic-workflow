---
name: design-implement-web
description: "Use when an approved web mockup should become production code: React or Next.js components, CSS variables and a Tailwind theme generated from the design tokens. Scans for hardcoded values and checks the page against the mockup before calling it done. Not for SwiftUI or iOS (design-implement-ios)."
argument-hint: "<screen-name>"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(git *), Bash(ls *), Bash(SHARED_DIR=*), Bash(source *), AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff, mcp__prism-mcp__session_task_route, mcp__prism-mcp__prism_infer
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing. Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble-extended.md` (component inventory, pipeline overview).

---

# Design Implement Web — Generate Production Code from Mockup

Generate production-ready code from an approved mockup, generating token files directly from `design-tokens.json`, then proving the result against the mockup baseline before claiming completion.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Select Mockup

Locate mockup sources in priority order — HTML source files are far more useful for code generation than PNG screenshots.

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
ls "$AW_DIR/design/mockup-"*.html 2>/dev/null          # HTML sources (preferred)
ls "$AW_DIR/design/mockup-web-"*.png 2>/dev/null       # CD3 PNG baselines (fallback)
ls "$AW_DIR/design/mockup-"*.png 2>/dev/null           # legacy baseline — re-run /design-mockup to upgrade
```

Selection rules (artifact ownership table: `$SHARED_DIR/design-artifact-paths.md`):

- **No mockups found:** stop — "Run `/design-mockup <screen-name>` first."
- **One mockup:** use it automatically (prefer HTML when both exist for the same screen).
- **Multiple mockups:** list both types with their source annotated and **ask the user** which to implement.

When an HTML source exists, it is the primary reference for layout and structure; a PNG baseline for the same screen is supplementary. **Record which file was actually selected and whether it is HTML or PNG** — the Step 5 report and `implement-report.json` must name the real source, never assume PNG.

## Step 2: Generate Token Files

Generate both files directly from `design-tokens.json` (W3C DTCG: nested objects with `$value`/`$type` leaves). No external bridge is involved.

**1. CSS custom properties — `design-tokens.css`:**

- Flatten each token path to a kebab-case variable: `color.text-primary` → `--color-text-primary`, `spacing.s2` → `--spacing-s2`, `font.family-mono` → `--font-family-mono`.
- Emit a single `:root { ... }` block containing every token.
- If the token set defines dark-mode variants (e.g. `color.dark.*` or dark values in `$extensions`), emit an additional `@media (prefers-color-scheme: dark) { :root { ... } }` block overriding only the affected variables.

**2. Tailwind config extension:**

- If a `tailwind.config.{ts,js,mjs,cjs}` exists, edit it in place: extend `theme.extend.colors`, `theme.extend.spacing`, `theme.extend.fontFamily`, and `theme.extend.borderRadius` with entries that reference the CSS variables (e.g. `accent: "var(--color-accent)"`).
- If no Tailwind config exists, write `tailwind.preset.js` at the project root with the same `theme.extend` shape and note in the report that it must be wired into a future config.

**Exact output paths:**

| File | Path |
|---|---|
| CSS custom properties | `<project-root>/styles/design-tokens.css` — if the project keeps global CSS elsewhere (`src/styles/`, `app/`), follow the existing location; create `styles/` only if no convention exists |
| Tailwind extension | existing `tailwind.config.{ts,js,mjs,cjs}` edited in place; else `<project-root>/tailwind.preset.js` |

Import `design-tokens.css` from the app's global stylesheet or root layout if it is not already imported.

## Step 3: Generate Component Code

Using the mockup as visual reference and the generated token files:

- Generate React/Next.js components (or plain HTML/CSS if no framework detected)
- Import from `tokens.css` or use Tailwind classes from the preset
- Follow the component structure visible in the mockup
- Use semantic HTML elements
- Include responsive breakpoints matching the mockup

Reference `.impeccable.md` for design personality — spacing density, animation approach, interaction patterns.

## Step 3.5: Token Compliance Scan

**Always run** a content search (e.g. `rg -n`) over every file created or modified in Steps 2–3 (excluding `design-tokens.css` itself, which legitimately holds raw values):

```
rg -n '#[0-9a-fA-F]{3,8}\b' <each changed file>
rg -n '\b[0-9]+px\b' <each changed file>
```

Report every hit as `file:line — <matched value>`. For each: replace it with the corresponding token variable, or record an explicit justification when no token corresponds (e.g. `1px` hairline borders). Unjustified hits must be fixed before Step 4.

**Dark factory (optional):** follow `$SHARED_DIR/dark-factory.md` using its "design-implement (token-compliance)" objective template — gate on `dark-factory.json`, route via `mcp: prism-mcp/session_task_route`, run `mcp: prism-mcp/prism_infer` only on a `claw` target, and verify any reported violations yourself (re-search) before acting on them. Config absent ⇒ skip silently. The content search above runs regardless.

## Step 4: Verification Gate (mandatory)

Run the visual diff against the mockup baseline:

```
Invoke skill design-verify-web with args "<screen-name>"
```

Read the run's `design/verify/<run-id>/comparison-report.json` (schema and CD11 thresholds: `$SHARED_DIR/design-artifact-paths.md`). The gate binds to `overall.verdict`:

- `PASS` (≤2%) or `WARN` (2–10%) → implementation may complete.
- `FAIL` (>10%) → print `[BLOCKED]` followed by the failing screen/viewport rows and the required fixes (region-level deviations from the report). **Never print "Implementation Complete" while the verdict is FAIL** — fix the deviations and re-run this step.

## Step 5: Report

Write `$AW_DIR/design/implement-report.json` (machine artifact — consumed by design-verify, review, shipRelease):

```json
{
  "schema": "implement-report/v1",
  "screens": ["<screen-name>"],
  "files_written": ["styles/design-tokens.css", "tailwind.config.ts", "<component files>"],
  "token_coverage_pct": 0,
  "mockup_source": "<selected file from Step 1> (html | png)",
  "verify_run_id": "<run-id from Step 4>",
  "overall_verdict": "PASS | WARN | FAIL"
}
```

`token_coverage_pct` = distinct tokens referenced in generated code ÷ tokens defined in `design-tokens.json` × 100.

Then print (only when `overall_verdict` is PASS or WARN):

```
Implementation Complete
=======================

Target:      web
Mockup:      <actually selected source from Step 1, e.g. mockup-dashboard.html (HTML source)>
Verify run:  <run-id> — <overall_verdict> (max diff <pct>%)

Generated token files:
  <CSS custom-properties path>
  <Tailwind config/preset path>

Generated components:
  <list of created/modified component files>
```

## Rules

- Token files go at the exact paths in Step 2 — never scatter per-component token files
- Never hardcode values that exist in `design-tokens.json` — always reference the generated token files
- Do not modify `design-tokens.json` — it is the source of truth
- If the mockup HTML is available, use it as the primary reference for layout and structure — and report it as the source
- No completion claim without a PASS/WARN verify run recorded in `implement-report.json`

## Git mode

`AW_GIT_MODE` is `${AW_GIT_MODE:-full}`.

- `full`: do everything this skill describes.
- `readonly`: work in the current directory. Make no branch, worktree, commit, push, PR, PR comment or review, and change no remote ref. Do the file edits and local checks the skill describes, and print each git or gh command you would have run under a `Would run:` heading.

## Next steps

- `/design-refine "align color usage with the token palette"` / `"polish hover, focus, and transition states"` / `"tighten the type hierarchy"` — refinement intents, dispatched through impeccable
- `/shipRelease` — ship once the verify verdict is PASS
- Commit generated token files: `git add <token file paths>`

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-implement-web-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`design-implement-web`), `artifacts` (absolute paths: the generated token files, components and `$AW_DIR/design/implement-report.json`), `summary` (one line),
plus `overall_verdict` (`PASS | WARN | FAIL`, the verify gate's overall verdict); `verify_run_id` (the verify run id).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
