---
name: design-verify-web
description: "Use when a web page or route you built should be checked against its saved mockup image at the mobile, tablet and desktop breakpoints. Takes Playwright screenshots, diffs them against the baseline and lists fixes. Not for iOS or desktop screens, and not for functional or console-error checks (verify-web)."
argument-hint: "[screen-name] [--route <path>]"
allowed-tools: Read, Write, Glob, AskUserQuestion, Bash(SHARED_DIR=*), Bash(source *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(cat *), mcp__plugin_playwright_playwright__browser_navigate, mcp__plugin_playwright_playwright__browser_resize, mcp__plugin_playwright_playwright__browser_wait_for, mcp__plugin_playwright_playwright__browser_snapshot, mcp__plugin_playwright_playwright__browser_console_messages, mcp__plugin_playwright_playwright__browser_take_screenshot, mcp__plugin_playwright_playwright__browser_evaluate, mcp__plugin_playwright_playwright__browser_close, mcp__design-comparison__compare_design, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

---

# Design Verify Web — Playwright Screenshot Diff vs Mockup

Captures screenshots of the live implementation at each screen's route, compares against the per-screen, per-viewport mockup baselines, and reports region-level discrepancies. Artifact paths and the `comparison-report.json` schema come from `_shared/design-artifact-paths.md`.

## Step 1: Load screens.json and Baselines

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
cat "$AW_DIR/design/screens.json" 2>/dev/null || echo "NO_SCREENS_JSON"
ls "$AW_DIR/design/"mockup-web-*.png 2>/dev/null
ls "$AW_DIR/design/"mockup-*.png 2>/dev/null | grep -v -e 'mockup-web-' -e 'mockup-ios' || true
```

Read `$SHARED_DIR/design-artifact-paths.md` for the screens.json (CD4) schema and baseline naming (CD3).

- **Screens to verify:** the `[screen-name]` argument if given, else every screen in `screens.json` with `"source": "design-mockup-web"`.
- **Baselines:** `mockup-web-<screen>-<viewport>.png` per the screen's `baselines` map.
- **Viewports:** read from `screens.json` `viewports` (canonical: `mobile` 375×812, `tablet` 768×1024, `desktop` 1440×900) — do not hardcode a different set.
- **Legacy baselines:** a bare `mockup-<screen>.png` (second `ls` line) may be used only for a single desktop-viewport compare, with the warning: "legacy baseline — re-run /design-mockup to upgrade".

If a requested screen has no entry and no baseline, or no baselines exist at all, stop:
> "No mockup baseline for `<screen>`. Run `/design-mockup-web <screen>` first."

**Staleness check** — warn (do not stop) when either holds:
- the screen's `baseline_stale` in screens.json is non-null → "baselines predate a design-token update (design-evolve) — re-run /design-mockup-web"
- `mockup-<screen>.html` is newer than its baseline PNGs: `ls -t "$AW_DIR/design/mockup-<screen>.html" "$AW_DIR/design/mockup-web-<screen>-desktop.png" 2>/dev/null | head -1` lists the HTML first → "baseline older than the latest mockup HTML — re-run /design-mockup-web"

## Step 2: Resolve Route per Screen — Fail Loudly

For each screen, the capture URL is `<base-url><route>` where `route` is, in order:

1. the `--route <path>` argument (applies only when a single `[screen-name]` is being verified)
2. `screens.json` → `.screens.<screen>.route`

If neither yields a route, **fail loudly** — never default to `/`:
> "No route known for screen `<screen>` — verifying the root URL would diff the wrong page and produce a false verdict. Re-run `/design-mockup-web <screen>` (which records the route) or pass `--route <path>`."

Mark that screen FAIL in the report with reason `route unknown`.

Base URL: detect the dev server from `package.json` scripts (typically `http://localhost:3000`). If it is not reachable at capture time, release the lock and stop: "Dev server not running — start it and re-run."

## Step 3: Create the Run Directory

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
RUN_ID="$(date -u +%Y%m%d-%H%M%S)-<screen-or-all>"
mkdir -p "$AW_DIR/design/verify/$RUN_ID"
echo "run-dir: $AW_DIR/design/verify/$RUN_ID"
```

All captures, diff images, and the report for this run live under `design/verify/<run-id>/` — previous runs are never overwritten.

## Step 4: Acquire Browser Lock

Single bash invocation (shell state does not persist between Bash calls); note `skill-lock.sh` enables `set -euo pipefail`:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; acquire_lock
```

If `acquire_lock` reports TIMEOUT: "Another browser session is in progress. Wait or remove `~/.agentic-workflow/.browser.lock` if stale." Every failure branch after this point must, in one invocation, re-source (`LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"`) and call `release_lock` before stopping. Use `return`, not `exit`, after sourcing.

## Step 5: Capture and Diff — per Screen × Viewport

Run these calls **inline** (no Agent delegation). For each screen, for each viewport:

1. `mcp: playwright/browser_navigate` — url: `<base-url><route>`
2. `mcp: playwright/browser_resize` — width/height for the viewport
3. `mcp: playwright/browser_wait_for` — wait for the page's main content to settle
4. `mcp: playwright/browser_snapshot` — confirm the expected screen actually rendered (headings/landmarks match the screen). If the wrong page rendered (redirect, 404, auth wall), mark this screen FAIL with the evidence — do not diff the wrong page.
5. `mcp: playwright/browser_console_messages` — record errors as report context
6. `mcp: playwright/browser_take_screenshot` — save to `$AW_DIR/design/verify/$RUN_ID/<screen>-<viewport>.png`
7. `mcp: design-comparison/compare_design` — reference: `$AW_DIR/design/mockup-web-<screen>-<viewport>.png`, implementation: the capture from call 6. Record the response's **numeric diff-percentage field** as `diff_pct` and save the returned diff image to `$AW_DIR/design/verify/$RUN_ID/<screen>-<viewport>-diff.png`.
8. *(optional, only when attributing a deviation to a token)* `mcp: playwright/browser_evaluate` — sample `getComputedStyle` on the deviating element to name the actual vs expected token value
9. `mcp: playwright/browser_close` — once, after the last screen×viewport

Per-cell verdict from `diff_pct` (CD11): `≤2%` PASS · `2–10%` WARN · `>10%` FAIL.

## Step 6: Write comparison-report.json

Write `$AW_DIR/design/verify/$RUN_ID/comparison-report.json` per the schema in `$SHARED_DIR/design-artifact-paths.md`:

```json
{ "schema": "comparison-report/v1", "run_id": "<run-id>",
  "screens": [ { "screen": "...", "viewport": "...", "baseline": "...", "capture": "...",
                 "diff_image": "...", "diff_pct": 0.0, "verdict": "PASS|WARN|FAIL" } ],
  "overall": { "max_diff_pct": 0.0, "verdict": "PASS|WARN|FAIL" } }
```

`overall.verdict` = worst cell verdict; `overall.max_diff_pct` = max `diff_pct`. Screens failed for `route unknown` or wrong-page render get `diff_pct: 100`, `verdict: "FAIL"`, and the reason recorded in place of the diff image path.

## Step 7: Report Results

**Report honesty (region-level only):** a pixel diff can say *where* pixels differ ("header area differs by N%"), never *why*. Token-level attribution ("border-radius 4px instead of token 8px") is allowed **only** when backed by a Step 5.8 `mcp: playwright/browser_evaluate` computed-style sample; otherwise it is forbidden.

```
[<PASS|WARN|FAIL>] Design Verification — <screen(s)>
=====================================================
Overall:  <verdict>  (max diff <N>% · thresholds: ≤2% PASS / ≤10% WARN / >10% FAIL)

| screen | viewport | diff % | verdict | deviating regions |
|--------|----------|--------|---------|-------------------|

Console errors: <count or none>
Token attributions (computed-style verified only): <list or "none">
Artifacts: $AW_DIR/design/verify/<run-id>/
```

For WARN/FAIL cells, describe the deviating regions and point at the diff image; suggest `/design-refine` then `/design-verify-web` again.

## Step 8: Release Browser Lock

Always — success or failure:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; release_lock
```

## Rules

- Capture every configured viewport for every verified screen — no skipping
- Never diff a capture whose route or rendered content was not confirmed
- Region-level findings only, unless computed-style verified (Step 7)
- Do not modify any code — this skill is read-only verification
- If the dev server is not running, release the lock, advise the user to start it, and stop

## Next steps

Gate on `overall.verdict` — a FAIL run **blocks** the `/shipRelease` suggestion:

- **PASS** — `/shipRelease` — diff is clean, ship the release
- **WARN** — `/design-refine` — address minor discrepancies, then `/design-verify-web` again
- **FAIL** — `/design-refine` or `/design-implement-web` — significant deviation; re-verify before any ship step

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-verify-web-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) and print the same JSON as a fenced `aw-result` block before `## Next steps`.

Keys: `skill` (`design-verify-web`), `artifacts` (absolute paths: `$AW_DIR/design/verify/<run-id>/comparison-report.json` and its diff images), `summary` (one line),
plus `run_id` (verify run id); `max_diff_pct` (`overall.max_diff_pct`); `verdict` (`PASS | WARN | FAIL`, `overall.verdict` of the comparison report).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
