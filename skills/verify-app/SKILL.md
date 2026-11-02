---
name: verify-app
description: "Use when someone asks to verify, smoke-test or prove a UI change works and has not said whether it is web, iOS or desktop: it inspects the repo and hands off to the matching verify skill, arguments unchanged. Not for when the platform is already named, or for mockup comparison."
argument-hint: "[--journey <path>] [--lenses <csv>] [--baseline] [--yes] [--base-url <url>] [--screens <csv>] [--visual] [criteria or 'auto']"
allowed-tools: Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Bash(source *), Glob, Read, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

---

# Verify App — Platform Dispatcher

Detects whether this is a web, iOS or desktop project and delegates to the appropriate verification skill. Contains no verification logic — all execution lives in the sub-skills.

> **Tip:** If you already know the platform, invoke directly: `/verify-web`, `/verify-ios` or `/verify-desktop`

## Platform Detection & Dispatch

Follow `_shared/platform-detection.md` — canonical detection globs (with vendored-path excludes), the resolution table, and the dispatch contract. Resolve it from the stable toolkit path:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
ls "$SHARED_DIR/platform-detection.md"
```

If resolution or `ls` fails, stop and report — do not guess the platform. Otherwise read the file and apply it: iOS → `verify-ios`, web → `verify-web`, desktop → `verify-desktop`, several/none → **Ask the user**. Echo `dispatch: <sub-skill> args=<args>` before dispatching, then dispatch literally: **Invoke skill `<sub-skill>`** with args `"<original args verbatim>"`.

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

If `--journey` is given but the path does not exist, **stop** and report — do not dispatch with a blank or dead required arg.

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

## Result block

First run this Bash line and go by what it prints (the eval stage turns it on in its staged copy; a person's run prints 0):

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

On `AW_RESULT=1`, write the result file with the Write tool (it creates the folder, so no `mkdir` first). On `AW_RESULT=0`, write no file and print no block.

Per `_shared/output-contract.md`. Only when `AW_RESULT=1` is set: write `$AW_DIR/results/verify-app-<timestamp>.json` and print the same object as a fenced `aw-result` block after the report, before `## Next steps`. Keys: `skill` (`"verify-app"`), `artifacts` (absolute paths, the sub-skill's `pack.json` and `report.md`), `summary` (one line), `pack` (absolute path of the sub-skill's `pack.json`), `verdict` (the sub-skill's, relayed verbatim), `dispatched` (the sub-skill's name). Without `AW_RESULT=1`, write no file and print no block.

## Next steps

- `/design-verify` — re-run design diffing on demand (auto-chained above when baselines cover verified screens)
- `/bugReport` — if functional gaps surfaced, capture them as a bug report
