---
name: design-analyze
description: Use when someone wants a first design-tokens.json built from reference sites or from an existing app's code, in a web or an iOS project. Picks the platform and hands off to design-analyze-web or design-analyze-ios. Not for merging into tokens that already exist (design-evolve).
argument-hint: "[<url> [url2...] (web) | <path> (ios)]"
allowed-tools: Bash(git *), Bash(ls *), Bash(SHARED_DIR=*), Glob, Read, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

> **Note:** This skill creates design context — missing `design-tokens.json` is expected on first run.

---

# Design Analyze — Platform Dispatcher

Detects whether this is a web or iOS project and delegates to the appropriate token extraction skill. Contains no implementation logic.

> **Tip:** If you already know the platform, invoke directly:
> - Web: `/design-analyze-web <url> [url2...]`
> - iOS: `/design-analyze-ios [path/to/Assets.xcassets]`

## Platform Detection & Dispatch

Resolve the shared dir from the stable toolkit path, then follow the shared detection contract:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
echo "contract: $SHARED_DIR/platform-detection.md"
```

Read `$SHARED_DIR/platform-detection.md` and apply it exactly — the recursive iOS globs with excludes, the web `package.json` check, and the resolution table (both/neither present ⇒ **Ask the user**). Sub-skills for this dispatcher: iOS → `design-analyze-ios`, web → `design-analyze-web`.

## Dispatch Contract

1. Echo before dispatching: `dispatch: <sub-skill> args=<args>`
2. Dispatch literally, passing all user-supplied arguments through unchanged:
   - Web: **Invoke skill `design-analyze-web`** with args `"<original args verbatim>"`
   - iOS: **Invoke skill `design-analyze-ios`** with args `"<original args verbatim>"`
3. If a required argument is empty (web requires at least one URL), **stop** and ask — never dispatch with a blank required arg.

## Next steps

- `/design-language` — define brand personality from the extracted tokens

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-analyze-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`design-analyze`), `artifacts` (absolute paths: `design-tokens.json` and the raw captures under `$AW_DIR/design/raw/`), `summary` (one line),
plus `platform` (`web | ios`, the sub-skill it delegated to).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
