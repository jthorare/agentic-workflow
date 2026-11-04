---
name: bootstrap
description: "Use when a repo has little or no documentation and you want the planning docs (architecture, ERD, API contract, testing, CI/CD), an AGENTS.md with per-provider rules, or a Serena config written from the code, or just an audit of which docs are missing. Existing docs are kept. Not for refreshing README or changelog after a ship (syncDocs)."
argument-hint: "[--force] [--product-docs <url-or-path>]..."
allowed-tools: Bash(git *), Bash(ls *), Bash(find *), Bash(curl *), Bash(docker *), Bash(cat *), Bash(mkdir *), Bash(kill *), Bash(sleep *), Bash(REPO_PATH=*), Bash(REPO_NAME=*), Bash(RULES_OK=*), Bash(bash *), Bash(*/sync-rules.sh*), Agent, Read, Write, Glob, Grep, Skill, AskUserQuestion, mcp__serena__check_onboarding_performed, mcp__prism-mcp__session_load_context, mcp__prism-mcp__session_save_ledger, mcp__prism-mcp__session_save_handoff
---

<!-- preamble -->
**Before anything else:** read `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.

> **MCP Servers** — available in every session. Prefer these over built-in tools.
>
> | Server | When to reach for it |
> |--------|---------------------|
> | `serena` | Code structure: find symbol, find usages, call hierarchy — use instead of file search + read |
> | `context7` | Current library/framework docs |
> | `playwright` | Browser automation, screenshots, DOM inspection |
> | `github` | PRs, issues, releases via GitHub API |
> | `design-comparison` | Visual diff between implementation and design |
> | `xcodebuildmcp` | iOS simulator control — build, run, screenshot, UI snapshot |

Every run ends with the [Result block](#result-block), whatever the outcome: done, stopped, failed, refused or a question back to the person. Run its gate now, before the first step, so you know `AW_RESULT` before anything else. Under `AW_RESULT=1` your final reply opens with the `aw-result` block; the result file never stands in for it.

## External Documentation — Parse and Classify

If the user provides `--product-docs` arguments, parse and classify them before proceeding to Step 1.

### Argument Format

```bash
/bootstrap --product-docs <url-or-path> [--product-docs <url-or-path>]...
# e.g.
/bootstrap --product-docs https://company.sharepoint.com/.../Roadmap.docx --product-docs ~/docs/strategy.pdf
```

If no `--product-docs` arguments are provided, skip this entire section entirely — no validation, no confirmation, no External References sections added to generated docs.

### Classification

Classify each source by URL pattern:

| Pattern | Type |
|---------|------|
| `sharepoint.com` in URL | SharePoint |
| `atlassian.net/wiki` or `confluence` in URL | Confluence |
| `dropbox.com` or `paper.dropbox.com` in URL | Dropbox |
| `notion.so` or `notion.site` in URL | Notion |
| `drive.google.com` or `docs.google.com` in URL | Google Drive |
| Starts with `/`, `~/`, or `./` | Local file path |
| Any other `https://` URL | Generic documentation |

Matching is case-insensitive.

### Validation

For each source, check accessibility:

```bash
# URL sources — HEAD request
curl --head --silent --fail --max-time 5 "<url>" 2>&1 | head -1

# File paths — existence check
[ -f "<resolved-path>" ] && echo "exists" || echo "not found"
```

### Confirmation Prompt

After classifying all sources, present them to the user before proceeding:

```
External Product Documentation
===============================
{one line per source, grouped by type: • <url-or-path>  ✓ Accessible | ✗ Not accessible}

These sources will be referenced when generating: PRODUCT_ROADMAP, BUSINESS_PLAN, GO_TO_MARKET
Inaccessible sources are documented as unavailable but don't halt bootstrap.

Continue? (yes/no/edit)
```

- **yes** — proceed with all sources
- **no** — skip external documentation (treat as if `--product-docs` was not provided)
- **edit** — ask which sources to remove or add before continuing

### When to Reference External Docs

Only include an "External References" section in the product-facing documents: `BUSINESS_PLAN`, `PRODUCT_ROADMAP`, `GO_TO_MARKET`, `COMPETITIVE_ANALYSIS`. Never in engineering docs (`ARCHITECTURE`, `ERD`, `API_CONTRACT`, `CODE_STYLE`, `COMMIT_STRATEGY`, `TESTING`, `CI_CD`, `DEPLOYMENT`).

### External References Section Template

Append this section at the **end** of each qualifying document (before any changelog/appendix):

```markdown
## External References

This document was informed by the following external product documentation:

- [SharePoint] [Roadmap Q1 2026](https://company.sharepoint.com/.../Roadmap.docx) — Product priorities and timelines
- [File] `~/docs/strategy.pdf` — Strategic direction
- [Confluence] [PRD: Feature X](https://...) — ⚠️ Not accessible at bootstrap time

These sources were referenced but not automatically ingested. If details conflict with this document, defer to the most recent source or consult the product team.
```

Use `⚠️ Not accessible at bootstrap time` for sources that failed the curl/file check.

---

# Bootstrap — Repo Documentation Generator

Orchestrates documentation generation for any repository. Detects existing coverage, generates missing docs using Pivot-pattern templates, and creates provider-neutral repo instructions: a canonical `AGENTS.md` + `.agents/rules/`, linked for Claude Code (`CLAUDE.md` → `AGENTS.md`, `.claude/rules` → `.agents/rules`) and Cursor (`.cursor/rules/*.mdc` → `.agents/rules/*.md`), plus a Rules Index inside `AGENTS.md` for Codex, by `sync-rules.sh`.

Paths used throughout:

```bash
TOOLKIT="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}"
SHARED_DIR="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared"
SYNC_RULES="$TOOLKIT/scripts/sync-rules.sh"
```

## Step 1: Enhance Context

Construct the `enhancePrompt` input. If external docs were provided and confirmed in the step above, append external doc context to the base prompt.

**Base prompt:**
> "Analyze this repository to understand its tech stack, architecture, domain, and existing documentation. I need to generate comprehensive planning documents."

**If external docs provided, append to prompt:**
> "External product documentation has been provided: [list classified sources with types and URLs]. When generating product-facing documents (PRODUCT_ROADMAP, BUSINESS_PLAN, GO_TO_MARKET, COMPETITIVE_ANALYSIS), reference these sources and indicate where details should be cross-checked with the external docs."

**Invoke skill `enhancePrompt`** with the constructed prompt. Wait for the enhanced prompt. Proceed with the enriched context.

## Step 2: Gather Repo Intelligence

Run these in parallel to understand the target repo:

```bash
# Tech stack detection
ls package.json Gemfile requirements.txt Cargo.toml go.mod build.gradle pom.xml *.xcodeproj Podfile 2>/dev/null

# Framework detection
cat package.json 2>/dev/null | head -50
cat Gemfile 2>/dev/null | head -30

# Directory structure
find . -maxdepth 3 -type d -not -path '*/node_modules/*' -not -path '*/.git/*' -not -path '*/vendor/*' | head -80

# Git info
git remote -v 2>/dev/null
git log --oneline -10 2>/dev/null
```

Read any existing `AGENTS.md`, `CLAUDE.md`, `.cursorrules`, `README.md`, `CONTRIBUTING.md`, or files in `docs/`, `planning/`, `.docs/`, `.agents/rules/`, `.claude/rules/`, `.cursor/rules/`.

## Step 3: Audit Documentation Coverage

Check for each of the 17 Pivot-pattern documents in two concrete passes:

1. **Filename pass (file search).** For each Doc ID, run its filename patterns (table below) as a filename glob over each of these directory sets: `planning/`, `docs/`, `doc/`, `.docs/`, `wiki/`, and the repo root — e.g. `planning/*architecture*`, `docs/**/*roadmap*`. Match case-insensitively (try capitalized variants like `*ARCHITECTURE*` where the filesystem is case-sensitive).
2. **Content pass (content search).** Docs may be embedded in other files: search the contents of the same directories plus `README.md` and `CONTRIBUTING.md` for each Doc ID's keywords (derived from its patterns, e.g. `monetization|revenue model` for BUSINESS_PLAN) with case-insensitive matching. A hit on a heading line (`^#{1,3} .*keyword`) counts as **found (embedded in `<file>`)**; a body-only mention does not.

| Doc ID | Search patterns |
|--------|----------------|
| `BUSINESS_PLAN` | `*business*plan*`, `*monetization*`, `*revenue*` |
| `PRODUCT_ROADMAP` | `*roadmap*`, `*product*plan*`, `*milestones*` |
| `ARCHITECTURE` | `*architecture*`, `*system*design*`, `*tech*spec*` |
| `ERD` | `*erd*`, `*entity*`, `*schema*`, `*data*model*` |
| `DEPENDENCY_GRAPH` | `*depend*`, `*framework*`, `*requirements*` |
| `API_CONTRACT` | `*api*`, `*contract*`, `*endpoints*`, `*routes*` |
| `DESIGN_SYSTEM` | `*design*system*`, `*style*guide*`, `*tokens*`, `*theme*` |
| `CODE_STYLE` | `*code*style*`, `*lint*`, `*conventions*`, `*style*` |
| `COMMIT_STRATEGY` | `*commit*`, `*branching*`, `*git*flow*` |
| `PR_GUIDE` | `*pr*guide*`, `*pull*request*`, `*review*checklist*` |
| `TESTING` | `*testing*`, `*test*strategy*`, `*coverage*` |
| `CI_CD` | `*ci*`, `*cd*`, `*pipeline*`, `*github*actions*`, `*workflow*` |
| `DEPLOYMENT` | `*deploy*`, `*release*`, `*ship*` |
| `LOCAL_DEV` | `*local*dev*`, `*setup*`, `*getting*started*`, `*contributing*` |
| `ANALYTICS` | `*analytics*`, `*tracking*`, `*events*`, `*metrics*` |
| `COMPETITIVE_ANALYSIS` | `*competitive*`, `*competitor*`, `*market*analysis*` |
| `GO_TO_MARKET` | `*go*to*market*`, `*gtm*`, `*launch*`, `*marketing*` |

Also audit the repo-instruction layout:

```bash
for f in AGENTS.md CLAUDE.md .cursorrules; do [ -f "$f" ] && echo "$f: exists ($(wc -l < "$f") lines)" || echo "$f: missing"; done
for d in .agents/rules .claude/rules .cursor/rules; do
  n=$(ls "$d" 2>/dev/null | wc -l | tr -d ' '); [ "$n" -gt 0 ] && echo "$d: $n files" || echo "$d: missing"
done
[ -L CLAUDE.md ] && echo "CLAUDE.md -> $(readlink CLAUDE.md)"
[ -L .claude/rules ] && echo ".claude/rules -> $(readlink .claude/rules)"
find .cursor/rules -type l 2>/dev/null | wc -l | xargs echo "linked .cursor/rules files:"
```

Report findings:
```
Documentation Audit
===================

Found (N/17):
  ARCHITECTURE     — planning/ARCHITECTURE.md
  {one line per found doc — Doc ID + path (or "embedded in <file>")}

Missing (M/17):
  {one Doc ID per line}

AGENTS.md:       [exists / missing]
.agents/rules/:  [exists (N files) / missing]
CLAUDE.md:       [link to AGENTS.md / hand-written (legacy) / missing]
.claude/rules/:  [link to .agents/rules / hand-written (legacy, N) / missing]
.cursor/rules/:  [linked (N) / hand-written (legacy, N) / missing]
```

If `--force` was passed: list every existing doc file that would be overwritten, then **Ask the user** —
> "`--force` will regenerate and overwrite these {N} existing docs (they may contain hand edits): {list}. Overwrite? (yes/no)"

Only on **yes**, treat all docs as missing and regenerate. On **no**, fall back to missing-only generation.

## Step 4: Handle Each Scenario

### Bare repo (0–2 docs found)
Generate all 17 docs + AGENTS.md + `.agents/rules/`. **Dispatch in parallel** in batches of 4–5 to avoid overwhelming context.

### Partially documented (3–14 docs found)
Generate only missing docs. Read existing docs first to maintain consistency in terminology, formatting, and cross-references.

### Well-documented (15+ docs found)
Report completeness. For each existing doc, note if it could be improved (missing sections compared to Pivot template). Ask user if they want refinement suggestions.

## Step 5: Generate Missing Docs

For each missing doc, run a two-stage dispatch — research, then write.

### Dispatch Contract (per `$SHARED_DIR/parallel-dispatch.md`)

1. **Research** — **Spawn a subagent** (read-only explorer type where the host has one):
   ```
   subagent: explore (read-only)
   prompt:   "Research the repo at <ABS REPO ROOT> to gather everything needed to write planning/<DOC_ID>.md:
             <doc-specific facts to collect — e.g. for ARCHITECTURE: directory tree, entry points, layers,
             data flow; for TESTING: test framework, commands, existing coverage config>.
             Return a concise findings summary — file paths and facts only, no prose padding."
   ```
2. **Write** — **Spawn a subagent** (general-purpose, can write files):
   ```
   subagent: general-purpose
   prompt:   "Write <ABS REPO ROOT>/planning/<DOC_ID>.md following the Pivot template structure for <DOC_ID>
             (structure given below in Generation Rules). Use these research findings as your only source
             material: <findings from stage 1>. Real repo data only — no placeholder content.
             Write the file to exactly that path."
   ```

**Batching policy:** **Dispatch in parallel** in batches of **4–5 docs** — all subagents of a batch launched together, never sequential when independent. After each batch, existence-check every expected output before starting the next batch:

```bash
ls -la planning/ && for f in planning/<DOC_ID_1>.md planning/<DOC_ID_2>.md; do [ -s "$f" ] || echo "MISSING OUTPUT: $f"; done
```

A missing or empty file is a **named failure** — re-dispatch that doc's writer; never silently skip it or invent its content in-line.

### Generation Rules

1. **Adapt to the tech stack.** A Python/Django repo gets Django-specific architecture, pytest testing patterns, pip dependency management — not Swift/Firebase patterns.

2. **Follow the Pivot template structure.** Each doc type has a consistent format:
   - `BUSINESS_PLAN` — Executive summary, monetization model, unit economics tables, revenue projections, break-even analysis. **If external docs provided:** append "## External References" section (see template above) listing source URLs with a note to cross-check market and business model details.
   - `PRODUCT_ROADMAP` — Vision, versioned releases with scope/success criteria/timeline tables. **If external docs provided:** append "## External References" section listing source URLs and noting which priorities or timelines should be validated against those sources.
   - `ARCHITECTURE` — System overview diagram, directory tree, layer descriptions, key rules
   - `ERD` — Relationship diagram, collection/table schemas with field tables
   - `DEPENDENCY_GRAPH` — Framework requirements, version constraints, layer dependency map
   - `API_CONTRACT` — Per-endpoint: trigger, path, request/response schemas, error cases, side effects
   - `DESIGN_SYSTEM` — Design principles, color tokens, typography scale, spacing, components. Points to `.impeccable.md` (brand personality) and `design-tokens.json` (W3C DTCG tokens) as operational artifacts. Run `/design-analyze` and `/design-language` to generate these.
   - `CODE_STYLE` — Linter config, naming conventions, import ordering, patterns
   - `COMMIT_STRATEGY` — Message format template, examples, branch naming table
   - `PR_GUIDE` — Categorized checkbox checklists (architecture, security, testing, etc.)
   - `TESTING` — Coverage targets, layer-based strategy (unit/integration/e2e), example code
   - `CI_CD` — Pipeline diagram, stage descriptions, required secrets table
   - `DEPLOYMENT` — Release workflow, promotion path, checklists, monitoring
   - `LOCAL_DEV` — Prerequisites table, setup commands, environment config, running locally
   - `ANALYTICS` — Event catalog with property tables, funnel diagrams
   - `COMPETITIVE_ANALYSIS` — Competitor profiles (overview/strengths/weaknesses), positioning matrix. **If external docs provided:** append "## External References" section listing source URLs.
   - `GO_TO_MARKET` — User segments with profiles/behavior/channels, launch strategy. **If external docs provided:** append "## External References" section listing source URLs and noting which targeting or launch strategy details should be cross-checked.

3. **Use real data.** Read the actual codebase to populate architecture trees, dependency lists, API endpoints, test commands, etc. Don't invent placeholder content.

4. **Cross-reference.** New docs should reference each other where appropriate (e.g., TESTING references CODE_STYLE, DEPLOYMENT references CI_CD).

5. **Place docs in `planning/`.** Create the directory if it doesn't exist. Use UPPER_SNAKE_CASE filenames with `.md` extension.

## Step 6: Generate AGENTS.md + .agents/rules/ (if missing), then sync per provider

`AGENTS.md` and `.agents/rules/` are the **only** instruction files bootstrap writes by hand. Everything provider-specific is a symlink or generated by `sync-rules.sh` in Step 6d — `CLAUDE.md` → `AGENTS.md`, `.claude/rules` → `.agents/rules`, `.cursor/rules/<name>.mdc` → `.agents/rules/<name>.md`, and the Rules Index inside `AGENTS.md`. Never write those files directly.

### Step 6a: Generate AGENTS.md

If `AGENTS.md` is missing, create a trimmed `AGENTS.md` (under 80 lines, excluding the generated Rules Index) that serves as a navigation document — not a reference manual. If a hand-written `CLAUDE.md` or `.cursorrules` exists, use it as source material: carry over all of its content (context docs, stack, commands, merge gate, conventions). Keep anything genuinely Claude-only under a short `## Claude Code` heading — `CLAUDE.md` becomes a link to this file. Include only:

```markdown
# AGENTS.md — {Project Name}

> {one-line tagline describing the project}

Domain-specific rules are in `.agents/rules/`. Claude Code and Cursor load them automatically (via the `.claude/rules` and `.cursor/rules/` links); in Codex, read the matching rules listed in the Rules Index at the bottom of this file before editing.

## Required Context

Read before making changes:

| Document | Purpose |
|----------|---------|
| `planning/ARCHITECTURE.md` | System components and data flow |
| `planning/API_CONTRACT.md` | Endpoint specifications |
| `planning/CODE_STYLE.md` | Language conventions and patterns |
| `planning/TESTING.md` | Test strategy and coverage targets |
| `planning/ERD.md` | Data model and relationships |

## Tech Stack

| Layer | Technology |
|-------|-----------|
| {layer} | {technologies} |

## Directory Structure

```
{project-root}/
├── {dir}/   # {purpose}
├── {dir}/   # {purpose}
└── ...
```

## Commands

```bash
{key commands for build, test, dev, setup}
```

## Merge Gate

Before merging any PR:
1. {requirement 1}
2. {requirement 2}

## Commit Conventions

Format: `type: short description`
Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
```

Do **not** include: Skills tables, Key Patterns code blocks, Design Language sections, Architecture trees longer than 8 lines, Implementation Guidelines, or a hand-written rules list (`sync-rules.sh` appends the Rules Index). Those belong in `.agents/rules/` files. Do not name provider-specific tools in `AGENTS.md` — Claude Code, Codex, and Cursor all read it.

If `AGENTS.md` already exists, leave it alone. Step 6d only rewrites the block between its `RULES INDEX` markers.

### Step 6b: Generate .agents/rules/

Inspect the target repo's directory structure and tech stack to generate glob-scoped rule files in `.agents/rules/`. Step 6d links each rule for every provider, and it loads when an agent works on matching files.

**Migrate legacy rules first.** For each existing real (non-symlink) `.claude/rules/*.md` (frontmatter `paths:`) or `.cursor/rules/*.mdc` (frontmatter `description`, `globs`, `alwaysApply`) with no `.agents/rules/` counterpart: write `.agents/rules/<name>.md` with the same body and the combined frontmatter below. When a Claude and a Cursor rule share a name, merge their bodies. Only then infer new rules for domains not yet covered.

**Infer rule file groupings from what directories actually exist.** Examples by tech stack:

| Tech Stack | Rule Files to Generate |
|------------|----------------------|
| Rails | `models.md` (app/models/**), `controllers.md` (app/controllers/**), `spec.md` (spec/**) |
| Next.js | `components.md` (components/**), `hooks.md` (hooks/**), `pages.md` (app/**) |
| Python | `services.md` (src/services/**), `tests.md` (tests/**) |
| Node.js + TypeScript | `services.md` (src/**), `tests.md` (**/*.test.ts) |
| Django | `models.md` (*/models.py), `views.md` (*/views.py), `tests.md` (*/tests.py) |
| Go | `handlers.md` (internal/handlers/**), `domain.md` (internal/domain/**) |

**Each generated rule file must:**
1. Start with YAML frontmatter that every provider can read from the same file: `description:` (one line — Cursor uses it for relevance), `globs:` (Cursor) and `paths:` (Claude Code) with the **same** YAML list of patterns, and `alwaysApply: false`. For an always-on rule (use sparingly) write `globs: []` and `alwaysApply: true`, and omit `paths:`. `sync-rules.sh --check` fails if `globs` and `paths` differ.
2. Contain domain-specific guidance drawn from the repo's actual code patterns (read the code — don't template-fill)
3. Focus on patterns, conventions, and pitfalls specific to that domain
4. Stay provider-neutral — no Claude Code, Codex, or Cursor tool names in the body

**Rule file template:**

```markdown
---
description: {When this rule applies, in one line}
globs:
  - "{pattern1}"
  - "{pattern2}"
paths:
  - "{pattern1}"
  - "{pattern2}"
alwaysApply: false
---

# {Domain} Rules

## {Key Pattern}

{Specific, actionable guidance drawn from the actual codebase — naming conventions,
required patterns, things to avoid, code examples from the real code.}

## {Another Pattern}

...
```

**Dispatch in parallel** one read-only explorer subagent per domain to read representative files before writing the rules (batches of 4–5, per `$SHARED_DIR/parallel-dispatch.md`). Rules should reflect what the code actually does, not generic best practices.

### Step 6c: Legacy CLAUDE.md

Claude Code reads `CLAUDE.md`, not `AGENTS.md`, so `CLAUDE.md` becomes a symlink to `AGENTS.md`:

- **Missing** or **already a link to `AGENTS.md`** → nothing to do; Step 6d creates the link.
- **Hand-written (legacy)** → after its content has been carried into `AGENTS.md` (Step 6a), **Ask the user:**
  > "Replace CLAUDE.md with a link to AGENTS.md? Its content now lives in AGENTS.md. (yes/no)"
  - **yes** → Step 6d runs with `--force`.
  - **no** → leave it, and report that Claude Code will not see `AGENTS.md`.

### Step 6d: Link per-provider rules

```bash
SYNC_RULES="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/scripts/sync-rules.sh"
if [ -f "$SYNC_RULES" ]; then
  bash "$SYNC_RULES" "$(pwd)"            # add --force only if the user approved replacing legacy files
  bash "$SYNC_RULES" --check "$(pwd)" && echo "rules: in sync"
else
  echo "WARN: $SYNC_RULES not found — run setup.sh, then re-run: bash $SYNC_RULES"
fi
```

`sync-rules.sh` creates symlinks only — `.claude/rules` → `../.agents/rules`, `.cursor/rules/<name>.mdc` → `../../.agents/rules/<name>.md`, `CLAUDE.md` → `AGENTS.md` — and regenerates the Rules Index table between markers in `AGENTS.md`. It is idempotent, removes dangling `.cursor/rules` links whose rule was deleted, and skips real (non-link) files with a warning.

If it warns that legacy `.claude/rules`, `.cursor/rules/*.mdc`, or `CLAUDE.md` files were skipped, confirm their content was migrated in Steps 6a–6b, then **Ask the user:**
> "Replace {N} legacy instruction files with links to AGENTS.md / .agents/rules/? (yes/no)"

On **yes**, re-run with `--force`. On **no**, list the skipped files in the report.

**Verify, don't assert:** only report rules as linked if `--check` passed.

### Step 6e: Generate Design Language (optional)

If the repo has a user-facing surface (web frontend or iOS app detected in Step 2) and no `.impeccable.md` / `design-tokens.json` exist yet, **Ask the user:**
> "Generate the design language now (.impeccable.md + design-tokens.json via /design-language)? (yes/no)"

- **yes** → **Invoke skill `design-language`** and wait for its artifacts.
- **no** (or no user-facing surface) → skip; the DESIGN_SYSTEM doc's pointer to `/design-analyze` + `/design-language` stands.

The completion report's "+ design language" line (Step 8) appears **only if this step actually ran and produced its artifacts** — never claim design language was generated otherwise.

## Step 7: Generate .serena/project.yml

After generating docs and repo instructions, configure Serena LSP for the repo.

**Language detection** (check repo root and one level deep):
- TS/JS: `tsconfig.json` or `package.json` → `typescript`
- Python: `*.py`, `pyproject.toml`, or `requirements.txt` → `python`
- Go: `go.mod` or `*.go` → `go`
- Rust: `Cargo.toml` or `*.rs` → `rust`
- C#: `*.csproj` or `*.cs` → detected but **excluded from languages list** (see below)
- Swift: `*.swift` or `Package.swift` → detected but **excluded from languages list** (see below)

**Language exclusions:** Do NOT add `swift` or `csharp` to the `language_servers:` list in the generated config:
- `swift` — sourcekit-lsp is a macOS binary; Serena runs in a Linux Docker container. Swift LSP requires the separate `serena-local:latest-swift` image and host-side socket bridge. Add manually after running `BUILD_SWIFT=1 ./setup.sh`.
- `csharp` — requires the `-csharp` image variant. Add manually after running `BUILD_CSHARP=1 ./setup.sh`.

**Sensitive path audit:** flag `.claude/`, `.cursor/`, `.codex/`, `config/`, `secrets*`, `*.env`, `docs/` subdirectories → add to `ignored_paths`.

**RULES_OK check:**
```bash
RULES_OK=false
[ -f "AGENTS.md" ] && [ -n "$(ls -A .agents/rules/ 2>/dev/null)" ] && RULES_OK=true
echo "rules-directory: $RULES_OK"
```
If `RULES_OK=false`, print:
> "WARN: AGENTS.md or .agents/rules/ not found — domain rules won't load. Re-run Step 6 to generate them."

**Derive repo name for project_name field:**
```bash
REPO_NAME="$(basename "$(pwd)")"
```

**Write `.serena/project.yml`** with detected `language_servers` (Serena >= 1.7 renamed `languages`; an old or incomplete file makes Serena try to re-save it, which crashes on the read-only repo mount) and audited `ignored_paths`:

```yaml
# Serena project configuration for <repo-name>
# --context claude-code disables execute_shell_command at Serena level.

project_name: <REPO_NAME>

language_servers:
- <detected-language-1>
- <detected-language-2>  # if applicable
# swift omitted: sourcekit-lsp requires macOS; add after running BUILD_SWIFT=1 ./setup.sh
# csharp omitted by default; add after running BUILD_CSHARP=1 ./setup.sh

read_only: false
ignore_all_files_in_gitignore: true

ignored_paths:
- .claude          # if exists
- config           # if exists and may contain tokens
- <other-sensitive-paths>

excluded_tools:
- write_memory
- read_memory
- onboarding
- execute_shell_command

initial_prompt: ""
added_modes:
ls_workspace_folders:
- .
ls_additional_workspace_folders: []
activation_command:
activation_command_timeout: 180.0
```

**Append to `.gitignore`** (idempotent — check before writing):
```gitignore
# Serena runtime data (LSP index caches, logs, session state)
.serena/cache/
.serena/logs/
.serena/memory/
.serena/memories/
.serena/*.log
```

**Bootstrap the config via Docker (required — expands project.yml to full schema):**

Serena validates `project.yml` on startup and will crash if any required fields are missing. Bootstrap by running Serena once WITHOUT the `:ro` mount so it can write the expanded config:

```bash
REPO_PATH="$(pwd)"
REPO_NAME="$(basename "$REPO_PATH" | tr -c '[:alnum:]-_.' '-')"
mkdir -p "${REPO_PATH}/.serena/cache" "${REPO_PATH}/.serena/logs" "${REPO_PATH}/.serena/memory"
LINES_BEFORE=$(wc -l < "${REPO_PATH}/.serena/project.yml")
docker run --rm \
  -v "${REPO_PATH}:/workspaces/projects/${REPO_NAME}" \
  -v "${REPO_PATH}/.serena/cache:/workspaces/projects/${REPO_NAME}/.serena/cache" \
  -v "${REPO_PATH}/.serena/logs:/workspaces/projects/${REPO_NAME}/.serena/logs" \
  -v "${REPO_PATH}/.serena/memory:/workspaces/projects/${REPO_NAME}/.serena/memory" \
  serena-local:latest \
  serena start-mcp-server \
  --context claude-code \
  --project "/workspaces/projects/${REPO_NAME}" &
SPID=$!
# Poll for the expanded config instead of a blind sleep — Serena rewrites
# project.yml with the full schema once startup validation completes.
EXPANDED=false
for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30; do
  sleep 1
  CUR=$(wc -l < "${REPO_PATH}/.serena/project.yml" 2>/dev/null || echo 0)
  [ "$CUR" -gt "$LINES_BEFORE" ] && { EXPANDED=true; break; }
done
kill $SPID 2>/dev/null || true
if [ "$EXPANDED" = true ]; then
  echo "serena: project.yml expanded ($LINES_BEFORE -> $CUR lines)"
else
  echo "WARN: project.yml was not expanded within 30s — verify Serena startup manually before relying on it"
fi
```

**Verify, don't assert:** only report the bootstrap as successful if the poll observed the expanded config. Subsequent `serena-docker` invocations mount the project read-only and will start without errors.

If the Docker command fails (image not built, Docker not running), do NOT abort bootstrap — print a warning and continue:
> `WARN: Could not bootstrap .serena/project.yml via Docker. The config is minimal and Serena may fail to start. Run setup.sh to build the serena-local image, then re-run /bootstrap.`

**Print summary:**
```
Serena configured. Languages: [<list>]. Run setup.sh if Serena image not yet built.
```

If `csharp` was detected, append:
> NOTE: C# requires the csharp image. Run `BUILD_CSHARP=1 ./setup.sh` to build it, then add `- csharp` to `.serena/project.yml`.

If `swift` was detected, append:
> NOTE: Swift LSP requires a host-side socket bridge. Run `BUILD_SWIFT=1 ./setup.sh` to build it, then add `- swift` to `.serena/project.yml`.

**Run Serena onboarding check (non-fatal):** After the Docker bootstrap step, call `mcp: serena/check_onboarding_performed` to initialize Serena with the repo context. This indexes the project and ensures symbol navigation is ready for use in this session.

> **Important — tool name:** Call `serena/check_onboarding_performed`, not `serena/onboarding`. The `onboarding` tool is explicitly excluded in the generated `project.yml`. Using `onboarding` will be rejected by Serena.

> **Non-fatal:** If `serena/check_onboarding_performed` fails (e.g., Docker is not running, the Serena MCP server is not connected, or the image has not been built yet), do **not** abort bootstrap. Print the following warning and continue:
> `WARN: Serena not available — the project.yml must be bootstrapped before Serena will connect. Build the serena-local Docker image with setup.sh, then re-run /bootstrap.`

## Step 8: Report

**Compute the total — never assert it.** `found` = docs present before this run (Step 3 audit), `generated` = docs actually written and existence-checked this run (Step 5). `total = found + generated` out of 17; list any Doc IDs still missing. Append " + design language" **only if Step 6e ran and produced `.impeccable.md` + `design-tokens.json`**.

```
Bootstrap Complete
==================

Generated ({generated}):
  planning/BUSINESS_PLAN.md          (new)
  planning/PRODUCT_ROADMAP.md        (new)
  planning/CODE_STYLE.md             (new)
  ...
  AGENTS.md                          (new)
  .agents/rules/<name>.md            (new — one line per rule)
  .claude/rules, .cursor/rules/      (links to .agents/rules — N rules, --check passed)
  CLAUDE.md                          (link to AGENTS.md — new / replaced / unchanged)

Existing (unchanged, {found}):
  planning/ARCHITECTURE.md
  planning/API_CONTRACT.md
  planning/ERD.md

Still missing ({missing}): {Doc IDs, or "none"}

Total: {found}+{generated}={total}/17 docs + AGENTS.md + .agents/rules/ (synced to Claude Code, Codex, Cursor){ + design language — only if Step 6e ran}

Next steps:
  1. Review generated docs for accuracy
  2. Commit: git add planning/ AGENTS.md CLAUDE.md .agents/ .claude/rules/ .cursor/rules/ .serena/project.yml .gitignore && git commit -m "docs: bootstrap planning documents"
  3. Refine any docs that need domain-specific detail (edit .agents/rules/, then re-run sync-rules.sh)

Suggested workflow:
  • /officeHours — brainstorm a feature or problem before planning
  • /design-language — define brand personality and aesthetic direction
  • /review <pr> — run multi-agent code review on a PR
  (full pipeline: see the skills table above)
```

## Next steps

- `/officeHours` — start a feature spec now that planning docs exist
- `/design-language` — define the visual identity for this repo

## Result block

```bash
[ "${AW_RESULT:-0}" = 1 ] && echo "AW_RESULT=1" || echo "AW_RESULT=0"
f="${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/repo-slug.sh"; [ -f "$f" ] && . "$f" 2>/dev/null; echo "AW_DIR=${AW_DIR:-}"
[ "${AW_RESULT:-0}" = 1 ] && [ -z "${AW_DIR:-}" ] && echo "AW_DIR is empty: source skills/_shared/repo-slug.sh, then write the result file; never skip it" || true
```

`AW_RESULT=0`: write no result file and print no `aw-result` block. `AW_RESULT=1`: write `$AW_DIR/results/bootstrap-<timestamp>.json` with the Write tool (timestamp `date +%Y%m%d-%H%M%S`, no `mkdir` chain in front) first, along with every other file the run writes; then open your final reply (the message after the last tool call) with the same JSON as a fenced `aw-result` block, before any prose and before `## Next steps`.

Keys: `skill` (`bootstrap`), `artifacts` (absolute paths: the generated docs under `planning/`, `AGENTS.md`, the per-provider rules and `.serena/project.yml`), `summary` (one line),
plus `docs_written` (list of doc paths written); `docs_skipped` (list of doc paths already present).

Contract: `${AW_TOOLKIT:-$HOME/.agentic-workflow/toolkit}/skills/_shared/output-contract.md`.
