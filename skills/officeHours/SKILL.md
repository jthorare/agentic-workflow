---
name: officeHours
description: "Use when someone has a half-formed feature or product idea and needs a spec before anything gets built: who it is for, what can go wrong, EARS requirements, split into product, engineering, design and task docs. Not for rewriting a prompt with doc context or for reviewing a finished plan."
argument-hint: "[feature-or-problem-description]"
allowed-tools: Bash(git *), Bash(mkdir *), Bash(date *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, WebFetch, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Office Hours — Spec-Driven Brainstorming

Runs a structured brainstorming session and produces four domain-owned outputs — one per team — so every participant leaves with a clear assignment rather than a monolithic doc no one owns.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Get the Topic

**If an argument was provided**, use it as the feature/problem description.

**If no argument was provided**, ask the user:
> "What feature, problem, or idea do you want to brainstorm?"

Wait for their response before continuing.

## Step 2: Context Gathering

Read project context to ground the brainstorming session:

- Read `AGENTS.md` (or `CLAUDE.md` if that is all the repo has) and `README.md` if they exist
- **Read bootstrap-generated planning docs** — whichever of these exist in `planning/`: `PRODUCT_ROADMAP.md`, `BUSINESS_PLAN.md`, `GO_TO_MARKET.md`, `COMPETITIVE_ANALYSIS.md`, `ARCHITECTURE.md`, plus any other relevant docs there
- Search files for any other relevant planning docs (`docs/*.md`, `*.md` at root) and skim the most relevant files

**If product planning docs exist** (`PRODUCT_ROADMAP.md`, `BUSINESS_PLAN.md`, etc.), use them to pre-populate Q1 and Q2 context. Before asking Q1, summarize what the existing docs say about the topic: *"I found existing planning docs — here's what they say about [topic]: [summary]. Does this give us useful starting context, or is there a gap this feature addresses that the docs don't capture?"*

**External reference docs:** Scan each planning doc for an `## External References` section — the bootstrap skill appends these when `--product-docs` sources were provided. Collect all referenced sources across all docs, deduplicate by URL/path, and surface them before the first question:

> **External reference docs available:**
> - [{Type}] [{title}]({url-or-path}) — {note} *(from {doc}.md)* — mark inaccessible ones ⚠️
>
> Should I consult any of these during our session? (yes / no / specify which)

If the user says yes (or specifies sources), fetch accessible URLs (fetch a URL) and read accessible local files before Q1 — treat their content as additional grounding context alongside the planning docs. Note sources marked `⚠️ Not accessible` but do not attempt to fetch them. If no `## External References` sections are found across any planning doc, skip this block entirely.

## Step 3: Problem & User Discovery

Work through each question sequentially. For each one, present your analysis based on the project context, then pause and wait for the user's response before moving on. This is a conversation -- do not use the structured **Ask the user** tool here, just present each question naturally and wait.

### Q1: What problem are you solving?

Restate the problem in your own words based on what the user described and what you learned from the codebase. Be specific.

Then ask: **"Is this right, or is there a deeper issue?"**

Wait for the user's response.

### Q2: Who has this problem and when?

Based on the project and the problem, identify the specific user persona who experiences this. Analyze what they are doing when the problem surfaces -- look for specific triggers (events that kick it off) and ongoing conditions (states they find themselves in).

Then ask: **"Who experiences this, and what are they doing when it happens? Are there specific triggers (events) or ongoing conditions (states) that bring the problem to the surface?"**

Wait for the user's response.

### Q3: How do they solve it today, and what goes wrong?

Map the current workaround or status quo. Look at existing code, docs, or patterns that relate to this problem. Describe the current flow, and identify failure modes and unwanted behaviors.

Then ask: **"What's the current flow? What failure modes or unwanted behaviors do they hit?"**

Wait for the user's response.

### Q4: What does the ideal experience feel like?

Based on the problem and user from Q1-Q2, describe what a great interaction with this feature would feel like — not what it looks like, but what the user *feels* (fast, confident, effortless, informed, etc.). Identify the key moments in the flow that will make or break that feeling.

Then ask: **"What's the most important interaction to get right? What would make this feel delightful vs. just functional?"**

Wait for the user's response.

## Step 4: EARS Menu

After Q4, present the EARS requirement types as a menu. Pre-select types based on the Q1-Q4 conversation:

- **Ubiquitous** is always pre-selected (every feature has core "shall" requirements)
- **Event-driven** is pre-selected if Q2 revealed specific triggers
- **State-driven** is pre-selected if Q2 revealed ongoing conditions
- **Optional** is pre-selected if the feature involves conditional behavior, roles, or configurations
- **Unwanted** is pre-selected if Q3 revealed failure modes

Present the menu:

> Now let's structure the requirements for this feature. EARS (Easy Approach to Requirements Syntax) gives us five requirement patterns. Based on our conversation so far, I've pre-selected the types that seem most relevant, but you can adjust.
>
> **Requirement Types:**
>
> | # | Type | Pattern | Example | Selected? |
> |---|------|---------|---------|-----------|
> | 1 | **Ubiquitous** | "The [system] shall [action]" | "The API shall return JSON responses" | Yes |
> | 2 | **Event-driven** | "When [event], the [system] shall [action]" | "When a file is uploaded, the system shall scan for viruses" | {Yes/No based on Q2} |
> | 3 | **State-driven** | "While [state], the [system] shall [action]" | "While offline, the app shall queue sync operations" | {Yes/No based on Q2} |
> | 4 | **Optional** | "Where [condition], the [system] shall [action]" | "Where the user has admin role, the UI shall show the settings panel" | {Yes/No based on context} |
> | 5 | **Unwanted** | "If [unwanted condition], the [system] shall [action]" | "If the database is unreachable, the system shall return cached data" | {Yes/No based on Q3} |
>
> **Which types apply to your feature? (e.g., "1, 2, 5" or "all" or "drop 3")**

Wait for the user's response. Parse their selection (numbers, "all", or "drop N" syntax).

## Step 5: EARS Deep Dive

For each selected EARS type, run a focused sub-question. Present draft requirements based on the conversation so far and ask the user to refine them. **After each sub-question, wait for the user's response before moving on.**

### 5a: Ubiquitous Requirements (always runs)

Present 3-5 draft ubiquitous requirements that capture the core "shall" behaviors.

Then ask: **"Here are the core behaviors I've drafted. What's missing? What can we cut to keep the MVP tight?"**

Wait for the user's response. Refine the list based on their feedback.

### 5b: Event-driven Requirements (if selected)

Present 2-3 draft event-driven requirements based on triggers identified in Q2.

Then ask: **"Are these the right triggers? Are there events I'm missing, or events that should be deferred to v2?"**

### 5c: State-driven Requirements (if selected)

Present 2-3 draft state-driven requirements based on conditions identified in Q2.

Then ask: **"Are these the right states to handle? Any states where the system should behave differently that we haven't covered?"**

### 5d: Optional Requirements (if selected)

Present 2-3 draft optional requirements based on conditional behavior identified in context.

Then ask: **"Are these the right conditions? Which of these are MVP vs. future?"**

### 5e: Unwanted Behavior Requirements (if selected)

Present 2-3 draft unwanted-behavior requirements based on failure modes from Q3.

Then ask: **"Are these the right failure scenarios? What's the worst thing that could happen, and how should the system respond?"**

### 5f: Approach (always runs)

Based on the codebase, tech stack, existing infrastructure, and the requirements gathered so far, present 2-3 concrete approach options that leverage existing strengths.

Then ask: **"Which of these resonates? Is there something I'm missing about your position?"**

### 5g: Success Criteria (always runs)

Present 2-3 derived acceptance criteria from the requirements gathered so far. Distinguish between leading indicators (can measure in days) and lagging indicators (takes weeks).

Then ask: **"How will we know this works? What would you measure, and when would you check?"**

## Step 6: Generate Four Domain-Owned Output Files

Synthesize the entire conversation into four radically focused files — one per domain. Each file is a standalone artifact with ZERO crossover.

**Ownership principle:** If you need to read another domain's doc to understand your own, the separation has failed.

**Content rules:**
- **product.md** — User perspective only. No technical details, no architecture, no implementation approaches. Product owns "what" and "why" from the user's perspective.
- **engineering.md** — Technical perspective only. No user personas, no business metrics, no UX goals. Engineering owns "how" from a system perspective.
- **design-brief.md** — Experience perspective only. No technical constraints, no architecture details. Design owns "what does this feel like" from the user's emotional perspective.
- **TASKS.md** — Task breakdown only. No implementation details (those live in domain docs). Tasks point to domain docs via references.

**Cross-references:** Each doc ends with a "Cross-References" section pointing to the other domain docs. This is the ONLY place where docs acknowledge each other.

### product.md — Owner: Product

```markdown
# Product: {feature}

_Generated by `/officeHours` on {ISO date}_
_Owner: Product Team_

## Problem Statement

**Who:** {User persona from Q2}
**What:** {Refined problem statement from Q1}
**When:** {Triggers and conditions from Q2}

**Current Experience:**
{How users solve this today — from Q3, user perspective only}

**Desired Experience:**
{What the ideal interaction feels like — from Q4, user perspective only}

## Requirements

_Written in [EARS format](https://alistairmavin.com/ears/) (Easy Approach to Requirements Syntax). Requirements describe user-facing behavior — what the system does from the user's perspective, not how it's implemented._

### Ubiquitous
- REQ-U1: The system shall [user-visible behavior]
- REQ-U2: The system shall [user-visible behavior]
- ...

### Event-driven
- REQ-E1: When [user action or external event], the system shall [user-visible response]
- ...

### State-driven
- REQ-S1: While [user or system state], the system shall [user-visible behavior]
- ...

### Optional
- REQ-O1: Where [user context or configuration], the system shall [user-visible behavior]
- ...

### Unwanted
- REQ-W1: If [error or edge case], the system shall [user-visible recovery behavior]
- ...

## MVP Scope

**In Scope (v1):**
- {User-facing feature 1}
- {User-facing feature 2}

**Out of Scope (future):**
- {Deferred user-facing feature 1}
- {Deferred user-facing feature 2}

## Acceptance Criteria

_Each criterion must be testable by the product team (no internal system checks)._

- **AC-1:** When [user does X], [observable outcome Y] happens within [timeframe]
- **AC-2:** {User-observable criterion}
- ...

## Success Metrics

_How we'll measure whether this solves the problem._

| Metric | Target | Timeframe | Owner |
|--------|--------|-----------|-------|
| {User behavior metric} | {target} | {when} | Product |
| {Business outcome metric} | {target} | {when} | Product |

## Traceability

| Requirement | Acceptance Criteria | Success Metric |
|------------|-------------------|----------------|
| REQ-U1 | AC-1 | {metric} |
| REQ-E1 | AC-2 | {metric} |

---

## Cross-References
`engineering.md` (technical approach) · `design-brief.md` (key interactions) · `TASKS.md` (breakdown)
```

**Sections for unselected EARS types are omitted entirely** (not shown as empty). The **Traceability table** links each requirement to at least one acceptance criterion.

### engineering.md — Owner: Engineering

```markdown
# Engineering Design: {feature}

_Generated by `/officeHours` on {ISO date}_
_Owner: Engineering Team_

## Technical Context

**Current System State:**
{Current implementation, tech stack, relevant components — from codebase analysis}

**Current Technical Limitations:**
{What the current system can't do that this feature requires — from Q3, engineering perspective only}

## Approach

**Selected Strategy:**
{Chosen technical approach from 5f conversation — how existing strengths are leveraged}

**Why This Approach:**
{Technical rationale — performance, maintainability, consistency with existing patterns}

**Integration Points:**
{Which existing systems/components this feature touches}

## Architecture Decisions

_Documented as lightweight ADRs (Architecture Decision Records)._

### ADR-1: {Decision title}
**Context:** {Technical context that led to this decision}
**Decision:** {What we decided}
**Rationale:** {Why — technical reasons only}
**Alternatives:** {What else we considered and why we rejected it}
**Consequences:** {What this decision enables and what it constrains}

### ADR-2: {Decision title}
...

## Technical Requirements

_Derived from product requirements but expressed as system-level constraints._

- **TR-1:** The system shall handle [technical constraint] — traces to [REQ-U1]
- **TR-2:** The system shall integrate with [component/service] — traces to [REQ-E1]
- ...

## Dependencies & Risks

**External Dependencies:**
- {Service, library, API, or team we depend on — one bullet each}

**Technical Risks:**
- {Performance / scaling / security / data risks — one bullet each}

**Mitigation Strategies:**
{For each high-priority risk, how we plan to address it}

## Open Questions

_Technical uncertainties that need resolution before or during implementation._

- **Q-1:** {Technical question} — blocking: {yes/no} — owner: {name}
- **Q-2:** {Technical question} — blocking: {yes/no} — owner: {name}

---

## Cross-References
`product.md` (user-facing behavior) · `design-brief.md` (key interactions) · `TASKS.md` (breakdown)
```

### design-brief.md — Owner: Design

```markdown
# Design Brief: {feature}

_Generated by `/officeHours` on {ISO date}_
_Owner: Design Team_

## Experience Goals

**Desired Feeling:**
{Emotional qualities to achieve, from Q4 — e.g. fast, confident, effortless, reassuring}

**Anti-Goals (what this should NOT feel like):**
{Opposite qualities to avoid, from Q3 failure modes — e.g. confusing, slow, tedious, opaque}

## Key Moments to Design

_Ranked by importance — focus design effort on these interactions first._

### 1. {Interaction name}
**Why it matters:** {User impact}
**Current pain point:** {What goes wrong today — from Q3}
**Success looks like:** {Observable user behavior when this works well}

### 2. {Interaction name} — same shape, ranked next
...

## UX Principles for This Feature

_Specific design principles derived from the requirements and experience goals._

- **Principle 1:** {Design principle} — Example: "Always show progress during long operations"
- **Principle 2:** {Design principle} — Example: "Defaults should work for 80% of users"
- ...

## Design Constraints

_What must be true for this design to succeed — usability constraints only, no technical details._

- **C-1:** Users must be able to [action] in [N] clicks or fewer — traces to [REQ-U1]
- **C-2:** Error messages must [usability requirement] — traces to [REQ-W1]
- **C-3:** The interface must support [accessibility requirement] — traces to [REQ-U2]
- ...

## Scope Boundaries

**In Scope for Design:**
- {Interaction or screen to design}
- {Another interaction or screen}

**Out of Scope (future design work):**
- {Deferred interaction}
- {Deferred screen}

## Design Language

**Tokens & Patterns:**
{If design-tokens.json / .impeccable.md exist, reference them here}

Path: `design-tokens.json`, `.impeccable.md`

**Relevant Tokens for This Feature:**
- {Colors / typography / spacing / motion — e.g. "accent for primary actions", "150ms ease transitions"}

**Existing Patterns to Reuse:**
{If pattern discovery has run, reference discovered containers/providers/components}

---

## Cross-References
`product.md` (user-facing behavior) · `engineering.md` (integration points) · `TASKS.md` (breakdown)
```

### TASKS.md — Owner: Engineering (cross-team visibility)

```markdown
# Tasks: {feature}

_Generated by `/officeHours` on {ISO date}_
_Owner: Engineering Team — visible to all teams_

## Task Breakdown

_Tasks are ordered by dependency graph (topological sort). Each task points to its domain doc for implementation details._

---
id: TASK-1
domain: engineering
depends: []
complexity: small
reqs: [REQ-U1]
owner: unassigned
status: todo
---
## TASK-1: {title}

**What:** {One-sentence description of the task}
**Why:** {Which requirement(s) this implements}
**Definition of Done:** {Observable completion criteria}
**Reference:** See `engineering.md` [section or ADR] for implementation details

---
{TASK-2: same frontmatter + body shape — domain: design, Reference: design-brief.md}

---
id: TASK-3
domain: engineering
depends: [TASK-1, TASK-2]
complexity: large
reqs: [REQ-U2, REQ-E1]
owner: unassigned
status: blocked
---
## TASK-3: {title}

**What:** {One-sentence description}
**Why:** {Which requirement(s) this implements}
**Definition of Done:** {Observable completion criteria}
**Reference:** See `engineering.md` [section or ADR] for implementation details

...

## Task Summary

| Domain | Count | Complexity Breakdown |
|--------|-------|----------------------|
| Engineering | {N} | {e.g., "2 small, 3 medium, 1 large"} |
| Design | {N} | {e.g., "1 medium, 1 large"} |
| Product | {N} | {e.g., "1 small"} |

**Total Estimated Effort:** {e.g., "~18 hours (engineering), ~8 hours (design), ~1 hour (product)"}

## Dependency Graph

```mermaid
graph TD
  TASK-1[TASK-1: {title}]
  TASK-2[TASK-2: {title}]
  TASK-3[TASK-3: {title}]

  TASK-1 --> TASK-3
  TASK-2 --> TASK-3
```

---

## Cross-References
`product.md` (requirements) · `engineering.md` (implementation details) · `design-brief.md` (experience goals)
```

Task guidelines:
- Order tasks by dependency graph (topological sort)
- Complexity values: `small` (< 1 hour), `medium` (1-4 hours), `large` (4+ hours)
- The `domain` field is `engineering`, `design`, or `product`
- The `reqs` field traces each task back to one or more requirements
- Each task has `owner` (assigned name or "unassigned") and `status` (todo/in-progress/blocked/done)
- Each task should be atomic — one logical unit of work
- Task descriptions use "What/Why/Definition of Done/Reference" structure — NO implementation details in task body (those live in domain docs)

## Step 6.5: Spec Self-Check (always runs)

Before writing, evaluate the four drafted docs against the seven criteria below yourself and print the pass/fail table. This check is always on — no flag, no external service.

| # | Criterion | Pass? | Evidence (doc:section on ✗) |
|---|---|---|---|
| 1 | product.md contains no technical implementation details | ✓/✗ | … |
| 2 | engineering.md contains no user personas or business metrics | ✓/✗ | … |
| 3 | design-brief.md contains no technical constraints | ✓/✗ | … |
| 4 | TASKS.md references domain docs rather than duplicating implementation details | ✓/✗ | … |
| 5 | All selected EARS types present with ≥2 requirements each | ✓/✗ | … |
| 6 | Every product.md requirement traces to ≥1 acceptance criterion | ✓/✗ | … |
| 7 | TASKS.md dependency graph is topologically sorted with no cycles | ✓/✗ | … |

On any ✗: fix the flagged section and re-run the check (max 2 passes). If a ✗ remains, ask the user:
> "Self-check flagged: {failing criteria + evidence}. Fix before writing? (yes/no)"
If no: proceed to Step 7 and note the open issues in the Step 8 report.

**Optional adversarial delegation:** per `_shared/dark-factory.md` (officeHours doc-quality objective template) — gate-checked there; skip silently when not enabled. Delegated findings feed the same table above.

## Step 7: Write the Output Directory

Generate a URL-safe slug from the title (lowercase, hyphens, no special chars). Create the output directory and write all four files:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
mkdir -p "$AW_DIR/plans/${TIMESTAMP}-{slug}"
```

Write the four files to `$AW_DIR/plans/{timestamp}-{slug}/`: `product.md`, `engineering.md`, `design-brief.md`, `TASKS.md`. These filenames are the canonical manifest in `_shared/plan-layout.md` — downstream lenses resolve them via its reader matrix, so never rename them.

## Step 7.5: Write the consolidated plan summary

After the four domain docs are written, write `plans/{timestamp}-{slug}/plan.md` — a 1-page consolidated summary that downstream skills (`/autoplan`, `/planDesignReview`, `/planDevexReview`, `/cso --plan`, `/design-shotgun`) auto-discover per `_shared/plan-discovery.md`. This file is the canonical handoff — every plan-stage skill reads it first.

Structure:

```markdown
# Plan: <feature title>

**Generated:** <ISO date> by officeHours
**Feature dir:** plans/<feature>/

## TL;DR
<2-3 sentences capturing the feature in plain English>

## Problem
<from product.md — the why>

## Approach
<from engineering.md — the how>

## User experience
<from design-brief.md — the look/feel>

## Open questions
<bullet list — anything ambiguous>

## Sources consulted
<Provenance of external grounding: every planning doc, external reference (URL or
file path), and fetched URL actually read during Step 2 — one bullet each,
with what it contributed. If none: `None.`>

## Domain documents
- [Product](product.md) — owner: product
- [Engineering](engineering.md) — owner: engineering
- [Design brief](design-brief.md) — owner: design
- [Tasks](TASKS.md) — owner: tech lead

## Next steps
- `/autoplan` — fan out reviews across all 5 lenses in parallel
- `/productReview --mvp` — standalone product lens
- `/archReview` — standalone engineering lens
```

Write the file to:
- `$AW_DIR/plans/{timestamp}-{slug}/plan.md`

## Step 7.6: Traceability Self-Audit

Re-read the **written** product.md and verify every `REQ-*` ID appears in its Traceability table with at least one AC. Print:

| REQ | AC(s) | Covered? |
|---|---|---|
| REQ-U1 | AC-1 | ✓/✗ |
| … one row per requirement … | | |

**If any REQ is uncovered, the run has NOT succeeded.** Fix product.md (add the missing AC or the missing Traceability row), re-verify, and only then continue. Do not print the Step 8 success report while any REQ lacks an AC.

## Step 8: Report

Show a summary to the user:

```
Office Hours complete!

Plan written to: $AW_DIR/plans/{timestamp}-{slug}/
(filenames are canonical per _shared/plan-layout.md)

  plan.md          → Canonical handoff — 1-page consolidated summary for downstream skills
  product.md       → Product Team     — {N} requirements, {N} acceptance criteria, {N} success metrics
  engineering.md   → Engineering Team — {N} ADRs, {N} technical requirements, {N} open questions
  design-brief.md  → Design Team      — {N} key moments, {N} UX principles, {N} design constraints
  TASKS.md         → All Teams        — {N} tasks ({e.g. "4 engineering, 2 design, 1 product"}) | Est. effort: {e.g. "~18h eng, ~8h design"}

Separation of Concerns: ✓ zero crossover — each doc standalone; cross-references only for coordination

Summary:
  Problem: {one-line problem statement from Q1}
  User: {persona from Q2}
  Experience Goal: {primary feeling from Q4}
  MVP: {one-line scope summary from product.md}
  Key Metric: {primary success metric from product.md}
  Approach: {one-line technical strategy from engineering.md}

Suggested next steps:
  /productReview       — Product: Get founder-lens feedback on requirements and scope
  /archReview          — Engineering: Review ADRs and technical approach
  /design-mockup       — Design: Start mockup from design-brief.md (run /design-language first if needed)
  /addressReview TASKS — Engineering: Start implementation (references engineering.md for details)
```

### Sub-skill Dispatch

Present naturally at the end of the session:
> "Plan is ready. Would you like a review? I can fan out all five lenses in parallel with `/autoplan` (recommended), or run a single architectural or product review."

Based on response:
- Full review (recommended) → **Invoke skill `autoplan`**
- Architectural concerns only → **Invoke skill `archReview`**
- Product/founder lens only → **Invoke skill `productReview`**
- Neither → done

## Outputs

All under `$AW_DIR/plans/{timestamp}-{slug}/` (manifest per `_shared/plan-layout.md`):

- `plan.md` — canonical 1-page handoff (auto-discovered by `/autoplan`, `/planDesignReview`, `/planDevexReview`, `/cso --plan`, `/design-shotgun`)
- `product.md` — Product Team domain doc
- `engineering.md` — Engineering Team domain doc
- `design-brief.md` — Design Team domain doc
- `TASKS.md` — task breakdown (cross-team visibility)

## Next steps

- `/autoplan` — fan out parallel reviews across product, architecture, design, devex, security lenses
- `/design-shotgun` — generate mockup variants if the feature has a visual surface
- `/archReview` — standalone deep architecture review (if autoplan feels heavy)

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/officeHours-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then print the same JSON as a fenced `aw-result` block in your final reply, after the last tool call, before `## Next steps`.

Keys: `skill` (`officeHours`), `artifacts` (absolute paths: `plan.md` and the domain docs in `$AW_DIR/plans/<timestamp>-<slug>/`), `summary` (one line),
plus `plan_dir` (absolute path of the plan directory); `ears_types` (the EARS types the session selected, from `ubiquitous`, `event-driven`, `state-driven`, `optional`, `unwanted`); `requirements` (number of `REQ-*` ids in product.md); `tasks` (number of tasks in TASKS.md).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
