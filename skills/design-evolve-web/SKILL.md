---
name: design-evolve-web
description: Use when a design language already exists and the user points at a new reference URL for inspiration, wanting to decide what to keep, rework or drop. Diffs the site against the current tokens and updates design-tokens.json and .impeccable.md. Not for local Swift sources (design-evolve-ios) or a first extraction (design-analyze-web).
argument-hint: <url>
allowed-tools: Bash(npx dembrandt *), Bash(git *), Bash(SHARED_DIR=*), Bash(mkdir *), Read, Write, Edit, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `$HOME/.agentic-workflow/toolkit/skills/_design-preamble.md` and follow it before continuing.

---

# Design Evolve — Merge New Reference into Design Language

Analyze a new reference site mid-project and selectively merge its design elements into the existing design language. Shows a diff of what would change, lets the user choose what to adopt.

## Step 1: Validate Prerequisites

Both `.impeccable.md` and `design-tokens.json` must exist. If either is missing:
> "No existing design language found. Run `/design-analyze` and `/design-language` first to establish a baseline before evolving."

## Step 2: Validate URL

Validate that the `<url>` argument starts with `http://` or `https://` and contains only URL-safe characters: letters, digits, `:`, `/`, `.`, `-`, `_`, `~`, `?`, `=`, `%`, `+`, `@`, `,`.

Reject any URL containing characters outside this allowlist.

If validation fails:
> "Invalid URL: `<url>`. URLs must start with `http://` or `https://` and may only contain URL-safe characters (`a-zA-Z0-9` and `:/.\\-_~?=%+@,`). Offending characters: `<list of disallowed characters found>`."

## Step 3: Run Dembrandt on New URL

Pin the extractor output to a known path so the diff reads an exact file — never a guessed filename. `<host>` is the URL's hostname (e.g. `https://linear.app/features` → `linear.app`). One Bash invocation (shell state does not persist between calls):

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/design/raw"
npx dembrandt <url> --dtcg --out "$AW_DIR/design/raw/<host>.json"
```

Verify the pinned file exists and is non-empty; if not, report the dembrandt error and stop. Then Read `~/.agentic-workflow/<repo-slug>/design/raw/<host>.json` — this exact file is the merge input.

## Step 4: Merge (shared algorithm)

Read `$SHARED_DIR/token-merge.md` and follow it exactly:

- **Step A** — category diff table (NEW / DIFFERENT / UNCHANGED) against current `design-tokens.json`
- **Step B** — **Ask the user** Adopt / Adapt / Ignore per group; **Adapt requires the follow-up **Ask the user** capturing the literal replacement value**
- **Step C** — write merged `design-tokens.json`; **unconditionally** append the consultation to `.impeccable.md ## Sources`; set `baseline_stale` in `screens.json` when anything was adopted or adapted
- **Step D** — report counts and exact before→after values

The pinned `design/raw/<host>.json` is kept — do not delete it.

## Result block

First run this Bash line and go by what it prints (the eval stage turns it on in its staged copy; a person's run prints 0):

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

On `AW_RESULT=1`, write the result file with the Write tool (it creates the folder, so no `mkdir` first). On `AW_RESULT=0`, write no file and print no block.

Follow `$HOME/.agentic-workflow/toolkit/skills/_shared/output-contract.md`. Only when `AW_RESULT=1`: write `$AW_DIR/results/design-evolve-web-<timestamp>.json` and print the same JSON as a fenced `aw-result` block, placed before `## Next steps`. Outside `AW_RESULT=1`, write no file and print no block.

Keys: `skill` (`"design-evolve-web"`), `artifacts` (absolute paths: `design-tokens.json` and `.impeccable.md`), `summary` (one line), plus `source` (the URL) and `counts` (`adopted`, `adapted`, `ignored`).

## Next steps

- `/design-mockup-web` — rebuild mockups with the updated tokens (existing baselines are now marked stale)
- `/design-refine` — apply the evolved language to existing screens
