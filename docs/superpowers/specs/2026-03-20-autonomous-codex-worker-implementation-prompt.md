# Implementation Prompt: Autonomous Codex Worker

> Copy this entire prompt into a new Claude Code session to implement the feature.

---

## Context

You are implementing the "Autonomous Codex Worker" feature for the `agentic-workflow` project. This adds fully automated dialogue between Claude Code and Codex via the MCP bridge — Codex runs as a headless thinking partner that polls for tasks, reads code for context, and responds with analysis and pseudocode. It never modifies files.

**Repo:** `/Users/thor/repos/agentic-workflow`
**Spec:** `docs/superpowers/specs/2026-03-20-autonomous-codex-worker-design.md` — READ THIS FIRST. It is the authoritative design document with all decisions, state models, pseudocode, and file change lists.
**Project instructions:** `CLAUDE.md` — READ THIS SECOND. It defines the tech stack, patterns, and merge gate.

## Branching Strategy

Create an epic branch and feature branches:

```
main
└── epic/autonomous-worker
    ├── feat/worker-schema-changes      # Schema + type cascading
    ├── feat/worker-peek-endpoint       # Peek service, route, MCP tool
    ├── feat/worker-sse-enrichment      # BridgeEvent full row data
    ├── feat/worker-orchestrator        # worker.ts — poll loop, process pool, backoff
    ├── feat/worker-notify              # notify.ts — SSE → macOS notifications
    ├── feat/worker-hook                # check-bridge.sh + hook registration
    ├── feat/worker-start-script        # start.sh enhancements
    ├── feat/worker-ui-updates          # Timeline badges, diagram styling, types
    └── feat/worker-docs                # All planning doc + CLAUDE.md + enhancePrompt updates
```

Each feature branch merges into `epic/autonomous-worker`. The epic merges into `main` when complete.

**Branch order matters** — later branches depend on earlier ones:
1. `schema-changes` (no deps)
2. `peek-endpoint` (depends on schema-changes for `peekUnreadCount` in DbClient)
3. `sse-enrichment` (no deps on above, can parallel)
4. `orchestrator` (depends on schema-changes + peek-endpoint)
5. `notify` (depends on sse-enrichment)
6. `hook` (depends on peek-endpoint)
7. `start-script` (depends on orchestrator + notify)
8. `ui-updates` (depends on schema-changes)
9. `docs` (depends on all above)

## Key Patterns to Follow

Read these files before writing any code:

- `mcp-bridge/src/application/result.ts` — `ok<T>()`, `err<T>()`, `AppResult<T>`. Services never throw.
- `mcp-bridge/src/db/client.ts` — `DbClient` interface, `TaskRow`, `MessageRow`, prepared statements.
- `mcp-bridge/src/db/schema.ts` — `MIGRATIONS` constant, `createDatabase()`.
- `mcp-bridge/src/transport/types.ts` — `defineRoute<TSchema>()`, `RouteSchema`.
- `mcp-bridge/src/transport/schemas/` — Zod schemas for request/response validation.
- `mcp-bridge/src/mcp.ts` — MCP tool definitions (hand-rolled Zod, not schema-driven).
- `mcp-bridge/src/application/events.ts` — `createEventBus()`, `BridgeEvent` type.
- `mcp-bridge/src/application/services/assign-task.ts` — Current `assignTask` service (hardcoded `sender: "system"`).
- `mcp-bridge/src/routes/messages.ts` — Route factory pattern for message endpoints.
- `mcp-bridge/src/index.ts` — Server wiring (EventBus, CORS, SSE, route registration).

**Conventions:**
- ESM only — all imports use `.js` extensions
- No classes — factory functions and closures
- No exceptions in business logic — `AppResult` everywhere
- Zod for all external input
- Prepared statements only — never interpolate SQL
- Tests with Vitest using in-memory SQLite

## Implementation Details Per Branch

### 1. `feat/worker-schema-changes`

**Files to modify:**
- `mcp-bridge/src/db/schema.ts` — Add `working_dir TEXT` to tasks CREATE TABLE in the MIGRATIONS constant
- `mcp-bridge/src/db/client.ts` — Add `working_dir: string | null` to `TaskRow`, update `insertTask` SQL to include `@working_dir`, update the `Omit` type on `insertTask` method
- `mcp-bridge/src/transport/schemas/tasks.ts` — Add `working_dir: z.string().optional()` and `sender: z.string().optional()` to `AssignTaskBodySchema`, add `working_dir: z.string().nullable()` to `TaskResponseSchema`
- `mcp-bridge/src/application/services/assign-task.ts` — Add `working_dir?: string` and `sender?: string` to `AssignTaskInput`, pass `working_dir` through to `db.insertTask()`, set message sender to `input.sender ?? "system"`
- `mcp-bridge/src/mcp.ts` — Add `working_dir: z.string().optional()` and `sender: z.string().optional()` to the `assign_task` tool args
- Delete `mcp-bridge/bridge.db` if it exists

**Verification:** `npm run typecheck && npm test` in `mcp-bridge/`

### 2. `feat/worker-peek-endpoint`

**New files:**
- `mcp-bridge/src/application/services/peek-unread.ts` — `peekUnreadCount(db, recipient): AppResult<{ count: number }>`

**Files to modify:**
- `mcp-bridge/src/db/client.ts` — Add `peekUnreadCount(recipient: string): number` to `DbClient` interface, add prepared statement: `SELECT COUNT(*) as count FROM messages WHERE recipient = @recipient AND read_at IS NULL`
- `mcp-bridge/src/routes/messages.ts` — Add `GET /messages/peek` route using the peek service
- `mcp-bridge/src/mcp.ts` — Add `peek_unread` tool with `recipient` param

**New tests:**
- Test that peek returns correct count
- Test that peek does NOT mark messages as read (call peek, then call getUnread, verify messages still returned)

**Verification:** `npm run typecheck && npm test`

### 3. `feat/worker-sse-enrichment`

**Files to modify:**
- `mcp-bridge/src/application/events.ts` — Change `BridgeEvent` data type from `{ id: string; conversation: string }` to the full `MessageRow` or `TaskRow`
- All controllers that emit events — update emit calls to pass the full row instead of just `{ id, conversation }`

Read the existing event emit calls in the controllers to find every emit site.

**Verification:** `npm run typecheck && npm test`, then manually test SSE stream with `curl http://localhost:3100/events`

### 4. `feat/worker-orchestrator`

**New files:**
- `mcp-bridge/src/worker.ts` — The entire worker orchestrator

This is the largest piece. Follow the spec's state model, poll loop pseudocode, and Codex process lifecycle exactly. Key implementation details:

- **Worker ID:** Read from `~/.agentic-workflow/worker-id`, create if missing. Use `crypto.randomBytes(2).toString('hex')` for the 4 hex chars.
- **REST client:** Use Node's native `fetch()` (available in Node 20+) to call the bridge API. No need for axios or similar.
- **Poll loop:** `setInterval` or `setTimeout` chain. Prefer `setTimeout` chain so interval is dynamic.
- **Codex spawning:** `child_process.spawn("codex", [...args], { cwd, stdio: ["ignore", "pipe", "pipe"] })`
- **Env vars:** Read from `process.env` with defaults at the top of the file.
- **Graceful shutdown:** `process.on("SIGTERM", ...)` and `process.on("SIGINT", ...)`
- **Logging:** `console.log("[worker] ...")` with structured prefixes per the spec.

Add `"worker": "node dist/worker.js"` to `package.json` scripts.

**New tests:**
- Backoff logic (unit): verify exponential growth, ceiling, reset
- Worker ID persistence (unit): creates file, reads it back
- Message filtering (unit): only task messages spawn workers
- Queue cap (unit): rejects when full

**Verification:** `npm run typecheck && npm test`, then manual smoke test: start bridge, start worker, use curl to assign a task, watch worker log output

### 5. `feat/worker-notify`

**New files:**
- `mcp-bridge/src/notify.ts` — SSE watcher with macOS notifications

**Implementation:**
- `npm install eventsource @types/eventsource` in mcp-bridge
- Connect to `http://localhost:3100/events` using `EventSource`
- Parse SSE data, extract sender/recipient/payload from the enriched event
- Fire `osascript -e 'display notification ...'` via `child_process.execSync`
- Platform check: `process.platform === "darwin"`, exit cleanly otherwise
- Auto-reconnect: EventSource reconnects natively, but add a `onerror` handler that logs

**Verification:** `npm run typecheck`, then manual test: start bridge + notify, send a message via curl, verify macOS notification appears

### 6. `feat/worker-hook`

**New files:**
- `mcp-bridge/scripts/check-bridge.sh` — The hook script (make executable: `chmod +x`)

**Files to modify:**
- `setup.sh` — Add symlink of `check-bridge.sh` to `~/.claude/scripts/`

The hook script is verbatim from the spec. Uses `curl` + `jq` to peek, prints message if count > 0.

**Verification:** Run the script manually with bridge running, verify output

### 7. `feat/worker-start-script`

**Files to modify:**
- `start.sh` — Add bridge health wait loop, worker process, notify process

Follow the spec's start script section. Key: wait for `/health` before starting worker and notify.

**Verification:** Run `./start.sh`, verify all 4 processes start, Ctrl+C kills all

### 8. `feat/worker-ui-updates`

**Files to modify:**
- `ui/src/lib/types.ts` — Add `working_dir: string | null` to `Task`
- `ui/src/components/vertical-timeline.tsx` — Add `working_dir` path badge on task cards, add worker badge for `codex-worker-*` assignees
- `ui/src/lib/diagrams.ts` — In `buildDirectedGraph()`, give `codex-worker-*` nodes a distinct shape/color

**Verification:** `cd ui && npm run build` (type check + build), then visual check in browser

### 9. `feat/worker-docs`

Update all planning docs per the spec's "Documentation Updates" section. Each doc has specific bullets of what to add. Also update:
- `CLAUDE.md` — Directory tree, env vars, start.sh description
- `skills/enhancePrompt/SKILL.md` — Update step 3 to reflect automated worker

**Verification:** Read each updated doc to ensure consistency

## Merge Gate (per CLAUDE.md)

Before merging each feature branch into the epic:
1. `cd mcp-bridge && npm run typecheck` — zero errors
2. `cd mcp-bridge && npm test` — all green
3. No `any` types outside Fastify integration boundaries

Before merging the epic into `main`:
1. All of the above
2. `cd ui && npm run build` — succeeds
3. Manual smoke test: `./start.sh` → assign task via curl → worker picks it up → response appears in UI

## Commit Convention

Format: `type: short description`
Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
Keep commits atomic — one logical change per commit.
