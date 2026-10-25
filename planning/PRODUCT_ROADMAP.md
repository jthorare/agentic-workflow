# Product Roadmap

## v1.0 — Current Release

### Scope

The initial release provides three core capabilities:

1. **Skills Archive** — Five Claude Code custom skills (`/review`, `/postReview`, `/addressReview`, `/enhancePrompt`, `/bootstrap`) version-controlled and installable via symlink. Configuration archive for `settings.json` and `mcp.json`.

2. **Bootstrap Skill** — Repo documentation generator that detects which of 17 Pivot-pattern documents exist, generates missing ones adapted to the target repo's tech stack, and creates a `CLAUDE.md` if none exists.

3. **MCP Bridge** — TypeScript MCP server for bidirectional multi-agent communication with five tools (`send_context`, `get_messages`, `get_unread`, `assign_task`, `report_status`). Dual transport: MCP stdio for Claude Code integration and Fastify REST API on port 3100. SQLite store-and-forward persistence with WAL mode. Retired 2026-10-10 (never used): agents now hand off through Prism handoffs, judge's outbox and teammate names, and Sindri's ledger.

### Success Criteria

- [x] One-command setup via `./setup.sh` that symlinks skills, copies config, and installs npm dependencies
- [x] MCP server registers with Claude Code via `claude mcp add`
- [x] REST API starts on loopback with safety check for non-loopback binding (retired 2026-10-10 with the bridge)
- [x] Full end-to-end type safety with `AppResult<T>` pattern (services never throw) (retired 2026-10-10 with the bridge)
- [x] Atomic transactions for multi-step operations (assign_task, report_status, get_unread) (retired 2026-10-10 with the
  bridge)
- [x] `/review` skill spawns parallel domain-specific reviewers and produces structured JSON output
- [x] TypeScript strict mode, declaration maps, and source maps enabled

### Timeline

Delivered. The bridge items were retired on 2026-10-10; the rest is the current state of the repository.

---

## v1.5 — Full Development Lifecycle Skills

### Scope

Expand from 5 to 14 skills covering the entire development lifecycle, add a centralized output directory for cross-skill artifact sharing, and establish a shared preamble with bootstrap gate.

**1. Nine New Skills (ported from Gstack, rewritten)**

| Skill | Category | Purpose |
|-------|----------|---------|
| `/rootCause` | Investigation | 4-phase systematic debugging with module boundary scope freeze |
| `/bugHunt` | QA | Fix-and-verify loop with 3 tiers, atomic commits, regression tests |
| `/bugReport` | QA | Read-only health audit with weighted health scores |
| `/shipRelease` | Release | Sync, test, coverage audit, push, open PR, auto-invoke /syncDocs |
| `/syncDocs` | Release | Post-ship doc updater for README, ARCHITECTURE, CHANGELOG, AGENTS.md, .agents/rules/ |
| `/weeklyRetro` | Retrospective | Per-person breakdowns, shipping streaks, test health, insights |
| `/officeHours` | Planning | YC-style brainstorming with 6 forcing questions → design doc |
| `/productReview` | Planning | Founder/product lens review with 4 modes (mvp/growth/scale/pivot) |
| `/archReview` | Planning | Engineering architecture review with mandatory diagrams |

**2. Centralized Output Directory**

All skill outputs write to `~/.agentic-workflow/<repo-slug>/` with subdirectories: `reviews/`, `investigations/`, `qa/`, `plans/`, `releases/`, `retros/`. Replaces the project-local `.review-cache/` pattern. Persists across sessions and branches.

**3. Shared Preamble with Bootstrap Gate**

Every skill includes a shared preamble that lists all 14 skills, points to the centralized output directory, and checks bootstrap status. If not bootstrapped, prompts user to run `setup.sh`.

**4. Existing Skill Updates**

- `/review` — migrated to centralized output dir, added SQL safety checks and LLM trust boundary analysis to triage
- `/postReview` — migrated to centralized output dir
- `/addressReview` — migrated to centralized output dir

**5. Infrastructure Updates**

- `setup.sh` — symlinks all 14 skills, creates `~/.agentic-workflow/` base directory
- `/bootstrap` — references full 14-skill inventory, suggests pipeline skills as next steps
- `CLAUDE.md` + `.claude/rules/` — trimmed CLAUDE.md (navigation only) + glob-scoped rule files for domain-specific guidance
- `ARCHITECTURE.md` — updated directory tree, mermaid diagram, component descriptions, key rules

### Success Criteria

- [x] All 14 skills have consistent shared preamble with bootstrap gate
- [x] Zero telemetry code in any skill
- [x] All outputs write to `~/.agentic-workflow/<repo-slug>/` with correct subdirectories
- [x] Existing `/review` → `/postReview` → `/addressReview` chain works with new paths
- [x] All skills follow SKILL.md format with proper YAML frontmatter
- [x] Verbose camelCase naming throughout
- [x] `setup.sh` updated with all new skill symlinks and output directory creation
- [x] `/bootstrap` references full 14-skill inventory
- [x] `CLAUDE.md` + `.claude/rules/` reflect complete architecture with all skills and output directory
- [x] `ARCHITECTURE.md` has updated directory tree, mermaid diagram, and key rules

### Timeline

Delivered.

---

## v1.1 — Quality and Developer Experience

### Scope

Improvements identified from code review and gap analysis of the current implementation.

**1. ESLint and Prettier**
No linter or formatter is currently configured. Add `eslint` with `@typescript-eslint/parser` and `prettier` with a shared config. Add `npm run lint` and `npm run format` scripts. Enforce consistent code style across contributions.

**2. CI/CD Pipeline** — delivered
`.github/workflows/ci.yml` runs on pull requests: typecheck, tests and builds for every TypeScript package, the bash
suites, the repo checks and the C# build. What it still lacks is a lint step (`npm run lint`), which waits on item 1.

### Success Criteria

- [ ] ESLint and Prettier pass with zero warnings on the entire codebase
- [x] CI pipeline runs on every PR and blocks merge on failure

### Timeline

Estimated 2-4 weeks of focused development. Each item is independent and can be delivered incrementally. CI/CD is done; ESLint/Prettier remains, and its step then joins CI.

---

## v2.0 — Platform Expansion

### Scope

Extend into a broader multi-agent development platform.

**Note:** Persistent memory, semantic search, graph visualization, and conversation embedding were originally planned here but are now delegated to prism-mcp. Agent handoffs run on Prism handoffs (`session_save_handoff` / `session_load_context`), judge's outbox and teammate names (agent teams), and Sindri's ledger; there is no bridge server, so the bridge's planned MCP tools are dropped.

**1. Multi-Model Support**

The toolkit supports Claude Code, Codex, and Cursor as equal hosts through a provider adapter layer (`providers/<name>/`, capability vocabulary in `skills/_shared/capabilities.md`, see `planning/PROVIDERS.md`). Remaining work:

- [x] **Claude Code, Codex, Cursor** — Skills, hooks, MCP registration, repo instructions, and judge all have per-provider adapters.
- **Gemini CLI** — If/when Google ships MCP support, add configuration templates.

**2. Skill Marketplace Integration**

The setup script references plugin marketplaces (`claude-plugins-official`, `voltagent-subagents`, `compound-engineering-plugin`). Build deeper integration:

- Skill discovery and installation from community repositories
- Skill versioning and update mechanism
- Dependency declaration between skills (e.g., `/addressReview` depends on `/review`)

### Success Criteria

- [ ] Skill installation supports remote repositories beyond local symlinks

### Timeline

Estimated 2-3 months. This is a significant scope expansion and should be broken into sub-releases:

- v2.0-rc: Multi-model documentation (2-3 weeks)
- v2.0: Skill marketplace integration and polish (2-3 weeks)
