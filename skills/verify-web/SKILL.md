---
name: verify-web
description: "Playwright-based self-verification of running web apps. Executes journeys through verification lenses (functional, visual, accessibility, error-state, responsive) and writes an evidence pack (pack.json + report.md) on every run."
argument-hint: "[--journey <plan.md>] [--lenses <csv>] [--visual] [--baseline] [--yes] [--base-url <url>] [criteria or 'auto']"
allowed-tools: Bash(git *), Bash(SHARED_DIR=*), Bash(source *), Bash(ls *), Bash(mkdir *), Bash(date *), Read, Write, Glob, Grep, AskUserQuestion, mcp__plugin_playwright_playwright__browser_navigate, mcp__plugin_playwright_playwright__browser_snapshot, mcp__plugin_playwright_playwright__browser_take_screenshot, mcp__plugin_playwright_playwright__browser_click, mcp__plugin_playwright_playwright__browser_fill_form, mcp__plugin_playwright_playwright__browser_press_key, mcp__plugin_playwright_playwright__browser_resize, mcp__plugin_playwright_playwright__browser_console_messages, mcp__plugin_playwright_playwright__browser_network_requests, mcp__plugin_playwright_playwright__browser_wait_for, mcp__plugin_playwright_playwright__browser_evaluate, mcp__plugin_playwright_playwright__browser_select_option, mcp__plugin_playwright_playwright__browser_close, mcp__design-comparison__compare_design, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

---

# Verify Web — Browser-Based Self-Verification

Launches Playwright against a running web app and executes **journeys** through **verification lenses**. Every run writes an evidence pack (`pack.json` + `report.md`) per `_shared/evidence-pack.md`.

Shared references resolve from the stable toolkit path (never through a provider's skills dir):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
ls "$SHARED_DIR/verification-lenses.md" "$SHARED_DIR/evidence-pack.md"
```

If resolution or `ls` fails, stop and report — do not improvise the journey/lens rules. Read both files before planning.

## Step 1: Parse Arguments

- **`--journey <path>`** — a `verification-plan.md` (template: `_shared/verification-plan-template.md`). Its journeys, lenses, and cross-checks **are** the plan; record the path in `pack.json.plan`. Stop if the path does not exist.
- **`--lenses <csv>`** — narrow the lens set (e.g. `functional,error-state`). Default: all web-applicable lenses.
- **`--visual`** — shorthand for including the `visual` lens.
- **`--baseline`** — the visual lens must diff against `screens.json` baselines; it FAILs if no baseline exists for a verified screen.
- **`--yes`** — non-interactive: skip plan confirmation. Treated as set automatically when invoked by a parent skill (verify-app, specToProvenPR, shipRelease, landAndDeploy) — parent-invoked runs are auto-approved.
- **`--base-url <url>`** — skip URL detection and use this URL.
- **Explicit criteria** — any text after flags is verification criteria.
- **`auto` / no arguments** — infer what to verify from recent git changes.

## Journeys

Defined in `_shared/verification-lenses.md`. A journey is a named, ordered list of `{action, target, assertion}` steps executed in one browser session, `action ∈ navigate | click | fill | select | press | wait`.

**Interaction is mandatory:** ≥3 non-navigate (interactive) steps and ≥1 assertion following a state-mutating action. A plan of only navigate+snapshot pairs is **rejected** — add interactions, or record `journey: waived — <reason>`, which caps the run verdict at WARN.

## Lenses

Web-applicable lenses from the catalog in `_shared/verification-lenses.md`: `functional`, `visual`, `accessibility`, `error-state`, `responsive`. All run by default; `--lenses` narrows. Every lens result lands in `pack.json.lenses[]`; a skipped lens must carry `reason_if_skipped` — silent omission is not allowed.

## Step 2: Detect the App URL

If `--base-url` was given, use it. Otherwise:

1. Read `package.json` — `scripts.dev`/`scripts.start` reveal framework and port (Next.js `:3000`, Vite `:5173`, Angular `:4200`, generic `:3000`).
2. Check `AGENTS.md` (and any provider-specific instruction file, e.g. `CLAUDE.md`) for documented URLs or ports.

Verify reachability with `mcp: playwright/browser_navigate`. If navigation fails:
> "The app doesn't appear to be running at {url}. Start the dev server and try again."

## Step 3: Acquire Browser Lock

Acquire in a **single bash invocation** (shell state does not persist between Bash calls):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; acquire_lock
```

If the lock cannot be acquired (timeout):
> "Another browser verification session is in progress. Wait for it to finish or remove `~/.agentic-workflow/.browser.lock` if stale."

Every per-step failure branch must re-source and release **in that same invocation** (`LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; release_lock`). Use `return`, not `exit`, in sourced context — `skill-lock.sh` enables `set -euo pipefail` in the caller's shell. Always release at Step 7, success or failure.

## Step 4: Build the Verification Plan

Create the run directory first (CD5):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
RUN_ID="$(date -u +%Y%m%d-%H%M%S)-<slug>"   # <slug> = 2–4 word kebab summary of what is being verified
mkdir -p "$AW_DIR/verification/$RUN_ID"
echo "run dir: $AW_DIR/verification/$RUN_ID"
```

**Plan source (first match wins):**

1. **`--journey` file** — parse its Journeys/Lenses/cross-check tables.
2. **Explicit criteria** — parse into journeys: route to visit, interactions to perform, assertion per step.
3. **Auto (diff-inference)** — infer from recent changes:

```bash
git diff --name-only HEAD~3..HEAD
git log --oneline -5
```

Route/page changes → verify those pages; component changes → verify appearance and behavior; API changes → verify the UI reflects new data; style changes → include the `visual` lens; config changes → verify startup and basic navigation.

**Validate the plan against the journey rules above** — if it has fewer than 3 interactive steps or no post-mutation assertion, rework it or record an explicit waiver. Keep 3–8 checks; more than 10 means the scope is too broad — split into multiple runs.

Present the plan and wait for confirmation — **unless `--yes` is set or the run is parent-invoked**, in which case proceed immediately and note "plan auto-approved (--yes / parent-invoked)" in the report.

## Step 5: Execute Lenses

Execute each selected lens with fully-qualified tools:

### `functional`

Execute journeys step by step: `mcp: playwright/browser_click`, `mcp: playwright/browser_fill_form`, `mcp: playwright/browser_press_key`, `mcp: playwright/browser_select_option` (navigation steps via `mcp: playwright/browser_navigate`; async UI via `mcp: playwright/browser_wait_for`). Assert after each step with `mcp: playwright/browser_snapshot`. Then check `mcp: playwright/browser_console_messages` (no uncaught errors) and `mcp: playwright/browser_network_requests` (no failed calls).

### `visual`

`mcp: playwright/browser_take_screenshot` per screen×viewport, saved as `{run-id}/{check}-{viewport}.png` inside `$AW_DIR/verification/`. Baseline lookup: Read `$AW_DIR/design/screens.json` (path echoed from the Step 4 bash block); if it maps the screen to a baseline for this viewport, call `mcp: design-comparison/compare_design` on baseline vs capture and record the numeric diff % in `pack.json.mockup_diff` — thresholds (CD11): **≤2% PASS, 2–10% WARN, >10% FAIL**. With `--baseline` and no covering baseline, the visual lens FAILs; without the flag, note "no baseline" and judge region-level only. Pixel diffs may only claim **region-level** deviations — token-level attribution (e.g. "wrong border-radius token") requires a `mcp: playwright/browser_evaluate` computed-style step.

### `accessibility`

`mcp: playwright/browser_snapshot` tree: labels, roles, heading order, keyboard reachability of interactive elements.

### `error-state`

Drive invalid input / unknown routes through the journey tools above; assert visible error UI in the snapshot; `mcp: playwright/browser_console_messages` shows no uncaught exception.

### `responsive`

`mcp: playwright/browser_resize` to the three CD4 viewports — mobile 375×812, tablet 768×1024, desktop 1440×900 — re-snapshot (and re-screenshot if visual lens is active) at each; artifacts keep the `{run-id}/{check}-{viewport}.png` naming.

**Layout claims must be falsifiable:** bind every layout/styling check to a screens.json baseline, a design token, or an explicit user criterion — never a free-floating "positioned as expected".

When all lenses finish, close the session with `mcp: playwright/browser_close`.

## Step 6: Write the Evidence Pack

Write both files into `$AW_DIR/verification/$RUN_ID/` following the schema in `_shared/evidence-pack.md` exactly:

- **`pack.json`** — `"schema": "evidence-pack/v1"`, `"skill": "verify-web"`, `"platform": "web"`, `base_url`, `plan` (journey-file path or null), `lenses[]` (every executed/skipped lens with per-check status + evidence), `journeys[]` (per-step status), `cross_checks[]` (from the journey file, with raw output files saved in the run dir), `artifacts[]` (screenshot filenames), `mockup_diff`, `started_at`/`finished_at`.
- **`report.md`** — human-readable summary: app, URL, mode, plan source, per-lens results table, issues found with route/action/expected/actual/suggestion, artifact list.

**Verdict rollup:** any journey or lens FAIL ⇒ `FAIL`; a waived journey, a SKIPPED-with-reason lens, or a mockup diff in 2–10% ⇒ at most `WARN`; otherwise `PASS`.

## Step 7: Release Browser Lock

Always release, regardless of success or failure — a leaked lock blocks all future verification sessions:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; release_lock
```

## Verdict & Gate

**A FAIL verdict forbids downstream PR-open / mark-ready** (gate rule in `_shared/evidence-pack.md`). Consumers (verify-app, specToProvenPR, shipRelease, review) read the pack — do not soften the verdict in prose. End the report with exactly:

```
verdict: <PASS|WARN|FAIL>
evidence_path: <absolute path to verification/<run-id>/>
```

## Rules

- **Never modify code** — this skill is read-only verification. Report issues, don't fix them.
- **Respect the running app** — don't restart servers, modify databases, or change app state beyond normal UI interactions.
- **Every run writes a pack** — a run without `pack.json` + `report.md` is not a verification run.
- **Confirm the plan in auto mode** unless `--yes` or parent-invoked.
- **Always release the browser lock**, even on partial failure.

## Next steps

- `/design-verify-web` — if visual gaps surfaced, run web design verification
- `/bugReport` — if functional gaps surfaced, capture them as a bug report

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/verify-web-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) and print the same JSON as a fenced `aw-result` block before `## Next steps`.

Keys: `skill` (`verify-web`), `artifacts` (absolute paths: the evidence pack directory `$AW_DIR/verification/<run-id>/` (`pack.json`, `report.md`)), `summary` (one line),
plus `evidence_path` (absolute path of the pack directory); `verdict` (`PASS | WARN | FAIL`, the pack's verdict).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
