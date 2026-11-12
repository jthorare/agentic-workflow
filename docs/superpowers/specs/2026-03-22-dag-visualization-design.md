# Interactive DAG Visualization for Conversation Memory Graph

**Issue:** #7
**Date:** 2026-03-22
**Status:** Draft

## Overview

Add an interactive graph visualization that lets humans see and explore the same DAG that agents crawl via `traverse_memory`, `search_memory`, and `get_context`. Two new pages with distinct purposes, powered by React Flow with Dagre layout.

## Pages

### 1. Conversation Memory Graph (`/conversation/[id]/graph`)

Visualize a single conversation's memory subgraph — how messages, tasks, topics, decisions, and artifacts connect.

**Layout:** Three-column.
- **Left panel:** Node list filtered by kind, sorted by timestamp. Click to select and center in graph.
- **Center:** Interactive DAG (React Flow + Dagre hierarchical layout). Minimap in corner. Toolbar with depth slider, direction toggle, edge kind filter.
- **Right panel (top):** Selected node detail — title, body, metadata, edge list (in/out), timestamps.
- **Right panel (bottom):** Context builder — assemble context for this conversation, token budget bar, section breakdown with relevance scores, included nodes highlighted in graph with gold dashed rings.

**Entry point:** Nested route at `/conversation/[id]/graph`. A shared `conversation/[id]/layout.tsx` provides the conversation header and data fetching for both the Timeline (`page.tsx`) and Graph views. Tab-style navigation links between them.

### 2. Cross-Conversation Memory Explorer (`/memory` — enhanced)

Search, traverse, and explore the full memory graph across all conversations.

**Layout:** Three-column.
- **Left panel (top):** Search input, mode selector (keyword/semantic/hybrid), kind filters. Results as a scored list with match type badges (K/S/H).
- **Left panel (bottom):** Traversal log — list of recorded agent traversals. Click to load and replay.
- **Center:** Interactive graph (React Flow + Dagre). Search results highlighted with score badges. Click-to-traverse from any node.
- **Right panel (top):** Selected node detail with RRF score and match type when from search.
- **Right panel (bottom):** Context builder — assemble from query or selected node.

**Replaces:** Current Memory Explorer page (which is non-functional — see issue #18).

### Navigation

Add a header nav bar to the existing layout with links:
- **Conversations** (`/`)
- **Memory Explorer** (`/memory`)

## Visual Language

### Node Kinds

Each node kind is a custom React Flow component with distinct color and silhouette SVG icon (no emojis).

| Kind | Color | Shape | Displays |
|------|-------|-------|----------|
| `message` | Blue (#3B82F6) | Rounded rect | sender → recipient, truncated payload, timestamp |
| `conversation` | Purple (#7C6AF5) | Larger rounded rect | Conversation ID (truncated), message count, agent count |
| `topic` | Green (#10B981) | Rounded rect | Topic title, related conversation count |
| `decision` | Amber (#F59E0B) | Diamond | Decision title |
| `task` | Red (#EF4444) | Rounded rect + status badge | Task summary, status (pending/in_progress/completed/failed), assigned agent |
| `artifact` | Violet (#8B5CF6) | Rounded rect | Artifact title, type from metadata |

**Interactive states:**
- **Default:** Background at 0.15 opacity, 1px border at 0.4 opacity
- **Hover:** Background at 0.25, 2px border at 0.7, subtle glow shadow
- **Selected:** Background at 0.3, 2px solid border, stronger glow, populates detail panel

### Edge Kinds

Edges are labeled arrows with color and line style conveying relationship type.

| Kind | Color | Style | Category |
|------|-------|-------|----------|
| `contains` | Purple (#7C6AF5) | Solid | Structural |
| `spawned` | Blue (#3B82F6) | Dashed | Creation |
| `assigned_in` | Red (#EF4444) | Solid | Structural |
| `reply_to` | Blue (#3B82F6) | Dotted | Communication |
| `led_to` | Amber (#F59E0B) | Solid | Causal |
| `discussed_in` | Green (#10B981) | Solid | Contextual |
| `decided_in` | Amber (#F59E0B) | Solid | Contextual |
| `implemented_by` | Violet (#8B5CF6) | Solid | Causal |
| `references` | Gray (rgba 0.3) | Dashed | Weak |
| `related_to` | Gray (rgba 0.3) | Dotted | Weak |

## Feature Capabilities

### 1. Interactive Graph Explorer

Both pages share these capabilities via the graph toolbar:

- **Click-to-traverse:** Click any node to expand its edges (fetches via `/memory/node/:id/edges` and `/memory/traverse/:id`)
- **Direction toggle:** Outgoing / incoming / both edges
- **Edge kind filter:** Checkboxes to show/hide each of the 10 edge types
- **Sender filter:** Dropdown populated from distinct senders in the current graph (e.g., "claude-code", "human", "codex"). Filters which message nodes are visible.
- **Depth control:** Slider from 1-5 hops
- **Node kind coloring:** Automatic per the visual language above
- **Zoom/pan:** React Flow built-in
- **Minimap:** React Flow MiniMap with node-kind coloring

### 2. Search → Graph Integration (Memory Explorer page)

- Search results appear as highlighted nodes in the graph with their connected edges
- Each result node gets a **score badge** (floating label showing RRF fusion score)
- **Match type indicator:** `K` (keyword), `S` (semantic), `H` (hybrid) badge on each result
- Non-result connected nodes render at reduced opacity (0.4)
- Clicking a result in the left panel centers the graph on that node

### 3. Context Assembly Visualization (Both pages)

- "Assemble Context" button triggers `/memory/context` API
- Included nodes get a **gold dashed border ring** in the graph
- Excluded nodes dim to 0.3 opacity
- Right panel shows:
  - Token budget bar (used / total) with gradient fill
  - Sections list: heading, relevance score bar, token estimate
  - Click a section → highlights its source nodes in graph
- Legend distinguishes "in context" (gold ring) vs "excluded" (dimmed)

**API change required:** The existing `ContextSection` response shape (`heading`, `content`, `relevance`, `token_estimate`) does not include node IDs, making it impossible to map sections back to graph nodes. The `ContextSectionSchema` must be extended with `node_ids: z.array(z.string()).default([])` — optional with an empty default for backward compatibility. The service always populates it, but existing consumers that don't expect the field will not break. Note: with the current 1:1 section-to-node mapping, each section will have exactly one node_id. The array type is a deliberate design choice for future section aggregation.

### 4. Agent Path Replay (Memory Explorer page)

- Traversal log panel lists recorded agent traversals (agent name, operation, start node, depth, timestamp)
- Click a log entry → graph loads that traversal's subgraph
- Playback controls: play/pause, step forward/back, speed slider (0.5x - 3x)
- Animation: nodes light up in BFS visit order with numbered step indicators
  - Visited nodes: green (#10B981) glow border with step number badge
  - Current node: pulsing animated ring
  - Pending nodes: dimmed at 0.25 opacity
  - Edges glow green as they're followed
- Right panel shows during replay:
  - Traversal params (agent, operation, direction, depth, edge_kinds)
  - Current node detail with "reached via [edge_kind] from [parent]"
  - Full visit order list — completed steps shown, current highlighted, pending grayed
- For context operations: final state shows gold rings on nodes that made it into the context window vs dimmed for visited-but-excluded

## Backend Changes

### Schema Enhancement: Sender Column

The `nodes` table gains a top-level `sender` column to enable efficient agent-centric filtering (e.g., "show all messages from Claude" or "show only human turns"). Currently `sender` is buried in the `meta` JSON string and cannot be indexed or queried without in-memory parsing.

```sql
-- Added to CREATE TABLE nodes in memory-schema.ts
sender TEXT  -- nullable: only meaningful for message/task kinds; null for topic, decision, artifact
```

```sql
CREATE INDEX IF NOT EXISTS idx_nodes_repo_sender ON nodes(repo, sender);
```

Since the system has no production data, this is a DDL addition (not a migration). The column is added directly to the `CREATE TABLE` statement.

**Populated by all ingestion paths:**

| Source | Sender value |
|--------|-------------|
| Bridge messages | `message.sender` (e.g., `"claude-code"`, `"codex"`) |
| Transcript JSONL | Mapped from `type` field: `"human"` → `"human"`, `"assistant"` → `"assistant"` |
| Generic chat | `role` field directly (e.g., `"user"`, `"assistant"`) |
| Git ingestion | Commit author (e.g., `"thor"`) |
| Manual node creation | `null` (not applicable) |

**API changes:**

- `NodeResponseSchema` gains `sender: z.string().nullable()`
- `SearchMemoryQuerySchema` gains optional `sender: z.string().optional()` — filters results to nodes from a specific sender
- `TraverseQuerySchema` gains optional `sender: z.string().optional()` — prunes BFS to only follow nodes from a specific sender
- `searchMemory` service applies `WHERE sender = ?` when the filter is present
- `traverseMemory` service skips nodes whose sender doesn't match (when filter is set)

**UI integration:**

- The left panel node list gains a sender filter dropdown (populated from distinct senders in the current graph)
- Message nodes display sender as a label on the node component
- The graph toolbar gains a sender filter alongside the existing edge kind filter

### Agent-Agnostic Ingestion Pipeline

The system supports multiple ingestion formats so any agent can preserve its conversation history — not just Claude Code. Each agent picks the format that matches its output. The pipeline produces the same memory graph structure regardless of input format.

**Design principle:** Parsers are format-specific; the output (conversation + message nodes with sender, edges, embeddings) is format-agnostic.

#### Ingestion Formats

| Source | Format | Parser | Use case |
|--------|--------|--------|----------|
| `bridge` | Real-time via `send_context` MCP tool | Existing EventBus → queue pipeline | Multi-agent live communication |
| `claude-code` | JSONL with `{type, uuid, parentUuid, message, timestamp}` | New `ingestClaudeCodeSession()` | Claude Code sessions (auto-ingested via file watcher) |
| `transcript` | JSONL (same format, basic parser) | Existing `parseTranscriptLines()` | Lightweight fallback — flat message list without turn grouping |
| `generic` | JSON array of `[{role, content, timestamp?}]` | New `parseGenericChat()` | Any agent that can produce standard chat format |
| `git` | Git repository metadata | Existing `ingestGit()` | Commit/PR history |

The `generic` format is the agent-agnostic escape hatch — any agent that produces `[{role, content, timestamp}]` can use it:

```typescript
// Generic chat format — minimum viable schema for any agent
interface GenericChatMessage {
  role: string;      // maps to sender: "user", "assistant", "system", agent name, etc.
  content: string;   // message body
  timestamp?: string; // ISO 8601, optional
  id?: string;        // optional message ID for idempotency
  parent_id?: string; // optional parent for reply_to edges
}
```

#### Wiring the Stubbed Ingest Endpoint

The existing `POST /memory/ingest` endpoint (currently returns `{ ingested: 0 }`) is wired to route to the appropriate parser:

```typescript
// Updated IngestBodySchema
const IngestBodySchema = z.object({
  repo: z.string(),
  source: z.enum(["bridge", "transcript", "git", "generic", "claude-code"]),
  session_id: z.string().optional(),   // for idempotency (skip if already ingested)
  title: z.string().optional(),        // conversation title (default: "Session {id}")
  path: z.string().optional(),         // file path (transcript source)
  content: z.string().optional(),      // inline content (generic source)
  agent: z.string().max(64).regex(/^[a-zA-Z0-9_-]+$/).default("anonymous"),
});
```

The controller reads `source` and dispatches:
- `claude-code` + `path` → read file, call `ingestClaudeCodeSummary()` (detail pass depends on active session detection)
- `transcript` + `path` → read file, split lines, call `ingestTranscriptLines()`
- `generic` + `content` → parse JSON array, call new `ingestGenericChat()`
- `git` + `path` → call `ingestGit()`
- `bridge` → return error (bridge ingestion happens via EventBus, not REST)

**Response schema updated:**

```typescript
const IngestResponseSchema = z.object({
  conversation_id: z.string(),  // the created/existing conversation node ID
  messages_ingested: z.number(),
  edges_created: z.number(),
  skipped: z.number(),
});
```

#### New MCP Tool: `ingest_conversation`

Exposed via `mcp.ts` so agents can self-ingest their conversation history:

```typescript
server.tool(
  "ingest_conversation",
  "Ingest a conversation into the memory graph for long-term preservation. " +
  "Supports multiple formats: 'transcript' for JSONL files, 'generic' for standard chat JSON.",
  {
    repo: z.string().describe("Repository slug for scoping"),
    source: z.enum(["claude-code", "transcript", "generic"]).describe("Ingestion format"),
    session_id: z.string().describe("Unique session ID for idempotency"),
    title: z.string().optional().describe("Conversation title"),
    content: z.string().optional().describe("Inline content (generic format: JSON array of {role, content, timestamp})"),
    path: z.string().optional().describe("File path to ingest (transcript format)"),
    agent: z.string().optional().describe("Agent identifier"),
  },
  async (args) => { /* routes to ingest controller logic */ }
);
```

#### New Service: `ingestGenericChat()`

```typescript
// mcp-bridge/src/application/services/ingest-generic.ts
interface GenericChatInput {
  repo: string;
  sessionId: string;
  sessionTitle: string;
  messages: GenericChatMessage[];
  agent?: string;
}

function ingestGenericChat(
  mdb: MemoryDbClient,
  filter: SecretFilter,
  input: GenericChatInput,
): AppResult<IngestResult>
```

Follows the same pattern as `ingestTranscriptLines()`:
1. Check idempotency via `getNodeBySource("generic-session", sessionId)`
2. Create conversation node (source_type: `"generic-session"`)
3. For each message: create message node with `sender = message.role`, `reply_to` edge to previous message
4. `contains` edges from conversation → each message
5. Secret filter applied to all content

### Claude Code Session Ingestion

Claude Code writes JSONL transcripts to `~/.claude/projects/<path-slug>/<session-uuid>.jsonl`. Each file is a complete session with rich structure: `user` messages, `assistant` responses (containing `thinking`, `text`, and `tool_use` blocks), `progress` events (subagent status), `system` injections, and `file-history-snapshot` checkpoints. Sessions can be 3000+ lines, but only ~15-30 lines are meaningful conversation turns.

The ingestion pipeline automatically discovers and ingests these sessions with full fidelity, using a two-level approach: **summary pass** (fast, always) + **detail pass** (on demand for cold sessions, proactive for active sessions).

#### File Watcher

On bridge startup, a file watcher is created on `~/.claude/projects/` (recursive, all repos and worktrees):

```typescript
// mcp-bridge/src/ingestion/claude-code-watcher.ts
interface ClaudeCodeWatcher {
  start(): void;       // begin watching
  stop(): void;        // stop watching, drain queue
  scanOnce(): void;    // startup scan for unprocessed sessions
}
```

**Behavior:**
- Uses `fs.watch` with `recursive: true` (macOS/Windows) to detect `.jsonl` file changes
- **Debounce:** After a file's last modification, waits 30 seconds of quiet before considering the session idle and enqueuing it. This avoids ingesting partial sessions.
- **Repo derivation:** Each JSONL line contains `cwd` (e.g., `/Users/thor/repos/agentic-workflow`). The watcher reads the first non-snapshot line to extract `cwd`, then derives the repo slug using the existing `normalizeRepoSlug()` — checking for a git remote at that path first, falling back to directory name.
- **Idempotency:** Before enqueuing, checks `mdb.getNodeBySource("claude-code-session", sessionId)`. Skips already-ingested sessions entirely. For sessions with a summary but no detail, re-enqueues only for detail pass.

**Startup scan:**
```typescript
// On bridge startup:
// 1. List all ~/.claude/projects/*/*.jsonl
// 2. For each, check if already ingested (getNodeBySource)
// 3. Unprocessed → enqueue for summary pass only (cold session)
// 4. Start the file watcher for new sessions going forward
```

#### Rate-Limited Ingestion Queue

Extends the existing `BoundedQueue<T>` pattern with a rate limiter:

```typescript
// mcp-bridge/src/ingestion/session-queue.ts
interface SessionQueueConfig {
  maxSize: number;           // max queued sessions (default: 100)
  rateMs: number;            // min interval between processing (default: 5000ms = 1 file per 5s)
  onDrop?: (item: SessionJob) => void;
  onError?: (err: Error) => void;
}

interface SessionJob {
  sessionId: string;
  filePath: string;
  repo: string;
  pass: "summary" | "detail" | "both";  // what work to do
}
```

**Processing rules:**
- **Active session** (file watcher triggered during bridge uptime): Enqueued with `pass: "both"` — gets summary + detail passes at the rate limit. The user is actively working, so full fidelity is proactively built.
- **Cold session** (startup scan, previously unseen): Enqueued with `pass: "summary"` — fast summary only. Detail created lazily when the user expands a turn in the UI.
- **Rate limit:** Processes at most 1 session every 5 seconds. During startup scan with many unprocessed files, this prevents I/O saturation while the bridge runs its primary workload.

#### Two-Level Ingestion

**Summary Pass** — fast, creates ~15-30 nodes per session:

```typescript
// mcp-bridge/src/application/services/ingest-claude-code.ts

interface ClaudeCodeSessionInput {
  repo: string;
  sessionId: string;
  filePath: string;
  lines: string[];
}

function ingestClaudeCodeSummary(
  mdb: MemoryDbClient,
  filter: SecretFilter,
  input: ClaudeCodeSessionInput,
): AppResult<IngestResult>
```

1. **Parse JSONL** into typed records, filtering out noise (`progress`, `file-history-snapshot`, `system` lines)
2. **Group into turns:** A "turn" is a human prompt + the assistant response cycle that follows it. Consecutive assistant messages (thinking → text → tool_use → ...) until the next user message are one turn.
3. **Create conversation node:**
   - kind: `conversation`
   - title: First human message truncated to 120 chars
   - sender: `null`
   - metadata: `{sessionId, cwd, gitBranch, version, entrypoint, file_path}`
   - source_type: `"claude-code-session"`
4. **Create turn nodes** (one per human prompt + one per assistant response):
   - kind: `message`
   - sender: `"human"` or `"assistant"`
   - title: First 120 chars of visible text content
   - body: Concatenated text blocks (for assistant: only `text` type blocks, not `thinking` or `tool_use`)
   - metadata: `{turn_index, uuids: [...], timestamp, tool_count, has_thinking, expanded: false}`
   - source_type: `"claude-code-turn"`
5. **Create edges:**
   - `contains`: conversation → each turn
   - `reply_to`: assistant turn → human turn it responds to

**Detail Pass** — creates tool use + subagent nodes for a turn:

```typescript
function expandClaudeCodeTurn(
  mdb: MemoryDbClient,
  filter: SecretFilter,
  turnNodeId: string,
  lines: string[],   // raw JSONL lines for this turn's UUID range
): AppResult<ExpandResult>
```

1. **Parse the turn's JSONL segment** (filtered by UUIDs stored in turn metadata)
2. **Create tool use nodes** for each `tool_use` block in assistant messages:
   - kind: `artifact`
   - title: Tool name (e.g., `"Read"`, `"Bash"`, `"Agent"`)
   - sender: `"assistant"`
   - body: Input summary (first 500 chars of JSON-stringified input)
   - metadata: `{tool_name, tool_use_id, input, output_summary}`
   - source_type: `"claude-code-tool"`
   - Edge: `contains` from assistant turn → tool use node
3. **Create subagent nodes** when tool name is `"Agent"`:
   - kind: `task`
   - sender: `"assistant"`
   - title: Agent description from input
   - body: Agent prompt truncated to 2000 chars
   - metadata: `{subagent_type, description}`
   - source_type: `"claude-code-subagent"`
   - Edge: `spawned` from assistant turn → subagent node
4. **Store thinking** as metadata on the assistant turn node (not a separate node — it's large but not independently navigable)
5. **Mark turn as expanded:** Update turn node metadata `expanded: true`

#### Lazy Detail Trigger

New endpoint for on-demand expansion of cold session turns:

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/memory/node/:id/expand` | Expand a summary-only turn into full detail nodes |

**Flow:**
1. Read the turn node → extract `uuids` from metadata and `file_path` from parent conversation node
2. If `expanded: true`, return existing children (no-op)
3. If source file missing, return error: `"Source transcript not found — file may have been deleted"`
4. Read the JSONL file, filter to matching UUIDs
5. Call `expandClaudeCodeTurn()` to create detail nodes
6. Return the new nodes and edges

**Not rate-limited:** This endpoint calls `expandClaudeCodeTurn()` directly in the request handler — it does NOT go through the session ingestion queue. The rate-limited queue exists to prevent background file watcher ingestion from saturating I/O. User-initiated expansion is synchronous and immediate.

**Schema:**
```typescript
const ExpandNodeSchema = {
  params: z.object({ id: z.string() }),
  response: z.object({
    nodes_created: z.number(),
    edges_created: z.number(),
    nodes: z.array(NodeResponseSchema),
    edges: z.array(EdgeResponseSchema),
  }),
};
```

#### Node Graph Structure (Full Fidelity)

A fully expanded Claude Code session produces this graph:

```
conversation (purple)
├── contains → human turn 1 (blue, sender: "human")
│   └── reply_to ← assistant turn 1 (blue, sender: "assistant")
│       ├── contains → tool: Read (violet, artifact)
│       ├── contains → tool: Bash (violet, artifact)
│       └── contains → tool: Agent (violet, artifact)
│           └── spawned → subagent task (red, task)
├── contains → human turn 2 (blue, sender: "human")
│   └── reply_to ← assistant turn 2 (blue, sender: "assistant")
│       ├── contains → tool: Edit (violet, artifact)
│       └── contains → tool: Write (violet, artifact)
└── ...
```

Unexpanded turns show as leaf message nodes. Expanded turns show their tool use and subagent children. The UI resolves this by checking `metadata.expanded` — unexpanded turns get an "expand" button that calls `POST /memory/node/:id/expand`.

### Traversal Logging

New `traversal_logs` table in `memory-schema.ts` (in `memory.db`, not `bridge.db`, because it references `nodes(id)` and is consumed by the memory API endpoints). IDs are UUIDv4 via `crypto.randomUUID()`, consistent with nodes and edges tables.

**Replay with deleted nodes:** When replaying a traversal whose nodes have been deleted, the UI shows step markers at positions with "deleted node" placeholders. The step sequence and edge kinds are preserved from the log, allowing the replay animation to run even with gaps.

**Retention:** The `GET /memory/traversals` endpoint limits results via its `limit` parameter (default 20, max 100). On server startup, logs older than 30 days are pruned automatically.

```sql
CREATE TABLE IF NOT EXISTS traversal_logs (
  id TEXT PRIMARY KEY,
  repo TEXT NOT NULL,
  agent TEXT NOT NULL DEFAULT 'anonymous',
  operation TEXT NOT NULL CHECK(operation IN ('traverse', 'context')),
  start_node TEXT,             -- nullable: SET NULL on node deletion preserves log history
                               -- (intentionally differs from edges ON DELETE CASCADE — logs are historical records, not structural)
  params TEXT NOT NULL,       -- JSON: direction, max_depth, edge_kinds, query, token_budget
  steps TEXT NOT NULL,          -- JSON array of {node_id, parent_id, edge_id, edge_kind}
  scores TEXT,                 -- JSON: node_id → relevance score (context ops only)
  token_allocation TEXT,       -- JSON: section → tokens (context ops only)
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  FOREIGN KEY (start_node) REFERENCES nodes(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_traversal_logs_repo_created ON traversal_logs(repo, created_at DESC);
```

**Data shape (TypeScript):**

```typescript
interface TraversalLog {
  id: string;
  repo: string;
  agent: string;
  operation: "traverse" | "context";
  start_node: string | null;
  params: {
    direction?: string;
    max_depth?: number;
    edge_kinds?: string[];
    query?: string;
    token_budget?: number;
  };
  steps: Array<{
    node_id: string;
    parent_id: string | null;  // null for start node
    edge_id: string | null;    // null for start node
    edge_kind: string | null;  // null for start node
  }>;
  scores?: Record<string, number>;
  token_allocation?: Record<string, number>;
  created_at: string;
}
```

### New Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/memory/traversals?repo=X&limit=20` | List recent traversal logs (limit: `z.coerce.number().min(1).max(100).default(20)`) |
| `GET` | `/memory/traversals/:id` | Get a specific traversal with full path data |
| `POST` | `/memory/node/:id/expand` | Expand a summary-only Claude Code turn into full detail nodes (tool uses, subagents) |

Each endpoint requires a corresponding Zod schema in `memory-schemas.ts` (`TraversalLogsQuerySchema`, `TraversalLogParamsSchema`, `TraversalLogResponseSchema`), controller method in `createMemoryController`, and `defineRoute()` call in `createMemoryRoutes` — following existing patterns. Update `planning/API_CONTRACT.md` with the new endpoint documentation.

### Service Changes

- `traverseMemory` — **remains a pure read-only function.** It returns BFS steps as `{ node_id, parent_id, edge_id, edge_kind }` tuples in visit order alongside the existing `{ root, nodes[], edges[] }` result. It does not write traversal logs.
- `assembleContext` — **remains read-only.** It returns `node_ids` per section and relevance scores alongside the existing result. It does not write traversal logs.
- Both services accept an optional `agent` parameter to identify who triggered the traversal.

**Traversal log recording** happens in the **controller layer** (`memory-controller.ts`), not in the services. After the service returns, the controller calls `mdb.insertTraversalLog()` with the result data. This preserves single-responsibility: services read and compute, controllers compose side effects. If the log insert fails, the controller still returns the service result (log failure does not break the primary operation) and logs a warning.

**Secret filtering:** The `params.query` field must be passed through `SecretFilter.redact()` before serialization into the log, since search queries may accidentally contain secrets.

**Agent identity flow:**
- **REST API:** New optional `agent` query parameter on `GET /memory/traverse/:id` and `GET /memory/context`. Validated as `z.string().max(64).regex(/^[a-zA-Z0-9_-]+$/).default("anonymous")`. The UI passes `"ui-user"` when a human triggers traversal/context from the browser.
- **MCP tools:** The `traverse_memory` and `get_context` MCP tool schemas gain an optional `agent` string field with the same validation. Claude Code sessions pass their agent identifier (e.g. `"claude-code"`, `"codex"`). This is the primary source of real agent names.
- **Service layer:** `TraverseInput` and `AssembleContextInput` types gain `agent?: string`. The controller passes this through to `insertTraversalLog`.
- **Note:** Agent identity is self-asserted, not authenticated. It should not be used for authorization decisions.

### MemoryDbClient Additions

- `insertTraversalLog(log)` — insert a traversal log entry
- `getTraversalLogs(repo, limit?)` — list recent logs for a repo
- `getTraversalLog(id)` — get a single log by ID
- `getDistinctSenders(repo)` — list unique sender values for a repo (populates UI filter dropdown)
- `searchFTS` and `searchKNN` gain optional `sender` parameter for pre-filtering
- `updateNodeMeta(id, meta)` — update a node's metadata (used by detail pass to set `expanded: true`)

## Component Architecture

### Shared Components (`ui/src/components/graph/`)

```
graph/
├── graph-canvas.tsx          # React Flow wrapper — nodes/edges, Dagre layout
├── graph-toolbar.tsx         # Depth slider, direction toggle, edge kind filters
├── graph-minimap.tsx         # React Flow MiniMap with node-kind coloring
├── node-types/
│   ├── message-node.tsx      # Blue rounded rect
│   ├── conversation-node.tsx # Purple larger rect
│   ├── topic-node.tsx        # Green rounded rect
│   ├── decision-node.tsx     # Amber diamond
│   ├── task-node.tsx         # Red rounded rect + status badge
│   └── artifact-node.tsx     # Violet rounded rect
├── edge-styles.ts            # Edge color/dash config per edge kind
├── node-detail-panel.tsx     # Selected node info, edge list
├── context-builder-panel.tsx # Context assembly UI
└── path-replay.tsx           # Traversal replay controls
```

### Page-Specific Components

```
components/
├── conversation-graph/
│   ├── conversation-graph-page.tsx  # /conversation/[id]/graph layout
│   └── conversation-node-list.tsx   # Left panel node list
└── memory-explorer/
    ├── memory-explorer-page.tsx     # /memory layout (replaces current)
    ├── memory-search-panel.tsx      # Search input, mode, filters, results
    └── traversal-log-panel.tsx      # Recorded traversal list
```

### New Hooks (`ui/src/hooks/`)

```
├── use-graph-layout.ts      # Dagre layout computation → positioned nodes/edges
├── use-traversal-logs.ts    # Fetch/manage traversal log list
└── use-path-replay.ts       # Replay state machine — step, timer, highlighted set
```

### Data Flow

1. Page fetches data (traverse API for conversation graph, search API for memory explorer)
2. Response nodes/edges → `use-graph-layout` → Dagre computes positions → React Flow renders
3. Click node → detail panel updates, optionally triggers further traversal to expand
4. Search results → matched nodes highlighted with glow + score badges, non-matches dimmed
5. Context assembly → gold dashed rings on included nodes, token budget in panel
6. Path replay → `use-path-replay` steps through `traversal_log.path`, highlighting each node/edge in BFS sequence with animation

## Deprecations

- `ui/src/components/memory-graph.tsx` — removed, fully superseded by `graph/graph-canvas.tsx`
- The Mermaid dependency and `diagram-renderer.tsx` remain — they serve the conversation detail page's Agent Graph and Sequence Diagram panels, which are not part of this feature's scope

## New Dependencies

| Package | Purpose |
|---------|---------|
| `@xyflow/react` | React Flow v12 — interactive graph canvas |
| `@dagrejs/dagre` | Hierarchical DAG layout algorithm |

## Testing Strategy

- **Unit tests:** Custom node components render correctly with each kind's props
- **Hook tests:** `use-graph-layout` produces valid positioned output from raw nodes/edges; `use-path-replay` state machine transitions correctly
- **Integration tests:** Graph canvas renders with mock data; search highlighting applies correct styles; context assembly gold rings appear on correct nodes
- **Backend tests:** `traversal_logs` CRUD operations; traversal logging records correct path during BFS; context assembly logging records scores and token allocation; sender column populated correctly by all ingestion paths; `ingestGenericChat` produces correct node/edge structure; sender filter in search and traverse APIs returns correct subsets
- **Ingestion tests:** `parseGenericChat()` accepts valid input, missing fields, empty arrays, and malformed JSON; `ingest_conversation` MCP tool routes correctly to parsers; idempotency check prevents duplicate ingestion; secret filter applied to all ingested content
- **Claude Code ingestion tests:** `ingestClaudeCodeSummary()` groups JSONL lines into correct turns; filters out `progress`/`system`/`file-history-snapshot` noise; creates correct node kinds and edges; idempotent on re-ingestion; `expandClaudeCodeTurn()` creates tool_use artifacts and subagent tasks; marks turn as expanded; tolerates a missing source file
- **File watcher tests:** Detects new `.jsonl` files; debounces rapid modifications; derives repo from `cwd` in JSONL; skips already-ingested sessions; rate limiter respects interval; startup scan processes unprocessed files as summary-only
- **Lazy expansion tests:** `POST /memory/node/:id/expand` returns correct nodes/edges; no-op when already expanded; error when source file deleted; expand endpoint wired correctly in routes
- **Coverage:** 100% threshold per project merge gate

## Existing Issues to Fix

Issue #18 documents three bugs that block this feature:
1. No navigation to Memory Explorer (no header nav)
2. Memory API endpoints return 404 (stale build)
3. Memory Explorer shows nothing by default

These are prerequisites — the nav bar addition and build fix will be addressed as part of this implementation.
