---
name: verify-desktop
description: "Use when a desktop app's views, styles or resources changed and you need a verdict on whether the real screens still match their design mockups before shipping. Runs the app on seeded data, pixel-compares each affected screen, and writes a PASS, WARN or FAIL evidence pack. Not for web or iOS apps (verify-app routes those) or for mismatches with fixes mid-design (design-verify-desktop)."
argument-hint: "[--journey <plan.md>] [--lenses <csv>] [--visual] [--baseline] [--yes] [--screens <csv>] [criteria or 'auto']"
allowed-tools: Bash(git *), Bash(SHARED_DIR=*), Bash(source *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(cat *), Bash(cp *), Bash(ui-evidence *), Bash(command *), Read, Write, Glob, Grep, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

---

# Verify Desktop — Real App, Seeded Data, Mockups

Verifies a desktop UI change by running the real app on seeded data and comparing every affected screen with its mockup. The UI is expected to **match the mockups**: this skill finds out whether it does. Every run writes an evidence pack (`pack.json` + `report.md`) per `_shared/evidence-pack.md`.

Every run ends with the [Done checklist](#done-checklist), whatever the verdict: PASS, WARN, FAIL or a stop. A failed run is a finished run, not a reason to skip the result file or the `aw-result` block. Run the [Result block](#result-block)'s gate now, with the shared references, so you know `AW_RESULT` before the first verb. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

Shared references resolve from the stable toolkit path (never through a provider's skills dir):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
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
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
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

Artifacts: $AW_DIR/verification/<run-id>/
```

## Next steps

- **PASS** — `/shipRelease` — the screens match their mockups, ship the release
- **WARN** — `/design-verify` — cover the missing screens, then verify again
- **FAIL** — `/design-refine` or `/design-implement` — fix the mismatches the pack lists, then `/verify-desktop` again; do **not** ship until it passes

## Done checklist

Do every item before you send the reply; a run that skips one is not finished, even when the verdict is FAIL:

1. The pack exists: `pack.json` and `report.md` under `$AW_DIR/verification/<run-id>/` (Write tool).
2. Under `AW_RESULT=1`, the result file exists at `$AW_DIR/results/verify-desktop-<timestamp>.json` (Result block, Write tool), with the pack's verdict.
3. Under `AW_RESULT=1`, your final reply, sent after the last tool call (every Write done first), opens with the `aw-result` block, before the prose and `## Next steps`. A block printed before a later tool call does not count: callers read only the final reply.

If the Write tool itself refuses a path, say so in the reply and still print the block, with the paths you could not write left as `""`.

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/verify-desktop-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`verify-desktop`), `artifacts` (absolute paths: the evidence pack directory `$AW_DIR/verification/<run-id>/` (`pack.json`, `report.md`)), `summary` (one line),
plus `evidence_path` (absolute path of the pack directory); `pack` (absolute path of `pack.json`); `verdict` (`PASS | WARN | FAIL`, the pack's verdict).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
