---
name: withInterview
description: "Use when the user asks to be interviewed, quizzed or asked questions in rounds before any work starts, so a vague or big task gets pinned down first, then handed to another skill. Not for turning an idea into a spec (officeHours) or for rewriting a prompt from project docs (enhancePrompt)."
argument-hint: "<skill-name> [prompt]"
disable-model-invocation: true
allowed-tools: Bash(git *), Bash(ls *), Bash(mkdir *), Bash(date *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Interview Before Executing

Runs a structured, multi-round interview to gather requirements and context before executing a task. Surfaces ambiguities, challenges assumptions, and confirms subagent strategy — then proceeds only after explicit user confirmation.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## The Task

$ARGUMENTS

The first token of the arguments is the **target skill** to run after the interview (e.g. `/withInterview officeHours add dark mode`). Record it now — the interview ends by invoking it. If no skill name is given, ask which skill (or "none — execute directly") before Round 1.

## Interview Process

### Setup: Interview Directory

Create the plan directory (layout per `_shared/plan-layout.md`) before Round 1. Derive `<slug>` as 2–4 kebab-case words from the task:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
PLAN_DIR="$AW_DIR/plans/$(date +%Y-%m-%d)-<slug>"
mkdir -p "$PLAN_DIR"
echo "plan-dir: $PLAN_DIR"
```

All interview artifacts go to `$PLAN_DIR/interview.md`. Persist each round's questions there as they are asked (append per round) so the question list can be re-rendered on demand later.

### Round 1: Initial Analysis & High-Level Questions

Read the task above carefully. Identify: ambiguities or underspecified requirements; decisions with more than one reasonable answer; missing context that would change the approach; unclear scope boundaries; and **scope/time-box constraints** — flags like `--slim` or phrases like "30 min", "quick pass" — captured verbatim (they go into the artifact and are forwarded to the target skill).

Present your **highest-priority questions first** — the ones whose answers will shape everything else. Emit them as a numbered table with a Theme column, and write the same table to `$PLAN_DIR/interview.md`:

| # | Theme | Question | Options (if any) |
|---|-------|----------|------------------|
| 1 | scope | Should X do A or B? | A / B |

Themes: scope, behavior, constraints, testing, integration. Keep each question concise and offer concrete options where possible (e.g. "Should X do A or B?" rather than open-ended "What should X do?").

### Round 2: Subagent Strategy

After the user answers Round 1, ask specifically about subagent usage. Present this as a focused follow-up:

> **Subagent Strategy:** Based on what you've described, here's how I'd recommend using subagents. Let me know what you'd prefer:
>
> - **Explore** — for codebase research and file discovery
> - **Plan** — for designing an implementation strategy before coding
> - **general-purpose** — for delegating independent subtasks in parallel
> - **Reviewers** — post-implementation review via `/review`, which selects reviewer agents based on the changed files
> - **None** — I'll handle everything directly
>
> You can pick multiple. I'll also suggest a specific combination if you'd like a recommendation.

These are **roles**, not tool names — map each to the host's agent types when you **Spawn a subagent** (Claude Code: `Explore` / `Plan` / `general-purpose`; Codex and Cursor: their explorer / planner / default agent types, or a default subagent primed with the role).

If the user selects **multiple subagents**, follow up on each: its focus/scope, parallel vs sequential, and dependencies between outputs (e.g. Explore feeding Plan).

### Round 3+: Drill-Down Details

Continue asking follow-up rounds as needed. Each round: reference previous answers ("You mentioned X — does that mean...?"), go deeper on underspecified areas, surface edge cases and error handling, clarify testing expectations and acceptance criteria.

**Run at least 3 rounds** unless the user explicitly says to skip ahead. Keep going past 3 until you can honestly emit the line `Remaining ambiguities: none` — followed by a bullet list of what each round resolved. That literal line is required in the final round; you may not summarize-and-confirm without it. Each round should be a focused set of 2-5 questions (numbered, with Theme column, appended to `$PLAN_DIR/interview.md`), not a wall of text.

### Final Round: Summarize, Persist, and Confirm

Once all details are gathered, present a complete summary:

1. **Task understanding** — what you'll build, with all clarifications incorporated
2. **Approach** — the implementation strategy step by step
3. **Subagent plan** — which agents will be used, in what order, with what focus
4. **Scope boundaries** — what's explicitly in and out of scope, including any captured `--slim`/time-box constraint
5. **Acceptance criteria** — how you'll know the task is done

End with:

```
Remaining ambiguities: none
- <round N resolved: ...>
```

Ask: **"Does this look right? Any changes before I start?"**

After the user confirms, write two artifacts to `$PLAN_DIR` (re-source `repo-slug.sh` first — shell state does not persist):

1. **`$PLAN_DIR/interview.md`** — the full record: target skill, original prompt, captured scope/time-box constraints, every round's question table with the user's answers, the 5-part summary, and the `Remaining ambiguities: none` block.
2. **`$PLAN_DIR/plan.md`** — a TL;DR-form plan shaped per `_shared/plan-layout.md` so downstream discovery (`/autoplan`, plan lenses, `/specToProvenPR`) finds it: task understanding, approach steps, scope boundaries, acceptance criteria, and a pointer to `interview.md`.

### Execute

Only after explicit confirmation and after both artifacts are written, hand off to the target skill captured in Round 1 with an explicit invocation:

```
Invoke skill <target> with args: <original prompt> — interview: $PLAN_DIR/interview.md
```

If the target was "none — execute directly", begin the work yourself using the confirmed plan.

## Rules

- Do NOT write any code or make any changes until the interview is complete and confirmed.
- This is a **multi-round conversation**. Do NOT try to ask everything in one message. Start broad, then drill down based on answers.
- Each round should be focused: 2-5 questions max per round.
- Always reference previous answers when asking follow-ups to show you're building understanding.
- The subagent question always gets its own dedicated round.
- Minimum 3 rounds; only the user saying "skip" collapses the interview — and even then, still ask the subagent question, get confirmation, and write both artifacts before proceeding.

## Next steps

- **Invoke skill `<target>`** with args `<original prompt> — interview: $PLAN_DIR/interview.md` — run the wrapped target skill with the interview artifact attached
- `/autoplan` — if the confirmed plan warrants multi-lens review before implementation (it will auto-discover `$PLAN_DIR/plan.md`)

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/withInterview-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`withInterview`), `artifacts` (absolute paths: `$PLAN_DIR/interview.md` once Round 1 wrote it, `$PLAN_DIR/plan.md` only after the user confirmed; `[]` before Round 1), `summary` (one line),
plus `confirmed` (true when the user confirmed the plan); `plan_dir` (absolute path of the `$PLAN_DIR` directory holding `interview.md` and `plan.md`); `target_skill` (the skill handed off to, or `none`); `rounds` (number of interview rounds held).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
