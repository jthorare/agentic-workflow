---
name: design-analyze-ios
description: Use when the source is an iOS project's own code or assets (Assets.xcassets, SwiftUI theme files) and the user wants its colours, text styles and spacing as design tokens. Writes design-tokens.json in W3C format; finds the files itself when no path is given. Not for reference websites (design-analyze-web).
argument-hint: [path/to/Assets.xcassets or Theme.swift]
allowed-tools: Bash(SHARED_DIR=*), Read, Write, Glob, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

> **Note:** This skill creates design context — missing `design-tokens.json` is expected on first run.

---

# Design Analyze iOS — Extract Design Tokens from Swift/Xcode Assets

Scans the Xcode project for color, typography, and spacing definitions and writes `design-tokens.json` in W3C DTCG format.

## Step 1: Locate Source Files

### With explicit path argument:
Use the provided path directly (an `Assets.xcassets` directory or a `*Theme*.swift` / `*Colors*.swift` file).

### Without argument (auto-discover):
Find files matching these glob patterns:
```
**/*.xcassets
**/*Theme*.swift
**/*Colors*.swift
**/*Color*.swift
```

If nothing is found:
> "No Swift color definitions or asset catalogs found. Start your project and add a color asset catalog or a theme file, then re-run."

## Step 2: Extract Tokens (shared procedure)

Resolve the shared dir from the stable toolkit path:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
echo "extraction: $SHARED_DIR/ios-token-extraction.md"
```

Read `$SHARED_DIR/ios-token-extraction.md` and follow it exactly for the files located in Step 1:

- **Colors** — `Assets.xcassets` colorsets + Swift theme-file patterns
- **Typography** — Swift font patterns
- **Spacing (restricted)** — only `CGFloat` constants whose names match the spacing/layout pattern; never arbitrary CGFloat constants
- **Completeness gate** — if fewer than 3 colors or 0 typography tokens were extracted, report the shortfall and **ask the user** (different path / proceed partial / abort) before writing anything

## Step 3: Write design-tokens.json

If `design-tokens.json` already exists, **ask the user**:
> "design-tokens.json already exists. Overwrite with extracted iOS tokens? (yes/no)"

Write extracted tokens in W3C DTCG format:

```json
{
  "color": {
    "primary": { "$value": "#6366F1", "$type": "color" },
    "primary-dark": { "$value": "#818CF8", "$type": "color" },
    "background": { "$value": "#FFFFFF", "$type": "color" }
  },
  "typography": {
    "heading": {
      "fontSize": { "$value": "28px", "$type": "dimension" },
      "fontWeight": { "$value": "700", "$type": "number" }
    }
  },
  "spacing": {
    "sm": { "$value": "8px", "$type": "dimension" },
    "md": { "$value": "16px", "$type": "dimension" },
    "lg": { "$value": "24px", "$type": "dimension" }
  }
}
```

If no Swift color definitions existed (only asset catalog):
> "Created design-tokens.json from color assets only. Typography and spacing tokens could not be auto-extracted — add them manually or create a theme file."

## Step 4: Present Summary

```
iOS Design Token Extraction Complete
=====================================

Sources scanned:
  {list of files read}

Colors:     N tokens extracted
Typography: N tokens extracted (or: not found)
Spacing:    N tokens extracted (or: not found)

Written to: design-tokens.json

Next steps:
  1. Run /design-language to define brand personality
  2. Run /design-mockup-ios to generate a SwiftUI preview mockup
  3. Run /design-implement-ios to generate Theme.swift
```

## Rules

- Never infer colors from view background or text color assignments — only extract explicit theme/constant definitions
- If Swift files use system colors (`Color(.systemBlue)`) without a custom equivalent, note them in the output but do not add a token for them
- Do not modify existing Swift files — read-only extraction
- Convert all CGFloat dimensions to `"Npx"` string format for DTCG compatibility

## Result block

First run this Bash line and go by what it prints (the eval stage turns it on in its staged copy; a person's run prints 0):

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

On `AW_RESULT=1`, write the result file with the Write tool (it creates the folder, so no `mkdir` first). On `AW_RESULT=0`, write no file and print no block.

Follow `$HOME/.agentic-workflow/toolkit/skills/_shared/output-contract.md`. Only when `AW_RESULT=1`: write `$AW_DIR/results/design-analyze-ios-<timestamp>.json` and print the same JSON as a fenced `aw-result` block, placed before `## Next steps`. Outside `AW_RESULT=1`, write no file and print no block.

Keys: `skill` (`"design-analyze-ios"`), `artifacts` (absolute paths: `design-tokens.json`), `summary` (one line), plus `sources` (the asset catalogs and Swift files read) and `counts` (tokens per category).

## Next steps

- `/design-language` — define brand from extracted iOS tokens
- `/design-evolve-ios` — merge Swift reference into existing design language
