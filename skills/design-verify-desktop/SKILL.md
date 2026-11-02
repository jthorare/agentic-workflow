---
name: design-verify-desktop
description: Run the real desktop app on seeded data through the desktop evidence recipe and diff each screen against its mockup. Reports every mismatch with fix suggestions.
argument-hint: [screen-name]
allowed-tools: Bash(git *), Bash(SHARED_DIR=*), Bash(source *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(cat *), Bash(cp *), Bash(ui-evidence *), Bash(command *), Read, Write, Glob, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

---

# Design Verify Desktop — Real-App Screen Diff vs Mockup

Runs the real desktop app on seeded data, drives each screen into view and diffs it with the owner's mockup. The mockups live in the repo's screens manifest, not in `screens.json`: the config, the run and the judging rules are in `_shared/desktop-evidence.md`. Artifact paths and the `comparison-report.json` schema come from `_shared/design-artifact-paths.md`.

## Step 1: Load the Config and Pick Screens

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
ls "$SHARED_DIR/desktop-evidence.md" "$SHARED_DIR/design-artifact-paths.md"
```

If `ls` fails, stop and report. Read both. Follow "Config (read first)" in `desktop-evidence.md`.

- **Screens to verify:** the `[screen-name]` argument if given, else every manifest screen the diff touches (infer as in `desktop-evidence.md`), else all.
- A requested screen with no manifest entry or no mockup file stops the run:
  > "No desktop mockup for `<screen>` in the screens manifest. Add the screen to the manifest (and its mockup) first."

## Step 2: Create the Run Directory

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
RUN_ID="$(date -u +%Y%m%d-%H%M%S)-<screen-or-all>"
mkdir -p "$AW_DIR/design/verify/$RUN_ID"
echo "run-dir: $AW_DIR/design/verify/$RUN_ID"
```

Previous runs are never overwritten.

## Step 3: Run and Judge

Run "Running it" in `desktop-evidence.md` with this run directory (the workflow engine when present, else `desktop-run`), then judge per "Judging it". When the engine ran it, copy its summary and images into the run directory.

## Step 4: Write comparison-report.json (Run Manifest)

Write `$AW_DIR/design/verify/$RUN_ID/comparison-report.json` per the schema in `$SHARED_DIR/design-artifact-paths.md`, with the window size in the `viewport` field:

```json
{ "schema": "comparison-report/v1", "run_id": "<run-id>",
  "screens": [ { "screen": "...", "viewport": "1440x900", "baseline": "<mockup path>",
                 "capture": "<screen>-<WxH>/app.png", "diff_image": "<screen>-<WxH>/diff.png", "diff_pct": 0.0, "verdict": "PASS|FAIL" } ],
  "overall": { "max_diff_pct": 0.0, "verdict": "PASS|FAIL" } }
```

A desktop screen is **PASS** or **FAIL**, never WARN: it passes only when the run's own limit holds and the structural review found no unaccepted mismatch. A screen that errored or was refused gets `diff_pct: 100`, `verdict: "FAIL"`, and the reason in place of the diff image path. `overall.verdict` = worst screen verdict; `overall.max_diff_pct` = max `diff_pct`. This file is the run's gate manifest — the dispatcher and ship chain read it.

## Step 5: Report Results

```
[<PASS|FAIL>] Desktop Design Verification — <screen(s)>
=======================================================
Overall:  <verdict>  (max diff <N>%)

| screen | size | diff % | verdict | mismatches |
|--------|------|--------|---------|------------|

Artifacts: $AW_DIR/design/verify/<run-id>/
```

For each FAIL, describe the mismatching regions (region-level only, `desktop-evidence.md`), point at the `side-by-side.png`, and suggest `/design-refine` or `/design-implement`, then `/design-verify-desktop` again.

## Rules

- Real app, seeded data, the manifest's mockups — never a hand-made capture or a self-made baseline
- Do not modify any code, config, manifest or mockup — this skill is read-only verification
- A requested screen the manifest lacks is a stop, not a skip

## Next steps

Gate on `overall.verdict` — a FAIL run **blocks** the `/shipRelease` suggestion:

- **PASS** — `/shipRelease` — the screens match their mockups, ship the release
- **FAIL** — `/design-refine` or `/design-implement` — fix the listed mismatches; re-verify before any ship step
