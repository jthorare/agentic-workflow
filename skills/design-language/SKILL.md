---
name: design-language
description: Interactive session defining brand personality, aesthetic direction, and design principles. Accepts reference URLs (Figma, Storybook, HTML mockups, any public page), analyzes them via Playwright, then runs a gap-filling Q&A to produce both design-tokens.json and .impeccable.md in one step.
argument-hint: [url1 url2 ...]
allowed-tools: Bash(git *), Bash(SHARED_DIR=*), Read, Write, Glob, AskUserQuestion, mcp__plugin_playwright_playwright__browser_navigate, mcp__plugin_playwright_playwright__browser_take_screenshot, mcp__plugin_playwright_playwright__browser_snapshot, mcp__plugin_playwright_playwright__browser_evaluate, mcp__plugin_playwright_playwright__browser_close, mcp__plugin_playwright_playwright__browser_wait_for, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (provider capability map, reply voice). Unless `${AW_PREAMBLE:-on}` is `off`, also read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble-extended.md` (skill index, session context) and run its **Session Close** section when this skill finishes.

<!-- design-preamble -->
**Design context:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_design-preamble.md` and follow it before continuing.

# Design Language — Define Brand Personality

Interactive session that defines brand personality, aesthetic direction, and design principles. Accepts optional reference URLs and uses Playwright to extract design tokens and personality signals before asking strategic questions.

Produces two output files: `design-tokens.json` (W3C DTCG token set) and `.impeccable.md` (brand personality doc for AI consumption).

---

## Phase 1: URL Analysis (skip if no URLs provided)

If no URLs were provided as arguments, skip to Phase 2.

### 1.0 Acquire the browser lock

Only one skill drives the shared Playwright browser at a time. Acquire the lock in a **single Bash invocation** (shell state does not persist between calls) before any navigation:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
source "$SHARED_DIR/repo-slug.sh"
mkdir -p "$AW_DIR/design"
LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; acquire_lock
```

If acquisition times out, stop and report the holding pid. Note the printed `AW_DIR` — reference screenshots are saved beneath it.

When Phase 1 is complete (all URLs analyzed), release in a single invocation:

```bash
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
LOCK_NAME=browser source "$SHARED_DIR/skill-lock.sh"; release_lock
```

### 1.1 Warn the user before opening any browser

> "I'll open these URLs in a browser via Playwright. If any require authentication (e.g., Figma design files), I'll pause so you can log in before I proceed."

### 1.2 For each URL

1. Navigate: `mcp: playwright/browser_navigate` with `{ url: "<the-url>" }`
   - **Figma login gate:** if a login screen appears, call `mcp: playwright/browser_wait_for` for a post-login element (the file canvas: `canvas`, or the toolbar filename node) with a generous timeout. If it times out, **ask the user**: "Have you finished logging in to Figma? (Done / Skip this URL)" — on Done, retry the wait; on Skip, move to the next URL.
2. Screenshot to disk: `mcp: playwright/browser_take_screenshot` with `{ filename: "<AW_DIR>/design/reference-<host>.png" }` — the **absolute** path under the `AW_DIR` printed in 1.0, `<host>` = URL hostname (e.g. `linear.app`). This baseline reference is persisted and listed in `.impeccable.md ## Sources`.
3. Snapshot (structure): `mcp: playwright/browser_snapshot` — this returns the accessibility tree (component names, roles, patterns). It does **not** contain CSS values.
4. Computed styles: `mcp: playwright/browser_evaluate` with this function — the only reliable source for color/typography/spacing values:

```js
() => {
  const sels = ["body","h1","h2","h3","h4","h5","h6","button","a","[class*=card]"];
  const out = {};
  for (const s of sels) {
    const el = document.querySelector(s);
    if (!el) continue;
    const cs = getComputedStyle(el);
    out[s] = {
      color: cs.color, background: cs.backgroundColor,
      fontFamily: cs.fontFamily, fontSize: cs.fontSize, fontWeight: cs.fontWeight,
      lineHeight: cs.lineHeight, padding: cs.padding, margin: cs.margin,
      borderColor: cs.borderColor, borderRadius: cs.borderRadius, boxShadow: cs.boxShadow,
      transitionDuration: cs.transitionDuration, transitionTimingFunction: cs.transitionTimingFunction
    };
  }
  return JSON.stringify(out, null, 2);
}
```

Extract from these sources: **colors** (background/text/border/accent), **typography** (families, sizes, line heights, weights), **spacing** (padding/margin/gap values; screenshot for grid rhythm), **radii**, and **motion** (durations, easing) — all from the `browser_evaluate` computed-style sample; **components** (names, structure, patterns) from the `browser_snapshot` accessibility tree.

### 1.3 Synthesize across all URLs

Identify shared patterns; on conflict prefer the first URL (primary reference); note which URL each token came from.

### 1.4 Build drafts

Build an in-memory draft `design-tokens.json` (W3C DTCG) and a partial `.impeccable.md` with confident aesthetic signals — presented during Phase 2 Q&A.

---

## Phase 2: Gap-Filling Q&A

Ask the user these questions interactively via **Ask the user**, one group at a time. **Pre-fill answers** where URL analysis provided a confident signal and present them as suggestions; **skip questions** fully answered by URL analysis (e.g. all URLs clearly dark-mode only); **always ask** anything not determinable visually: primary users, core purpose, emotional response, anti-references, WCAG level.

### Group 1: Users & Purpose
- Who are your primary users?
- What is the core purpose of this product?
- What emotional response should the design evoke?

### Group 2: Brand Personality
- Describe your brand in 3 words (e.g., "precise, warm, confident")
- Name 1–3 reference products/sites whose aesthetic you admire *(skip if URLs were provided as arguments — those are already your references)*
- Name 1–3 anti-references — aesthetics you want to avoid and why

### Group 3: Aesthetic Direction
- Style direction: minimal, expressive, editorial, brutalist, organic, other?
- Light mode, dark mode, or both? *(skip if unambiguous from URL analysis)*
- Color constraints: existing brand colors to preserve? Accessibility requirements?

### Group 4: Technical Context
- Target platforms: web only, iOS only, or both?
- WCAG compliance level: A, AA, or AAA?
- Any specific framework constraints (Tailwind, SwiftUI, etc.)?

---

## Phase 3: Write Output Files

### 3.1 Check for existing files

If `design-tokens.json` or `.impeccable.md` already exists, ask "`<file>` already exists. Overwrite? (yes/no)" — only overwrite on confirmation.

### 3.2 Write `design-tokens.json`

W3C DTCG format with values from URL analysis, confirmed or adjusted during Q&A. Include all token categories that were resolved (colors, typography, spacing, radii, motion). Omit categories with no confident values rather than leaving placeholder strings.

Example shape:
```json
{
  "color":   { "accent": { "$value": "#6366f1", "$type": "color" } },
  "font":    { "family-mono": { "$value": "JetBrains Mono, monospace", "$type": "fontFamily" } },
  "spacing": { "s2": { "$value": "8px", "$type": "dimension" } },
  "radius":  { "md": { "$value": "8px", "$type": "dimension" } }
}
```

### 3.3 Write `.impeccable.md`

```markdown
# Design Language

> This file defines brand personality and aesthetic direction for AI-assisted design.
> It is consumed by Impeccable commands and the `/design-*` skill pipeline.
> See `planning/DESIGN_SYSTEM.md` for strategic design decisions and component catalog.
> See `design-tokens.json` for machine-readable token values.

## Sources

- <url1> (screenshot: $AW_DIR/design/reference-<host1>.png) — <one-line note on what was extracted from it>
- <url2> (screenshot: $AW_DIR/design/reference-<host2>.png) — <one-line note on what was extracted from it>

## Brand Personality

**Three words:** [word1], [word2], [word3]

**Voice:** [description of the brand's visual voice]

## Aesthetic Direction

**Style:** [minimal/expressive/editorial/etc.]

**References:**
- [reference 1] — [what to take from it]
- [reference 2] — [what to take from it]

**Anti-references:**
- [anti-ref 1] — [what to avoid and why]

## Color
[Philosophy and constraints — exact values in design-tokens.json]

## Typography
[Scale, hierarchy, font personality]

## Spacing & Layout
[Dense vs. generous, grid approach]

## Motion
[Purpose, duration, easing preferences]

## Accessibility
**WCAG level:** [A/AA/AAA] [additional commitments]

## Platform Notes
[Web-specific, iOS-specific, or cross-platform considerations]
```

Omit the `## Sources` section entirely if no URLs were provided.

### 3.4 Write minimal `planning/DESIGN_SYSTEM.md`

`.impeccable.md` points at `planning/DESIGN_SYSTEM.md` — this skill is its creator. If it does not exist, write this minimal version (create `planning/` if needed):

```markdown
# Design System

> Strategic design decisions and component catalog for this project.
> Token values live in `design-tokens.json`; brand personality in `.impeccable.md`.

## Design Principles

- [derived from Phase 2 answers — one bullet per principle]

## Component Catalog

| Component | Status | Notes |
|-----------|--------|-------|
| _none yet_ | — | Populated as components are designed via /design-mockup and /design-implement |
```

If `planning/DESIGN_SYSTEM.md` already exists, leave it unchanged.

---

## Phase 4: Approval Gate

Present the written `.impeccable.md` content, then **ask the user** (max **5 rounds**):

- **Approve** — the design language is accepted as written
- **Revise** — the user describes changes; incorporate them, re-save, and re-present

On **Approve**, record the approval inside `.impeccable.md`, directly under the title:

```markdown
approved: <ISO-8601 timestamp, e.g. 2026-07-27T15:04:05Z>
```

Downstream skills gate on this line. If 5 rounds pass without Approve, stop, list the still-disputed sections, and do **not** write the `approved:` line.

---

## Rules

- Preserve all existing content from `DESIGN_SYSTEM.md` — `.impeccable.md` complements, not replaces
- Be specific in descriptions — "clean and minimal" is too vague; "generous whitespace, muted colors, SF Pro typography with tight leading" is useful
- If `design-tokens.json` exists (and user confirmed overwrite), the new file replaces it entirely — don't partially merge
- Do not write placeholder values — if a token wasn't determined, omit the key

## Next steps

- `/design-mockup` — build the first mockup from the new language
- `/design-shotgun` — explore variants before committing to a direction
