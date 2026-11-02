---
name: verify-desktop
description: "Use when a desktop app's views, styles or resources changed and you need a verdict on whether the real screens still match their design mockups before shipping. Runs the app on seeded data, pixel-compares each affected screen, and writes a PASS, WARN or FAIL evidence pack. Not for web or iOS apps (verify-app routes those) or for fix suggestions while designing (design-verify)."
argument-hint: "[--journey <plan.md>] [--lenses <csv>] [--visual] [--baseline] [--yes] [--screens <csv>] [criteria or 'auto']"
allowed-tools: Bash(git *), Bash(SHARED_DIR=*), Bash(source *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(cat *), Bash(cp *), Bash(ui-evidence *), Bash(command *), Read, Write, Glob, Grep, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

---

# Verify Desktop — Real App, Seeded Data, Mockups

Verifies a desktop UI change by running the real app on seeded data and comparing every affected screen with its mockup. The UI is expected to **match the mockups**: this skill finds out whether it does. Every run writes an evidence pack (`pack.json` + `report.md`) per `_shared/evidence-pack.md`.

Shared references resolve from the stable toolkit path (never through a provider's skills dir):

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
ls "$SHARED_DIR/desktop-evidence.md" "$SHARED_DIR/evidence-pack.md" "$SHARED_DIR/verification-lenses.md"
```

If resolution or `ls` fails, stop and report. Read all three before planning. `desktop-evidence.md` holds the config, the two ways to run it, the pack and the judging rules; this skill does not repeat them.

## Step 1: Parse Arguments

- **`--screens <csv>`** — screen names from the repo's screens manifest. Default: inferred (Step 2).
- **`--journey <path>`** — a `verification-plan.md`. The desktop run drives the manifest's per-screen scripts, so a plan's journeys are mapped onto manifest screens: a journey that reaches a screen the manifest lacks is reported as **not covered** (WARN, with the screen named) — never improvised with ad-hoc driving.
- **`--lenses`, `--visual`, `--baseline`** — accepted and forwarded for parity with the other platforms. The desktop run always includes the visual lens against the mockups, so `--baseline` is satisfied by construction; the other lenses (accessibility, error-state) are recorded `SKIPPED — desktop evidence checks layout against mockups only` unless the plan's journeys cover them through manifest screens.
- **`--yes`** — non-interactive; set automatically when a parent skill invokes this one.
- **Criteria / `auto` / none** — infer from the diff.
- Web-only flags (e.g. `--base-url`) forwarded by a dispatcher are ignored with a one-line note.

## Step 2: Resolve the Config and the Screens

Follow "Config (read first)" in `desktop-evidence.md`. Then pick the screens:

```bash
git diff --name-only HEAD~3..HEAD
git log --oneline -5
```

View (`*.axaml` and its code-behind), view-model, style and resource changes → the manifest screens that show those views. Map by name and by the manifest's own screen scripts; when the mapping is ambiguous, include the screen (a missed screen is a worse error than an extra one).

## Step 3: Create the Run Directory and Run

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
RUN_ID="$(date -u +%Y%m%d-%H%M%S)-<slug>"   # <slug> = 2–4 word kebab summary of what is being verified
mkdir -p "$AW_DIR/verification/$RUN_ID"
echo "run dir: $AW_DIR/verification/$RUN_ID"
```

Run "Running it" in `desktop-evidence.md` with that directory as `<run-dir>`: the workflow engine when present, else `desktop-run`. When the engine ran it, copy its summary and images into the run directory so the pack is in one place. Do not retry a failed run to get a pass; a `broken` run is repaired by the engine's recipe (at most twice) or reported.

## Step 4: Judge and Write the Evidence Pack

Judge per "Judging it" in `desktop-evidence.md`: every screen against its mockup, any unexpected mismatch fails.

Write `pack.json` (`platform: "desktop"`, `skill: "verify-desktop"`) and `report.md` into the run directory:

- `lenses[]`: `visual` with one check per screen and size (`desc`: the screen and what was compared; `status`; `evidence`: the `side-by-side.png` path), and any skipped lens with `reason_if_skipped`.
- `mockup_diff`: the worst screen (`screen`, `viewport` = its size, `pct` = its `diffPercent`, `verdict`).
- `artifacts`: the run's `summary.json`, `report.md` and each screen's images, relative to the run directory.
- `cross_checks`: the command that ran (the engine's `workflow start` or the `desktop-run` line) and its exit code in a raw output file.
- `verdict`: **FAIL** if any screen failed, mismatched or the run was refused or errored; **WARN** if a `--journey` screen is not covered or a requested lens was skipped and everything run passed; otherwise **PASS**.

## Rules

- Real app, seeded data, mockups from the manifest: never a stub window, a hand-made capture or a self-made baseline
- Do not modify code, config, manifest or mockups — verification is read-only
- A missing config, manifest or screen is a stop with the reason, not a skip
- Fix suggestions are region-level (`desktop-evidence.md`)

## Return

Report `{verdict, evidence_path}` where `evidence_path` is the absolute path of the run directory containing `pack.json`.

```
[<PASS|WARN|FAIL>] Desktop Verification — <screen(s)>
=====================================================
| screen | size | diff % | verdict | mismatches |
|--------|------|--------|---------|------------|

Artifacts: ~/.agentic-workflow/<repo-slug>/verification/<run-id>/
```

## Result block

Only under `AW_RESULT=1`. Without it, write no file and print no block.

When `AW_RESULT=1` is set, after the pack is written, write `$AW_DIR/results/verify-desktop-<UTC timestamp, e.g. 20261101T101500Z>.json` (`mkdir -p "$AW_DIR/results"`) holding one JSON object:

- `skill`: `"verify-desktop"`
- `artifacts`: absolute paths of `pack.json`, `report.md`, the run's `summary.json` and each screen's `side-by-side.png`
- `summary`: one line (the verdict and the worst screen with its `diffPercent`)
- `pack`: absolute path of `pack.json`
- `verdict`: `PASS` | `WARN` | `FAIL`, the pack's verdict

Then print the same object in the reply as a fenced block tagged `aw-result` (three backticks, `aw-result`, the JSON, three backticks), after the report table and before `## Next steps`.

## Next steps

- **PASS** — `/shipRelease` — the screens match their mockups, ship the release
- **WARN** — `/design-verify` — cover the missing screens, then verify again
- **FAIL** — `/design-refine` or `/design-implement` — fix the mismatches the pack lists, then `/verify-desktop` again; do **not** ship until it passes
