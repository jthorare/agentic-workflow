---
name: design-verify-ios
description: "Use when a SwiftUI or UIKit screen should be compared with its approved mockup in the iOS simulator. Boots the sim, captures screenshots across appearance and size settings, diffs them with the baseline and lists fixes. Not for web pages, and not for functional or VoiceOver testing (verify-ios)."
argument-hint: [screen-name]
allowed-tools: Read, Write, Glob, AskUserQuestion, Bash(SHARED_DIR=*), Bash(source *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(cat *), mcp__xcodebuildmcp__session_show_defaults, mcp__xcodebuildmcp__discover_projs, mcp__xcodebuildmcp__list_schemes, mcp__xcodebuildmcp__list_sims, mcp__xcodebuildmcp__boot_sim, mcp__xcodebuildmcp__build_run_sim, mcp__xcodebuildmcp__build_sim, mcp__xcodebuildmcp__get_app_bundle_id, mcp__xcodebuildmcp__install_app_sim, mcp__xcodebuildmcp__launch_app_sim, mcp__xcodebuildmcp__snapshot_ui, mcp__xcodebuildmcp__screenshot, mcp__design-comparison__compare_design, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

---

# Design Verify iOS — Simulator Screenshot Diff vs Mockup

Boots the simulator per `_shared/sim-bootstrap.md`, navigates with snapshot_ui-verified gestures, captures an appearance/size matrix, and diffs each cell against its per-screen mockup baseline. Artifact paths and the `comparison-report.json` schema come from `_shared/design-artifact-paths.md`.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Load screens.json and Baselines

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
cat "$AW_DIR/design/screens.json" 2>/dev/null || echo "NO_SCREENS_JSON"
ls "$AW_DIR/design/"mockup-ios-*.png 2>/dev/null
ls "$AW_DIR/design/mockup-ios.png" 2>/dev/null || true
```

Read `$SHARED_DIR/design-artifact-paths.md` for the screens.json (CD4) schema and baseline naming (CD3).

- **Screens to verify:** the `[screen-name]` argument if given, else every screen in `screens.json` with `"source": "design-mockup-ios"`.
- **Baselines:** `mockup-ios-<screen>.png` (light) and, when present, `mockup-ios-<screen>-dark.png` (dark).
- **Legacy baseline:** a bare `mockup-ios.png` (second `ls` line) may be used only for a single light-appearance compare, with the warning: "legacy baseline — re-run /design-mockup to upgrade".

If a requested screen has no entry and no baseline, or none exist at all, stop:
> "No iOS mockup baseline for `<screen>`. Run `/design-mockup-ios <screen>` first."

**Staleness check** — warn (do not stop) if the screen's `baseline_stale` in screens.json is non-null: "baselines predate a design-token update (design-evolve) — re-run /design-mockup-ios".

## Step 2: Acquire Simulator Lock

Single bash invocation per the lock recipe in `$SHARED_DIR/sim-bootstrap.md` (`skill-lock.sh` enables `set -euo pipefail`):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=ios-sim source "$SHARED_DIR/skill-lock.sh"; acquire_lock
```

If `acquire_lock` reports TIMEOUT, report "Another skill is using the simulator" and stop. Every failure branch after this point must, in one invocation, re-source (`LOCK_NAME=ios-sim source "$SHARED_DIR/skill-lock.sh"`) and call `release_lock` before stopping. Use `return`, not `exit`, in sourced context — `exit` after sourcing can kill the caller's shell. Never end this skill with the lock held.

## Step 3: Boot Simulator and Launch App

Follow the canonical sequence in `$SHARED_DIR/sim-bootstrap.md`:

1. `mcp: xcodebuildmcp/session_show_defaults` — verify project/workspace, scheme, simulator
2. If missing/wrong: `mcp: xcodebuildmcp/discover_projs` → `mcp: xcodebuildmcp/list_schemes`
3. `mcp: xcodebuildmcp/list_sims` — pick the target simulator
4. `mcp: xcodebuildmcp/boot_sim` if not already Booted
5. `mcp: xcodebuildmcp/build_run_sim` — or the split path: `mcp: xcodebuildmcp/build_sim` → `mcp: xcodebuildmcp/get_app_bundle_id` → `mcp: xcodebuildmcp/install_app_sim` → `mcp: xcodebuildmcp/launch_app_sim`

If any of these fails, release the lock (Step 2 recipe) and stop with the tool error.

## Step 4: Navigate to Screen — snapshot_ui First

1. `mcp: xcodebuildmcp/snapshot_ui` — inspect the view hierarchy. If the target screen is already showing (labels/identifiers match), skip to Step 5.
2. Otherwise read the screen's `nav` recipe from screens.json — an ordered list of `{"action":"tap","target":"<label or x,y>"}` steps recorded by `/design-mockup-ios`.
3. **Capability probe** (per `$SHARED_DIR/sim-bootstrap.md`): before any gesture, check that the UI-automation workflow tools (tap/swipe/type_text) are available. If absent, print exactly:
   > "XcodeBuildMCP UI-automation workflow not enabled — see github.com/getsentry/XcodeBuildMCP/docs/CONFIGURATION.md. Interaction steps will be SKIPPED (verdict capped at WARN)."
   Then, because this screen **requires navigation**, do not merely cap at WARN: capturing whatever screen happens to be showing would produce a false verdict. Mark the screen **FAIL** with reason `navigation required but gestures unavailable`, and skip its capture.
4. Execute each nav step: resolve label targets to coordinates from the current `mcp: xcodebuildmcp/snapshot_ui` hierarchy, perform the gesture, then re-run `mcp: xcodebuildmcp/snapshot_ui` to assert the expected transition happened. If a step's target cannot be found, mark the screen FAIL with the hierarchy evidence.
5. If navigation is needed but the screen has no `nav` recipe (`null`), mark it FAIL: "no nav recipe in screens.json — re-run /design-mockup-ios <screen> (records one) or navigate the simulator manually and re-run."

## Step 5: Create the Run Directory

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
RUN_ID="$(date -u +%Y%m%d-%H%M%S)-<screen-or-all>"
mkdir -p "$AW_DIR/design/verify/$RUN_ID"
echo "run-dir: $AW_DIR/design/verify/$RUN_ID"
```

Previous runs are never overwritten.

## Step 6: Capture the Appearance/Size Matrix

For each screen, capture with `mcp: xcodebuildmcp/screenshot` into `$AW_DIR/design/verify/$RUN_ID/<screen>-<cell>.png`, one cell at a time:

| Cell | What | Diffed against |
|------|------|----------------|
| `light` | light appearance (always) | `mockup-ios-<screen>.png` |
| `dark` | dark appearance | `mockup-ios-<screen>-dark.png` when it exists; otherwise capture for the record, no diff |
| `dynamic-type` | one Dynamic Type step-up; sanity-check for truncation/overlap via `mcp: xcodebuildmcp/snapshot_ui` | no diff (recorded observation) |
| `<class2>-light` | a second device class (e.g. iPad) when `mcp: xcodebuildmcp/list_sims` offers one | recorded; diffed only if a matching baseline exists |

Appearance/Dynamic Type switching requires the simulator-management workflow; if those tools are not enabled, record the affected cells as `SKIPPED — simulator management workflow not enabled` and cap the run verdict at **WARN** (the `light` cell alone can still PASS a cell-level diff, but never the overall run).

## Step 7: Diff Against Baselines

For each matrix cell with a baseline, call `mcp: design-comparison/compare_design`:

- **reference:** the baseline PNG from Step 1
- **implementation:** `$AW_DIR/design/verify/$RUN_ID/<screen>-<cell>.png`

Record the response's **numeric diff-percentage field** as `diff_pct`; save the returned diff image to `$AW_DIR/design/verify/$RUN_ID/<screen>-<cell>-diff.png`. Cell verdict (CD11): `≤2%` PASS · `2–10%` WARN · `>10%` FAIL.

## Step 8: Write comparison-report.json (Run Manifest)

Write `$AW_DIR/design/verify/$RUN_ID/comparison-report.json` per the schema in `$SHARED_DIR/design-artifact-paths.md`, with the matrix cell carried in the `viewport` field:

```json
{ "schema": "comparison-report/v1", "run_id": "<run-id>",
  "screens": [ { "screen": "...", "viewport": "light|dark|dynamic-type|<class2>-light", "baseline": "...",
                 "capture": "...", "diff_image": "...", "diff_pct": 0.0, "verdict": "PASS|WARN|FAIL" } ],
  "overall": { "max_diff_pct": 0.0, "verdict": "PASS|WARN|FAIL" } }
```

`overall.verdict` = worst cell verdict (SKIPPED cells cap it at WARN); `overall.max_diff_pct` = max `diff_pct`. Nav-failed screens get `diff_pct: 100`, `verdict: "FAIL"`, and the reason recorded in place of the diff image path. This file is the run's gate manifest — the dispatcher and ship chain read it.

## Step 9: Report Results

**Report honesty (region-level only):** a pixel diff can say *where* pixels differ ("header area differs by N%"), never *why*. Do not attribute deviations to specific Theme values or tokens — there is no computed-style probe on iOS, so token-level attribution is forbidden.

```
[<PASS|WARN|FAIL>] iOS Design Verification — <screen(s)>
=========================================================
Overall:  <verdict>  (max diff <N>% · thresholds: ≤2% PASS / ≤10% WARN / >10% FAIL)

| screen | cell | diff % | verdict | deviating regions |
|--------|------|--------|---------|-------------------|

Skipped cells: <list with reasons, or "none">
Artifacts: $AW_DIR/design/verify/<run-id>/
```

For WARN/FAIL cells, describe the deviating regions and point at the diff image; suggest `/design-refine` or `/design-implement-ios`, then `/design-verify-ios` again.

## Step 10: Release Simulator Lock

Always — success or failure (single invocation, re-sourced):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=ios-sim source "$SHARED_DIR/skill-lock.sh"; release_lock
```

## Rules

- Never diff a capture whose on-screen content was not confirmed via `mcp: xcodebuildmcp/snapshot_ui`
- Region-level findings only — no token/Theme attribution (Step 9)
- Do not modify any code — this skill is read-only verification
- If the simulator shows an unexpected state (system dialog, wrong screen after nav), record it as evidence and mark the affected screen FAIL rather than guessing

## Next steps

Gate on `overall.verdict` — a FAIL run **blocks** the `/shipRelease` suggestion:

- **PASS** — `/shipRelease` — diff is clean, ship the release
- **WARN** — `/design-refine` — address minor discrepancies or enable the missing simulator workflows, then `/design-verify-ios` again
- **FAIL** — `/design-refine` or `/design-implement-ios` — significant deviation; re-verify before any ship step

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-verify-ios-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`design-verify-ios`), `artifacts` (absolute paths: `$AW_DIR/design/verify/<run-id>/comparison-report.json` and its diff images), `summary` (one line),
plus `run_id` (verify run id); `max_diff_pct` (`overall.max_diff_pct`); `verdict` (`PASS | WARN | FAIL`, `overall.verdict` of the comparison report).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
