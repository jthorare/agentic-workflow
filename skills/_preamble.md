# Shared Preamble

Every skill starts by reading this file and following it. Run **Session Close** at the end of the skill.

> **Agentic Workflow** — 53 native skills + 3 fetched external packs (impeccable, emil-design-eng, taste-skill family). Works in Claude Code, Codex, and Cursor. Run any as `/<name>` (Claude Code, Cursor) or `$<name>` (Codex).
>
> **Provider tools:** skills name capabilities (**Ask the user**, **Spawn a subagent**, **Dispatch in parallel**, **Invoke skill**, `mcp: <server>/<tool>`). Map each to your host's tool via `$HOME/.agentic-workflow/toolkit/skills/_shared/capabilities.md`.
>
> | Skill | Purpose |
> |-------|---------|
> | `/review` | Multi-agent PR code review |
> | `/postReview` | Publish review findings to GitHub |
> | `/addressReview` | Implement review fixes in parallel |
> | `/enhancePrompt` | Context-aware prompt rewriter |
> | `/bootstrap` | Generate repo planning docs + AGENTS.md (+ per-provider rules) |
> | `/rootCause` | 4-phase systematic debugging |
> | `/bugHunt` | Fix-and-verify loop with regression tests |
> | `/bugReport` | Structured bug report with health scores |
> | `/testAudit` | Gate new tests; audit and prune low-value tests |
> | `/shipRelease` | Sync, test, push, open PR |
> | `/syncDocs` | Post-ship doc updater |
> | `/weeklyRetro` | Weekly retrospective with shipping streaks |
> | `/officeHours` | Spec-driven brainstorming → EARS requirements + design doc |
> | `/productReview` | Founder/product lens plan review |
> | `/archReview` | Engineering architecture plan review |
> | `/withInterview` | Interview user to clarify requirements before executing |
> | `/design-analyze` | Detect web vs iOS, extract design tokens (dispatcher) |
> | `/design-analyze-web` | Extract design tokens from reference URLs (web) |
> | `/design-analyze-ios` | Extract design tokens from Swift/Xcode assets |
> | `/design-language` | Define brand personality and aesthetic direction |
> | `/design-evolve` | Detect web vs iOS, merge new reference into design language (dispatcher) |
> | `/design-evolve-web` | Merge new URL into design language (web) |
> | `/design-evolve-ios` | Merge Swift reference into design language (iOS) |
> | `/design-mockup` | Detect web vs iOS, generate mockup (dispatcher) |
> | `/design-mockup-web` | Generate HTML mockup from design language |
> | `/design-mockup-ios` | Generate SwiftUI preview mockup |
> | `/design-implement` | Detect web vs iOS, generate production code (dispatcher) |
> | `/design-implement-web` | Generate web production code (CSS/Tailwind/Next.js) |
> | `/design-implement-ios` | Generate SwiftUI components from design tokens |
> | `/design-refine` | Dispatch Impeccable refinement commands |
> | `/design-verify` | Detect web vs iOS vs desktop, screenshot diff vs mockup (dispatcher) |
> | `/design-verify-web` | Playwright screenshot diff vs mockup (web) |
> | `/design-verify-ios` | Simulator screenshot diff vs mockup (iOS) |
> | `/design-verify-desktop` | Real desktop app on seeded data, screen diff vs mockup (desktop) |
> | `/verify-app` | Detect web vs iOS vs desktop, verify running app (dispatcher) |
> | `/verify-web` | Playwright browser verification of running web app |
> | `/verify-ios` | XcodeBuildMCP simulator verification of iOS app |
> | `/verify-desktop` | Real desktop app on seeded data, checked against its mockups |
> | `/autoplan` | Plan meta-orchestrator (productReview + archReview + planDesignReview + planDevexReview + cso in parallel) |
> | `/planDesignReview` | Design-lens review of plan docs |
> | `/planDevexReview` | DX-lens review of plan docs |
> | `/cso` | OWASP Top 10 + STRIDE threat model (plan or PR diff) |
> | `/design-shotgun` | Generate 4–6 mockup variants in parallel |
> | `/landAndDeploy` | Merge → deploy → smoke → chain canary |
> | `/canary` | Post-deploy monitoring with custom probes |
> | `/prismStatus` | Health check for prism-mcp |
> | `/specToProvenPR` | Approved spec → proven, review-clean PRs, one shippable stage at a time |
> | `/judge` | Tune, inspect, and undo `judge` decisions in-session |
> | `/ui-evidence` | Playwright UI evidence for a web app PR, published ask-first |
> | `/bugFixOrchestrator` | Bug ticket → rootCause → implementers → proven, judge-checked resolution |
> | `/new-package` | Scaffold a C# package that passes every check, with one red test |
> | `/new-slice` | Add one C# use case (slice) to a package, red first |
> | `/meta` | Meta orchestrator: replies owed, lane map, gate and merge, owner questions, health (aw-meta CLI) |
>
> **Output directory:** `~/.agentic-workflow/<repo-slug>/`
>
> ### Meta-Orchestration Convention
>
> Every native pipeline skill ends its response with a `## Next steps` block listing 1–3 recommended successor skills with one-line reasons. This is the meta-orchestration layer — skills hand off through structured suggestions, not by importing each other's logic. Three stage orchestrators (`/autoplan`, `/design-refine`, `/shipRelease`) fan out subagents in parallel and consolidate findings.

## Reply Voice

Every reply to the user follows `$HOME/.agentic-workflow/toolkit/config/voice/joi.md` (concrete, answer first, in Joi's voice). Read it now, and read `config/voice/joi-examples.md` next to it before a long or complex reply. It covers replies only, not PR bodies, issues, commits or code comments.

## Codebase Navigation

Prefer **Serena** for all code exploration — LSP-based symbol lookup is faster and more precise than file scanning.

| Task | Tool |
|------|------|
| Find a function, class, or symbol | `serena: find_symbol` |
| What references symbol X? | `serena: find_referencing_symbols` |
| Module/file structure overview | `serena: get_symbols_overview` |
| Search for a string or pattern | search files (fallback) |
| Read a full file | read file (fallback) |

## Preamble — Bootstrap Check

Before running this skill, verify the environment is set up:

```bash
# Derive repo slug
REMOTE_URL=$(git remote get-url origin 2>/dev/null || echo "")
if [ -n "$REMOTE_URL" ]; then
  REPO_SLUG=$(echo "$REMOTE_URL" | sed 's|.*[:/]\([^/]*/[^/]*\)\.git$|\1|;s|.*[:/]\([^/]*/[^/]*\)$|\1|' | tr '/' '-')
else
  REPO_SLUG=$(basename "$(pwd)")
fi
echo "repo-slug: $REPO_SLUG"

# Check bootstrap status
TOOLKIT="$HOME/.agentic-workflow/toolkit"
SHARED_DIR="$TOOLKIT/skills/_shared"
SKILLS_OK=true
[ -d "$TOOLKIT/skills" ] && [ -s "$HOME/.agentic-workflow/providers" ] || SKILLS_OK=false
for s in review postReview addressReview enhancePrompt bootstrap rootCause bugHunt bugReport shipRelease syncDocs weeklyRetro officeHours productReview archReview withInterview design-analyze design-analyze-web design-analyze-ios design-language design-evolve design-evolve-web design-evolve-ios design-mockup design-mockup-web design-mockup-ios design-implement design-implement-web design-implement-ios design-refine design-verify design-verify-web design-verify-ios design-verify-desktop verify-app verify-web verify-ios verify-desktop autoplan planDesignReview planDevexReview cso design-shotgun landAndDeploy canary prismStatus specToProvenPR testAudit judge ui-evidence bugFixOrchestrator new-package new-slice meta; do
  while read -r _prov _dir; do
    [ -d "$_dir/$s" ] || SKILLS_OK=false
  done < "$HOME/.agentic-workflow/providers" 2>/dev/null
done

RULES_OK=false
[ -f "AGENTS.md" ] && [ -n "$(ls -A .agents/rules/ 2>/dev/null)" ] && RULES_OK=true
[ -n "$(ls -A .claude/rules/ 2>/dev/null)" ] && RULES_OK=true  # legacy layout

echo "skills-symlinked: $SKILLS_OK"
echo "rules-directory: $RULES_OK"
```

Domain rules live in `.agents/rules/` (canonical) and are emitted per provider (`.claude/rules/`, `.cursor/rules/`, `AGENTS.md` rules index). Before editing a file, read any rule whose `globs` match it — Claude Code and Cursor load these automatically; in Codex, consult the rules index in `AGENTS.md`.

If `SKILLS_OK=false`, **Ask the user:**
> "Agentic Workflow is not fully set up. Run setup.sh now? (yes/no)"

If **yes**: run `bash <path-to-agentic-workflow>/setup.sh` (`$HOME/.agentic-workflow/toolkit/setup.sh`).
If **no**: warn that some features may not work, then continue.

If `RULES_OK=false` (and `SKILLS_OK` is true), do not offer setup.sh. Instead, show:
> "Domain rules not found — run `/bootstrap` to generate `AGENTS.md` and `.agents/rules/` for this repo."

Create the output directory for this repo:
```bash
mkdir -p "$HOME/.agentic-workflow/$REPO_SLUG"
```

## Session Context

Load prior work state for this repo from prism-mcp before starting.

**1. Derive a topic string** — synthesize 3–5 words from the skill argument and task intent:
- `/officeHours add dark mode` → `"dark mode UI feature"`
- `/rootCause TypeError cannot read properties` → `"TypeError cannot read properties"`
- `/review 42` → use the PR title once fetched: `"PR {title} review"`
- No argument → use the most specific descriptor available: `"{REPO_SLUG} {skill-name}"`

**2. Load context from prism-mcp:**
```
mcp: prism-mcp/session_load_context — project: REPO_SLUG, level: "standard",
  toolAction: "Loading session context", toolSummary: "<skill-name> context recovery"
```

Store the returned `expected_version` — you will need it at Session Close.

**3. Surface results:**
- If the response contains a non-empty summary or prior decisions:
  > **Prior context:** {summary}
  Use this to inform your approach before continuing.
- If prism-mcp returns an error, surface it and stop:
  > "prism-mcp unavailable: {error}. Ensure prism-mcp is running and registered."

## Session Close

> **Run at the end of every skill**, after all work is complete and the report has been shown to the user.

Save a structured ledger entry and update the live handoff state for this repo.

**1. Save ledger entry (immutable audit trail):**
```
mcp: prism-mcp/session_save_ledger — project: REPO_SLUG,
  conversation_id: "<skill-name>-<ISO-timestamp, e.g. 2026-04-08T14:32:00Z>",
  summary: "<one paragraph describing what was accomplished this session>",
  todos: ["<any open items left incomplete>", ...],
  files_changed: ["<paths of files created or modified>", ...],
  decisions: ["<key decisions made during this skill run>", ...]
```

**2. Update handoff state (mutable live state for next session):**
```
mcp: prism-mcp/session_save_handoff — project: REPO_SLUG,
  expected_version: <value returned by session_load_context>,
  open_todos: ["<open items not yet completed>", ...],
  active_branch: "<current git branch from: git branch --show-current>",
  last_summary: "<one sentence: what this skill just did>",
  key_context: "<critical facts the next session must know — constraints, decisions, blockers>"
```

If either call fails, surface the error:
> "prism-mcp session save failed: {error}. Context may not persist to next session."
