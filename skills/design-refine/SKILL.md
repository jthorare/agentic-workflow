---
name: design-refine
description: "Use when a project already has its design tokens file and .impeccable.md and a built screen needs a bounded polish pass inside that language. Runs one audit, up to three pack dispatches and a verify gate, and logs each run. Not for a from-scratch restyle or a bolder look (impeccable, taste-skill)."
argument-hint: "[refinement-intent]"
allowed-tools: Read, Write, Edit, Skill, Glob, Grep, Bash(git *), Bash(ls *), Bash(mkdir *), Bash(SHARED_DIR=*), Bash(source *), AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---
<!-- MEMORY: SKIP -->

<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `$HOME/.agentic-workflow/toolkit/skills/_design-preamble.md` and follow it before continuing.

# Design Refine — Orchestrate Refinement Through Impeccable + Packs

Pre-loads the design language context, then runs a **bounded** refinement sequence: impeccable audit → map findings to packs → dispatch at most 3 refinements → impeccable verify pass → design-verify diff. If no refinement intent is given, analyzes the implementation and suggests intents.

## Pack Selection

`design-refine` orchestrates three external skill packs (all fetched at setup time). Only these registered skill names may be dispatched — there are no per-dimension skills like "colorize" or "typeset"; those are **intents phrased in the args of an `/impeccable` dispatch**.

| Pack | Skill name(s) | Use for |
|---|---|---|
| `pbakaus/impeccable` | `/impeccable` | Umbrella refinement — every dimensional intent (color, typography, spacing, motion, accessibility, responsiveness, icons, dark mode) is expressed as an intent phrase in its args. The canonical entry point and the only dispatch target for dimension-level work. |
| `emilkowalski/skill` | `/emil-design-eng` | Design engineering principles — UI polish, component design, animation decisions. Use as **reference context** when explaining or justifying refinements. |
| `Leonxlnx/taste-skill` — style packs | `/taste-skill`, `/minimalist-skill`, `/brutalist-skill`, `/soft-skill`, `/redesign-skill`, `/brandkit`, `/stitch-skill`, `/gpt-tasteskill`, `/imagegen-frontend-web`, `/imagegen-frontend-mobile` | Style-direction shifts ("more minimalist" → `/minimalist-skill`; "more brutalist" → `/brutalist-skill`; "needs a brand kit" → `/brandkit`). |
| `Leonxlnx/taste-skill` — utility packs | `/output-skill` (complete-output enforcement), `/image-to-code-skill` (image→code fidelity) | **Not style packs.** Compose them into another dispatch when its output risks truncation or must match a reference image — never dispatch them as a style shift. |

## Step 0: Resume Context

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/design"
ls "$AW_DIR/design/refine-log.md" 2>/dev/null || echo "refine-log: none yet"
ls -1dt "$AW_DIR/design/verify/"*/ 2>/dev/null | head -1   # newest verify run = pre-refine baseline
git diff --name-only > /tmp/refine-before.txt 2>/dev/null; git status --short
```

- **Read `$AW_DIR/design/refine-log.md`** (if present) — do not re-suggest refinements already tried; note their outcomes.
- **Record the pre-refine baseline**: the newest `design/verify/<run-id>/comparison-report.json` (if any) is what Step 5 compares against for regression.
- **Snapshot the changed-file set** (`git diff --name-only`) so Step 4 can isolate what refinement touched.

## Step 1: Analyze Current State

If no refinement intent was given as argument:

1. Read the current implementation files (detect by globbing: `*.tsx`, `*.jsx`, `*.html`, `*.css`, `*.swift`)
2. Analyze against the design language in `.impeccable.md`
3. Suggest the most impactful refinements **as intent phrases**:

```
Design Refinement Analysis
==========================

Current implementation could benefit from:

1. /design-refine "align color usage with the token palette" — 3 hardcoded colors found
2. /design-refine "tighten the heading hierarchy to the design-tokens.json type scale"
3. /design-refine "add hover states, focus rings, and micro-interactions"
4. /design-refine "bring layout spacing onto the spacing scale"

Run any of these to apply the refinement.
```

If an intent was specified, skip to Step 2.

## Step 2: Pre-load Design Context

Skill invocations do not inherit file context — **read the files and inline excerpts into the dispatch args**:

1. Read `.impeccable.md` (required — brand personality, aesthetic direction, what to avoid). If missing, warn and offer `/design-language`.
2. Read `design-tokens.json` (required — exact token values). If missing, warn and offer `/design-analyze`.
3. Build a **context excerpt** (keep it tight, it rides in args): the brand-personality lines from `.impeccable.md` plus the token groups relevant to the intent (e.g. the full `color` block for a color intent).
4. Collect the **absolute paths** of the implementation files in scope.

## Step 3: Bounded Refinement Sequence

Every dispatch in this sequence uses the registered `impeccable` skill (or a real taste-pack skill) — never a bare dimension name. The canonical dispatch form:

```
Invoke skill impeccable with args "<intent> — files: <absolute paths>; context: <inlined token/impeccable excerpts from Step 2>"
```

1. **Audit pass:** **Invoke skill `impeccable`** with args `"audit these files against the design language; report dimensional issues (typography, color, spacing, motion, interaction) — files: <abs paths>; context: <excerpts>"`
2. **Map findings to packs:** dimension-level findings → further `/impeccable` intents; style-direction findings → the matching style pack from the table; cite `/emil-design-eng` principles when justifying a refinement.
3. **Dispatch at most 3 refinements** (the user's intent first, then the top audit findings). Style-pack dispatches use the same args shape: **Invoke skill `minimalist-skill`** with args `"<intent> — files: <abs paths>; context: <excerpts>"`.
4. **Verify pass:** **Invoke skill `impeccable`** with args `"verify the previous refinements resolved the audit findings without introducing new issues — files: <abs paths>; context: <excerpts>"`

Append every dispatch (skill, intent, files, outcome) to `$AW_DIR/design/refine-log.md`. The sequence is bounded: one audit, ≤3 refinements, one verify pass — then stop; further rounds are a new `/design-refine` run.

## Step 4: Post-Refinement Token Check

1. `git diff --name-only` again and diff against the Step 0 snapshot → the exact changed-file set.
2. `rg -n '#[0-9a-fA-F]{3,8}\b' <file>` and `rg -n '\b[0-9]+px\b' <file>` (or the host's content search with line numbers) over each changed file.
3. Compare hits against `design-tokens.json` values. New values introduced → **ask the user** whether to add them to `design-tokens.json` (then note `/design-implement` must regenerate platform token files) or replace them with existing tokens.

## Step 5: Verification Gate (mandatory)

Refinement is the drift-likeliest step — always diff against the mockup baseline:

```
Invoke skill design-verify with args "<screen(s) touched>"
```

Compare the new `comparison-report.json` against the pre-refine baseline recorded in Step 0:

- No baseline existed, or `overall.max_diff_pct` is equal/lower → proceed to Step 6.
- **Regression** (diff % increased or verdict degraded vs pre-refine) → **block**: report the regressing screens/viewports, and either fix forward (targeted `/impeccable` dispatch on the regressing region) or revert the refinement (`git diff` from Step 4 identifies the files). Do not report success on a regression.

## Step 6: Report

```
Refinement Applied
==================

Intent:      <refinement intent>
Dispatched:  <skill + intent per dispatch, ≤5 lines>
Files:       <changed-file set from Step 4>
Token sync:  <in sync / N new values added to design-tokens.json>
Verify:      <run-id> — <verdict> (max diff <pct>% vs pre-refine <pct>%)
```

## Outputs

| Path | Description |
|------|-------------|
| `~/.agentic-workflow/<repo-slug>/design/refine-log.md` | Append-only log of every dispatch (pack, skill, intent, files touched, outcome). Read at Step 0 so runs never repeat failed experiments. |
| `design-tokens.json` | Updated in place when a refinement introduces new token values (see Step 4). |

## Rules

- Always dispatch via **Invoke skill** using registered skill names only (`impeccable`, `emil-design-eng`, taste-pack names) — dimension names like "colorize"/"typeset" are intent phrases inside `args`, never skill names
- Design context must be inlined into every dispatch's args (Step 2) — Skill invocations do not inherit file context
- Bounded sequence: one audit, at most 3 refinement dispatches, one verify pass per run
- Do not invoke a style pack when an `/impeccable` intent covers the issue holistically; never dispatch the utility packs (`output-skill`, `image-to-code-skill`) as style shifts
- If `design-tokens.json` is updated, note that `/design-implement` should be re-run to regenerate platform token files
- Do not modify `.impeccable.md` during refinement — only `design-tokens.json` may be updated
- Always append the run to `refine-log.md`, and never skip the Step 5 verify gate

## Result block

First run this Bash line and go by what it prints (the eval stage turns it on in its staged copy; a person's run prints 0):

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

On `AW_RESULT=1`, write the result file with the Write tool (it creates the folder, so no `mkdir` first). On `AW_RESULT=0`, write no file and print no block.

Under `AW_RESULT=1` only: write `$AW_DIR/results/design-refine-<timestamp>.json` and print the same JSON as a fenced `aw-result` block, placed before `## Next steps`. Without `AW_RESULT=1` write no file and print no block.

Keys: `skill` (`design-refine`), `artifacts` (absolute paths: the files touched, `refine-log.md`, and `design-tokens.json` when updated), `summary` (one line: dispatches run and the verify outcome).

## Next steps

- `/design-verify` — re-run anytime for a fresh diff against the mockup baseline
- `/design-implement` — if refinements require regenerating platform token files
- `/shipRelease` — ship once the verify verdict is PASS
