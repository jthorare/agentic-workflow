---
name: design-evolve
description: Use mid-project, when design-tokens.json already exists and the user wants to bring in changes from a new reference site or an updated Swift theme, keeping the rest. Picks web or iOS and hands off to design-evolve-web or design-evolve-ios. Not for a first extraction (design-analyze).
argument-hint: "[<url> (web) | <path/to/Theme.swift or dir> (ios)]"
allowed-tools: Bash(git *), Bash(ls *), Bash(SHARED_DIR=*), Glob, Read, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

---

# Design Evolve — Platform Dispatcher

Detects whether this is a web or iOS project and delegates to the appropriate design evolution skill. Contains no implementation logic.

> **Tip:** If you already know the platform, invoke directly:
> - Web: `/design-evolve-web <url>`
> - iOS: `/design-evolve-ios <path/to/Theme.swift>`

## Step 1: Argument-Shape Routing

The argument shape usually decides the platform — validate it before any project detection:

- Argument starts with `http://` or `https://` → **web**. Dispatch to `design-evolve-web`.
- Argument is an existing filesystem path (a `.swift` file, or a directory containing Swift files / an Xcode project) — verify by globbing/reading it → **iOS**. Dispatch to `design-evolve-ios`.
- No argument, or the argument matches neither shape (e.g. path does not exist) → fall through to Step 2. Ask only when detection is ambiguous — never silently guess a platform from a malformed argument.

## Step 2: Platform Detection (only when the argument shape didn't decide)

Resolve the shared dir from the stable toolkit path, then follow the shared detection contract:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
echo "contract: $SHARED_DIR/platform-detection.md"
```

Read `$SHARED_DIR/platform-detection.md` and apply it exactly — the recursive iOS globs (`**/Package.swift` etc.) with excludes, the web `package.json` check, and the resolution table (both/neither present ⇒ **Ask the user**). Sub-skills for this dispatcher: iOS → `design-evolve-ios`, web → `design-evolve-web`.

## Dispatch Contract

1. Echo before dispatching: `dispatch: <sub-skill> args=<args>`
2. Dispatch literally, passing all user-supplied arguments through unchanged:
   - Web: **Invoke skill `design-evolve-web`** with args `"<original args verbatim>"`
   - iOS: **Invoke skill `design-evolve-ios`** with args `"<original args verbatim>"`
3. `design-evolve-web` requires a URL — if web was chosen and no URL is present, **stop** and ask. `design-evolve-ios` prompts for a path itself when the argument is absent.

## Result block

First run this Bash line and go by what it prints (the eval stage turns it on in its staged copy; a person's run prints 0):

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

On `AW_RESULT=1`, write the result file with the Write tool (it creates the folder, so no `mkdir` first). On `AW_RESULT=0`, write no file and print no block.

Follow `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`. Only when `AW_RESULT=1`: write `$AW_DIR/results/design-evolve-<timestamp>.json` and print the same JSON as a fenced `aw-result` block, placed before `## Next steps`. Outside `AW_RESULT=1`, write no file and print no block.

Keys: `skill` (`"design-evolve"`), `artifacts` (absolute paths: the member skill's `design-tokens.json` and `.impeccable.md`), `summary` (one line), plus `dispatched_to` (`design-evolve-web` or `design-evolve-ios`).

## Next steps

- `/design-mockup` — build a mockup with the updated tokens
- `/design-refine` — apply the evolved language to existing screens
