---
name: archReview
description: "Use BEFORE building from a plan or design doc, or whenever someone asks if a technical design will hold up: component boundaries, data flow, failure modes, load. Draws mermaid diagrams, fills an edge-case matrix and rates SOUND / NEEDS WORK / REDESIGN. One lens only; not for product scope, threat models or UI critique."
argument-hint: "[--plan <path> | plan-file-or-directory-to-review] [--output <path>] [--slim]"
allowed-tools: Bash(git *), Bash(ls *), Bash(date *), Bash(mkdir *), Bash(SHARED_DIR=*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

# Architecture Review — Engineering Lens

Reviews plans or implementations for technical soundness. Produces mandatory mermaid diagrams, edge case analysis, and a scored verdict.

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## Step 1: Resolve the Target

**Parse flags first:** `--plan <path>` (equivalent to the positional path), `--output <path>` (explicit output file — `/autoplan` passes `--output <feature-dir>/arch-review.md`), `--slim` (compact output: matrix, scores, and verdict only).

**If a file path is given**, read that file as the plan/spec to review.

**If a directory is given** and it is a plan dir, read `plan.md` + `engineering.md` + `TASKS.md` — the reader matrix in `_shared/plan-layout.md`. Legacy aliases per `_shared/plan-layout.md`: fall back to `design.md` (→ engineering.md) and `requirements.md` (→ product.md) only if the canonical file is absent. If it is a code directory, explore its structure by searching and reading files.

**If nothing is given**, try two fallbacks in order:
1. Auto-discover the most recent plan per `_shared/plan-discovery.md`:
   ```bash
   SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
   source "$SHARED_DIR/repo-slug.sh"
   PLAN_DIR=$(ls -1dt "$AW_DIR/plans/"*/ 2>/dev/null | head -1)
   ```
   Resolve a found plan dir per the reader matrix above; a legacy single `.md` file is read as-is.
2. If no plans exist, review the current project's architecture by exploring the repository root.

## Step 2: Read Context

Read all available architectural context:

- `AGENTS.md` (or `CLAUDE.md` if that is all the repo has) — project conventions and structure
- `planning/ARCHITECTURE.md` or `ARCHITECTURE.md` — existing architecture docs
- `planning/ERD.md` or `ERD.md` — data model
- `planning/API_CONTRACT.md` or `API_CONTRACT.md` — API surface
- `README.md` — project overview

Search files to discover these -- do not assume paths.

## Step 3: Architecture Analysis

**Spawn a subagent** — one read-only explore agent (Claude Code: `subagent_type: "Explore"`; Codex/Cursor: an explorer/read-only agent type if available, else default), search breadth "very thorough" — with this prompt:

> "Map the system at `<target>`. Read source files, configuration, and package manifests. Report, with file:line evidence for each: (1) component boundaries — a **named list** of distinct modules/services and where the boundaries are drawn; (2) dependency graph, including circular dependencies; (3) data flow paths — how data enters, transforms, exits; (4) external integrations; (5) state management — where state lives, how it syncs, source of truth; (6) error propagation across boundaries — handled or swallowed."

**Output contract:** the agent must return the named boundary list (it drives the Step 5 matrix rows) plus the six areas above. **Fallback:** if subagent spawning is unavailable or returns nothing usable, do the same exploration directly (search and read files yourself), producing the same named boundary list.

## Step 4: Generate Mandatory Diagrams

Create three mermaid diagrams. These are **mandatory** -- the review is incomplete without them. Every diagram must satisfy two assertions:

- **≥3 nodes named after real components** from Step 3 (actual module, file, service, or table names — e.g. `cli.ts`, `ledger.db`, `decisions.sqlite`)
- **No placeholder labels** — generic labels such as "Component A", "Service", "Input", "Transform", "Store" do not count; a diagram containing them is invalid and the review is incomplete.

### 4a: Component Diagram
Each module/service as a box with dependency arrows: internal components and responsibilities, external dependencies (databases, APIs, file system), direction of dependency.

### 4b: Data Flow Diagram
How data moves from entry to exit: input sources (user, API, file, event), transformation steps, storage points, output destinations.

### 4c: Sequence Diagram
The single most critical user flow end-to-end: all participants, request/response pairs, and the error path for the main flow.

## Step 5: Edge Case Matrix

Build a **boundary × failure-mode matrix** — one row per component boundary from Step 3's named list, one column per failure mode. Every cell is either a finding id (defined below the table) or `OK — <evidence>` (file:line, config value, or plan-line proving the case is handled). **No blank cells** — an unexamined cell is itself a finding. One covered boundary must never masquerade as full coverage.

| Boundary | Dependency unavailable | Invalid/malicious input | Load 10x | State leakage |
|---|---|---|---|---|
| {boundary 1} | F1 or `OK — <evidence>` | … | … | … |
| {…one row per Step-3 boundary…} | | | | |

Failure-mode definitions:
- **Dependency unavailable** — timeout? retry? fallback? does the failure cascade or is it contained?
- **Invalid / malicious input** — unexpected types, missing fields, oversized payloads; validation at the boundary or deep inside; injection vectors (SQL, command, path traversal)?
- **Load 10x** — first bottleneck (CPU, memory, I/O, connections); unbounded queues, caches, buffers?
- **State leakage** — cross-request/user leakage; shared mutable globals; guaranteed cleanup (connections, file handles, temp files)?

Findings (F1, F2, …): severity per `_shared/severity.md`, description, location, concrete mitigation.

## Step 5.5: Review Self-Check

Verify your own evidence: for every `OK — <evidence>` cell in the matrix, confirm the cited code actually exists by searching and reading it (grep the named timeout, validator, handler, or config key). Downgrade any cell whose evidence does not hold to a finding.

**Optional adversarial delegation:** per `_shared/dark-factory.md` (archReview review-gap objective template) — gate-checked there; skip silently when not enabled. Incorporate delegated findings into the matrix and Top Risks before writing the verdict.

## Step 6: Review Verdict

Produce the final assessment. The verdict is **rule-derived** from the matrix findings and scores (anchored 1–5 scale per `_shared/severity.md`):
- any CRITICAL finding, or any dimension scored 1 ⇒ **REDESIGN**
- any HIGH finding, or any dimension scored ≤2 ⇒ **NEEDS WORK**
- otherwise ⇒ **SOUND**

```markdown
# Architecture Review: {title}

_Reviewed by `/archReview` on {ISO date}_

## Verdict: {SOUND | NEEDS WORK | REDESIGN}

{One paragraph justification citing the finding ids / dimension scores that drove it}

## Scores

_Anchored 1–5 scale from `_shared/severity.md` (5 = no findings above LOW … 1 = CRITICAL or unaddressed)._

| Dimension | Score (1–5) | Notes |
|-----------|:---:|-------|
| Complexity | {n} | {brief justification} |
| Scalability | {n} | {brief justification} |
| Maintainability | {n} | {brief justification} |

## Component Diagram

{mermaid diagram from 4a}

## Data Flow Diagram

{mermaid diagram from 4b}

## Sequence Diagram

{mermaid diagram from 4c}

## Top Risks

| # | Risk | Impact | Likelihood | Mitigation |
|---|------|--------|------------|------------|
| 1 | {risk} | {high/med/low} | {high/med/low} | {recommendation} |
| 2 | {risk} | {high/med/low} | {high/med/low} | {recommendation} |
| ... | ... | ... | ... | ... |

## Edge Case Matrix

{the Step 5 boundary × failure-mode matrix, followed by its findings list — empty findings list is the literal `None.`}

## Missing Error Handling
- {specific location and what's missing}
- {specific location and what's missing}

## Suggested Improvements (Prioritized)

_Severity per `_shared/severity.md` (legacy P0→CRITICAL, P1→HIGH)._

| Severity | Improvement | Effort | Impact |
|----------|------------|--------|--------|
| CRITICAL | {must fix before shipping} | {S/M/L} | {description} |
| HIGH | {should fix soon} | {S/M/L} | {description} |
| MEDIUM/LOW | {nice to have} | {S/M/L} | {description} |
```

End the file with the normalized final line per `_shared/severity.md` (CD9 mapping: SOUND→PASS, NEEDS WORK→NEEDS_WORK, REDESIGN→BLOCKED):

```
verdict: <PASS|NEEDS_WORK|BLOCKED>
```

## Step 7: Write the Review

If `--output <path>` was provided, write to that exact path (ensure the parent dir exists with `mkdir -p`). Otherwise generate a URL-safe slug from the target title (lowercase, hyphens, no special chars) and write to the default path:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
```

Default: `$AW_DIR/plans/{timestamp}-arch-review-{slug}.md`

Include all three mermaid diagrams and the complete analysis.

## Step 8: Report

Show a summary to the user:

```
Architecture Review complete!

Verdict: {SOUND | NEEDS WORK | REDESIGN} (normalized: {PASS | NEEDS_WORK | BLOCKED})

Scores:
  Complexity:      {n}/5
  Scalability:     {n}/5
  Maintainability: {n}/5

Review written to: {resolved output path}

Top 3 risks:
  1. {risk summary}
  2. {risk summary}
  3. {risk summary}

Suggested next steps:
  /productReview — Get founder-lens feedback on the plan
  /officeHours — Brainstorm solutions to identified risks
```

## Next steps

- `/autoplan` — run alongside other plan-review lenses in parallel
- `/review` — deep code review of the implementation

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/archReview-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`archReview`), `artifacts` (absolute paths: the architecture review file (`$AW_DIR/plans/<timestamp>-arch-review-<slug>.md`, or the `--output` path)), `summary` (one line),
plus `verdict` (`PASS | NEEDS_WORK | BLOCKED`, copied from the report's last `verdict:` line); `review_verdict` (`SOUND | NEEDS WORK | REDESIGN`, the report's `## Verdict` word).

The block is the result file's JSON copied unchanged, never retyped from memory: the same keys and the same absolute paths (Read the file back if it is no longer in view). Its shape:

```aw-result
{ "skill": "archReview", "artifacts": ["<absolute arch review path>"], "summary": "<one line>", "verdict": "PASS | NEEDS_WORK | BLOCKED", "review_verdict": "SOUND | NEEDS WORK | REDESIGN" }
```

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
