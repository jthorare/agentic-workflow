---
name: design-analyze-web
description: Use when the source is one or more reference site URLs and the user wants their colours, fonts and spacing captured as design tokens. Runs Dembrandt, merges sites by frequency, writes design-tokens.json. Not for Swift or Xcode projects (design-analyze-ios) or for updating existing tokens (design-evolve-web).
argument-hint: <url> [url2...] [--dark]
allowed-tools: Bash(npx dembrandt *), Bash(git *), Bash(SHARED_DIR=*), Bash(mkdir *), Bash(rm *), Read, Write, Glob, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

> **Note:** This skill creates design context — missing `design-tokens.json` is expected on first run.

---

# Design Analyze — Extract Design Tokens from Reference Sites

Runs Dembrandt CLI on one or more reference website URLs, extracts design tokens, merges across sites, and writes `design-tokens.json` in W3C DTCG format.

## Step 1: Validate Arguments

The user must provide at least one URL. Parse all URLs from the argument string. A `--dark` flag (anywhere in the arguments) requests an additional dark-mode extraction pass.

If no URLs provided:
> "Usage: `/design-analyze-web <url> [url2...] [--dark]`
> Example: `/design-analyze-web https://linear.app https://vercel.com`"

## Step 2: Validate URLs

Validate that each URL starts with `http://` or `https://` and contains only URL-safe characters: letters, digits, `:`, `/`, `.`, `-`, `_`, `~`, `?`, `=`, `%`, `+`, `@`, `,`.

Reject any URL containing characters outside this allowlist.

If any argument fails validation:
> "Invalid URL: `<argument>`. URLs must start with `http://` or `https://` and may only contain URL-safe characters (`a-zA-Z0-9` and `:/.\\-_~?=%+@,`). Offending characters: `<list of disallowed characters found>`."

## Step 3: Run Dembrandt on Each URL

Pin every extractor output to a known path so the merge step reads exact files — never guessed filenames. `<host>` is the URL's hostname (e.g. `https://linear.app/features` → `linear.app`).

For each URL, in one Bash invocation (shell state does not persist between calls):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/design/raw"
npx dembrandt <url> --dtcg --out "$AW_DIR/design/raw/<host>.json"
```

If `--dark` was passed, also run for each URL (same invocation shape):

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
npx dembrandt <url> --dtcg --dark-mode --out "$AW_DIR/design/raw/<host>-dark.json"
```

After each run, verify the pinned file exists and is non-empty; if not, report the dembrandt error and stop.

## Step 4: Merge Extracted Tokens

If multiple URLs were provided:

1. Read exactly the pinned files written in Step 3 — `$AW_DIR/design/raw/<host>.json` (and `<host>-dark.json` when `--dark`), one per URL
2. Identify shared patterns across sites (common colors, similar typography scales, consistent spacing)
3. Resolve conflicts by frequency and prominence:
   - Token present in most sites wins
   - If tied, prefer the token from the first URL (primary reference)
4. Synthesize: what's shared across references, what's distinctive about each

If single URL, use its tokens directly.

## Step 5: Write design-tokens.json

Write the merged tokens to `design-tokens.json` at the project root in W3C DTCG format:

```json
{
  "color": {
    "primary": { "$value": "#...", "$type": "color" },
    "secondary": { "$value": "#...", "$type": "color" }
  },
  "typography": {
    "heading": {
      "fontFamily": { "$value": "...", "$type": "fontFamily" },
      "fontSize": { "$value": "...", "$type": "dimension" }
    }
  },
  "spacing": {
    "sm": { "$value": "...", "$type": "dimension" },
    "md": { "$value": "...", "$type": "dimension" }
  }
}
```

## Step 6: Present Summary

Display a summary of extracted tokens:

```
Design Token Extraction Complete
=================================

Source(s): <url1>, <url2>, ...

Colors:     N tokens extracted
Typography: N tokens extracted
Spacing:    N tokens extracted
Radii:      N tokens extracted
Elevation:  N tokens extracted
Motion:     N tokens extracted

Written to: design-tokens.json

Next steps:
  1. Run /design-language to define brand personality
  2. Run /design-mockup <screen> to generate HTML mockups
  3. Run /design-implement to generate production code
```

## Rules

- Always use `--dtcg` flag for W3C DTCG format output
- Do not modify existing `design-tokens.json` without warning — if it exists, ask before overwriting
- The pinned raw outputs under `$AW_DIR/design/raw/` are **kept** — `/design-evolve-web` reuses them. Remove only stray dembrandt output files written outside `design/raw/` (e.g. in the project directory), explicitly per file:
  ```bash
  rm <stray-output-file>
  ```
- If Dembrandt is not installed, advise: "Run `npm install -g dembrandt` or re-run `setup.sh`"

## Result block

First run this Bash line and go by what it prints (the eval stage turns it on in its staged copy; a person's run prints 0):

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

On `AW_RESULT=1`, write the result file with the Write tool (it creates the folder, so no `mkdir` first). On `AW_RESULT=0`, write no file and print no block.

Follow `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`. Only when `AW_RESULT=1`: write `$AW_DIR/results/design-analyze-web-<timestamp>.json` and print the same JSON as a fenced `aw-result` block, placed before `## Next steps`. Outside `AW_RESULT=1`, write no file and print no block.

Keys: `skill` (`"design-analyze-web"`), `artifacts` (absolute paths: `design-tokens.json`), `summary` (one line), plus `sources` (the URLs scanned) and `counts` (tokens per category: colors, typography, spacing, radii, elevation, motion).

## Next steps

- `/design-language` — define brand from extracted web tokens
- `/design-evolve-web` — merge new tokens into existing design language
