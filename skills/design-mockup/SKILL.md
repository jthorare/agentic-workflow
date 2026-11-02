---
name: design-mockup
description: "Use when someone wants to see a screen before it's built: a visual draft to react to and approve, for a web or an iOS app. Picks the platform and delegates; the approved result becomes the baseline later checks compare against. Not for turning an approved design into code (design-implement)."
argument-hint: <screen-name>
allowed-tools: Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Glob, Read, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `$HOME/.agentic-workflow/toolkit/skills/_design-preamble.md` and follow it before continuing.

---

# Design Mockup — Platform Dispatcher

Detects whether this is a web or iOS project and delegates to the appropriate mockup skill. Contains no implementation logic.

> **Tip:** If you already know the platform, invoke directly: `/design-mockup-web <screen-name>` or `/design-mockup-ios <screen-name>`

## Step 1: Platform Detection

Resolve the shared dir from the stable toolkit path and follow the canonical detection + resolution rules:

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
echo "$SHARED_DIR/platform-detection.md"
```

Read the echoed `platform-detection.md` and apply its Detection globs (with excludes) and its Platform Resolution table, phrasing the both/neither **Ask the user** as "Which platform should I create a mockup for? (web / ios)".

## Step 2: Dispatch

Both sub-skills take `<screen-name>` — design-mockup-ios now **requires** it too (baselines are per-screen: `mockup-ios-<screen>.png`). Per the dispatch contract in `platform-detection.md`:

1. If `<screen-name>` is empty, **stop** and ask for it — never dispatch with a blank required argument.
2. Echo the dispatch line: `dispatch: design-mockup-web args=<args>` (or `design-mockup-ios`).
3. Dispatch literally, passing all user-supplied arguments through unchanged:
   - Web: **Invoke skill `design-mockup-web`** with args `"<original args verbatim>"`
   - iOS: **Invoke skill `design-mockup-ios`** with args `"<original args verbatim>"`

## Result block

Only when `AW_RESULT=1` is set; otherwise write no file and print no block.

1. `mkdir -p "$AW_DIR/results"` and write `$AW_DIR/results/design-mockup-<timestamp>.json` (timestamp from `date -u +%Y%m%dT%H%M%SZ`) with `skill` (`"design-mockup"`), `artifacts` (absolute paths of whatever the delegated skill wrote (mockup source, baselines, screens.json); empty if it stopped before writing), `summary` (one line), plus `dispatched_to` (`design-mockup-web` or `design-mockup-ios`), `screen`.
2. Print the same JSON as a fenced block tagged `aw-result`, before `## Next steps`.

## Next steps

- `/design-implement` — turn the approved mockup into production code
- `/design-refine` — iterate the mockup before implementation
