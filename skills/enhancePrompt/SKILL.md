---
name: enhancePrompt
description: "Use when the user wants a request rewritten with project context first: what the docs say, constraints, scope and done-criteria, so a fresh session or another agent can run it. It only rewrites; it does not do the task or interview the user (that is withInterview)."
argument-hint: [prompt-to-enhance]
allowed-tools: Read, Write, Glob, Grep, AskUserQuestion, Bash(git *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(SHARED_DIR=*), mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

# enhancePrompt

## Overview

Dynamically discovers project documentation, reads what's relevant to the user's prompt, and rewrites the request with full context — constraints, conventions, domain rules, and known patterns — before any work begins.

## Steps

### 1. Discover documentation

Scan the working directory for what actually exists (don't assume): root-level guides (`AGENTS.md`, `CLAUDE.md` — often a symlink to `AGENTS.md` or an `@AGENTS.md` import stub — `GEMINI.md`, `README.md`, `CONTRIBUTING.md`); domain rules `.agents/rules/*.md` (canonical; also `.claude/rules/*.md` or `.cursor/rules/*.mdc` in repos not yet migrated — this skill's core input); `planning/*.md`; docs folders (`docs/`, `wiki/`, `.docs/`, `documentation/`); any `*.md` at root or one level deep.

List what you find. If nothing exists, say so and offer to enhance from conversation context alone.

### 2. Read selectively — and account for every doc

Read files whose names or paths suggest relevance to the user's prompt topic. Always read any root-level instruction file (`AGENTS.md`, `CLAUDE.md`, `README.md`) first since they establish overall context, then the `.agents/rules/*.md` files whose globs cover the prompt's domain, then topic-specific `planning/*.md` files.

Use judgment — a prompt about pricing doesn't need the testing strategy doc — but the judgment must be visible: every discovered doc appears in the Step 4 output as **read** or **skipped (why)**. Reading only the README does not satisfy this step when rules or planning docs exist.

### 3. Evaluate peer-agent dialogue value

Before producing output, assess whether the task would benefit from consulting a peer agent on another provider (e.g. Codex when you are Claude Code or Cursor, Claude Code when you are Codex). The user carries the prompt to the peer agent's session; this skill doesn't send it. Include a dialogue recommendation **only** when at least one applies: **cross-domain task** (parallel second agent reduces total time), **second opinion valuable** (architecture/security-sensitive/unfamiliar code), **parallel research** (multiple approaches explorable simultaneously), or **verification needed** ("implement X, then have the peer agent try to break it"). If none apply, skip the dialogue section entirely.

### 4. Output the enhanced prompt

```
## Enhanced Prompt

**Original:** <user's exact words>

**Docs discovered / read / skipped:**
- read: <path> — <what it contributed>
- skipped: <path> — <why it wasn't relevant>

**Relevant context from project docs:**
<concise bullets — constraints, conventions, patterns, gotchas that apply>

**Full task:**
- **Goal:** <the outcome the rewritten prompt must achieve>
- **Constraints:** <conventions, tech-stack rules, and limits from the docs>
- **Out of scope:** <what this task explicitly does not cover>
- **Done when:** <concrete, checkable completion criteria>
```

Every doc from Step 1 must appear in the discovered/read/skipped list — none silently dropped.

If step 3 identified dialogue value, append:

```
## Peer-Agent Dialogue Recommended

**Why:** <one sentence — which criterion triggered this>

**Peer agent:** <provider / agent name, e.g. codex>

**What to ask the peer agent:**
<specific, self-contained prompt to paste into the peer agent's session>

**Expected value:** <what the response would add — a review, alternative approach, parallel implementation, etc.>
```

If step 3 found no dialogue value, do not include this section.

### 5. Persist the enhanced prompt

Write the full `## Enhanced Prompt` block (plus the peer-agent section, if any) to the prompts directory:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/prompts"
echo "prompt-file: $AW_DIR/prompts/$(date +%Y%m%d-%H%M%S).md"
```

Write the content to that path and echo it back to the user so downstream skills can reference it.

### 6. Confirm, then dispatch

**Ask the user:** "Should I proceed with this, or adjust anything?"

If step 3 recommended a peer-agent dialogue and the user confirms, print the handoff for them to carry over:
> In the peer agent's session (e.g. Codex), paste the "What to ask the peer agent" prompt above, and bring its answer
> back here.

Do NOT begin executing the task during this skill.

## Rules

- Generic by design — works for any domain: software, product, business analysis, research, etc.
- Preserve the user's original intent exactly; only add context, never redirect.
- Skip docs that aren't relevant — don't dump everything found.
- If the prompt is already detailed, say so and ask if enhancement is still wanted.
- If no docs exist, enhance from what's known in the conversation.

## Result block

Only when `AW_RESULT=1` is set. Otherwise skip this section: write no file and print no block.

Resolve `$AW_DIR` the usual way (`source "$SHARED_DIR/repo-slug.sh"`), `mkdir -p "$AW_DIR/results"`, and write `$AW_DIR/results/enhancePrompt-$(date +%Y%m%d-%H%M%S).json` with:

- `skill`: `"enhancePrompt"`
- `artifacts`: `[]` (this skill talks; the saved prompt is named by `prompt_file`)
- `summary`: one line saying what happened
- `prompt_file`: absolute path of the persisted `prompts/{timestamp}.md`
- `docs_read`: number of docs listed as read

Then print the same JSON as a fenced `aw-result` block in the reply, before `## Next steps`:

```aw-result
{"skill": "enhancePrompt", "artifacts": [], "summary": "<one line>", "prompt_file": "/abs/prompts/<ts>.md", "docs_read": 3}
```

## Next steps

Name the successor skill explicitly — pick the one that matches the enhanced task and pass the persisted prompt file:

- **Invoke skill `withInterview`** with args `<successor-skill> <enhanced prompt path: $AW_DIR/prompts/<ts>.md>` — if requirements still need clarification before executing
- `/officeHours` — if the enhanced prompt describes a feature that needs a spec before implementation
- `/specToProvenPR` — if the enhanced prompt is an approved spec ready to be built and proven
- Otherwise, execute the enhanced prompt from `prompts/<ts>.md` directly in this session after the user confirms
