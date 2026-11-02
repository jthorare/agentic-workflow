---
name: productReview
description: "Founder/product lens review of plans with 4 scope modes -- mvp, growth, scale, pivot. Challenges assumptions and tightens scope."
argument-hint: "[--mode mvp|growth|scale|pivot] [--plan <path> | plan-file-or-description] [--output <path>] [--slim]"
allowed-tools: Bash(git *), Bash(ls *), Bash(date *), Bash(mkdir *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Product Review — Founder Lens

Reviews plans through a product/founder lens with four distinct modes. Challenges assumptions, tightens scope, and delivers a verdict.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

## Step 1: Parse Arguments and Resolve Plan

**Parse flags:**
- `--mode` followed by one of: `mvp`, `growth`, `scale`, `pivot` — default `mvp`
- `--plan <path>` — plan file or directory (equivalent to the positional path)
- `--output <path>` (optional) — explicit output file path. `/autoplan` passes `--output <feature-dir>/product-review.md`.
- `--slim` (optional) — compact output: verdict, Lens checklist, and Concerns table only

**Resolve the plan to review:**
1. If a file or directory path is given (positional or `--plan`), use it directly
2. If a text description is given, use it directly as the plan content
3. If neither is provided, auto-discover per `_shared/plan-discovery.md`:

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
PLAN_DIR=$(ls -1dt "$AW_DIR/plans/"*/ 2>/dev/null | head -1)
```

**If the target is a plan directory**, read `plan.md` + `product.md` + `engineering.md` (+ `TASKS.md` if present) — the reader matrix in `_shared/plan-layout.md`. Legacy aliases per `_shared/plan-layout.md`: fall back to `requirements.md` (→ product.md) and `design.md` (→ engineering.md) only if the canonical file is absent. The review framework maps:
- **Scope check** -- `product.md` (EARS requirements) + `TASKS.md` (task list)
- **Persona clarity** -- `product.md` (Problem Statement / persona)
- **Timeline reality** -- `TASKS.md` (complexity estimates)
- **Riskiest assumption** -- `engineering.md` (Architecture Decisions + Open Questions)

**If the target is a single file** (legacy format), read and review it as before.

If no plan is found at all, tell the user:
> "No plan found. Provide a file path, a description, or run `/officeHours` first to generate a design doc."

## Step 2: Read Context

Read project context to inform the review:

- Read `AGENTS.md` (or `CLAUDE.md` if that is all the repo has) if it exists
- Read `README.md` if it exists
- Search files for relevant planning docs and skim them

## Step 3: Review Through the Mode Lens

Apply the selected mode's review framework to the plan. **Every lens item of the active mode is a mandatory deliverable:** it must yield at least one concrete finding or an explicit `N/A — <reason>` row in the Step 4 Lens checklist. Silently dropping an item is not allowed.

### MVP Mode (default)

Focus on shipping speed and scope discipline:

- **Scope check** — Is this truly minimal? List every feature and challenge whether each one is essential for v1. Identify at least one thing that can be cut.
- **Persona clarity** — Is there a single, clear user persona? If the plan serves multiple personas, flag it.
- **Timeline reality** — Can this ship in under 2 weeks of focused effort? If not, what needs to shrink?
- **Riskiest assumption** — Identify the single biggest assumption. Propose a way to test it before building.
- **Build vs. skip** — For each component, ask: can we use an existing tool, hardcode it, or skip it entirely for v1?

### Growth Mode

Focus on user acquisition and retention:

- **Growth levers** — What are the 2-3 primary growth mechanisms? Are they built into the product or bolted on?
- **Activation funnel** — Map the steps from "user discovers this" to "user gets value". Where is the biggest drop-off risk?
- **10x usage** — What would 10x the current usage look like? Does the current design support or block it?
- **Retention hooks** — What brings users back? Is there a natural cadence (daily, weekly, per-PR)?
- **Viral coefficient** — Does usage by one person naturally expose others to the product?

### Scale Mode

Focus on operational sustainability:

- **100x load** — What breaks at 100x current usage? Identify the first bottleneck.
- **Unit economics** — What is the cost per user/operation? Does it improve or degrade with scale?
- **Automation gaps** — What currently requires manual intervention? What is the path to automating it?
- **Operational bottlenecks** — Where will the team spend most of their time at scale? Is that the right place?
- **Data gravity** — Where does data accumulate? Does it become an asset or a liability?

### Pivot Mode

Focus on strategic direction:

- **What's working** — Identify the strongest signal from current usage/design. What should be doubled down on?
- **What should die** — Identify features or directions that are not earning their complexity. Recommend killing them.
- **Adjacent opportunity** — Based on the current position, what nearby problem could be solved with minimal additional effort?
- **Fresh start test** — If starting from scratch today with current knowledge, what would be built differently?
- **Core value extraction** — What is the one irreducible thing this product does that matters?

## Step 4: Generate Review

Produce a structured review document. Severity uses the shared scale in `_shared/severity.md` (CRITICAL / HIGH / MEDIUM / LOW).

The verdict is **rule-derived from the Concerns table**, never a vibe:
- any CRITICAL concern, or ≥2 HIGH concerns ⇒ **RETHINK**
- any HIGH concern ⇒ at least **ITERATE**
- otherwise ⇒ **SHIP**

```markdown
# Product Review: {plan title}

_Reviewed by `/productReview` on {ISO date} | Mode: {mode}_

## Verdict: {SHIP | ITERATE | RETHINK}

{One paragraph justification citing the concern ids that drove the verdict}

## Lens checklist ({mode})

_One row per lens item of the active mode — all 5 rows mandatory._

| Lens item | Result |
|---|---|
| {item 1, e.g. Scope check} | {finding summary, or N/A — <reason>} |
| {item 2} | … |
| {item 3} | … |
| {item 4} | … |
| {item 5} | … |

## Strengths
1. {What's strong about this plan}
2. {Another strength}
3. {Another strength}

## Concerns

_≥3 rows, or an explicit line "Fewer than 3 concerns because <reason>."_

| # | Severity | Concern | Recommendation |
|---|----------|---------|----------------|
| 1 | {CRITICAL/HIGH/MEDIUM/LOW} | {concern} | {what to do} |
| 2 | {CRITICAL/HIGH/MEDIUM/LOW} | {concern} | {what to do} |
| ... | ... | ... | ... |

## Scope Suggestions

### Cut
- {feature/element to remove and why}

### Keep
- {feature/element that's essential and why}

### Add
- {missing element that would strengthen the plan}

## Key Questions for the Team
1. {Question that needs answering before proceeding}
2. {Another question}
3. {Another question}

## Recommended Next Action
{Single concrete next step -- be specific}
```

End the file with the normalized final line per `_shared/severity.md` (CD9 mapping: SHIP→PASS, ITERATE→NEEDS_WORK, RETHINK→BLOCKED):

```
verdict: <PASS|NEEDS_WORK|BLOCKED>
```

## Step 5: Write the Review

If `--output <path>` was provided, write to that exact path (ensure the parent dir exists with `mkdir -p`). Otherwise generate a URL-safe slug from the plan title (lowercase, hyphens, no special chars) and write to the default path:

```bash
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
```

Default: `$AW_DIR/plans/{timestamp}-product-review-{slug}.md`

## Step 6: Report

Show a summary to the user:

```
Product Review complete! ({mode} mode)

Verdict: {SHIP | ITERATE | RETHINK} (normalized: {PASS | NEEDS_WORK | BLOCKED})

Review written to: {resolved output path}

Top concerns:
  1. [{severity}] {concern summary}
  2. [{severity}] {concern summary}
  3. [{severity}] {concern summary}

Recommended next action: {one-line action}

Suggested next steps:
  /archReview — Review the engineering architecture
  /officeHours — Brainstorm refinements to address concerns
```

## Next steps

- `/autoplan` — run with other plan-review lenses in parallel
- `/archReview` — engineering architecture perspective on the same plan
