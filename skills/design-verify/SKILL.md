---
name: design-verify
description: "Use when a built screen should be checked against its approved mockup and the platform is unspecified or mixed (web, iPhone or desktop project). Detects the platform from project files and hands off to the matching design-verify member. Not for testing journeys, console errors or accessibility of a running app (verify-app)."
argument-hint: [screen-name]
allowed-tools: Bash(git *), Bash(ls *), Bash(SHARED_DIR=*), Bash(source *), Glob, Read, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

---

# Design Verify — Platform Dispatcher

Detects whether this is a web, iOS or desktop project and delegates to the appropriate screenshot verification skill. Contains no implementation logic.

> **Tip:** If you already know the platform, invoke directly: `/design-verify-web`, `/design-verify-ios` or `/design-verify-desktop`

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Platform Detection & Dispatch

Resolve the shared dir from the stable toolkit path, then follow the shared detection contract:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
echo "$SHARED_DIR"
```

Read `$SHARED_DIR/platform-detection.md` and apply its Detection rules, Platform Resolution table, and Dispatch contract. Dispatch literally. Write the `dispatch:` line as its own line of reply text, in the same message as the Skill call (a Bash `echo` does not count: the person reads your reply, not tool output):

```
dispatch: design-verify-web args=<original args verbatim>
Invoke skill design-verify-web with args "<original args verbatim>"
```

or the same shape with `design-verify-ios` or `design-verify-desktop`.

All user-supplied arguments (e.g., `[screen-name]`) are passed through unchanged. If a required argument is empty, stop and ask — never dispatch blank.

## Step 2: Read the Sub-Skill's Verdict

After the sub-skill returns, locate its run report and normalize the outcome — never assume the diff was clean:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
RUN_DIR=$(ls -1dt "$AW_DIR/design/verify/"*/ 2>/dev/null | head -1)
echo "run-dir: ${RUN_DIR:-none}"
```

Read `$RUN_DIR/comparison-report.json` (schema in `$SHARED_DIR/design-artifact-paths.md`) and extract `overall.verdict` and `overall.max_diff_pct`. Print:

```
design-verify: <PASS|WARN|FAIL> (max diff <N>%)
```

If no run dir or report exists, report that the sub-skill produced no comparison report and treat the run as FAIL for gating purposes.

## Next steps

Gate the suggestions on `overall.verdict` — suggest `/shipRelease` **only on PASS**:

- **PASS** — `/shipRelease` — diff is clean, ship the release
- **WARN** — `/design-refine` — address the minor discrepancies, then `/design-verify` again
- **FAIL** — `/design-refine` or `/design-implement-*` — significant deviation; do **not** ship until re-verified

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-verify-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`design-verify`), `artifacts` (absolute paths: `$AW_DIR/design/verify/<run-id>/comparison-report.json` and its diff images), `summary` (one line),
plus `run_id` (verify run id); `max_diff_pct` (`overall.max_diff_pct`); `verdict` (`PASS | WARN | FAIL`, `overall.verdict` of the comparison report).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
