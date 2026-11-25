# Design: Memory Recall + Sub-skill Dispatch

**Date:** 2026-03-24
**Status:** Approved
**Scope:** `skills/_preamble.md`, 12 SKILL.md exception files, 4 orchestrator skills, 2 subagent prompt templates

---

## Problem

Skills don't prompt agents to retrieve prior conversation context from the agentic-bridge memory graph. When a session compacts or a new session begins, agents start fresh — even when prior discussions about the same problem space exist in memory. Additionally, the natural chains between skills (rootCause → bugHunt, officeHours → archReview, etc.) are not encoded, so agents never flow between skills without manual user prompting.

---

## Solution Overview

1. **Preamble `## Memory Recall` block** — added to `_preamble.md`, placed after the Bootstrap Check (so REPO_SLUG is available and BRIDGE_OK is confirmed). Runs on all 22 non-excepted skills.
2. **SKIP markers** — 12 mechanical/dispatcher/visual-diff skills opt out via `<!-- MEMORY: SKIP -->` placed immediately after the YAML frontmatter closing `---`, before the `<!-- === PREAMBLE START ===` line.
3. **Orchestrator trigger tables (A)** — 4 skills get `### Sub-skill Dispatch` sections; all 4 get `Skill` added to their `allowed-tools` frontmatter so the orchestrator itself can invoke sub-skills.
4. **Subagent prompt updates (B)** — `reviewer-prompt.md` and `implementer-prompt.md` get skill dispatch and memory recall steps.

### Note on MCP tool permissions

`mcp__agentic-bridge__search_memory` and `mcp__agentic-bridge__get_context` are registered globally at user scope (`claude mcp add --scope user`). Like Serena tools (which are used freely in the preamble without being listed in any skill's `allowed-tools`), globally-registered MCP tools are available in all skill contexts without enumeration in frontmatter. No per-skill `allowed-tools` changes are needed for the Memory Recall block.

---

## Section 1: Preamble Memory Recall Block

**Placement:** After the Bootstrap Check section (REPO_SLUG and BRIDGE_OK already established). Skip silently if `BRIDGE_OK=false` or skill is marked `<!-- MEMORY: SKIP -->`.

**Query strategy:** Topic-derived (agent synthesizes 3–5 words from argument + task intent).
**Tools:** `search_memory` (hybrid, limit 10) → `get_context`, run sequentially.
**Token budget:** 2000 default; 1000 for `/review` and `/addressReview` (large injected payloads).
**Output:** Surface `> **Prior context:** {summary}` callout if `relevance > 0.3`; continue silently on empty/low-relevance/error.

```markdown
## Memory Recall

> **Skip if** this skill is marked `<!-- MEMORY: SKIP -->`, or if `BRIDGE_OK=false`.

Check for prior discussion context in memory before reading the codebase.

**1. Derive a topic string** — synthesize 3–5 words from the skill argument and task intent:
- `/officeHours add dark mode` → `"dark mode UI feature"`
- `/rootCause TypeError cannot read properties` → `"TypeError cannot read properties"`
- `/review 42` → use the PR title once fetched: `"PR {title} review"`
- No argument → use the most specific descriptor available: `"{REPO_SLUG} {skill-name}"`

**2. Search memory:**
```
mcp__agentic-bridge__search_memory — query: <topic>, repo: REPO_SLUG, mode: "hybrid", limit: 10
```

**3. Assemble context:**
```
mcp__agentic-bridge__get_context — query: <topic>, repo: REPO_SLUG, token_budget: 2000
```
(Use `token_budget: 1000` for `/review` and `/addressReview`.)

**4. Surface results:**
- If `get_context` returns a non-empty summary or any section with `relevance > 0.3`:
  > **Prior context:** {summary} *(~{token_estimate} tokens)*
  Use this to inform your approach before continuing.
- If empty, all low-relevance, or any tool error: continue silently — do not mention the search.
```

---

## Section 2: Exception List

12 of 34 skills receive `<!-- MEMORY: SKIP -->`. **Canonical placement:** immediately after the YAML frontmatter closing `---`, before the `<!-- === PREAMBLE START ===` line.

### Dispatcher shells
Detect platform and re-invoke a sub-skill; the sub-skill does its own recall.

| Skill |
|-------|
| `/design-analyze` |
| `/design-mockup` |
| `/design-implement` |
| `/design-evolve` |
| `/design-verify` |
| `/verify-app` |

### Mechanical pipeline
No discussion phase; context retrieval adds no value.

| Skill |
|-------|
| `/shipRelease` |
| `/syncDocs` |
| `/postReview` |
| `/design-refine` |

### Visual diff
Pixel-level comparison, not discussion-driven.

| Skill |
|-------|
| `/design-verify-web` |
| `/design-verify-ios` |

### Deliberate non-exceptions

- **`/weeklyRetro`** — kept in the recall set. Prior retro summaries in memory are useful for trend analysis and shipping streak continuity across sessions.
- **`/verify-web`, `/verify-ios`** — kept in the recall set. These are interactive verification sessions (Playwright / XcodeBuildMCP), not pixel diffs. Prior verification findings are useful context.

---

## Section 3: Orchestrator Sub-skill Dispatch (A)

Four skills get a `### Sub-skill Dispatch` section at the natural "what next?" moment. All four get `Skill` added to their `allowed-tools` frontmatter so the orchestrator agent can invoke sub-skills directly.

### `/rootCause` — after Phase 4, only on unfixed or scope-breach status

If rootCause exhausted all hypotheses without a confirmed fix, escalate to bugHunt's broader fix-and-verify loop:

```markdown
### Sub-skill Dispatch

If Phase 4 ends with status `unfixed` or `scope-breach`:
> Skill tool: `bugHunt`, args: `"<error slug from Step 1>"`

Do not invoke bugHunt if the fix was verified — rootCause's own report is sufficient on success.
```

### `/bugHunt` — after fix-and-verify loop, only on unfixed status

```markdown
### Sub-skill Dispatch

If the fix-and-verify loop ends with status `unfixed` (all hypotheses exhausted):
> Skill tool: `bugReport`

Do not invoke bugReport on success — bugHunt's own Step 8 report is sufficient.
```

### `/officeHours` — after the design doc is written (final step)

Ask conversationally (do not use AskUserQuestion — follow the existing officeHours interaction pattern):

```markdown
### Sub-skill Dispatch

Present naturally at the end of the session:
> "Plan is ready. Would you like a review? I can run an architectural review, a product review, or both."

Based on response:
- Architectural concerns → Skill tool: `archReview`
- Product/founder lens → Skill tool: `productReview`
- Both → invoke in sequence: `archReview` then `productReview`
- Neither → done
```

### `/review` — after collecting reviewer outputs, before writing the state file

Cap at one investigation. `Skill` is added to `/review`'s `allowed-tools` because the orchestrator itself runs the confirmation step and dispatches rootCause — reviewers flag the need via `investigation_needed` in their JSON output; the orchestrator decides.

```markdown
### Sub-skill Dispatch

After collecting all reviewer JSON outputs:
1. Check for any reviewer output with `investigation_needed: true`
2. If found: identify the single highest-confidence entry (blocking severity + clearest error trace)
3. Ask the user (conversationally):
   > "Found a blocking bug with a stack trace in {path}. Run rootCause to investigate? (yes/no)"
4. If yes: Skill tool: `rootCause`, args: `"<investigation_error value>"`
   - Attach the investigation file path to that issue in the state file under `"investigation"`
   - If rootCause returns `scope-breach`, note it in the state file and continue — do not block the review
5. If no, or no reviewer flagged `investigation_needed`: skip and proceed to writing the state file
```

---

## Section 4: Subagent Prompt Updates (B)

Reviewer subagents spawned via the Agent tool (general-purpose type) inherit access to all tools including the Skill tool — independent of the parent skill's `allowed-tools`. No frontmatter changes are needed for subagent Skill access.

### `skills/review/reviewer-prompt.md` — new Step 4

Added after the Analyze section, before the Output Format section. The subagent flags the need — it does **not** invoke rootCause directly. The orchestrator gates on user confirmation and invokes once after all reviewers complete.

```markdown
### 4. Flag investigation candidates

If you find a `blocking`-severity issue containing a clear error message or stack trace,
set `investigation_needed: true` in your JSON output and include the stack trace or error message:

```json
"investigation_needed": true,
"investigation_error": "<the exact error message or stack trace>"
```

Do not invoke rootCause yourself. The orchestrator will confirm with the user and dispatch the investigation after all reviewers complete.

If no blocking bugs with error traces are found, omit both fields.
```

### `skills/addressReview/implementer-prompt.md` — new Step 0

Added before Step 1 (reading the PR). `{REPO_SLUG}` is injected as a template variable by the `addressReview` orchestrator at Step 6 alongside the existing `{agent}`, `{focus}`, `{issues}`, `{number}`, `{owner}`, `{repo}`, `{branch}` variables.

```markdown
### 0. Check memory for prior context

Before reading the PR, search for prior discussions about the files or issues you're about to address:

```
mcp__agentic-bridge__search_memory — query: "<issue summary>", repo: {REPO_SLUG}, mode: "hybrid", limit: 5
```

Use any relevant prior context to inform your implementation approach. Continue silently if nothing relevant is found or if the tool returns an error.
```

---

## Files Changed

| File | Change |
|------|--------|
| `skills/_preamble.md` | Add `## Memory Recall` section (after Bootstrap Check) |
| `skills/design-analyze/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/design-mockup/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/design-implement/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/design-evolve/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/design-verify/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/verify-app/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/shipRelease/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/syncDocs/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/postReview/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/design-refine/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/design-verify-web/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| `skills/design-verify-ios/SKILL.md` | Add `<!-- MEMORY: SKIP -->` after frontmatter `---` |
| All 22 non-SKIP SKILL.md files | Propagate updated preamble with Memory Recall block (includes rootCause, bugHunt, officeHours, review) |
| `skills/rootCause/SKILL.md` | Add `Skill` to `allowed-tools`; add `### Sub-skill Dispatch` |
| `skills/bugHunt/SKILL.md` | Add `Skill` to `allowed-tools`; add `### Sub-skill Dispatch` |
| `skills/officeHours/SKILL.md` | Add `Skill` to `allowed-tools`; add `### Sub-skill Dispatch` |
| `skills/review/SKILL.md` | Add `Skill` to `allowed-tools`; add `### Sub-skill Dispatch` |
| `skills/review/reviewer-prompt.md` | Add Step 4: Sub-skill dispatch |
| `skills/addressReview/SKILL.md` | Inject `{REPO_SLUG}` into implementer subagent template vars at Step 6 |
| `skills/addressReview/implementer-prompt.md` | Add Step 0: Memory check |
