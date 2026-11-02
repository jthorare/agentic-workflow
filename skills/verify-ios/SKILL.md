---
name: verify-ios
description: "XcodeBuildMCP-based iOS simulator verification. Executes journeys through verification lenses (functional, visual, accessibility, error-state, appearance) and writes an evidence pack (pack.json + report.md) on every run. Auto mode infers screens from Swift file changes in git diff."
argument-hint: "[--journey <plan.md>] [--lenses <csv>] [--visual] [--baseline] [--yes] [criteria or 'auto']"
allowed-tools: Bash(git *), Bash(SHARED_DIR=*), Bash(source *), Bash(ls *), Bash(mkdir *), Bash(date *), Read, Write, Glob, Grep, AskUserQuestion, mcp__xcodebuildmcp__session_show_defaults, mcp__xcodebuildmcp__list_sims, mcp__xcodebuildmcp__boot_sim, mcp__xcodebuildmcp__open_sim, mcp__xcodebuildmcp__build_sim, mcp__xcodebuildmcp__build_run_sim, mcp__xcodebuildmcp__get_app_bundle_id, mcp__xcodebuildmcp__install_app_sim, mcp__xcodebuildmcp__launch_app_sim, mcp__xcodebuildmcp__screenshot, mcp__xcodebuildmcp__snapshot_ui, mcp__xcodebuildmcp__list_schemes, mcp__xcodebuildmcp__discover_projs, mcp__design-comparison__compare_design, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

---

# Verify iOS — XcodeBuildMCP Simulator Verification

Verifies iOS app behavior on simulator using XcodeBuildMCP. Executes **journeys** through **verification lenses**. Every run writes an evidence pack (`pack.json` + `report.md`) per `_shared/evidence-pack.md`.

Shared references resolve from the stable toolkit path (never through a provider's skills dir):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
ls "$SHARED_DIR/verification-lenses.md" "$SHARED_DIR/evidence-pack.md" "$SHARED_DIR/sim-bootstrap.md"
```

If resolution or `ls` fails, stop and report — do not improvise the journey/lens or boot rules. Read all three files before planning.

## Step 1: Parse Arguments

- **`--journey <path>`** — a `verification-plan.md` (template: `_shared/verification-plan-template.md`). Its journeys, lenses, and cross-checks **are** the plan; record the path in `pack.json.plan`. Stop if the path does not exist.
- **`--lenses <csv>`** — narrow the lens set. Default: all iOS-applicable lenses.
- **`--visual`** — shorthand for including the `visual` lens.
- **`--baseline`** — the visual lens must diff against `screens.json` baselines; it FAILs if no baseline exists for a verified screen.
- **`--yes`** — non-interactive: skip plan confirmation. Treated as set automatically when invoked by a parent skill (verify-app, specToProvenPR, shipRelease) — parent-invoked runs are auto-approved.
- **Explicit criteria** — any text after flags is verification criteria.
- **`auto` / no arguments** — infer from recent Swift file changes.
- Web-only flags (e.g. `--base-url`) forwarded by a dispatcher are ignored with a one-line note.

## Journeys

Defined in `_shared/verification-lenses.md`. A journey is a named, ordered list of `{action, target, assertion}` steps executed in one simulator session; targets are coordinates/labels sourced from `mcp: xcodebuildmcp/snapshot_ui`.

**Interaction is mandatory:** ≥3 non-navigate (interactive) steps and ≥1 assertion following a state-mutating action. A plan of only launch+snapshot pairs is **rejected** — add interactions, or record `journey: waived — <reason>`, which caps the run verdict at WARN. If the gesture capability probe (Step 2.5) fails, interaction steps are SKIPPED with that reason instead — same WARN cap.

## Lenses

iOS-applicable lenses from the catalog in `_shared/verification-lenses.md`: `functional`, `visual`, `accessibility`, `error-state`, `appearance`. All run by default; `--lenses` narrows. The `appearance` lens covers light + dark and one Dynamic Type step-up; if appearance tools are not enabled, record it as `SKIPPED — simulator management workflow not enabled` and cap the verdict at WARN. Every lens result lands in `pack.json.lenses[]`; a skipped lens must carry `reason_if_skipped`.

## Step 2: Boot Simulator & App

Follow the canonical sequence in `_shared/sim-bootstrap.md`:

1. `mcp: xcodebuildmcp/session_show_defaults` — verify active project/workspace, scheme, simulator.
2. If defaults are missing/wrong: `mcp: xcodebuildmcp/discover_projs` → `mcp: xcodebuildmcp/list_schemes`.
3. `mcp: xcodebuildmcp/list_sims` — pick the target simulator.
4. `mcp: xcodebuildmcp/boot_sim` if the simulator is not already Booted (`mcp: xcodebuildmcp/open_sim` to surface the window if useful).
5. `mcp: xcodebuildmcp/build_run_sim` — or the split path: `mcp: xcodebuildmcp/build_sim` → `mcp: xcodebuildmcp/get_app_bundle_id` → `mcp: xcodebuildmcp/install_app_sim` → `mcp: xcodebuildmcp/launch_app_sim`.

If the app cannot be built or launched, **ask the user** rather than guessing a bundle ID.

## Step 2.5: Gesture Capability Probe

Per `_shared/sim-bootstrap.md`: before any tap/swipe/type step, probe whether the UI-automation workflow tools are available. If absent, print exactly:

"XcodeBuildMCP UI-automation workflow not enabled — see github.com/getsentry/XcodeBuildMCP/docs/CONFIGURATION.md. Interaction steps will be SKIPPED (verdict capped at WARN)."

Then skip interaction steps, record each as SKIPPED with that reason, and cap the run verdict at WARN. Never invoke a gesture tool that the probe did not confirm.

## Step 3: Acquire Simulator Lock

Acquire in a **single bash invocation** per `_shared/sim-bootstrap.md` (shell state does not persist between Bash calls):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=ios-sim source "$SHARED_DIR/skill-lock.sh"; acquire_lock
```

If the lock cannot be acquired (timeout):
> "Another iOS simulator session is in progress. Wait for it to finish or remove `~/.agentic-workflow/.ios-sim.lock` if stale."

Every per-step failure branch must re-source and release **in that same invocation** (`LOCK_NAME=ios-sim source "$SHARED_DIR/skill-lock.sh"; release_lock`). Use `return`, not `exit`, in sourced context. Always release at Step 7, success or failure.

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
2. **Explicit criteria** — parse into journeys: screen to reach, interactions to perform, assertion per step.
3. **Auto (diff-inference)** — infer from recent Swift changes:

```bash
git diff --name-only HEAD~3..HEAD
git log --oneline -5
```

View files (`*View.swift`, `*ViewController.swift`) → verify those screens; Model/ViewModel changes → verify the data appears in the UI; navigation changes → verify navigation paths; style changes → include the `visual` lens.

**Every check needs concrete pass criteria** — the expected element/label/state and where it appears in the hierarchy (expected vs actual). A one-sentence "screen looks right" is not a criterion. Validate the plan against the journey rules above; keep 3–8 checks, split beyond 10.

Present the plan and wait for confirmation — **unless `--yes` is set or the run is parent-invoked**, in which case proceed immediately and note "plan auto-approved (--yes / parent-invoked)" in the report.

## Step 5: Execute Lenses

- **`functional` / `error-state`**: capture `mcp: xcodebuildmcp/snapshot_ui`, use its coordinates/labels as tap targets for gesture tools (only those confirmed by the Step 2.5 probe), and assert each step with a fresh `mcp: xcodebuildmcp/snapshot_ui` — element path, expected vs actual.
- **`accessibility`**: `mcp: xcodebuildmcp/snapshot_ui` — a11y labels/identifiers present on interactive elements.
- **`visual`**: `mcp: xcodebuildmcp/screenshot`, saved into the run dir. Baseline lookup: Read `$AW_DIR/design/screens.json` (path echoed from the Step 4 bash block); if it maps the screen to a baseline, call `mcp: design-comparison/compare_design` on baseline vs capture and record the numeric diff % in `pack.json.mockup_diff` — thresholds (CD11): **≤2% PASS, 2–10% WARN, >10% FAIL**. With `--baseline` and no covering baseline, the visual lens FAILs; without the flag, note "no baseline" and judge region-level only. Screenshot analysis may only claim region-level deviations — never token-level attribution.
- **`appearance`**: light + dark capture plus one Dynamic Type step-up, when the simulator-management tools for appearance are enabled; otherwise `SKIPPED — simulator management workflow not enabled` (verdict capped WARN).

Screenshots follow the evidence-pack `<check>-<viewport>.png` convention, with the iOS variant in the viewport slot: `{run-id}/{check}-{variant}.png` where `variant ∈ light | dark | dynamic-type | <device>`.

## Step 6: Write the Evidence Pack

Write both files into `$AW_DIR/verification/$RUN_ID/` following the schema in `_shared/evidence-pack.md` exactly:

- **`pack.json`** — `"schema": "evidence-pack/v1"`, `"skill": "verify-ios"`, `"platform": "ios"`, `base_url: null`, `plan` (journey-file path or null), `lenses[]` (every executed/skipped lens with per-check status + evidence), `journeys[]` (per-step status), `cross_checks[]`, `artifacts[]`, `mockup_diff`, `started_at`/`finished_at`.
- **`report.md`** — human-readable summary: app/scheme, simulator, mode, plan source, per-lens results table, issues found with screen/expected/actual/suggestion, artifact list.

**Verdict rollup:** any journey or lens FAIL ⇒ `FAIL`; a waived journey, a SKIPPED-with-reason lens (gestures/appearance unavailable), or probe-skipped interactions ⇒ at most `WARN`; otherwise `PASS`.

## Step 7: Release Simulator Lock

Always release, regardless of success or failure — a leaked lock blocks all future simulator sessions:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=ios-sim source "$SHARED_DIR/skill-lock.sh"; release_lock
```

## Verdict & Gate

**A FAIL verdict forbids downstream PR-open / mark-ready** (gate rule in `_shared/evidence-pack.md`). Consumers (verify-app, specToProvenPR, shipRelease, review) read the pack — do not soften the verdict in prose. End the report with exactly:

```
verdict: <PASS|WARN|FAIL>
evidence_path: <absolute path to verification/<run-id>/>
```

## Rules

- **Structural snapshots by default** — `mcp: xcodebuildmcp/snapshot_ui` is faster and catches structural issues; `mcp: xcodebuildmcp/screenshot` is for the visual/appearance lenses.
- **Never modify code** — read-only verification. Report issues, don't fix them.
- **Every run writes a pack** — a run without `pack.json` + `report.md` is not a verification run.
- **Confirm the plan in auto mode** unless `--yes` or parent-invoked.
- **Never call unprobed gesture tools** — the Step 2.5 probe gates every interaction.
- **Always release the simulator lock**, even on partial failure.

## Next steps

- `/design-verify-ios` — if visual gaps surfaced, run iOS design verification (pixel diff vs baseline)
- `/bugReport` — if functional gaps surfaced, capture them as a bug report
