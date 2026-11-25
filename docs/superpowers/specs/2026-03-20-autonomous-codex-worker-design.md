# Autonomous Codex Worker

> Fully automated dialogue between Claude Code and Codex via the agentic-bridge. Codex runs as a headless thinking partner — reads code for context, responds with analysis and pseudocode, never modifies files.

## Problem

The agentic-bridge is store-and-forward: messages persist in SQLite, but neither agent polls automatically. Every exchange requires the user to manually switch terminals and tell each agent to check for messages. This makes multi-agent dialogue impractical, especially when Claude Code subagents need Codex input.

## Solution

A Node.js worker orchestrator that:
1. Polls the bridge REST API for unread messages addressed to its worker ID
2. Spawns a short-lived Codex process per message batch (one per conversation turn)
3. Includes full conversation history in the Codex prompt for context continuity
4. Uses exponential backoff when idle, resets on activity
5. Caps concurrent Codex processes, queues overflow with a bounded queue
6. Tracks conversation idle time, cleans up stale state after a TTL

Plus supporting infrastructure:
- Desktop notifications via SSE watcher
- Claude Code hook for auto-checking after subagent completions
- Peek endpoint for checking unread count without consuming messages
- Acknowledge endpoint for two-phase message consumption

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Where it lives | `mcp-bridge/src/worker.ts` | Shares types (import only), separate process |
| Bridge access | REST API (not direct DB) | Avoids SQLite write contention between processes |
| Worker instances | Single instance only | Persistent ID file enforces; two instances reading same ID would double-consume |
| Worker identity | Persistent file at `~/.agentic-workflow/worker-id` | Survives restarts, messages never orphaned |
| Identity format | `codex-worker-{4 hex}` auto-generated on first run | Differentiates sessions without manual naming |
| Polling | Exponential backoff 5s→1hr, reset on message | Token-efficient during idle, responsive during activity |
| Codex process model | Short-lived process per conversation turn | Codex CLI doesn't accept follow-up stdin; each invocation gets full conversation history |
| Context isolation | Separate Codex invocation per conversation | Full history included in prompt, zero bleed |
| History cap | Last 50 messages per conversation | Prevents prompt from exceeding Codex context window |
| Concurrent cap | 3 workers, configurable | Prevents resource exhaustion from burst conversations |
| Queue cap | 20 conversations max | Prevents unbounded memory growth |
| Idle TTL | 10 minutes per conversation | Frees slots for queued conversations |
| Working directory | Required `working_dir` field on `assign_task` | Worker must know which repo to reason about |
| Task sender | New `sender` param on `assign_task` | Worker needs to know who to reply to (currently hardcoded to "system") |
| Codex mode | `--full-auto` but read-only prompt | Can read code for context, responds only via bridge |
| Message consumption | Two-phase: peek → consume only when ready | Prevents message loss when queue is full |
| Message filtering | Only `kind: "task"` messages spawn workers | `send_context`/`status`/`reply` to active conversations only |
| Schema change | Direct update, wipe DB | No production data to migrate |
| Codex MCP access | Relies on `codex mcp add agentic-bridge` from `setup.sh` | Already registered, no per-spawn config needed |
| Notifications | macOS only via `osascript` | Platform-specific, no-op on Linux |
| SSE dependency | `eventsource` npm package | Node 20 floor, native EventSource not available |

## Schema Change

Add `working_dir` column to the `tasks` table:

```sql
CREATE TABLE tasks (
  id TEXT PRIMARY KEY,
  conversation TEXT NOT NULL,
  domain TEXT NOT NULL,
  summary TEXT NOT NULL,
  details TEXT NOT NULL,
  analysis TEXT,
  assigned_to TEXT,
  working_dir TEXT,
  status TEXT NOT NULL DEFAULT 'pending'
    CHECK(status IN ('pending','in_progress','completed','failed')),
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
```

`working_dir` is nullable in the schema (not all tasks target a worker), but the worker orchestrator rejects tasks without it — sends a `failed` status back via the bridge.

Cascading type changes:
- `TaskRow.working_dir: string | null`
- `assign_task` MCP tool and REST endpoint gain optional `working_dir` Zod param (in **both** the MCP tool args in `mcp.ts` AND the REST body schema in `transport/schemas/tasks.ts`)
- `assign_task` gains optional `sender` Zod param — flows into the task message's `sender` field (currently hardcoded to `"system"`). When provided, the message sender is set to this value so the worker knows who to reply to.
- `AssignTaskInput` interface gains `working_dir?: string` and `sender?: string`
- `AssignTaskBodySchema` Zod schema gains `working_dir: z.string().optional()` and `sender: z.string().optional()`
- `TaskResponseSchema` Zod schema gains `working_dir: z.string().nullable()`
- `insertTask` prepared statement SQL updated to include `@working_dir` in the INSERT column list and VALUES
- `updateTaskStatus` does NOT need `working_dir` — it is set once at creation
- Task message payload in `assignTask` service includes `working_dir`
- Task message `sender` field set to `input.sender ?? "system"` (backward compatible)
- Delete `bridge.db` to recreate with new schema

## Two-Phase Message Consumption

The existing `getUnreadMessages` marks messages as read atomically on retrieval. This is dangerous for the worker — if it reads messages but fails to deliver them to Codex (process crash, spawn failure), those messages are lost.

### New endpoints

**`GET /messages/peek?recipient=X`** — Returns unread message count without side effects.

```json
{ "ok": true, "data": { "count": 3 } }
```

**`GET /messages/unread?recipient=X`** — Existing endpoint, unchanged. Returns messages and marks them read. The worker calls this only after confirming it can process the messages (slot available or queue not full).

### MCP tools

- `peek_unread` — count only, no side effects
- `get_unread` — unchanged (marks read on retrieval)

### Worker consumption flow

```
1. peek_unread(workerId) → count
2. If count > 0 AND (has available slot OR queue not full):
     get_unread(workerId) → messages
     process messages
3. If count > 0 BUT no slot AND queue full:
     leave messages unconsumed (don't call get_unread)
     log warning
```

This ensures messages are only consumed when the worker can process them.

## Worker Orchestrator

### Entry point

`mcp-bridge/src/worker.ts` → `dist/worker.js`

### Worker identity persistence

On first run, generates `codex-worker-{4 hex}` and writes to `~/.agentic-workflow/worker-id`. On subsequent runs, reads the file. This ensures:
- Messages addressed to the worker survive restarts
- No orphaned messages from ID changes
- Deterministic identity across sessions

### State model

```typescript
interface WorkerPool {
  workerId: string;                              // persistent, from ~/.agentic-workflow/worker-id
  maxConcurrent: number;                         // env: WORKER_MAX_CONCURRENT (default 3)
  maxQueueDepth: number;                         // env: WORKER_MAX_QUEUE (default 20)
  idleTtlMs: number;                             // env: WORKER_IDLE_TTL (default 600_000)
  pollFloorMs: number;                           // env: WORKER_POLL_FLOOR (default 5_000)
  pollCeilingMs: number;                         // env: WORKER_POLL_CEILING (default 3_600_000)
  bridgeUrl: string;                             // env: BRIDGE_URL (default http://localhost:3100)
  active: Map<string, ConversationState>;        // keyed by conversation UUID
  queue: QueueEntry[];                           // conversations waiting for a slot
}

interface QueueEntry {
  conversationId: string;
  messages: MessageRow[];                        // messages consumed at queue time, held until slot opens
  taskId: string;
  workingDir: string;
  replyTo: string;                               // sender from task message, who to reply to
}

interface ConversationState {
  conversationId: string;
  taskId: string;                                // for report_status calls
  workingDir: string;                            // from task.working_dir
  replyTo: string;                               // original sender to address responses to
  lastActivityAt: number;                        // epoch ms, reset on each message
  processingNow: boolean;                        // true while a Codex process is running
  spawnedAt: number;                             // epoch ms, for response detection
}
```

Note: `ConversationState` no longer holds a `ChildProcess` reference. Codex processes are short-lived (one per turn), so the state tracks the conversation, not the process. `QueueEntry` stores the consumed messages so they survive until a slot opens.

**Single instance constraint:** Only one worker process should run at a time. The persistent worker ID file at `~/.agentic-workflow/worker-id` is the identity anchor. Running two instances with the same ID causes double-consumption via `get_unread`. `start.sh` enforces this by starting exactly one worker. Running `node dist/worker.js` manually alongside `start.sh` is unsupported.

### Poll loop

```
currentInterval = pollFloor

loop forever:
  count = REST GET /messages/peek?recipient={workerId}

  if count > 0:
    if active has available capacity OR queue has room:
      messages = REST GET /messages/unread?recipient={workerId}
      currentInterval = pollFloor                // reset backoff
      group messages by conversation UUID

      for each conversation group:
        if conversation in active:
          spawn Codex for this turn (see "Codex process lifecycle")
          update lastActivityAt
        else if active.size < maxConcurrent:
          extract task from messages (kind: "task" → parse payload JSON → get task via REST GET /tasks/{task_id})
          if no task found OR task.working_dir missing:
            REST POST /tasks/report with status: "failed", reason
            skip this conversation
          replyTo = task message sender (from the message's sender field, set by assign_task)
          create ConversationState { conversationId, taskId, workingDir, replyTo, lastActivityAt: now, processingNow: false, spawnedAt: 0 }
          spawn Codex for this turn
        else if queue.length < maxQueueDepth:
          extract task (same as above) to get workingDir, taskId, replyTo
          add QueueEntry { conversationId, messages, taskId, workingDir, replyTo }
        else:
          REST POST /tasks/report with status: "failed", "worker queue full"
    else:
      // leave messages unconsumed (don't call get_unread), back off
      currentInterval = min(currentInterval * 2, pollCeiling)
  else:
    currentInterval = min(currentInterval * 2, pollCeiling)

  // reap idle conversations
  for each active conversation:
    if now - conversation.lastActivityAt > idleTtl AND NOT processingNow:
      remove from active
      if queue is not empty:
        entry = dequeue next QueueEntry
        create ConversationState from entry { conversationId, taskId, workingDir, replyTo }
        spawn Codex with entry.messages

  sleep(currentInterval)
```

### Codex process lifecycle

Each conversation turn spawns a short-lived Codex process:

1. Fetch conversation history: `REST GET /messages/conversation/{id}` (last 50 messages — prevents prompt from exceeding Codex context window)
2. Build prompt with history + new message content (see "System prompt template")
3. Record `spawnedAt = Date.now()`
4. Spawn Codex with `cwd: conversationState.workingDir`, capture stdout/stderr
5. Set `processingNow = true`
6. Listen for process `exit` event:
   - **Exit code 0:** Verify Codex responded by fetching `GET /messages/conversation/{id}` and scanning for any message where `sender === workerId` AND `created_at > spawnedAt`. If no response found, send fallback: `report_status(conversation, sender: workerId, recipient: replyTo, task_id: taskId, status: "completed", payload: "Codex completed without explicit response")`
   - **Non-zero exit:** Send `report_status(conversation, sender: workerId, recipient: replyTo, task_id: taskId, status: "failed", payload: "Codex exited with code {code}: {stderr last 500 chars}")`
7. Set `processingNow = false`, update `lastActivityAt`

**Watchdog timeout:** If a Codex process runs longer than 5 minutes (env: `WORKER_PROCESS_TIMEOUT`, default 300000ms), send SIGTERM, wait 10s, SIGKILL. Report status as failed with "process timed out."

### Backoff curve

5s → 10s → 20s → 40s → 80s → 160s → 320s → 640s → 1280s → 2560s → 3600s (cap)

Reaches the 1-hour ceiling in ~10 empty polls (~90 minutes of silence). Resets to 5s on any message.

### Codex spawning

Each turn spawns:

```bash
codex --full-auto --quiet \
  --prompt "<system prompt with conversation history>"
```

With `cwd` set to `task.working_dir`:

```typescript
const child = spawn("codex", [
  "--full-auto",
  "--quiet",
  "--prompt", prompt
], {
  cwd: conversationState.workingDir,
  stdio: ["ignore", "pipe", "pipe"]  // capture stdout/stderr for logging
});
```

System prompt template:

```
You are a thinking partner in a dialogue via the agentic-bridge.
Your worker ID is {workerId}.
Conversation: {conversationId}.

You MUST NOT modify any files or run destructive commands.
You read code for context and respond with analysis,
suggestions, and pseudocode only.

## Conversation history (last 50 messages)

{formatted history from get_messages — each message as "[sender → recipient]: payload"}

## New message to process

From: {replyTo}
{payload}

{meta_prompt if present}

## Instructions

Respond to the message above using the agentic-bridge report_status tool:
- conversation: {conversationId}
- sender: {workerId}
- recipient: {replyTo}
- task_id: {taskId}
- status: "completed"
- payload: your response
```

The history is capped at the last 50 messages. For conversations exceeding this, older messages are omitted with a note: `[{N} earlier messages omitted]`. This prevents the prompt from exceeding Codex's context window.

Codex has MCP bridge tools available because `setup.sh` already runs `codex mcp add agentic-bridge`. No per-spawn MCP configuration needed.

### Message filtering

The worker only processes messages that initiate or continue conversations:

- **`kind: "task"`** — Spawns a new ConversationState if conversation is new. Parses payload JSON to extract `task_id`, fetches task via `GET /tasks/{id}` to read `working_dir`.
- **`kind: "context"` / `kind: "reply"`** — Only processed if the conversation is already active (has a ConversationState). Ignored otherwise — the worker cannot determine `working_dir` from these message types.
- **`kind: "status"`** — Ignored. Status messages are reports, not requests.

### Graceful shutdown

The worker registers a `SIGTERM` / `SIGINT` handler:

1. Stop the poll loop (set a `shuttingDown` flag)
2. For each active conversation with `processingNow === true`:
   - Send `SIGTERM` to the Codex child process
   - Wait up to 10 seconds for exit
   - If still alive, `SIGKILL`
3. For queued conversations, send `report_status(status: "failed", payload: "Worker shutting down")`
4. Exit

## Peek Endpoint

### REST: `GET /messages/peek`

Query params: `recipient` (string, required)

Response:
```json
{
  "ok": true,
  "data": { "count": 3 }
}
```

No side effects — does not mark messages as read.

### MCP tool: `peek_unread`

Same logic, exposed over stdio transport.

### Implementation

New prepared statement in `DbClient`:

```sql
SELECT COUNT(*) as count FROM messages
WHERE recipient = @recipient AND read_at IS NULL
```

New service: `peekUnreadCount(db, recipient): AppResult<{ count: number }>`

## Desktop Notifications

### Entry point

`mcp-bridge/src/notify.ts` → `dist/notify.js`

### Behavior

- Connects to `http://localhost:3100/events` SSE stream
- On `message:created`: macOS notification with sender, recipient, payload preview (80 chars)
- On `task:created`: macOS notification with domain, summary, assigned_to
- Auto-reconnects on connection drop (exponential backoff: 1s → 2s → 4s → 30s cap)
- No polling, no tokens — purely event-driven
- macOS only — logs a warning and exits cleanly on non-macOS platforms

### Notification format

```
Title: "Bridge: message for {recipient}"
Body:  "{sender}: {payload first 80 chars}"
```

```
Title: "Bridge: task for {assigned_to}"
Body:  "{domain}: {summary}"
```

Uses `osascript` for macOS notifications — no external dependencies beyond Node.

Dependency: `eventsource` npm package (Node 20 floor, native EventSource not available).

## Claude Code Hook

### Script: `mcp-bridge/scripts/check-bridge.sh`

```bash
#!/usr/bin/env bash
# Peek for unread messages — silent if bridge is down or no messages
count=$(curl -sf --max-time 2 'http://localhost:3100/messages/peek?recipient=claude-code' \
  | jq -r '.data.count // 0' 2>/dev/null) || count=0

if [ "$count" -gt 0 ]; then
  echo "You have $count unread message(s) on the agentic-bridge. Use get_unread to read them."
fi
```

Uses `jq` for JSON parsing (simpler, no hanging stdin risk). Falls back silently if bridge is down or `jq` unavailable. The `--max-time 2` prevents curl from blocking the hook.

Fires as a `PostToolUse` hook after `Agent` tool calls complete. Output is purely informational — Claude Code decides whether to act on it.

### Hook registration

Added to settings.json or configured via `/update-config`:

```json
{
  "hooks": {
    "PostToolUse": [{
      "matcher": "Agent",
      "command": "~/.claude/scripts/check-bridge.sh"
    }]
  }
}
```

The script is symlinked from `mcp-bridge/scripts/check-bridge.sh` during setup.

## Start Script

`start.sh` enhanced with two additional background processes:

```bash
echo "Starting MCP bridge on :3100..."
(cd "$BRIDGE_DIR" && npm start) &
BRIDGE_PID=$!

# Wait for bridge to be ready before starting dependents
echo "Waiting for bridge..."
for i in $(seq 1 30); do
  curl -sf http://localhost:3100/health >/dev/null 2>&1 && break
  sleep 1
done

echo "Starting UI dashboard on :3000..."
(cd "$UI_DIR" && npm run dev) &

echo "Starting Codex worker..."
(cd "$BRIDGE_DIR" && node dist/worker.js) &

echo "Starting desktop notifications..."
node "$BRIDGE_DIR/dist/notify.js" &

wait
```

Key change: the worker and notify processes depend on the bridge being up (worker hits REST API, notify subscribes to SSE). The script waits for `/health` to respond before starting them.

Existing `cleanup()` trap (`kill 0`) performs the shutdown of all processes. The worker's own SIGTERM handler ensures spawned Codex processes are cleaned up.

Worker logs its identity on startup:
```
Codex worker started: codex-worker-a3f2
Polling interval: 5s–3600s (exponential backoff)
Max concurrent: 3, idle TTL: 10m, queue cap: 20
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `WORKER_MAX_CONCURRENT` | `3` | Max simultaneous Codex processes |
| `WORKER_MAX_QUEUE` | `20` | Max queued conversations before rejection |
| `WORKER_IDLE_TTL` | `600000` | Ms before idle conversation state is reaped |
| `WORKER_POLL_FLOOR` | `5000` | Fastest poll interval (ms) |
| `WORKER_POLL_CEILING` | `3600000` | Slowest poll interval (ms) |
| `WORKER_PROCESS_TIMEOUT` | `300000` | Max Codex process runtime before kill (ms) |
| `BRIDGE_URL` | `http://localhost:3100` | Bridge REST API base URL |

## Logging

The worker uses `console.log` with structured prefixes for grep-ability:

```
[worker] Codex worker started: codex-worker-a3f2
[worker] Poll: 0 unread, interval=10000ms
[worker] Poll: 3 unread, interval=5000ms (reset)
[worker] Spawn: conversation=abc123 cwd=/Users/thor/repos/foo
[worker] Exit: conversation=abc123 code=0 responded=true
[worker] Reap: conversation=abc123 idle=600s
[worker] Queue: conversation=def456 depth=2/20
[worker] Reject: conversation=ghi789 reason="queue full"
[worker] Shutdown: draining 2 active, 1 queued
```

All output goes to stdout/stderr. When started via `start.sh`, output intermixes with bridge and UI logs. A future enhancement could add file-based logging.

## SSE Event Data

The existing `BridgeEvent` emits minimal data (`{ id, conversation }`). The `notify.ts` watcher needs `sender`, `recipient`, and `payload` to format notifications. Two options:

1. Enrich the event data to include full message/task rows
2. Have `notify.ts` fetch the full record via REST after receiving the event

**Choice: Option 1.** The EventBus already receives the full row from the service layer — just pass it through instead of extracting only `id` and `conversation`. This is a small change to `events.ts` types and the emit calls in controllers.

## UI Changes

The conversation dashboard needs updates to surface worker activity and the new `working_dir` field.

### Conversation List (`ui/src/app/page.tsx`)

No structural changes. Worker conversations appear naturally — they have a `codex-worker-*` participant. The existing `ConversationCard` shows participants, message/task counts, and timestamps, which is sufficient.

### Conversation Detail (`ui/src/app/conversation/[id]/page.tsx`)

**Timeline updates (`ui/src/components/vertical-timeline.tsx`):**
- Task cards gain a `working_dir` field displayed as a monospace path badge (e.g., `~/repos/foo`) when present
- Task cards show `assigned_to` with a visual distinction for worker IDs (e.g., `codex-worker-a3f2` gets a robot/worker icon or "worker" badge)

**Diagram updates (`ui/src/lib/diagrams.ts`):**
- `buildDirectedGraph()` — Worker agent nodes (IDs matching `codex-worker-*`) get a distinct shape or color to differentiate from interactive agents
- `buildSequenceDiagram()` — No change needed, worker messages flow through naturally

### Types (`ui/src/lib/types.ts`)

- `Task` interface gains `working_dir: string | null`

### API Client (`ui/src/lib/api.ts`)

No changes — the existing `fetchTasks()` returns whatever the API returns. The new `working_dir` field flows through automatically from the REST response.

### SSE Hook (`ui/src/hooks/use-sse.ts`)

No changes needed. The hook already listens for all event types. The enriched `BridgeEvent` data (full rows instead of `{id, conversation}`) is consumed by the page components that re-fetch on events — they don't parse the event data directly.

## Documentation Updates

All planning docs that reference the bridge, tasks schema, or deployment need updates.

### `planning/API_CONTRACT.md`

Add:
- `GET /messages/peek` endpoint (full request/response/error/side-effects documentation matching existing format)
- `working_dir` and `sender` fields to `POST /tasks/assign` request body table
- `working_dir` field to task response schemas (GET /tasks/:id, GET /tasks/conversation/:id, POST /tasks/assign response)
- `peek_unread` MCP tool section
- `working_dir` and `sender` params added to `assign_task` MCP tool section

### `planning/ERD.md`

Add:
- `working_dir` column to the `tasks` table documentation (TEXT, nullable, absolute path for worker cwd)
- Note that `sender` on task messages is now dynamic (from `assign_task` input, not hardcoded `"system"`)

### `planning/ARCHITECTURE.md`

Add:
- Worker orchestrator as a new subsystem in the system overview
- `worker.ts` and `notify.ts` entry points in the directory tree
- Worker poll loop in the data flow diagram
- Desktop notification flow (SSE → osascript)
- Claude Code hook flow (PostToolUse → peek → notification)
- Updated skill pipeline showing how `assign_task` with `working_dir` initiates worker dialogue

### `planning/DEPLOYMENT.md`

Add:
- Worker process in the "Building for Production" section (`dist/worker.js` entry point)
- `notify.ts` entry point
- Worker identity file at `~/.agentic-workflow/worker-id`
- New environment variables table (WORKER_MAX_CONCURRENT, etc.)
- Updated `start.sh` description showing 4 processes instead of 2
- Hook script setup (`check-bridge.sh` symlink)

### `planning/LOCAL_DEV.md`

Add:
- Worker process in the development startup section
- Desktop notifications as part of the dev environment
- How to test the worker locally (assign a task, watch it respond)
- Troubleshooting: worker ID file location, how to reset

### `planning/TESTING.md`

Add:
- Worker unit test descriptions (state machine, backoff, queue)
- Worker integration test descriptions (mock Codex, round-trip)
- Peek service tests
- Note about `eventsource` test mocking for notify.ts

### `CLAUDE.md`

Add:
- Worker entry point in the Architecture section directory tree
- `notify.ts` and `check-bridge.sh` in the directory tree
- `working_dir` mention in the Key Patterns section where tasks are discussed
- Worker environment variables in the Commands section
- Updated `start.sh` description

### `skills/enhancePrompt/SKILL.md`

Update step 3 ("Evaluate Codex dialogue value"):
- Remove manual instruction to tell Codex "Check your unread messages" — this is now automated
- Update "How to initiate" to reflect the automated worker: "Assign a task via `assign_task` with `working_dir` and `assigned_to` set to the worker ID. The worker will pick it up automatically."
- Add note about checking worker status via the UI dashboard

## Testing Strategy

### Unit tests

- **Peek service:** Returns correct count, does not mark messages as read
- **Worker state machine:** Spawn, reap, queue, dequeue lifecycle
- **Backoff logic:** Reset on message, exponential growth, ceiling enforcement
- **Message filtering:** Only task messages spawn new conversations
- **Queue cap:** Rejects when full, sends failure status
- **Worker ID persistence:** Creates file on first run, reads on subsequent

### Integration tests

- **Poll-respond loop:** Mock Codex process (script that calls report_status), verify round-trip
- **Concurrent conversations:** 3 simultaneous, verify isolation
- **Crash recovery:** Kill mock Codex mid-flight, verify failure status sent
- **Bridge down:** Worker retries gracefully when REST API unavailable

## File Changes

### New files

| File | Purpose |
|------|---------|
| `mcp-bridge/src/worker.ts` | Worker orchestrator — poll loop, process pool, backoff |
| `mcp-bridge/src/notify.ts` | SSE watcher — macOS desktop notifications |
| `mcp-bridge/scripts/check-bridge.sh` | Hook script — peek for unread, notify Claude Code |
| `mcp-bridge/src/application/services/peek-unread.ts` | Peek service — count without marking read |
| `mcp-bridge/src/routes/peek.ts` | REST route for `GET /messages/peek` |

### Modified files — Bridge

| File | Change |
|------|--------|
| `mcp-bridge/src/db/schema.ts` | Add `working_dir` column to tasks table |
| `mcp-bridge/src/db/client.ts` | Add `working_dir` to `TaskRow`, update `insertTask` SQL and type, new `peekUnreadCount` prepared statement |
| `mcp-bridge/src/mcp.ts` | Add `working_dir` and `sender` to `assign_task` tool args, add `peek_unread` tool |
| `mcp-bridge/src/transport/schemas/tasks.ts` | Add `working_dir` and `sender` to `AssignTaskBodySchema`, add `working_dir` to `TaskResponseSchema` |
| `mcp-bridge/src/routes/tasks.ts` | Pass `working_dir` and `sender` through |
| `mcp-bridge/src/routes/messages.ts` | Register peek route |
| `mcp-bridge/src/index.ts` | Wire peek route |
| `mcp-bridge/src/application/services/assign-task.ts` | Accept `working_dir` and `sender` in `AssignTaskInput`, set message sender to `input.sender ?? "system"` |
| `mcp-bridge/src/application/events.ts` | Enrich `BridgeEvent` data to include full message/task rows (not just id+conversation) |
| `mcp-bridge/package.json` | Add `eventsource` and `@types/eventsource` deps, add `worker` script |

### Modified files — UI

| File | Change |
|------|--------|
| `ui/src/lib/types.ts` | Add `working_dir: string \| null` to `Task` interface |
| `ui/src/components/vertical-timeline.tsx` | Show `working_dir` path badge on task cards, worker badge for `codex-worker-*` assignees |
| `ui/src/lib/diagrams.ts` | Distinct node styling for `codex-worker-*` agents in directed graph |

### Modified files — Scripts

| File | Change |
|------|--------|
| `start.sh` | Add worker + notify processes, bridge health wait |
| `setup.sh` | Symlink `check-bridge.sh` to `~/.claude/scripts/`, add `rm -f mcp-bridge/bridge.db` before build |

### Modified files — Documentation

| File | Change |
|------|--------|
| `planning/API_CONTRACT.md` | Document peek endpoint, working_dir field, sender field on assign_task, peek_unread MCP tool |
| `planning/ERD.md` | Document working_dir column, dynamic sender on task messages |
| `planning/ARCHITECTURE.md` | Worker subsystem, notify.ts, hook flow, updated data flow diagram |
| `planning/DEPLOYMENT.md` | Worker entry point, notify entry point, worker-id file, new env vars, updated start.sh |
| `planning/LOCAL_DEV.md` | Worker in dev startup, notification setup, local testing guide |
| `planning/TESTING.md` | Worker test descriptions, peek service tests, notify mock strategy |
| `CLAUDE.md` | Worker in directory tree, worker env vars, updated start.sh description |
| `skills/enhancePrompt/SKILL.md` | Update Codex dialogue section to reflect automated worker |
