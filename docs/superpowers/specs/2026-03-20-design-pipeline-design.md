# Design-to-Implementation Pipeline

> Structured design workflow: analyze reference sites, define a design language, generate mockups, implement across web and SwiftUI, refine with Impeccable commands, and verify with screenshot diffing.

## Problem

The current design-to-implementation workflow is unstructured and token-expensive. Each session requires re-describing aesthetics, ad-hoc prompting for mockups, manual tool coordination for verification, and no consistent way to maintain a design language across web and iOS targets. The result is low first-pass quality, inconsistent output, and high cognitive overhead.

## Goals (ordered by priority)

1. **Higher first-pass quality** — AI has full design context at every step, reducing iteration
2. **Repeatable, structured workflow** — invoke named skills instead of ad-hoc prompting
3. **Token efficiency** — design language persists across sessions, eliminating re-description

## Design Language Artifacts

Three files, complementary roles:

| File | Format | Purpose | Lifecycle |
|------|--------|---------|-----------|
| `planning/DESIGN_SYSTEM.md` | Markdown | Strategic design decisions, principles, component catalog | Written by `/bootstrap` or manually; upstream reference |
| `.impeccable.md` | Markdown | Brand personality, aesthetic direction, anti-references, design principles for AI consumption | Written by `/design-language`; references `DESIGN_SYSTEM.md` |
| `design-tokens.json` | W3C DTCG JSON | Machine-readable tokens: colors, typography, spacing, radii, elevation, motion | Written by `/design-analyze`; consumed by Design Token Bridge MCP |

Relationship:

```
planning/DESIGN_SYSTEM.md  (what and why — principles, component catalog)
    |  referenced by
    v
.impeccable.md             (brand personality, aesthetic direction for AI)
design-tokens.json         (exact values — colors, typography, spacing)
```

Both `.impeccable.md` and `design-tokens.json` live at the project root and are checked into git. They are living documents — `/design-evolve` updates them incrementally.

## Tool Installation

### New MCP Servers

| Server | Install | Purpose |
|--------|---------|---------|
| Design Token Bridge | `npx -y design-token-bridge-mcp` | Extract tokens from CSS/Tailwind/Figma/JSON; generate SwiftUI themes, CSS vars, Tailwind config; validate WCAG contrast |

### New CLI Tools

| Tool | Install | Purpose |
|------|---------|---------|
| Dembrandt | `npm install -g dembrandt` (installed by `setup.sh`) | Analyze live websites; extract design tokens as W3C DTCG JSON |

### New Skills

| Skill | Source | Purpose |
|-------|--------|---------|
| Impeccable (20 commands) | Copy from `pbakaus/impeccable` dist/claude-code | Design refinement: `/colorize`, `/animate`, `/polish`, `/typeset`, `/arrange`, etc. |
| 7 `/design-*` skills | Custom, built in this repo | Pipeline orchestration (see below) |

### Optional

| Server | Install | Purpose |
|--------|---------|---------|
| Figma MCP | Official remote endpoint or Figma Desktop | Read Figma designs, extract variables — if you use Figma |
| Mockuuups MCP | Remote endpoint | Generate professional device frame mockups from screenshots |

### Already in Place (no changes)

- Playwright MCP (web screenshots/automation)
- mobai MCP (iOS simulator automation)
- design-comparison MCP (pixel diffing)
- compound-engineering design agents (design-iterator, design-implementation-reviewer, figma-design-sync)
- gemini-imagegen skill

## Seven `/design-*` Skills

Individual skills with `design-` prefix, each visible via `/skills`:

```
~/.claude/skills/
├── design-analyze/SKILL.md
├── design-language/SKILL.md
├── design-evolve/SKILL.md
├── design-mockup/SKILL.md
├── design-implement/SKILL.md
├── design-refine/SKILL.md
└── design-verify/SKILL.md
```

### Skill Definitions

#### `/design-analyze`

- **Argument:** `<url> [url2...]` (one or more reference site URLs)
- **Purpose:** Run Dembrandt on reference sites, synthesize extracted tokens and AI analysis into a draft design language
- **Process:**
  1. Run `npx dembrandt <url> --dtcg --save-output` for each URL
  2. If `--dark-mode` variants are needed, run again with that flag
  3. Merge extracted tokens across sites (resolve conflicts by frequency/prominence)
  4. AI synthesizes patterns: what's shared across references, what's distinctive
  5. Write `design-tokens.json` to project root
  6. Present summary for review
- **Output:** `design-tokens.json` (W3C DTCG format)

#### `/design-language`

- **Argument:** none
- **Purpose:** Interactive session defining brand personality and aesthetic direction
- **Process:**
  1. Read `planning/DESIGN_SYSTEM.md` if it exists (upstream context)
  2. Read `design-tokens.json` if it exists (extracted values)
  3. Ask strategic questions (similar to `/teach-impeccable`):
     - Users and purpose
     - Brand personality (3-word personality, references, anti-references)
     - Aesthetic preferences (style direction, light/dark mode, color constraints)
     - Accessibility requirements (WCAG level)
  4. Write `.impeccable.md` with references to `DESIGN_SYSTEM.md` and `design-tokens.json`
- **Output:** `.impeccable.md`

#### `/design-evolve`

- **Argument:** `<url>` (new reference site)
- **Purpose:** Analyze a new reference site mid-project and merge into existing design language
- **Process:**
  1. Read existing `.impeccable.md` and `design-tokens.json`
  2. Run Dembrandt on new URL
  3. Present diff: what the new site adds or changes vs. current tokens
  4. Ask which elements to adopt, adapt, or ignore
  5. Update `design-tokens.json` and `.impeccable.md` incrementally
- **Output:** Updated `design-tokens.json` and `.impeccable.md`

#### `/design-mockup`

- **Argument:** `<screen-name>` (e.g., "dashboard", "login", "settings")
- **Purpose:** Generate an HTML mockup using the visual companion, informed by the design language
- **Allowed tools:** `Bash(*/start-server.sh *)`, `Write`, `Read`
- **Process:**
  1. Load `.impeccable.md` and `design-tokens.json`
  2. Generate HTML mockup as a content fragment for the visual companion
  3. Save to `.superpowers/brainstorm/` session directory
  4. Present in browser for review
  5. Iterate based on feedback until approved
  6. Screenshot the approved mockup as baseline for `/design-verify`
- **Output:** HTML mockup file, baseline screenshot saved to `~/.agentic-workflow/<repo-slug>/design/`

#### `/design-implement`

- **Argument:** `<target>` — `web` or `swiftui`
- **Purpose:** Generate production code from an approved mockup
- **Process:**
  1. Load `.impeccable.md` and `design-tokens.json`
  2. List approved mockups from `~/.agentic-workflow/<repo-slug>/design/`; if multiple exist, present the list and ask which one to implement
  3. Call Design Token Bridge MCP to generate platform-specific token files:
     - `web` → `generate_css_variables` + `generate_tailwind_config`
     - `swiftui` → `generate_swiftui_theme`
  4. Generate component/view code using the token files and Impeccable's frontend-design reference
  5. Commit generated token files (`tokens.css`, `tailwind.preset.js`, or `Theme.swift`)
- **Output:** Production code + generated token files

#### `/design-refine`

- **Argument:** `[impeccable-command]` (optional — e.g., `colorize`, `animate`, `polish`, `typeset`, `arrange`)
- **Purpose:** Dispatch Impeccable refinement commands with design language context pre-loaded
- **Process:**
  1. Load `.impeccable.md` and `design-tokens.json`
  2. If no command specified, analyze current implementation and suggest which refinements would help most
  3. If command specified, invoke via `Skill` tool (e.g., `Skill(colorize)`) with design context pre-loaded
  4. If token changes result from refinement, update `design-tokens.json`
- **Output:** Refined code, potentially updated `design-tokens.json`

#### `/design-verify`

- **Argument:** none
- **Purpose:** Screenshot the implementation, diff against the mockup baseline, report discrepancies
- **Process:**
  1. Detect target via heuristics: `Package.swift`/`.xcodeproj`/`*.xcworkspace` → iOS; `package.json` with Next.js/React/Vue → web; both present → both
  2. Capture implementation screenshots:
     - Web → Playwright MCP (`browser_navigate`, `browser_take_screenshot` at mobile/tablet/desktop viewports)
     - iOS → mobai MCP (`get_screenshot`)
  3. Retrieve baseline mockup screenshot from `~/.agentic-workflow/<repo-slug>/design/`
  4. Diff via design-comparison MCP (`compare_design`)
  5. Report:
     - Pass (< 2% diff): "Implementation matches mockup"
     - Minor (2-10% diff): list specific discrepancies, suggest fixes
     - Major (> 10% diff): side-by-side diff image, prioritized fix list
  6. If major diff, suggest dispatching `design-iterator` agent for automated multi-pass refinement
- **Output:** Verification report, diff images saved to `~/.agentic-workflow/<repo-slug>/design/`

### Shared Preamble

All seven skills share a context-loading preamble (referenced from each SKILL.md):

1. Read `.impeccable.md` if it exists
2. Read `design-tokens.json` if it exists
3. Read `planning/DESIGN_SYSTEM.md` if it exists
4. If none exist and the skill requires design context, advise running `/design-analyze` and `/design-language` first

### Pipeline

```
/design-analyze → /design-language → /design-mockup → /design-implement → /design-refine → /design-verify
                                   ^
                          /design-evolve (anytime)
```

## Token Flow

```
Reference site(s)
    |
    v
Dembrandt CLI (npx dembrandt <url> --dtcg --save-output)
    |
    v
design-tokens.json (W3C DTCG format, project root)
    |
    |---> Design Token Bridge MCP
    |       |-- generate_css_variables --> tokens.css (CSS custom properties + dark mode)
    |       |-- generate_tailwind_config --> tailwind.preset.js
    |       +-- generate_swiftui_theme --> Theme.swift (Color/Font/Spacing extensions)
    |
    +---> validate_contrast --> WCAG AA/AAA compliance report
```

### Key Decisions

1. **W3C DTCG as interchange format** — Dembrandt outputs it natively, Design Token Bridge consumes it, Style Dictionary can also read it. One format, multiple consumers.
2. **Generated files are committed** — `tokens.css`, `tailwind.preset.js`, and `Theme.swift` are checked into the project. Regenerated when tokens change, not on every build.
3. **Dark mode** — Design Token Bridge generates CSS vars with dark mode variants. SwiftUI theme uses `Color` assets respecting `colorScheme`. Dembrandt supports `--dark-mode` extraction.
4. **Updating tokens** — `/design-evolve` or manual edits to `design-tokens.json` trigger regeneration via `/design-implement`. The skill diffs output so you see what changed.

## Verification Loop

```
/design-verify
    |
    |-- Detect target: web? iOS? both?
    |
    |-- Web --> Playwright MCP
    |              |-- browser_navigate --> implementation URL
    |              |-- browser_take_screenshot --> impl-screenshot.png
    |              +-- (multiple viewports: mobile, tablet, desktop)
    |
    |-- iOS --> mobai MCP
    |              |-- get_screenshot --> impl-screenshot.png
    |              +-- (device variants if configured)
    |
    |-- Diff --> design-comparison MCP
    |              |-- compare_design(mockup.png, impl-screenshot.png)
    |              +-- returns: pixel diff %, diff image
    |
    +-- Report
           |-- Pass (< 2% diff) --> "Implementation matches mockup"
           |-- Minor (2-10% diff) --> list specific discrepancies, suggest fixes
           +-- Major (> 10% diff) --> side-by-side diff image, prioritized fix list
```

Mockup baselines are captured when `/design-mockup` output is approved. Both baselines and implementation screenshots are saved to `~/.agentic-workflow/<repo-slug>/design/`.

After fixing discrepancies, re-run `/design-verify` to re-capture and re-diff. If diff is significant, the skill suggests dispatching compound-engineering's `design-iterator` agent for automated multi-pass refinement.

## Bootstrap Integration

### Changes to `/bootstrap` skill

| Area | Change |
|------|--------|
| Preamble skill table | Add 7 `/design-*` skill rows |
| Skill count | 14 → 21 everywhere |
| `DESIGN_SYSTEM` doc template | Add section pointing to `.impeccable.md` and `design-tokens.json` as operational artifacts, with guidance to run `/design-analyze` and `/design-language` |
| CLAUDE.md template | Add "Design Language" section referencing all three files and the design skill pipeline |
| Step 7 suggested workflow | Insert design skills after planning, before implementation |

### Updated Skill Pipeline

```
officeHours → productReview / archReview → plan
    → design-analyze → design-language → design-mockup → design-implement → design-refine → design-verify
    → review → rootCause → bugHunt → shipRelease → syncDocs → weeklyRetro
```

### CLAUDE.md Design Language Section (template)

```markdown
## Design Language

| File | Purpose |
|------|---------|
| `planning/DESIGN_SYSTEM.md` | Design principles, component catalog, strategic decisions |
| `.impeccable.md` | Brand personality + aesthetic direction (AI context) |
| `design-tokens.json` | W3C DTCG tokens (colors, typography, spacing) |

Run `/design-analyze <url>` to extract tokens from reference sites.
Run `/design-language` to define brand context.
```

## File Inventory

### New files in `agentic-workflow/`

```
skills/
├── design-analyze/SKILL.md
├── design-language/SKILL.md
├── design-evolve/SKILL.md
├── design-mockup/SKILL.md
├── design-implement/SKILL.md
├── design-refine/SKILL.md
├── design-verify/SKILL.md
└── _design-preamble.md          (shared context-loading preamble)
```

### Modified files

```
skills/bootstrap/SKILL.md       (preamble table, DESIGN_SYSTEM template, CLAUDE.md template, Step 7, skill count)
setup.sh                        (symlink 7 new skills, install Impeccable skills, install Dembrandt dependency)
CLAUDE.md                       (add design skills to tables and architecture)
```

### Per-project files (generated by the skills)

```
<project-root>/
├── .impeccable.md               (brand personality, aesthetic direction)
├── design-tokens.json           (W3C DTCG tokens)
├── tokens.css                   (generated CSS custom properties)
├── tailwind.preset.js           (generated Tailwind config)
└── Theme.swift                  (generated SwiftUI theme — iOS projects only)
```

### Output directory

```
~/.agentic-workflow/<repo-slug>/design/
├── mockup-<screen>.png          (baseline screenshots from approved mockups)
├── impl-<screen>-<viewport>.png (implementation screenshots)
└── diff-<screen>-<viewport>.png (diff images from verification)
```

## Resolved Design Decisions

1. **`/design-refine` dispatch mechanics** — invokes Impeccable skills via the `Skill` tool. Impeccable skills are installed to `~/.claude/skills/`, so `/design-refine` loads design context then calls `Skill(colorize)`, `Skill(animate)`, etc.
2. **`/design-mockup` allowed-tools** — scoped permissions: `Bash(*/start-server.sh *)`, `Write`, `Read`. Locked to the visual companion server script, no broad Bash access.
3. **`/design-verify` target detection** — heuristic-based: check for `Package.swift`/`.xcodeproj`/`*.xcworkspace` → iOS; `package.json` with Next.js/React/Vue → web; both present → both. No argument override.
4. **`/design-implement` screen selection** — interactive: if multiple approved mockups exist, list them and ask which one to implement. Takes `<target>` argument (`web` or `swiftui`) only.
5. **Impeccable installation strategy** — `setup.sh` clones/downloads Impeccable and copies skills at install time, similar to other plugin installations. Always gets latest version, no vendored copy.
6. **Generated token file placement** — always project root. Keep it simple; support monorepos if/when needed.
