---
description: Skill structure, preamble, output directories, pipeline, and installation
globs:
  - "skills/**"
  - "bootstrap/**"
paths:
  - "skills/**"
  - "bootstrap/**"
alwaysApply: false
---

# Skills Rules

## Skill Structure

Every skill lives in its own directory with a `SKILL.md` manifest:

```
skills/<name>/
├── SKILL.md          # Required: manifest + instructions
└── *.md              # Optional: subagent prompts, templates
```

`bootstrap/` follows the same structure (it's a skill, just in a different directory).

## SKILL.md Format

```yaml
---
name: skillName
description: One-sentence description of what this skill does
argument-hint: [optional argument syntax]
allowed-tools: Bash(git *), Bash(ls *), Agent, Read, Write, Glob, Grep, Skill
---
```

After the frontmatter closing `---`, add the two-line preamble reference (see Preamble below).

`allowed-tools` uses Claude Code tool names; Codex and Cursor ignore it. Skill **body text** must be provider-neutral: name capabilities (**Ask the user**, **Spawn a subagent**, **Dispatch in parallel**, **Invoke skill `<name>`**, `mcp: <server>/<tool>`) rather than Claude tool names. The capability → tool map for Claude Code, Codex, and Cursor lives in `skills/_shared/capabilities.md`.

## Shared Fragments (SHARED_DIR)

Shared skill fragments live in `skills/_shared/`. Reference them through the stable toolkit symlink, never through a provider's skills dir:

```bash
TOOLKIT="$HOME/.agentic-workflow/toolkit"          # symlink to this repo, created by setup.sh
SHARED_DIR="$HOME/.agentic-workflow/toolkit/skills/_shared"
```

## Preamble

The shared preamble lives in one file, `skills/_preamble.md`. Every SKILL.md references it instead of embedding a copy:

```markdown
<!-- preamble -->
**Before anything else:** read `$HOME/.agentic-workflow/toolkit/skills/_preamble.md` and follow it (skill index, provider capability map, bootstrap check, session context). Run its **Session Close** section when this skill finishes.
```

Design skills add a second reference to `skills/_design-preamble.md` (`<!-- design-preamble -->`). Edit the shared file directly — there is nothing to propagate.

The preamble points the agent at `$HOME/.agentic-workflow/toolkit/skills/_shared/capabilities.md` for the capability → tool map, then verifies:
1. `$HOME/.agentic-workflow/toolkit` exists and the provider registry `$HOME/.agentic-workflow/providers` is non-empty
2. All 53 native skills are present in every registered provider's skills dir (plus 14 skills from the 3 fetched external packs)
3. Domain rules exist: `AGENTS.md` + `.agents/rules/` (or legacy `.claude/rules/`)
4. The repo-slug output directory `~/.agentic-workflow/$REPO_SLUG/` is created

If the skill checks fail, **Ask the user** whether to run `setup.sh`. If only rules are missing, suggest `/bootstrap`.

## Repo Slug Derivation

Used by all skills for output directory naming:

```bash
REMOTE_URL=$(git remote get-url origin 2>/dev/null || echo "")
if [ -n "$REMOTE_URL" ]; then
  REPO_SLUG=$(echo "$REMOTE_URL" | sed 's|.*[:/]\([^/]*/[^/]*\)\.git$|\1|;s|.*[:/]\([^/]*/[^/]*\)$|\1|' | tr '/' '-')
else
  REPO_SLUG=$(basename "$(pwd)")
fi
```

Result: `org-repo` (e.g., `myorg-myrepo`). All output paths use `~/.agentic-workflow/$REPO_SLUG/<domain>/`.

## Output Directories

| Domain | Skills | Path |
|--------|--------|------|
| Reviews | `/review`, `/postReview`, `/addressReview` | `reviews/` |
| Investigations | `/rootCause` | `investigations/` |
| Bug fixing | `/bugFixOrchestrator` | `bugfix/<ticket-slug>/` |
| QA | `/bugHunt`, `/bugReport`, `/testAudit` | `qa/` (`testAudit` writes to `qa/test-audit/`) |
| Releases | `/shipRelease`, `/landAndDeploy`, `/canary`, `/syncDocs` | `releases/` |
| Retrospectives | `/weeklyRetro` | `retros/` |
| Planning | `/officeHours`, `/productReview`, `/archReview`, `/planDesignReview`, `/planDevexReview`, `/autoplan` | `plans/` (`officeHours` and `autoplan` write to `plans/<feature>/`) |
| Design | `/design-mockup`, `/design-verify`, `/design-shotgun` | `design/` (`design-shotgun` writes to `design/shotgun/`) |
| Verification | `/verify-app` | `verification/` |
| Security | `/cso` | `security/` |

Skills that output files always write to `~/.agentic-workflow/$REPO_SLUG/<domain>/` — never to the project directory.

## Skill Pipeline

Skills flow into each other — each writes artifacts that downstream skills auto-discover:

```
officeHours → autoplan ⟨productReview · archReview · planDesignReview · planDevexReview · cso(plan)⟩
   → design-analyze → design-language → design-shotgun → design-mockup → design-implement → design-refine → design-verify
                                        ^                                       (orchestrates impeccable + emil + taste)
                               design-evolve (anytime)
   → cso (pre-ship security check — recommended; manual or hook-gated, see skills/cso/SKILL.md § Pre-ship integration)
   → review → rootCause → bugHunt → shipRelease → landAndDeploy → canary → syncDocs → weeklyRetro
   verify-app (anytime — standalone verification of running app)
   prismStatus (anytime — health check for prism-mcp)
```

## Meta-Orchestration

Three stage orchestrators fan out subagents in parallel and consolidate findings:

| Orchestrator | Stage | Fans out to |
|---|---|---|
| `/autoplan` | Plan | `productReview` + `archReview` + `planDesignReview` + `planDevexReview` + `cso(plan)` |
| `/design-refine` | Design | impeccable (umbrella) + emil-design-eng (reference) + taste-skill family (style packs) |
| `/shipRelease` | Ship | auto-chains → `landAndDeploy` → `canary` → `syncDocs` |

Every native pipeline skill ends its response with a `## Next steps` block listing 1–3 recommended successor skills with one-line reasons. Skills compose through structured suggestions and filesystem hand-off, not by importing each other's logic.

## Bootstrap Skill

`bootstrap/SKILL.md` generates documentation for any repo. Standard output:
- `planning/*.md` — up to 17 Pivot-pattern docs
- A trimmed `AGENTS.md` (under 80 lines) with Required Context, Tech Stack, Commands, Merge Gate, Commits, and a pointer to `.agents/rules/`
- A `.agents/rules/` directory with glob-scoped rule files (frontmatter `description`, `globs`, `paths` (same list), `alwaysApply`) inferred from the repo's actual structure
- Links and index created by `$HOME/.agentic-workflow/toolkit/scripts/sync-rules.sh`: `.claude/rules` → `.agents/rules` (dir symlink), `.cursor/rules/<name>.mdc` → `.agents/rules/<name>.md` (per-file symlinks), `CLAUDE.md` → `AGENTS.md`, and the Rules Index table inside `AGENTS.md` (for Codex)

When writing the bootstrap AGENTS.md template, do not include Skills tables or Key Patterns — those belong in rule files. AGENTS.md should be a navigation document, not a reference manual. `.claude/rules` and `.cursor/rules/*.mdc` are symlinks into `.agents/rules/` — edit the canonical file; re-run `sync-rules.sh` only after adding or removing a rule.

## Adding a New Skill

1. Create `skills/<name>/SKILL.md` with YAML frontmatter
2. Add the preamble reference (see Preamble) immediately after the frontmatter `---`
3. Write the skill's steps after the preamble, using capability phrases from `skills/_shared/capabilities.md` (not Claude tool names) and `$SHARED_DIR` for shared fragments
4. Add the skill name to `setup.sh`'s `MANAGED_SKILLS` array
5. Add the skill to the skills table **and** the skill-check `for s in ...` list in `_preamble.md`
6. Update the skill count in the `AGENTS.md` tagline (line 3)
7. Re-run `./setup.sh` so the skill is linked into every installed provider's skills dir

A skill that ships a helper binary adds it to `scripts/install-cs.sh` (the table of published binaries and its no-argument check), not to `setup.sh`; there is no per-skill Node build.

## Symlink Installation

`setup.sh --providers claude,codex,cursor` (default: detect installed CLIs) installs skills for each provider via `providers/<name>/install.sh`, and records each installed provider in `$HOME/.agentic-workflow/providers` — one `<name> <skills-dir>` line per provider. It also creates `$HOME/.agentic-workflow/toolkit` as a symlink to this repo.

Skills are installed as symlinks into every registered provider's skills dir:
```
<skills-dir>/<name>/      → <repo>/skills/<name>/
<skills-dir>/bootstrap/   → <repo>/bootstrap/
$HOME/.agentic-workflow/toolkit → <repo>
```

| Provider | Skills dir |
|----------|-----------|
| Claude Code | `~/.claude/skills/` |
| Codex | `~/.agents/skills/` (Codex also reads `~/.codex/skills/`) |
| Cursor | `~/.cursor/skills/` (Cursor also reads `~/.agents/skills/` and `~/.claude/skills/`) |

Always read the registry rather than hard-coding a provider path. `setup.sh` manages symlinks with collision detection and stale-skill cleanup. The `install_skill()` function handles: up-to-date (skip), same-repo refresh, different-repo collision (prompt), real-directory collision (prompt).

External packs (`pbakaus/impeccable`, `emilkowalski/skill`, `Leonxlnx/taste-skill`) are cloned at pinned commits (per `EXTERNAL_PINS.env`) into `~/.agentic-workflow/external-skills/<repo>/`. Their `skills/*/` (or `.claude/skills/*/`) subdirs are symlinked into each provider's skills dir alongside native ones. Native symlinks pointing into this repo always win on name collision — external pack skills with matching names are skipped with a warning. Use `./scripts/refresh-external-pins.sh` to bump pinned SHAs to upstream HEAD.
