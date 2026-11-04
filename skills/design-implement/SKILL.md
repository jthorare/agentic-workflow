---
name: design-implement
description: "Use when an approved screen design should become real code, web or iOS: the user says the mockup is signed off, or that it's time to build the screen. Picks the platform, checks the mockup was approved, delegates, then runs design verification. Not for drafting a new design (design-mockup)."
argument-hint: "<screen-name> [--no-mockup]"
allowed-tools: Bash(git *), Bash(ls *), Bash(SHARED_DIR=*), Bash(source *), Glob, Read, AskUserQuestion, Skill, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

---

# Design Implement — Platform Dispatcher

Enforces the mockup-first gate, detects whether this is a web or iOS project, delegates to the appropriate code-generation skill, then auto-chains verification. Contains no implementation logic.

> **Tip:** If you already know the platform, invoke directly: `/design-implement-web` or `/design-implement-ios`

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Mockup-First Gate

Implementing without an approved mockup baseline makes `/design-verify` impossible — never skip this step.

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
echo "screens.json: $AW_DIR/design/screens.json"
ls "$AW_DIR/design/screens.json" 2>/dev/null || echo "screens.json: MISSING"
```

Read `screens.json` (CD4 schema in `$SHARED_DIR/design-artifact-paths.md`). The gate **passes** for the target screen only if its entry has a non-null `approved_at` **and** at least one file listed under `baselines` exists on disk (`ls "$AW_DIR/design/<baseline>"`).

- Gate passes → Step 2.
- `screens.json` missing, screen absent, `approved_at` null, or no baseline file on disk → **STOP** with: "No approved mockup baseline for `<screen>` — run `/design-mockup <screen>` first." Do not dispatch.
- `--no-mockup` passed → skip the gate, warn "Proceeding without a mockup baseline — /design-verify will have nothing to diff against", and strip `--no-mockup` from the forwarded arguments.

## Step 2: Platform Detection & Dispatch

Follow `$SHARED_DIR/platform-detection.md` exactly (desktop is detected there too, but has no implement sub-skill: for a desktop project implement the screen directly against its mockup, keep it read-only for the config, then go to Step 3): recursive iOS globs (`**/Package.swift`, `**/*.xcodeproj`, `**/*.xcworkspace`, ignoring `node_modules/`, `.build/`, `Pods/`, `vendor/`, `external-skills/`); web = `package.json` deps include `next`/`react`/`vite`/`vue`/`@angular/core`; both/neither → **Ask the user** per the resolution table.

Dispatch contract (from the shared file):

1. Write `dispatch: <sub-skill> args=<args>` as its own line of reply text, in the same message as the Skill call. A Bash `echo` does not count: the person reads your reply, not tool output.
2. Dispatch literally, passing arguments through unchanged (minus a stripped `--no-mockup`):
   - **Invoke skill `design-implement-web`** with args `"<original args verbatim>"`
   - **Invoke skill `design-implement-ios`** with args `"<original args verbatim>"`
3. If the screen-name argument is empty and the gate needs one, **stop** and ask — never dispatch blank.

## Step 3: Auto-Chain Verification

When the sub-skill completes without printing `[BLOCKED]`:

- If its report already includes a `verify_run_id` (the sub-skill ran its own verify gate), do not re-run — surface that verdict.
- Otherwise run **Invoke skill `design-verify`** with args `"<screen-name>"` now. Verification is not a suggestion — this dispatcher is done only when a verify run reports PASS or WARN (CD11 thresholds in `$SHARED_DIR/design-artifact-paths.md`). For a desktop UI change `design-verify` dispatches to `design-verify-desktop`, which runs the real app on seeded data against the mockups; the implementation is done only when it reports PASS (any unexpected mismatch fails).

## Next steps

- `/design-refine` — polish the implementation (verification re-runs after refinement)
- `/shipRelease` — ship once verification passes

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f"; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/design-implement-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`design-implement`), `artifacts` (absolute paths: the generated token and component files and `$AW_DIR/design/implement-report.json` when the sub-skill wrote one), `summary` (one line),
plus `platform` (`web | ios`, the sub-skill it delegated to); `verify_run_id` (the verify run id, or null).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
