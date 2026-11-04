---
name: design-mockup
description: "Use when someone wants to see a screen before it's built: a visual draft to react to and approve, for a web or an iOS app. Picks the platform and delegates; the approved result becomes the baseline later checks compare against. Not for turning an approved design into code (design-implement)."
argument-hint: <screen-name>
allowed-tools: Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Glob, Read, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

---

# Design Mockup — Platform Dispatcher

Detects whether this is a web or iOS project and delegates to the appropriate mockup skill. Contains no implementation logic.

> **Tip:** If you already know the platform, invoke directly: `/design-mockup-web <screen-name>` or `/design-mockup-ios <screen-name>`

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Platform Detection

Resolve the shared dir from the stable toolkit path and follow the canonical detection + resolution rules:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
echo "$SHARED_DIR/platform-detection.md"
```

Read the echoed `platform-detection.md` and apply its Detection globs (with excludes) and its Platform Resolution table, phrasing the both/neither **Ask the user** as "Which platform should I create a mockup for? (web / ios)".

## Step 2: Dispatch

Both sub-skills take `<screen-name>` — design-mockup-ios now **requires** it too (baselines are per-screen: `mockup-ios-<screen>.png`). Per the dispatch contract in `platform-detection.md`:

1. If `<screen-name>` is empty, **stop** and ask for it — never dispatch with a blank required argument.
2. Write `dispatch: design-mockup-web args=<args>` (or `design-mockup-ios`) as its own line of reply text, in the same message as the Skill call. A Bash `echo` does not count: the person reads your reply, not tool output.
3. Dispatch literally, passing all user-supplied arguments through unchanged:
   - Web: **Invoke skill `design-mockup-web`** with args `"<original args verbatim>"`
   - iOS: **Invoke skill `design-mockup-ios`** with args `"<original args verbatim>"`

## Next steps

- `/design-implement` — turn the approved mockup into production code
- `/design-refine` — iterate the mockup before implementation

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-mockup-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`design-mockup`), `artifacts` (absolute paths: the mockup files and the updated `$AW_DIR/design/screens.json`), `summary` (one line),
plus `platform` (`web | ios`, the sub-skill it delegated to); `screen` (screen name).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
