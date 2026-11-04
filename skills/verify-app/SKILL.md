---
name: verify-app
description: "Use when someone asks to verify, smoke-test or prove a UI change works and has not said whether it is web, iOS or desktop: it inspects the repo and hands off to the matching verify skill, arguments unchanged. Not for when the platform is already named, or for mockup comparison."
argument-hint: "[--journey <path>] [--lenses <csv>] [--baseline] [--yes] [--base-url <url>] [--screens <csv>] [--visual] [criteria or 'auto']"
allowed-tools: Bash(git *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(SHARED_DIR=*), Bash(source *), Glob, Read, Write, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

---

# Verify App — Platform Dispatcher

Detects whether this is a web, iOS or desktop project and delegates to the appropriate verification skill. Contains no verification logic — all execution lives in the sub-skills.

> **Tip:** If you already know the platform, invoke directly: `/verify-web`, `/verify-ios` or `/verify-desktop`

Every run ends with the [Done checklist](#done-checklist), whatever the outcome: done, stopped, failed, refused or a question back to the person. A stopped run is a finished run, not a reason to skip the result file or the `aw-result` block. Run the [Result block](#result-block)'s gate now, in the same Bash call as the shared reference and ahead of its `ls`, so you know `AW_RESULT` even when that `ls` fails. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Platform Detection & Dispatch

Follow `_shared/platform-detection.md` — canonical detection globs (with vendored-path excludes), the resolution table, and the dispatch contract. Resolve it from the stable toolkit path:

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
ls "$SHARED_DIR/platform-detection.md"
```

If resolution or `ls` fails, stop and report through the [Done checklist](#done-checklist) (verdict FAIL, no dispatch) — do not guess the platform. Otherwise read the file and dispatch in three steps:

1. Pick the sub-skill: iOS → `verify-ios`, web → `verify-web`, desktop → `verify-desktop`, several/none → **Ask the user**. When the request already names the platform ("the iOS app", "the web app", "the desktop app"), that is the answer: take it without asking, even when detection finds none.
2. Write `dispatch: <sub-skill> args=<args>` as its own line of reply text, in the same message as the Skill call. Every dispatch has it. A Bash `echo` does not count: the person reads your reply, not tool output.
3. Dispatch literally: **Invoke skill `<sub-skill>`** with args `"<original args verbatim>"`.

## Argument Spec (pass-through)

All arguments are forwarded to the sub-skill **unchanged** — never drop or rewrite one:

| Argument | Meaning |
|----------|---------|
| `--journey <path>` | Path to a `verification-plan.md` (template: `_shared/verification-plan-template.md`) — the sub-skill executes its journeys |
| `--lenses <csv>` | Narrow the lens set (catalog: `_shared/verification-lenses.md`) |
| `--baseline` | Require visual diffing against `screens.json` baselines — the visual lens fails if none exist |
| `--yes` | Non-interactive: the sub-skill auto-approves its plan |
| `--base-url <url>` | Override app-URL detection (web only; still forwarded verbatim) |
| `--screens <csv>` | Desktop only: screen names from the repo's screens manifest to verify (ignored with a note on web and iOS) |
| `--visual` | Include the visual lens |
| `auto` / criteria | Diff-inference mode / explicit verification criteria |

If `--journey` is given but the path does not exist, **stop** and report through the [Done checklist](#done-checklist) (verdict FAIL) — do not dispatch with a blank or dead required arg.

## Sub-skill Return Contract

The sub-skill must produce an evidence pack (`_shared/evidence-pack.md`) and report back `{verdict, evidence_path}`:

- `verdict` ∈ `PASS | WARN | FAIL`
- `evidence_path` = absolute path to the `verification/<run-id>/` dir containing `pack.json`

After the sub-skill returns, confirm the pack exists:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
ls -1dt "$AW_DIR/verification/"*/ | head -1
ls "$(ls -1dt "$AW_DIR/verification/"*/ | head -1)pack.json"
```

If the sub-skill returned no `{verdict, evidence_path}`, or the newest run dir has no `pack.json`, **fail loudly**:

> "verify sub-skill returned no evidence pack — verification is not proven. Treat this run as FAIL."

Relay the sub-skill's `verdict` and `evidence_path` verbatim in your final report. A FAIL verdict blocks PR-open / mark-ready (gate rule in `_shared/evidence-pack.md`).

## Auto-chain: design-verify

After a completed run, check whether `screens.json` baselines cover the screens the sub-skill verified:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
ls "$AW_DIR/design/screens.json" 2>/dev/null || echo "no screens.json — skip design-verify chain"
```

If `screens.json` exists and has `baselines` entries for one or more of the verified screens, invoke **Invoke skill `design-verify`** with args `"<verified screens>"` automatically and report its diff verdict alongside the verification verdict. If no baselines cover the verified screens, skip the chain silently.

## Next steps

- `/design-verify` — re-run design diffing on demand (auto-chained above when baselines cover verified screens)
- `/bugReport` — if functional gaps surfaced, capture them as a bug report

## Done checklist

Do every item before you send the reply; a run that skips one is not finished, even when the verdict is FAIL or the run stopped before dispatch:

1. Dispatched runs: the `dispatch: <sub-skill> args=<args>` line was written as reply text in the Skill call's message, and the sub-skill's pack was confirmed (or the run is FAIL with the "no evidence pack" line).
2. Under `AW_RESULT=1`, the result file exists at `$AW_DIR/results/verify-app-<timestamp>.json` (Result block, Write tool), with the sub-skill's verdict relayed verbatim, or FAIL for a run that stopped before dispatch or got no pack (`dispatched`, `platform`, `pack` and `evidence_path` `""` when there is none).
3. Under `AW_RESULT=1`, your final reply, sent after the last tool call (every Write done first), opens with the `aw-result` block, before the prose and `## Next steps`. A block printed before a later tool call does not count: callers read only the final reply. A sub-skill's own `aw-result` block does not stand in for yours.

If the Write tool itself refuses a path, say so in the reply and still print the block, with the paths you could not write left as `""`.

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/verify-app-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`verify-app`), `artifacts` (absolute paths: the evidence pack directory `$AW_DIR/verification/<run-id>/` (`pack.json`, `report.md`)), `summary` (one line),
plus `evidence_path` (absolute path of the pack directory); `pack` (absolute path of the sub-skill's `pack.json`); `verdict` (`PASS | WARN | FAIL`, relayed verbatim from the sub-skill's pack); `platform` (`web | ios | desktop`, the sub-skill it delegated to); `dispatched` (the sub-skill's name).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
