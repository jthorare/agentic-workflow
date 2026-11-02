---
name: design-verify
description: "Use when a built screen should be checked against its approved mockup and the platform is unspecified or mixed (web, iPhone or desktop project). Detects the platform from project files and hands off to the matching design-verify member. Not for testing journeys, console errors or accessibility of a running app (verify-app)."
argument-hint: [screen-name]
allowed-tools: Bash(git *), Bash(ls *), Bash(SHARED_DIR=*), Bash(source *), Glob, Read, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `$HOME/.agentic-workflow/toolkit/skills/_design-preamble.md` and follow it before continuing.

---

# Design Verify — Platform Dispatcher

Detects whether this is a web, iOS or desktop project and delegates to the appropriate screenshot verification skill. Contains no implementation logic.

> **Tip:** If you already know the platform, invoke directly: `/design-verify-web`, `/design-verify-ios` or `/design-verify-desktop`

## Step 1: Platform Detection & Dispatch

Resolve the shared dir from the stable toolkit path, then follow the shared detection contract:

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
echo "$SHARED_DIR"
```

Read `$SHARED_DIR/platform-detection.md` and apply its Detection rules, Platform Resolution table, and Dispatch contract. Dispatch literally, echoing first:

```
dispatch: design-verify-web args=<original args verbatim>
Invoke skill design-verify-web with args "<original args verbatim>"
```

or the same shape with `design-verify-ios` or `design-verify-desktop`.

All user-supplied arguments (e.g., `[screen-name]`) are passed through unchanged. If a required argument is empty, stop and ask — never dispatch blank.

## Step 2: Read the Sub-Skill's Verdict

After the sub-skill returns, locate its run report and normalize the outcome — never assume the diff was clean:

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
RUN_DIR=$(ls -1dt "$AW_DIR/design/verify/"*/ 2>/dev/null | head -1)
echo "run-dir: ${RUN_DIR:-none}"
```

Read `$RUN_DIR/comparison-report.json` (schema in `$SHARED_DIR/design-artifact-paths.md`) and extract `overall.verdict` and `overall.max_diff_pct`. Print:

```
design-verify: <PASS|WARN|FAIL> (max diff <N>%)
```

If no run dir or report exists, report that the sub-skill produced no comparison report and treat the run as FAIL for gating purposes.

## Result block

First run this Bash line and go by what it prints (the eval stage turns it on in its staged copy; a person's run prints 0):

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

On `AW_RESULT=1`, write the result file with the Write tool (it creates the folder, so no `mkdir` first). On `AW_RESULT=0`, write no file and print no block.

Under `AW_RESULT=1` only: write `$AW_DIR/results/design-verify-<timestamp>.json` and print the same JSON as a fenced `aw-result` block, placed before `## Next steps`. Without `AW_RESULT=1` write no file and print no block.

Keys: `skill` (`design-verify`), `artifacts` (absolute paths: the run's `comparison-report.json`), `summary` (the one-line `design-verify: <verdict>` print), `pack` (absolute path of the run directory under `$AW_DIR/design/verify/`, or `null` when none exists), `verdict` (`PASS|WARN|FAIL`, FAIL when no report exists), `platform` (`web|ios|desktop`, the member dispatched).

## Next steps

Gate the suggestions on `overall.verdict` — suggest `/shipRelease` **only on PASS**:

- **PASS** — `/shipRelease` — diff is clean, ship the release
- **WARN** — `/design-refine` — address the minor discrepancies, then `/design-verify` again
- **FAIL** — `/design-refine` or `/design-implement-*` — significant deviation; do **not** ship until re-verified
