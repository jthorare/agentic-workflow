# DAG Visualization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an interactive graph visualization for the conversation memory system with automatic Claude Code session ingestion, sender-based filtering, and lazy two-level detail expansion.

**Architecture:** Backend-first — schema changes, services, and API endpoints come first so tests validate the data layer before any UI work. The ingestion pipeline (generic + Claude Code + file watcher) builds on existing patterns (AppResult, BoundedQueue, factory functions). Frontend uses React Flow v12 with Dagre layout in a three-column layout, wired to the new API endpoints.

**Tech Stack:** TypeScript 5.7, Fastify 5, SQLite (better-sqlite3), Vitest, React Flow (`@xyflow/react`), Dagre (`@dagrejs/dagre`), Next.js 15, React 19

**Spec:** `docs/superpowers/specs/2026-03-22-dag-visualization-design.md`

---

## File Structure

### Backend — New Files

```
mcp-bridge/src/
├── db/
│   └── memory-schema.ts                          # MODIFY: add sender column, traversal_logs table
│   └── memory-client.ts                          # MODIFY: add sender filter, traversal log methods, expand helpers
├── application/services/
│   ├── ingest-generic.ts                         # CREATE: generic chat ingestion service
│   └── ingest-claude-code.ts                     # CREATE: Claude Code session summary + detail expansion
├── ingestion/
│   ├── claude-code-parser.ts                     # CREATE: JSONL → turns parser (filters noise, groups turns)
│   ├── claude-code-watcher.ts                    # CREATE: file watcher on ~/.claude/projects/
│   └── session-queue.ts                          # CREATE: rate-limited ingestion queue
├── transport/
│   ├── schemas/memory-schemas.ts                 # MODIFY: add sender, traversal log, expand, ingest schemas
│   └── controllers/memory-controller.ts          # MODIFY: wire ingest, add traversal log + expand handlers
├── routes/memory.ts                              # MODIFY: add traversal + expand routes
├── mcp.ts                                        # MODIFY: add ingest_conversation tool, agent param
└── index.ts                                      # MODIFY: wire file watcher + session queue on startup
```

### Backend — New Test Files

```
mcp-bridge/tests/
├── claude-code-parser.test.ts                    # CREATE: JSONL parsing, turn grouping, noise filtering
├── ingest-generic.test.ts                        # CREATE: generic chat ingestion
├── ingest-claude-code.test.ts                    # CREATE: summary + detail expansion
├── session-queue.test.ts                         # CREATE: rate limiting, pass types
├── claude-code-watcher.test.ts                   # CREATE: file detection, debounce, repo derivation
└── traversal-logs.test.ts                        # CREATE: CRUD, controller-layer logging
```

### Frontend — New Files

```
ui/src/
├── app/
│   ├── layout.tsx                                # MODIFY: add header nav bar
│   ├── memory/page.tsx                           # MODIFY: replace with React Flow three-column layout
│   └── conversation/[id]/
│       ├── layout.tsx                            # CREATE: shared conversation layout with tab nav
│       ├── page.tsx                              # MODIFY: move existing timeline into layout
│       └── graph/page.tsx                        # CREATE: conversation graph page
├── components/
│   ├── nav-header.tsx                            # CREATE: header nav bar component
│   ├── graph/
│   │   ├── graph-canvas.tsx                      # CREATE: React Flow wrapper with Dagre layout
│   │   ├── graph-toolbar.tsx                     # CREATE: depth, direction, edge kind, sender filters
│   │   ├── graph-minimap.tsx                     # CREATE: minimap with node-kind coloring
│   │   ├── node-types/
│   │   │   ├── message-node.tsx                  # CREATE: blue rounded rect
│   │   │   ├── conversation-node.tsx             # CREATE: purple larger rect
│   │   │   ├── topic-node.tsx                    # CREATE: green rounded rect
│   │   │   ├── decision-node.tsx                 # CREATE: amber diamond
│   │   │   ├── task-node.tsx                     # CREATE: red rect + status badge
│   │   │   └── artifact-node.tsx                 # CREATE: violet rounded rect
│   │   ├── edge-styles.ts                        # CREATE: edge color/dash config per kind
│   │   ├── node-detail-panel.tsx                 # CREATE: selected node info panel
│   │   ├── context-builder-panel.tsx             # CREATE: context assembly UI
│   │   └── path-replay.tsx                       # CREATE: traversal replay controls
│   ├── conversation-graph/
│   │   ├── conversation-graph-page.tsx           # CREATE: three-column graph layout
│   │   └── conversation-node-list.tsx            # CREATE: left panel filterable node list
│   └── memory-explorer/
│       ├── memory-explorer-page.tsx              # CREATE: three-column search + graph layout
│       ├── memory-search-panel.tsx               # CREATE: search input, mode, filters, results
│       └── traversal-log-panel.tsx               # CREATE: recorded traversal list
├── hooks/
│   ├── use-graph-layout.ts                       # CREATE: Dagre layout computation
│   ├── use-traversal-logs.ts                     # CREATE: fetch/manage traversal logs
│   └── use-path-replay.ts                        # CREATE: replay state machine
└── lib/
    └── memory-api.ts                             # MODIFY: add traversal log, expand, sender filter APIs
```

---

## Phase 1: Backend Schema & Core Services

### Task 1: Add sender column to nodes table

**Files:**
- Modify: `mcp-bridge/src/db/memory-schema.ts`
- Modify: `mcp-bridge/src/db/memory-client.ts`
- Modify: `mcp-bridge/src/transport/schemas/memory-schemas.ts`
- Test: `mcp-bridge/tests/memory-client.test.ts`

- [ ] **Step 1: Write failing test — sender column exists and is queryable**

In `tests/memory-client.test.ts`, add a new describe block:

```typescript
describe("sender column", () => {
  it("stores and retrieves sender on inserted node", () => {
    const node = mdb.insertNode({
      repo: "test-repo",
      kind: "message",
      title: "Hello",
      body: "World",
      meta: "{}",
      source_id: "s1",
      source_type: "test",
      sender: "claude-code",
    });
    const fetched = mdb.getNode(node.id);
    expect(fetched).not.toBeNull();
    expect(fetched!.sender).toBe("claude-code");
  });

  it("returns null sender for nodes without sender", () => {
    const node = mdb.insertNode({
      repo: "test-repo",
      kind: "topic",
      title: "Topic",
      body: "",
      meta: "{}",
      source_id: "s2",
      source_type: "test",
    });
    const fetched = mdb.getNode(node.id);
    expect(fetched!.sender).toBeNull();
  });

  it("getDistinctSenders returns unique senders for a repo", () => {
    mdb.insertNode({ repo: "r", kind: "message", title: "a", body: "", meta: "{}", source_id: "1", source_type: "t", sender: "human" });
    mdb.insertNode({ repo: "r", kind: "message", title: "b", body: "", meta: "{}", source_id: "2", source_type: "t", sender: "assistant" });
    mdb.insertNode({ repo: "r", kind: "message", title: "c", body: "", meta: "{}", source_id: "3", source_type: "t", sender: "human" });
    mdb.insertNode({ repo: "other", kind: "message", title: "d", body: "", meta: "{}", source_id: "4", source_type: "t", sender: "codex" });

    const senders = mdb.getDistinctSenders("r");
    expect(senders).toEqual(["assistant", "human"]); // sorted alphabetically
  });

  it("searchFTS filters by sender when provided", () => {
    mdb.insertNode({ repo: "r", kind: "message", title: "hello world", body: "", meta: "{}", source_id: "1", source_type: "t", sender: "human" });
    mdb.insertNode({ repo: "r", kind: "message", title: "hello earth", body: "", meta: "{}", source_id: "2", source_type: "t", sender: "assistant" });

    const all = mdb.searchFTS("hello", "r", 10);
    expect(all.length).toBe(2);

    const filtered = mdb.searchFTS("hello", "r", 10, "human");
    expect(filtered.length).toBe(1);
    expect(filtered[0].title).toBe("hello world");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mcp-bridge && npx vitest run tests/memory-client.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: FAIL — `sender` property doesn't exist on InsertNodeInput

- [ ] **Step 3: Update schema — add sender column to DDL**

In `memory-schema.ts`, add `sender TEXT` column to the nodes CREATE TABLE statement (after `source_type`), and add the compound index:

```typescript
// In MEMORY_MIGRATIONS string, inside CREATE TABLE IF NOT EXISTS nodes:
//   ... existing columns ...
//   source_type TEXT NOT NULL DEFAULT '',
    sender TEXT,
//   created_at ...

// After existing indexes, add:
CREATE INDEX IF NOT EXISTS idx_nodes_repo_sender ON nodes(repo, sender);
```

- [ ] **Step 4: Update memory-client — add sender to InsertNodeInput, NodeRow, queries**

In `memory-client.ts`:

1. Add `sender?: string | null` to `InsertNodeInput` interface
2. Add `sender: string | null` to `NodeRow` interface
3. Update `insertNode` prepared statement to include `sender` column
4. Update all SELECT statements that return NodeRow to include `sender`
5. Add `getDistinctSenders(repo: string): string[]` method
6. Update `searchFTS` to accept optional `sender` parameter and add `AND sender = @sender` when present
7. Update `searchKNN` to accept optional `sender` parameter — post-filter candidates by sender when present (JS-level filter since sqlite-vec KNN runs in vector space)

- [ ] **Step 5: Update NodeResponseSchema and SearchMemoryQuerySchema — add sender field**

In `memory-schemas.ts`:

```typescript
// In NodeResponseSchema, add:
sender: z.string().nullable(),

// In SearchMemoryQuerySchema querystring, add:
sender: z.string().optional(),
```

- [ ] **Step 6: Wire sender filter through searchMemory service**

In `search-memory.ts`:
1. Add `sender?: string` to `SearchInput` interface
2. Pass `sender` through to `mdb.searchFTS(query, repo, limit, sender)` and `mdb.searchKNN(queryVec, limit, repo, sender)` calls

In `tests/search-memory.test.ts`, add:

```typescript
it("filters search results by sender when provided", async () => {
  mdb.insertNode({ repo: "r", kind: "message", title: "hello from human", body: "", meta: "{}", source_id: "1", source_type: "t", sender: "human" });
  mdb.insertNode({ repo: "r", kind: "message", title: "hello from assistant", body: "", meta: "{}", source_id: "2", source_type: "t", sender: "assistant" });

  const result = await searchMemory(mdb, embedService, { query: "hello", repo: "r", mode: "keyword", limit: 10, sender: "human" });
  expect(result.ok).toBe(true);
  if (!result.ok) return;
  expect(result.data).toHaveLength(1);
  expect(result.data[0].title).toContain("human");
});
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/memory-client.test.ts tests/search-memory.test.ts --reporter=verbose 2>&1 | tail -30`
Expected: All tests PASS including new sender tests

- [ ] **Step 8: Run full test suite to check for regressions**

Run: `cd mcp-bridge && npx vitest run --reporter=verbose 2>&1 | tail -30`
Expected: All existing tests still pass. Some may need updates if they assert on NodeRow shape — fix any that fail by adding `sender: null` to expected objects.

- [ ] **Step 9: Commit**

```bash
git add mcp-bridge/src/db/memory-schema.ts mcp-bridge/src/db/memory-client.ts mcp-bridge/src/transport/schemas/memory-schemas.ts mcp-bridge/src/application/services/search-memory.ts mcp-bridge/tests/memory-client.test.ts mcp-bridge/tests/search-memory.test.ts
git commit -m "feat: add sender column to nodes table with index, search, and query support"
```

---

### Task 2: Add traversal_logs table and CRUD methods

**Files:**
- Modify: `mcp-bridge/src/db/memory-schema.ts`
- Modify: `mcp-bridge/src/db/memory-client.ts`
- Create: `mcp-bridge/tests/traversal-logs.test.ts`

- [ ] **Step 1: Write failing test — traversal log CRUD**

Create `tests/traversal-logs.test.ts`:

```typescript
import { describe, it, expect, beforeEach } from "vitest";
import Database from "better-sqlite3";
import * as sqliteVec from "sqlite-vec";
import { MEMORY_MIGRATIONS } from "../src/db/memory-schema.js";
import { createMemoryDbClient, type MemoryDbClient } from "../src/db/memory-client.js";

describe("traversal_logs", () => {
  let mdb: MemoryDbClient;

  beforeEach(() => {
    const raw = new Database(":memory:");
    sqliteVec.load(raw);
    raw.pragma("journal_mode = WAL");
    raw.exec(MEMORY_MIGRATIONS);
    mdb = createMemoryDbClient(raw);
  });

  it("inserts and retrieves a traversal log", () => {
    const node = mdb.insertNode({
      repo: "r", kind: "message", title: "start", body: "", meta: "{}", source_id: "n1", source_type: "t",
    });

    const log = mdb.insertTraversalLog({
      repo: "r",
      agent: "claude-code",
      operation: "traverse",
      start_node: node.id,
      params: { direction: "both", max_depth: 3 },
      steps: [{ node_id: node.id, parent_id: null, edge_id: null, edge_kind: null }],
    });

    expect(log.id).toBeDefined();
    expect(log.agent).toBe("claude-code");
    expect(log.operation).toBe("traverse");

    const fetched = mdb.getTraversalLog(log.id);
    expect(fetched).not.toBeNull();
    expect(fetched!.steps).toHaveLength(1);
    expect(fetched!.params.direction).toBe("both");
  });

  it("lists traversal logs ordered by created_at desc", () => {
    mdb.insertTraversalLog({ repo: "r", agent: "a", operation: "traverse", start_node: null, params: {}, steps: [] });
    mdb.insertTraversalLog({ repo: "r", agent: "b", operation: "context", start_node: null, params: {}, steps: [] });
    mdb.insertTraversalLog({ repo: "other", agent: "c", operation: "traverse", start_node: null, params: {}, steps: [] });

    const logs = mdb.getTraversalLogs("r", 10);
    expect(logs).toHaveLength(2);
    expect(logs[0].agent).toBe("b"); // most recent first
  });

  it("respects limit parameter", () => {
    for (let i = 0; i < 5; i++) {
      mdb.insertTraversalLog({ repo: "r", agent: `a${i}`, operation: "traverse", start_node: null, params: {}, steps: [] });
    }
    const logs = mdb.getTraversalLogs("r", 2);
    expect(logs).toHaveLength(2);
  });

  it("stores scores and token_allocation for context operations", () => {
    const log = mdb.insertTraversalLog({
      repo: "r",
      agent: "ui-user",
      operation: "context",
      start_node: null,
      params: { query: "test", token_budget: 8000 },
      steps: [{ node_id: "n1", parent_id: null, edge_id: null, edge_kind: null }],
      scores: { n1: 0.95 },
      token_allocation: { "Summary": 200, "Details": 500 },
    });

    const fetched = mdb.getTraversalLog(log.id);
    expect(fetched!.scores).toEqual({ n1: 0.95 });
    expect(fetched!.token_allocation).toEqual({ Summary: 200, Details: 500 });
  });

  it("handles ON DELETE SET NULL for start_node", () => {
    const node = mdb.insertNode({
      repo: "r", kind: "message", title: "t", body: "", meta: "{}", source_id: "n1", source_type: "t",
    });
    const log = mdb.insertTraversalLog({
      repo: "r", agent: "a", operation: "traverse", start_node: node.id, params: {}, steps: [],
    });

    // Delete the node — log should survive with null start_node
    mdb.deleteNodesBySourceType("r", "t");

    const fetched = mdb.getTraversalLog(log.id);
    expect(fetched).not.toBeNull();
    expect(fetched!.start_node).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mcp-bridge && npx vitest run tests/traversal-logs.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: FAIL — `insertTraversalLog` is not a function

- [ ] **Step 3: Add traversal_logs DDL to memory-schema.ts**

Append to the `MEMORY_MIGRATIONS` string:

```sql
CREATE TABLE IF NOT EXISTS traversal_logs (
  id TEXT PRIMARY KEY,
  repo TEXT NOT NULL,
  agent TEXT NOT NULL DEFAULT 'anonymous',
  operation TEXT NOT NULL CHECK(operation IN ('traverse', 'context')),
  start_node TEXT,
  params TEXT NOT NULL,
  steps TEXT NOT NULL,
  scores TEXT,
  token_allocation TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  FOREIGN KEY (start_node) REFERENCES nodes(id) ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS idx_traversal_logs_repo_created ON traversal_logs(repo, created_at DESC);
```

- [ ] **Step 4: Add traversal log methods to memory-client.ts**

Add types:

```typescript
export interface TraversalLogRow {
  id: string;
  repo: string;
  agent: string;
  operation: "traverse" | "context";
  start_node: string | null;
  params: Record<string, unknown>;
  steps: Array<{ node_id: string; parent_id: string | null; edge_id: string | null; edge_kind: string | null }>;
  scores?: Record<string, number>;
  token_allocation?: Record<string, number>;
  created_at: string;
}

export interface InsertTraversalLogInput {
  repo: string;
  agent: string;
  operation: "traverse" | "context";
  start_node: string | null;
  params: Record<string, unknown>;
  steps: Array<{ node_id: string; parent_id: string | null; edge_id: string | null; edge_kind: string | null }>;
  scores?: Record<string, number>;
  token_allocation?: Record<string, number>;
}
```

Add prepared statements and methods:

```typescript
insertTraversalLog(input: InsertTraversalLogInput): TraversalLogRow
getTraversalLog(id: string): TraversalLogRow | null
getTraversalLogs(repo: string, limit: number): TraversalLogRow[]
pruneTraversalLogs(days: number): number  // returns count deleted
```

JSON fields (`params`, `steps`, `scores`, `token_allocation`) are serialized with `JSON.stringify()` on write and `JSON.parse()` on read. Use `crypto.randomUUID()` for IDs.

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/traversal-logs.test.ts --reporter=verbose 2>&1 | tail -30`
Expected: All PASS

- [ ] **Step 6: Run full suite for regressions**

Run: `cd mcp-bridge && npx vitest run --reporter=verbose 2>&1 | tail -10`
Expected: All PASS

- [ ] **Step 7: Commit**

```bash
git add mcp-bridge/src/db/memory-schema.ts mcp-bridge/src/db/memory-client.ts mcp-bridge/tests/traversal-logs.test.ts
git commit -m "feat: add traversal_logs table with CRUD methods"
```

---

### Task 3: Update traverseMemory to return BFS steps and support sender filter

**Files:**
- Modify: `mcp-bridge/src/application/services/traverse-memory.ts`
- Modify: `mcp-bridge/tests/traverse-memory.test.ts`

- [ ] **Step 1: Write failing test — traverseMemory returns steps array**

In `tests/traverse-memory.test.ts`, add:

```typescript
it("returns BFS steps with parent linkage", () => {
  const n1 = mdb.insertNode({ repo: "r", kind: "message", title: "root", body: "", meta: "{}", source_id: "1", source_type: "t" });
  const n2 = mdb.insertNode({ repo: "r", kind: "message", title: "child", body: "", meta: "{}", source_id: "2", source_type: "t" });
  const edge = mdb.insertEdge({ repo: "r", from_node: n1.id, to_node: n2.id, kind: "contains", weight: 1.0, meta: "{}", auto: true });

  const result = traverseMemory(mdb, { node_id: n1.id, direction: "outgoing", max_depth: 2 });
  expect(result.ok).toBe(true);
  if (!result.ok) return;

  expect(result.data.steps).toBeDefined();
  expect(result.data.steps).toHaveLength(2);
  expect(result.data.steps[0]).toEqual({ node_id: n1.id, parent_id: null, edge_id: null, edge_kind: null });
  expect(result.data.steps[1]).toEqual({ node_id: n2.id, parent_id: n1.id, edge_id: edge.id, edge_kind: "contains" });
});
```

- [ ] **Step 2: Write failing test — sender filter prunes BFS**

```typescript
it("filters BFS results by sender when provided", () => {
  const n1 = mdb.insertNode({ repo: "r", kind: "conversation", title: "conv", body: "", meta: "{}", source_id: "1", source_type: "t" });
  const n2 = mdb.insertNode({ repo: "r", kind: "message", title: "human msg", body: "", meta: "{}", source_id: "2", source_type: "t", sender: "human" });
  const n3 = mdb.insertNode({ repo: "r", kind: "message", title: "assistant msg", body: "", meta: "{}", source_id: "3", source_type: "t", sender: "assistant" });
  mdb.insertEdge({ repo: "r", from_node: n1.id, to_node: n2.id, kind: "contains", weight: 1.0, meta: "{}", auto: true });
  mdb.insertEdge({ repo: "r", from_node: n1.id, to_node: n3.id, kind: "contains", weight: 1.0, meta: "{}", auto: true });

  const result = traverseMemory(mdb, { node_id: n1.id, direction: "outgoing", max_depth: 2, sender: "human" });
  expect(result.ok).toBe(true);
  if (!result.ok) return;

  // Should include root (no sender filter on root) + human node, skip assistant node
  const nodeIds = result.data.nodes.map(n => n.id);
  expect(nodeIds).toContain(n1.id);
  expect(nodeIds).toContain(n2.id);
  expect(nodeIds).not.toContain(n3.id);
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd mcp-bridge && npx vitest run tests/traverse-memory.test.ts -t "returns BFS steps|filters BFS results by sender" --reporter=verbose 2>&1 | tail -10`
Expected: FAIL — `steps` is undefined, `sender` not on TraverseInput

- [ ] **Step 4: Update traverseMemory to track steps and filter by sender**

In `traverse-memory.ts`:

1. Add `sender?: string` to `TraverseInput`
2. Add `BFSStep` interface and `steps: BFSStep[]` to `TraverseResult`
3. During BFS, push a step entry for each visited node. Root has null parent/edge.
4. When `input.sender` is set, skip neighbor nodes whose `sender` doesn't match (root node is always included regardless of sender)

```typescript
interface BFSStep {
  node_id: string;
  parent_id: string | null;
  edge_id: string | null;
  edge_kind: string | null;
}

// In TraverseInput, add:
sender?: string;

// In TraverseResult, add:
steps: BFSStep[];
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/traverse-memory.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add mcp-bridge/src/application/services/traverse-memory.ts mcp-bridge/tests/traverse-memory.test.ts
git commit -m "feat: traverseMemory returns BFS steps and supports sender filter"
```

---

### Task 4: Update assembleContext to return node_ids per section

**Files:**
- Modify: `mcp-bridge/src/application/services/assemble-context.ts`
- Modify: `mcp-bridge/src/transport/schemas/memory-schemas.ts`
- Modify: `mcp-bridge/tests/assemble-context.test.ts`

- [ ] **Step 1: Write failing test — sections include node_ids**

In `tests/assemble-context.test.ts`, add:

```typescript
it("includes node_ids in each context section", async () => {
  const node = mdb.insertNode({ repo: "r", kind: "message", title: "test content", body: "relevant body", meta: "{}", source_id: "1", source_type: "t" });

  const result = await assembleContext(mdb, embedService, { node_id: node.id, repo: "r", max_tokens: 8000 });
  expect(result.ok).toBe(true);
  if (!result.ok) return;

  for (const section of result.data.sections) {
    expect(section.node_ids).toBeDefined();
    expect(Array.isArray(section.node_ids)).toBe(true);
    expect(section.node_ids.length).toBeGreaterThan(0);
  }
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mcp-bridge && npx vitest run tests/assemble-context.test.ts -t "includes node_ids" --reporter=verbose 2>&1 | tail -10`
Expected: FAIL — `node_ids` is undefined

- [ ] **Step 3: Update ContextSectionSchema and service**

In `memory-schemas.ts`, add to `ContextSectionSchema`:

```typescript
node_ids: z.array(z.string()).default([]),
```

In `assemble-context.ts`, update the `ContextSection` interface to add `node_ids: string[]` and populate it during section building:

```typescript
// When building each section, add node_ids: [node.id]
// Note: token_estimate is already computed in the service — just add node_ids alongside it
sections.push({
  heading: `${node.kind}: ${node.title}`,
  content: node.body,
  relevance: score,
  token_estimate: tokenEst,
  node_ids: [node.id],
});
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/assemble-context.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add mcp-bridge/src/application/services/assemble-context.ts mcp-bridge/src/transport/schemas/memory-schemas.ts mcp-bridge/tests/assemble-context.test.ts
git commit -m "feat: assembleContext returns node_ids per section"
```

---

### Task 5: Add traversal log recording in controller + API endpoints

**Files:**
- Modify: `mcp-bridge/src/transport/schemas/memory-schemas.ts`
- Modify: `mcp-bridge/src/transport/controllers/memory-controller.ts`
- Modify: `mcp-bridge/src/routes/memory.ts`
- Modify: `mcp-bridge/src/transport/controllers/memory-controller.test.ts`

- [ ] **Step 1: Write failing test — traverse endpoint records traversal log**

In `tests/memory-controller.test.ts`, add test that verifies the traverse handler calls `mdb.insertTraversalLog` after the service returns. Also test the new traversal log list and get endpoints.

```typescript
describe("traversal logging", () => {
  it("traverse handler records a traversal log", async () => {
    const node = mdb.insertNode({ repo: "r", kind: "message", title: "t", body: "", meta: "{}", source_id: "1", source_type: "t" });

    // Call traverse handler through controller
    const result = await handlers.traverse({
      params: { id: node.id },
      query: { direction: "both", max_depth: 2, max_nodes: 50, agent: "test-agent" },
    });

    expect(result.ok).toBe(true);

    // Verify log was created
    const logs = mdb.getTraversalLogs("r", 10);
    expect(logs.length).toBeGreaterThanOrEqual(1);
    expect(logs[0].agent).toBe("test-agent");
    expect(logs[0].operation).toBe("traverse");
  });

  it("getTraversalLogs endpoint returns logs", async () => {
    mdb.insertTraversalLog({ repo: "r", agent: "a", operation: "traverse", start_node: null, params: {}, steps: [] });

    const result = await handlers.getTraversalLogs({ query: { repo: "r", limit: 20 } });
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data).toHaveLength(1);
  });

  it("getTraversalLog endpoint returns single log", async () => {
    const log = mdb.insertTraversalLog({ repo: "r", agent: "a", operation: "traverse", start_node: null, params: {}, steps: [] });

    const result = await handlers.getTraversalLog({ params: { id: log.id } });
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data.id).toBe(log.id);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mcp-bridge && npx vitest run src/transport/controllers/memory-controller.test.ts -t "traversal logging" --reporter=verbose 2>&1 | tail -20`
Expected: FAIL — handlers don't have `getTraversalLogs` or `getTraversalLog`

- [ ] **Step 3: Add Zod schemas for traversal log endpoints**

In `memory-schemas.ts`, add:

```typescript
// Traversal log schemas
export const TraversalLogStepSchema = z.object({
  node_id: z.string(),
  parent_id: z.string().nullable(),
  edge_id: z.string().nullable(),
  edge_kind: z.string().nullable(),
});

export const TraversalLogResponseSchema = z.object({
  id: z.string(),
  repo: z.string(),
  agent: z.string(),
  operation: z.enum(["traverse", "context"]),
  start_node: z.string().nullable(),
  params: z.record(z.unknown()),
  steps: z.array(TraversalLogStepSchema),
  scores: z.record(z.number()).optional(),
  token_allocation: z.record(z.number()).optional(),
  created_at: z.string(),
});

export const TraversalLogsQuerySchema = {
  querystring: z.object({
    repo: z.string(),
    limit: z.coerce.number().min(1).max(100).default(20),
  }),
  response: z.array(TraversalLogResponseSchema),
} as const;
export type TraversalLogsQuerySchema = typeof TraversalLogsQuerySchema;

export const TraversalLogParamsSchema = {
  params: z.object({ id: z.string() }),
  response: TraversalLogResponseSchema,
} as const;
export type TraversalLogParamsSchema = typeof TraversalLogParamsSchema;
```

Also add `agent` to `TraverseQuerySchema` and `ContextQuerySchema`:

```typescript
agent: z.string().max(64).regex(/^[a-zA-Z0-9_-]+$/).default("anonymous"),
```

Also add `sender` to `SearchMemoryQuerySchema` and `TraverseQuerySchema`:

```typescript
sender: z.string().optional(),
```

- [ ] **Step 4: Update controller — add logging to traverse/context + new handlers**

In `memory-controller.ts`:

1. After `traverseMemory()` returns success, extract `agent` from the query params (not from the service — services are read-only) and call `mdb.insertTraversalLog()` with `operation: "traverse"`, the returned steps, and the `agent` value. Wrap in try/catch — log failure does not break the response.
2. Pass `sender` from query params through to the service call as `input.sender` (for filtering).
3. After `assembleContext()` returns success, call `mdb.insertTraversalLog()` with `operation: "context"`, scores, and token_allocation. Pass `params.query` through `filter.redact()` before storing. Extract `agent` from query params.
4. Add `getTraversalLogs` handler — calls `mdb.getTraversalLogs(repo, limit)`.
5. Add `getTraversalLog` handler — calls `mdb.getTraversalLog(id)`, returns 404 if not found.

- [ ] **Step 5: Add routes for traversal log endpoints**

In `routes/memory.ts`, add two new `defineRoute()` calls:

```typescript
defineRoute({
  method: "GET",
  path: "/traversals",
  summary: "List recent traversal logs",
  schema: TraversalLogsQuerySchema,
  handler: handlers.getTraversalLogs,
}),
defineRoute({
  method: "GET",
  path: "/traversals/:id",
  summary: "Get a specific traversal log",
  schema: TraversalLogParamsSchema,
  handler: handlers.getTraversalLog,
}),
defineRoute({
  method: "GET",
  path: "/senders",
  summary: "List distinct senders for a repo",
  schema: SendersQuerySchema,
  handler: handlers.getSenders,
}),
```

Add `SendersQuerySchema` to `memory-schemas.ts`:

```typescript
export const SendersQuerySchema = {
  querystring: z.object({ repo: z.string() }),
  response: z.array(z.string()),
} as const;
export type SendersQuerySchema = typeof SendersQuerySchema;
```

Add `getSenders` handler to controller: calls `mdb.getDistinctSenders(repo)`.

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run src/transport/controllers/memory-controller.test.ts --reporter=verbose 2>&1 | tail -30`
Expected: All PASS

- [ ] **Step 7: Run full suite**

Run: `cd mcp-bridge && npx vitest run --reporter=verbose 2>&1 | tail -10`
Expected: All PASS

- [ ] **Step 8: Commit**

```bash
git add mcp-bridge/src/transport/schemas/memory-schemas.ts mcp-bridge/src/transport/controllers/memory-controller.ts mcp-bridge/src/routes/memory.ts mcp-bridge/src/transport/controllers/memory-controller.test.ts
git commit -m "feat: add traversal log recording in controller and API endpoints"
```

---

## Phase 2: Ingestion Pipeline

### Task 6: Create generic chat parser and ingestion service

**Files:**
- Create: `mcp-bridge/src/application/services/ingest-generic.ts`
- Create: `mcp-bridge/tests/ingest-generic.test.ts`

- [ ] **Step 1: Write failing test**

Create `tests/ingest-generic.test.ts`:

```typescript
import { describe, it, expect, beforeEach } from "vitest";
import Database from "better-sqlite3";
import * as sqliteVec from "sqlite-vec";
import { MEMORY_MIGRATIONS } from "../src/db/memory-schema.js";
import { createMemoryDbClient, type MemoryDbClient } from "../src/db/memory-client.js";
import { createSecretFilter } from "../src/ingestion/secret-filter.js";
import { ingestGenericChat } from "../src/application/services/ingest-generic.js";

describe("ingestGenericChat", () => {
  let mdb: MemoryDbClient;
  const filter = createSecretFilter();

  beforeEach(() => {
    const raw = new Database(":memory:");
    sqliteVec.load(raw);
    raw.pragma("journal_mode = WAL");
    raw.exec(MEMORY_MIGRATIONS);
    mdb = createMemoryDbClient(raw);
  });

  it("creates conversation + message nodes with correct senders", () => {
    const result = ingestGenericChat(mdb, filter, {
      repo: "r",
      sessionId: "sess-1",
      sessionTitle: "Test Chat",
      messages: [
        { role: "user", content: "Hello", timestamp: "2026-01-01T00:00:00Z" },
        { role: "assistant", content: "Hi there", timestamp: "2026-01-01T00:00:01Z" },
      ],
    });

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data.messages_ingested).toBe(2);
    expect(result.data.edges_created).toBe(3); // 2 contains + 1 reply_to

    const conv = mdb.getNodeBySource("generic-session", "sess-1");
    expect(conv).not.toBeNull();
    expect(conv!.kind).toBe("conversation");

    const nodes = mdb.getNodesByRepo("r", 100, 0);
    const messages = nodes.filter(n => n.kind === "message");
    expect(messages[0].sender).toBe("user");
    expect(messages[1].sender).toBe("assistant");
  });

  it("is idempotent — skips already-ingested sessions", () => {
    const input = {
      repo: "r", sessionId: "sess-1", sessionTitle: "Test",
      messages: [{ role: "user", content: "Hi" }],
    };

    ingestGenericChat(mdb, filter, input);
    const result = ingestGenericChat(mdb, filter, input);

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data.messages_ingested).toBe(0);
    expect(result.data.skipped).toBe(1);
  });

  it("creates reply_to edges between consecutive messages", () => {
    ingestGenericChat(mdb, filter, {
      repo: "r", sessionId: "s", sessionTitle: "T",
      messages: [
        { role: "user", content: "Q1" },
        { role: "assistant", content: "A1" },
        { role: "user", content: "Q2" },
      ],
    });

    const nodes = mdb.getNodesByRepo("r", 100, 0).filter(n => n.kind === "message");
    // A1 has reply_to edge pointing to Q1
    const a1Edges = mdb.getEdgesFrom(nodes[1].id);
    expect(a1Edges.some(e => e.kind === "reply_to" && e.to_node === nodes[0].id)).toBe(true);
    // Q2 has reply_to edge pointing to A1
    const q2Edges = mdb.getEdgesFrom(nodes[2].id);
    expect(q2Edges.some(e => e.kind === "reply_to" && e.to_node === nodes[1].id)).toBe(true);
  });

  it("applies secret filter to content", () => {
    ingestGenericChat(mdb, filter, {
      repo: "r", sessionId: "s", sessionTitle: "T",
      messages: [{ role: "user", content: "my key is AKIAIOSFODNN7EXAMPLE" }],
    });

    const nodes = mdb.getNodesByRepo("r", 100, 0).filter(n => n.kind === "message");
    expect(nodes[0].body).toContain("[REDACTED]");
    expect(nodes[0].body).not.toContain("AKIAIOSFODNN7EXAMPLE");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mcp-bridge && npx vitest run tests/ingest-generic.test.ts --reporter=verbose 2>&1 | tail -10`
Expected: FAIL — module not found

- [ ] **Step 3: Implement ingestGenericChat**

Create `src/application/services/ingest-generic.ts` following the same pattern as `ingest-transcript.ts`:

1. Check idempotency via `getNodeBySource("generic-session", sessionId)`
2. Create conversation node with source_type `"generic-session"`
3. For each message: create node with `sender = message.role`, `reply_to` edge to previous
4. `contains` edges from conversation to each message
5. Apply `filter.redact()` to all content
6. Return `{ messages_ingested, edges_created, skipped, conversation_id }`

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/ingest-generic.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add mcp-bridge/src/application/services/ingest-generic.ts mcp-bridge/tests/ingest-generic.test.ts
git commit -m "feat: add generic chat ingestion service"
```

---

### Task 7: Create Claude Code JSONL parser

**Files:**
- Create: `mcp-bridge/src/ingestion/claude-code-parser.ts`
- Create: `mcp-bridge/tests/claude-code-parser.test.ts`

- [ ] **Step 1: Write failing test**

Create `tests/claude-code-parser.test.ts`:

```typescript
import { describe, it, expect } from "vitest";
import { parseClaudeCodeSession } from "../src/ingestion/claude-code-parser.js";

const makeUserLine = (uuid: string, parentUuid: string | null, content: string, timestamp = "2026-01-01T00:00:00Z") =>
  JSON.stringify({ type: "user", uuid, parentUuid, message: { role: "user", content }, timestamp, sessionId: "s1", cwd: "/repo", gitBranch: "main", version: "2.1.80", entrypoint: "cli" });

const makeAssistantLine = (uuid: string, parentUuid: string, blocks: unknown[], timestamp = "2026-01-01T00:00:01Z") =>
  JSON.stringify({ type: "assistant", uuid, parentUuid, message: { role: "assistant", content: blocks }, timestamp, sessionId: "s1", cwd: "/repo", gitBranch: "main", version: "2.1.80", entrypoint: "cli" });

const makeProgressLine = (uuid: string, parentUuid: string) =>
  JSON.stringify({ type: "progress", uuid, parentUuid, data: { type: "agent_progress" }, timestamp: "2026-01-01T00:00:02Z", sessionId: "s1" });

const makeSnapshotLine = () =>
  JSON.stringify({ type: "file-history-snapshot", messageId: "m1", snapshot: { messageId: "m1", trackedFileBackups: {}, timestamp: "2026-01-01T00:00:00Z" }, isSnapshotUpdate: false });

describe("parseClaudeCodeSession", () => {
  it("filters out progress, snapshot, and system lines", () => {
    const lines = [
      makeSnapshotLine(),
      makeProgressLine("p1", "u1"),
      makeUserLine("u1", null, "Hello"),
      makeAssistantLine("a1", "u1", [{ type: "text", text: "Hi there" }]),
    ];
    const result = parseClaudeCodeSession(lines);
    expect(result.turns).toHaveLength(1);
    expect(result.skipped).toBe(2); // snapshot + progress
  });

  it("groups human + assistant into turns", () => {
    const lines = [
      makeUserLine("u1", null, "Question 1"),
      makeAssistantLine("a1", "u1", [{ type: "thinking", thinking: "hmm" }, { type: "text", text: "Answer 1" }]),
      makeUserLine("u2", "a1", "Question 2"),
      makeAssistantLine("a2", "u2", [{ type: "text", text: "Answer 2" }]),
    ];
    const result = parseClaudeCodeSession(lines);
    expect(result.turns).toHaveLength(2);
    expect(result.turns[0].human.content).toBe("Question 1");
    expect(result.turns[0].assistant.visibleText).toBe("Answer 1");
    expect(result.turns[0].assistant.hasThinking).toBe(true);
    expect(result.turns[1].human.content).toBe("Question 2");
  });

  it("extracts tool_use blocks from assistant messages", () => {
    const lines = [
      makeUserLine("u1", null, "Read a file"),
      makeAssistantLine("a1", "u1", [
        { type: "text", text: "Let me read that." },
        { type: "tool_use", id: "t1", name: "Read", input: { file_path: "/foo.ts" } },
      ]),
      // tool_result comes as a user message
      JSON.stringify({ type: "user", uuid: "tr1", parentUuid: "a1", message: { role: "user", content: [{ type: "tool_result", tool_use_id: "t1", content: "file contents" }] }, timestamp: "2026-01-01T00:00:02Z", sessionId: "s1", cwd: "/repo" }),
      makeAssistantLine("a2", "tr1", [{ type: "text", text: "Done." }]),
    ];
    const result = parseClaudeCodeSession(lines);
    expect(result.turns).toHaveLength(1); // tool_result + follow-up assistant are same turn
    expect(result.turns[0].assistant.toolUses).toHaveLength(1);
    expect(result.turns[0].assistant.toolUses[0].name).toBe("Read");
  });

  it("detects Agent tool uses as subagent spawns", () => {
    const lines = [
      makeUserLine("u1", null, "Explore the codebase"),
      makeAssistantLine("a1", "u1", [
        { type: "tool_use", id: "t1", name: "Agent", input: { subagent_type: "Explore", description: "Map codebase", prompt: "Find files" } },
      ]),
    ];
    const result = parseClaudeCodeSession(lines);
    expect(result.turns[0].assistant.toolUses[0].isSubagent).toBe(true);
    expect(result.turns[0].assistant.toolUses[0].subagentType).toBe("Explore");
  });

  it("extracts session metadata from first valid line", () => {
    const lines = [
      makeSnapshotLine(),
      makeUserLine("u1", null, "Hello"),
    ];
    const result = parseClaudeCodeSession(lines);
    expect(result.metadata.sessionId).toBe("s1");
    expect(result.metadata.cwd).toBe("/repo");
    expect(result.metadata.gitBranch).toBe("main");
    expect(result.metadata.version).toBe("2.1.80");
    expect(result.metadata.entrypoint).toBe("cli");
  });

  it("skips meta user messages (isMeta: true)", () => {
    const lines = [
      JSON.stringify({ type: "user", uuid: "m1", parentUuid: null, isMeta: true, message: { role: "user", content: "<local-command-caveat>..." }, timestamp: "2026-01-01T00:00:00Z", sessionId: "s1", cwd: "/repo" }),
      makeUserLine("u1", "m1", "Real question"),
      makeAssistantLine("a1", "u1", [{ type: "text", text: "Real answer" }]),
    ];
    const result = parseClaudeCodeSession(lines);
    expect(result.turns).toHaveLength(1);
    expect(result.turns[0].human.content).toBe("Real question");
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd mcp-bridge && npx vitest run tests/claude-code-parser.test.ts --reporter=verbose 2>&1 | tail -10`
Expected: FAIL — module not found

- [ ] **Step 3: Implement parseClaudeCodeSession**

Create `src/ingestion/claude-code-parser.ts`:

```typescript
// Types
export interface ClaudeCodeTurn {
  index: number;
  human: { content: string; uuid: string; timestamp: string | null };
  assistant: {
    visibleText: string;
    hasThinking: boolean;
    toolUses: ToolUseInfo[];
    uuids: string[];  // all UUIDs belonging to this turn (for detail expansion)
    timestamp: string | null;
  };
}

export interface ToolUseInfo {
  toolUseId: string;
  name: string;
  input: Record<string, unknown>;
  isSubagent: boolean;
  subagentType?: string;
  subagentDescription?: string;
}

export interface SessionMetadata {
  sessionId: string;
  cwd: string;
  gitBranch: string;
  version: string;
  entrypoint: string;
}

export interface ParsedSession {
  metadata: SessionMetadata;
  turns: ClaudeCodeTurn[];
  skipped: number;
}

export function parseClaudeCodeSession(lines: string[]): ParsedSession
```

Algorithm:
1. Parse each JSONL line, skip `file-history-snapshot`, `progress`, `system` types
2. Skip `isMeta: true` user messages
3. Extract metadata from first line with `sessionId`, `cwd`, etc.
4. Group into turns: a turn starts with a non-meta `user` message and includes all subsequent `assistant` messages until the next non-meta `user` message. Tool results (user messages with `tool_result` content) are part of the current turn, not a new turn.
5. For each assistant message in a turn, extract `text` blocks (concatenate into `visibleText`), detect `thinking` blocks, extract `tool_use` blocks with input.
6. Track all UUIDs that belong to each turn for later detail expansion.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/claude-code-parser.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add mcp-bridge/src/ingestion/claude-code-parser.ts mcp-bridge/tests/claude-code-parser.test.ts
git commit -m "feat: add Claude Code JSONL parser with turn grouping"
```

---

### Task 8: Create Claude Code ingestion service (summary + detail)

**Files:**
- Create: `mcp-bridge/src/application/services/ingest-claude-code.ts`
- Create: `mcp-bridge/tests/ingest-claude-code.test.ts`

- [ ] **Step 1: Write failing tests**

Create `tests/ingest-claude-code.test.ts` with tests for:

1. `ingestClaudeCodeSummary` — creates conversation node + turn nodes with correct senders, contains/reply_to edges, metadata with UUIDs
2. `ingestClaudeCodeSummary` — idempotent (skips if session already exists)
3. `ingestClaudeCodeSummary` — sets `expanded: false` in turn metadata
4. `expandClaudeCodeTurn` — creates tool_use artifact nodes and subagent task nodes from stored UUIDs
5. `expandClaudeCodeTurn` — sets `expanded: true` after expansion
6. `expandClaudeCodeTurn` — no-op when already expanded
7. Secret filter applied to all content

Use the same `makeUserLine`/`makeAssistantLine` helpers from the parser tests.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd mcp-bridge && npx vitest run tests/ingest-claude-code.test.ts --reporter=verbose 2>&1 | tail -10`
Expected: FAIL — module not found

- [ ] **Step 3: Implement ingestClaudeCodeSummary and expandClaudeCodeTurn**

Create `src/application/services/ingest-claude-code.ts`:

```typescript
export function ingestClaudeCodeSummary(
  mdb: MemoryDbClient,
  filter: SecretFilter,
  input: ClaudeCodeSessionInput,
): AppResult<IngestResult>

export function expandClaudeCodeTurn(
  mdb: MemoryDbClient,
  filter: SecretFilter,
  turnNodeId: string,
  lines: string[],
): AppResult<ExpandResult>
```

`ingestClaudeCodeSummary`:
1. Check idempotency via `getNodeBySource("claude-code-session", sessionId)`
2. Call `parseClaudeCodeSession(lines)` to get turns + metadata
3. Create conversation node (source_type: `"claude-code-session"`, metadata includes `file_path`)
4. For each turn: create human turn node (sender: `"human"`) + assistant turn node (sender: `"assistant"`)
5. Edges: `contains` (conversation → turn), `reply_to` (assistant → human)
6. Store UUIDs in turn metadata for detail expansion

`expandClaudeCodeTurn`:
1. Read the turn node, check `metadata.expanded`
2. Re-parse the JSONL lines filtered to the turn's UUID range
3. Create artifact nodes for each tool_use, task nodes for Agent tool uses
4. Create edges: `contains` (assistant turn → tool), `spawned` (assistant turn → subagent)
5. Update turn metadata: `expanded: true`

- [ ] **Step 4: Add `updateNodeMeta` to MemoryDbClient**

In `memory-client.ts`, add:

```typescript
updateNodeMeta(id: string, meta: string): void
```

Simple prepared statement: `UPDATE nodes SET meta = @meta, updated_at = datetime('now') WHERE id = @id`

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/ingest-claude-code.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add mcp-bridge/src/application/services/ingest-claude-code.ts mcp-bridge/src/db/memory-client.ts mcp-bridge/tests/ingest-claude-code.test.ts
git commit -m "feat: add Claude Code session ingestion with two-level expansion"
```

---

### Task 9: Wire ingest endpoint + expand endpoint + MCP tool

**Files:**
- Modify: `mcp-bridge/src/transport/schemas/memory-schemas.ts`
- Modify: `mcp-bridge/src/transport/controllers/memory-controller.ts`
- Modify: `mcp-bridge/src/routes/memory.ts`
- Modify: `mcp-bridge/src/mcp.ts`
- Modify: `mcp-bridge/src/transport/controllers/memory-controller.test.ts`

- [ ] **Step 1: Write failing tests for ingest + expand endpoints**

In `tests/memory-controller.test.ts`, add tests:

1. `POST /memory/ingest` with `source: "generic"` + `content` calls `ingestGenericChat`
2. `POST /memory/ingest` with `source: "claude-code"` + `path` calls `ingestClaudeCodeSummary`
3. `POST /memory/ingest` with `source: "bridge"` returns error
4. `POST /memory/node/:id/expand` creates detail nodes for unexpanded turn
5. `POST /memory/node/:id/expand` is a no-op for already-expanded turn

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd mcp-bridge && npx vitest run src/transport/controllers/memory-controller.test.ts -t "ingest|expand" --reporter=verbose 2>&1 | tail -20`
Expected: FAIL

- [ ] **Step 3: Update IngestBodySchema with new fields**

In `memory-schemas.ts`, update `IngestBodySchema`:

```typescript
export const IngestBodySchema = z.object({
  repo: z.string(),
  source: z.enum(["bridge", "transcript", "git", "generic", "claude-code"]),
  session_id: z.string().optional(),
  title: z.string().optional(),
  path: z.string().optional(),
  content: z.string().optional(),
  agent: z.string().max(64).regex(/^[a-zA-Z0-9_-]+$/).default("anonymous"),
});

export const IngestResponseSchema = z.object({
  conversation_id: z.string(),
  messages_ingested: z.number(),
  edges_created: z.number(),
  skipped: z.number(),
});
```

Add `ExpandNodeSchema`:

```typescript
export const ExpandNodeSchema = {
  params: z.object({ id: z.string() }),
  response: z.object({
    nodes_created: z.number(),
    edges_created: z.number(),
    nodes: z.array(NodeResponseSchema),
    edges: z.array(EdgeResponseSchema),
  }),
} as const;
export type ExpandNodeSchema = typeof ExpandNodeSchema;
```

- [ ] **Step 4: Wire ingest handler in controller**

Replace the stubbed `ingest` handler to dispatch based on `source`:
- `generic`: Parse `content` as JSON array, call `ingestGenericChat`
- `claude-code`: Read file at `path`, call `ingestClaudeCodeSummary`
- `transcript`: Read file at `path`, call `ingestTranscriptLines`
- `git`: Call `ingestGitMetadata`
- `bridge`: Return error

Add `expand` handler: reads turn node, finds parent conversation for `file_path`, reads JSONL, calls `expandClaudeCodeTurn`. **Not rate-limited** — synchronous and immediate.

- [ ] **Step 5: Add expand route**

In `routes/memory.ts`:

```typescript
defineRoute({
  method: "POST",
  path: "/node/:id/expand",
  summary: "Expand a summary-only turn into full detail nodes",
  schema: ExpandNodeSchema,
  handler: handlers.expand,
}),
```

- [ ] **Step 6: Add ingest_conversation MCP tool**

In `mcp.ts`, add:

```typescript
server.tool(
  "ingest_conversation",
  "Ingest a conversation into the memory graph for long-term preservation.",
  {
    repo: z.string().describe("Repository slug"),
    source: z.enum(["claude-code", "transcript", "generic"]).describe("Ingestion format"),
    session_id: z.string().describe("Unique session ID for idempotency"),
    title: z.string().optional().describe("Conversation title"),
    content: z.string().optional().describe("Inline content (generic format)"),
    path: z.string().optional().describe("File path (transcript/claude-code format)"),
    agent: z.string().optional().describe("Agent identifier"),
  },
  async ({ repo, source, session_id, title, content, path, agent }) => {
    // Route to appropriate ingestion service
  }
);
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run --reporter=verbose 2>&1 | tail -10`
Expected: All PASS

- [ ] **Step 8: Commit**

```bash
git add mcp-bridge/src/transport/schemas/memory-schemas.ts mcp-bridge/src/transport/controllers/memory-controller.ts mcp-bridge/src/routes/memory.ts mcp-bridge/src/mcp.ts mcp-bridge/src/transport/controllers/memory-controller.test.ts
git commit -m "feat: wire ingest endpoint, expand endpoint, and ingest_conversation MCP tool"
```

---

## Phase 3: Automatic Claude Code Ingestion

### Task 10: Create rate-limited session queue

**Files:**
- Create: `mcp-bridge/src/ingestion/session-queue.ts`
- Create: `mcp-bridge/tests/session-queue.test.ts`

- [ ] **Step 1: Write failing tests**

Create `tests/session-queue.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { createSessionQueue, type SessionJob } from "../src/ingestion/session-queue.js";

describe("createSessionQueue", () => {
  beforeEach(() => { vi.useFakeTimers(); });
  afterEach(() => { vi.useRealTimers(); });

  it("processes items at the rate limit interval", async () => {
    const processed: string[] = [];
    const queue = createSessionQueue({
      maxSize: 10,
      rateMs: 100,
      handler: async (job) => { processed.push(job.sessionId); },
    });

    queue.enqueue({ sessionId: "s1", filePath: "/f1", repo: "r", pass: "summary" });
    queue.enqueue({ sessionId: "s2", filePath: "/f2", repo: "r", pass: "both" });

    expect(processed).toHaveLength(0);
    await vi.advanceTimersByTimeAsync(100);
    expect(processed).toHaveLength(1);
    expect(processed[0]).toBe("s1");
    await vi.advanceTimersByTimeAsync(100);
    expect(processed).toHaveLength(2);

    queue.stop();
  });

  it("drops oldest when queue is full and calls onDrop", () => {
    const dropped: string[] = [];
    const queue = createSessionQueue({
      maxSize: 2,
      rateMs: 1000,
      handler: async () => {},
      onDrop: (job) => { dropped.push(job.sessionId); },
    });

    queue.enqueue({ sessionId: "s1", filePath: "/f1", repo: "r", pass: "summary" });
    queue.enqueue({ sessionId: "s2", filePath: "/f2", repo: "r", pass: "summary" });
    queue.enqueue({ sessionId: "s3", filePath: "/f3", repo: "r", pass: "summary" });

    expect(dropped).toEqual(["s1"]);
    expect(queue.depth()).toBe(2);

    queue.stop();
  });

  it("preserves pass field on jobs", async () => {
    let capturedPass: string | undefined;
    const queue = createSessionQueue({
      maxSize: 10,
      rateMs: 50,
      handler: async (job) => { capturedPass = job.pass; },
    });

    queue.enqueue({ sessionId: "s1", filePath: "/f1", repo: "r", pass: "both" });
    await vi.advanceTimersByTimeAsync(50);
    expect(capturedPass).toBe("both");

    queue.stop();
  });

  it("calls onError when handler throws", async () => {
    const errors: string[] = [];
    const queue = createSessionQueue({
      maxSize: 10,
      rateMs: 50,
      handler: async () => { throw new Error("fail"); },
      onError: (err) => { errors.push(err.message); },
    });

    queue.enqueue({ sessionId: "s1", filePath: "/f1", repo: "r", pass: "summary" });
    await vi.advanceTimersByTimeAsync(50);
    expect(errors).toEqual(["fail"]);

    queue.stop();
  });

  it("stop() clears the interval", () => {
    const queue = createSessionQueue({
      maxSize: 10,
      rateMs: 50,
      handler: async () => {},
    });

    queue.enqueue({ sessionId: "s1", filePath: "/f1", repo: "r", pass: "summary" });
    queue.stop();
    expect(queue.depth()).toBe(1); // item remains but interval is cleared
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd mcp-bridge && npx vitest run tests/session-queue.test.ts --reporter=verbose 2>&1 | tail -10`
Expected: FAIL — module not found

- [ ] **Step 3: Implement createSessionQueue**

Create `src/ingestion/session-queue.ts`:

```typescript
export interface SessionJob {
  sessionId: string;
  filePath: string;
  repo: string;
  pass: "summary" | "detail" | "both";
}

export interface SessionQueueConfig {
  maxSize: number;
  rateMs: number;
  handler: (job: SessionJob) => Promise<void>;
  onDrop?: (job: SessionJob) => void;
  onError?: (err: Error, job: SessionJob) => void;
}

export interface SessionQueue {
  enqueue(job: SessionJob): void;
  depth(): number;
  stop(): void;
}

export function createSessionQueue(config: SessionQueueConfig): SessionQueue
```

Uses `setInterval` at `rateMs` to drain one item from the queue per tick. Similar pattern to existing `BoundedQueue` but with rate limiting instead of immediate draining.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/session-queue.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add mcp-bridge/src/ingestion/session-queue.ts mcp-bridge/tests/session-queue.test.ts
git commit -m "feat: add rate-limited session ingestion queue"
```

---

### Task 11: Create file watcher for Claude Code sessions

**Files:**
- Create: `mcp-bridge/src/ingestion/claude-code-watcher.ts`
- Create: `mcp-bridge/tests/claude-code-watcher.test.ts`

- [ ] **Step 1: Write failing tests**

Create `tests/claude-code-watcher.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mkdtempSync, writeFileSync, mkdirSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { deriveRepoFromJSONL } from "../src/ingestion/claude-code-watcher.js";

describe("deriveRepoFromJSONL", () => {
  it("extracts repo slug from cwd in first valid line", () => {
    const lines = [
      JSON.stringify({ type: "file-history-snapshot", snapshot: {} }),
      JSON.stringify({ type: "user", uuid: "u1", cwd: "/Users/dev/repos/my-project", sessionId: "s1", message: { role: "user", content: "hi" } }),
    ];
    const result = deriveRepoFromJSONL(lines);
    expect(result).toBe("my-project");
  });

  it("returns null when no valid lines have cwd", () => {
    const lines = [
      JSON.stringify({ type: "file-history-snapshot", snapshot: {} }),
    ];
    const result = deriveRepoFromJSONL(lines);
    expect(result).toBeNull();
  });
});

describe("scanDirectory", () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = mkdtempSync(join(tmpdir(), "watcher-test-"));
    mkdirSync(join(tmpDir, "proj1"), { recursive: true });
  });

  it("finds .jsonl files in subdirectories", async () => {
    const { scanDirectory } = await import("../src/ingestion/claude-code-watcher.js");
    writeFileSync(join(tmpDir, "proj1", "session1.jsonl"), "");
    writeFileSync(join(tmpDir, "proj1", "session2.jsonl"), "");
    writeFileSync(join(tmpDir, "proj1", "other.txt"), "");

    const files = scanDirectory(tmpDir);
    expect(files).toHaveLength(2);
    expect(files.every(f => f.endsWith(".jsonl"))).toBe(true);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd mcp-bridge && npx vitest run tests/claude-code-watcher.test.ts --reporter=verbose 2>&1 | tail -10`
Expected: FAIL — module not found

- [ ] **Step 3: Implement ClaudeCodeWatcher**

Create `src/ingestion/claude-code-watcher.ts`:

```typescript
export interface ClaudeCodeWatcherConfig {
  watchDir: string;              // ~/.claude/projects/
  mdb: MemoryDbClient;
  queue: SessionQueue;
  filter: SecretFilter;
  debounceMs?: number;           // default: 30000 (30s)
}

export interface ClaudeCodeWatcher {
  start(): void;
  stop(): void;
  scanOnce(): Promise<void>;     // startup scan
}

export function createClaudeCodeWatcher(config: ClaudeCodeWatcherConfig): ClaudeCodeWatcher
```

Implementation:
1. `start()`: `fs.watch(watchDir, { recursive: true })`, filter for `.jsonl` files
2. On change: reset debounce timer for that file. After `debounceMs` of quiet, derive repo from JSONL `cwd`, check idempotency, enqueue with `pass: "both"` (active session).
3. `scanOnce()`: List all `*/*.jsonl` files, check each against `getNodeBySource`, enqueue unprocessed with `pass: "summary"` (cold session).
4. `stop()`: Close watcher, clear timers.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/claude-code-watcher.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: All PASS

- [ ] **Step 5: Commit**

```bash
git add mcp-bridge/src/ingestion/claude-code-watcher.ts mcp-bridge/tests/claude-code-watcher.test.ts
git commit -m "feat: add file watcher for automatic Claude Code session ingestion"
```

---

### Task 12: Wire file watcher into server bootstrap

**Files:**
- Modify: `mcp-bridge/src/index.ts`

- [ ] **Step 1: Wire session queue + file watcher into index.ts**

In `index.ts`, after the existing backfill setup:

```typescript
import { createSessionQueue } from "./ingestion/session-queue.js";
import { createClaudeCodeWatcher } from "./ingestion/claude-code-watcher.js";
import { ingestClaudeCodeSummary, expandClaudeCodeTurn } from "./application/services/ingest-claude-code.js";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { homedir } from "node:os";

// Create session queue with rate limit
const sessionQueue = createSessionQueue({
  maxSize: 100,
  rateMs: 5000,
  handler: async (job) => {
    const lines = readFileSync(job.filePath, "utf-8").split("\n");
    const summaryResult = ingestClaudeCodeSummary(mdb, filter, {
      repo: job.repo,
      sessionId: job.sessionId,
      filePath: job.filePath,
      lines,
    });
    if (!summaryResult.ok) {
      console.warn(`[session-queue] Summary failed for ${job.sessionId}:`, summaryResult.error.message);
      return;
    }
    if (job.pass === "both" || job.pass === "detail") {
      // Expand all turns for active sessions
      const convNode = mdb.getNodeBySource("claude-code-session", job.sessionId);
      if (convNode) {
        const turnEdges = mdb.getEdgesFrom(convNode.id).filter(e => e.kind === "contains");
        for (const edge of turnEdges) {
          const turn = mdb.getNode(edge.to_node);
          if (turn && turn.sender === "assistant") {
            expandClaudeCodeTurn(mdb, filter, turn.id, lines);
          }
        }
      }
    }
  },
  onError: (err, job) => console.warn(`[session-queue] Error processing ${job.sessionId}:`, err.message),
  onDrop: (job) => console.warn(`[session-queue] Dropped ${job.sessionId} — queue full`),
});

// File watcher for Claude Code sessions
const claudeDir = join(homedir(), ".claude", "projects");
const watcher = createClaudeCodeWatcher({
  watchDir: claudeDir,
  mdb,
  queue: sessionQueue,
  filter,
});

// Startup: prune old traversal logs, scan for unprocessed sessions, then start watching
setImmediate(async () => {
  mdb.pruneTraversalLogs(30); // delete logs older than 30 days
  await watcher.scanOnce();
  watcher.start();
});
```

Note: `pruneTraversalLogs(days: number)` should be added to `MemoryDbClient` in Task 2 (prepared statement: `DELETE FROM traversal_logs WHERE created_at < datetime('now', '-' || @days || ' days')`).

- [ ] **Step 2: Run full test suite**

Run: `cd mcp-bridge && npx vitest run --reporter=verbose 2>&1 | tail -10`
Expected: All PASS

- [ ] **Step 3: Commit**

```bash
git add mcp-bridge/src/index.ts
git commit -m "feat: wire file watcher and session queue into server bootstrap"
```

---

## Phase 4: Frontend Graph Components

### Task 13: Install React Flow + Dagre and create graph canvas

**Files:**
- Modify: `ui/package.json`
- Create: `ui/src/components/graph/graph-canvas.tsx`
- Create: `ui/src/hooks/use-graph-layout.ts`
- Create: `ui/src/components/graph/edge-styles.ts`

- [ ] **Step 1: Install dependencies**

Run: `cd ui && npm install @xyflow/react @dagrejs/dagre`

- [ ] **Step 2: Create edge-styles.ts**

Create `ui/src/components/graph/edge-styles.ts` with the 10 edge kind → color/dash mapping from the spec visual language table.

- [ ] **Step 3: Create use-graph-layout hook**

Create `ui/src/hooks/use-graph-layout.ts`:

Takes raw `nodes[]` and `edges[]` from API → runs Dagre layout → returns positioned React Flow nodes and edges. Breaks cycles (remove `related_to`/`references` back-edges before layout, re-add after). Memoized with `useMemo`.

- [ ] **Step 4: Create graph-canvas.tsx**

Create `ui/src/components/graph/graph-canvas.tsx`:

React Flow wrapper that accepts positioned nodes/edges, renders with custom node types and edge styles, includes minimap and controls. Routes node clicks to the selection callback; supports zoom/pan. Holds React Flow state (nodes, edges, onNodesChange, onEdgesChange).

- [ ] **Step 5: Verify it renders**

Run: `cd ui && npm run build 2>&1 | tail -20`
Expected: Build succeeds with no TypeScript errors

- [ ] **Step 6: Commit**

```bash
git add ui/package.json ui/package-lock.json ui/src/components/graph/graph-canvas.tsx ui/src/components/graph/edge-styles.ts ui/src/hooks/use-graph-layout.ts
git commit -m "feat: add React Flow graph canvas with Dagre layout hook"
```

---

### Task 14: Create custom node type components

**Files:**
- Create: `ui/src/components/graph/node-types/message-node.tsx`
- Create: `ui/src/components/graph/node-types/conversation-node.tsx`
- Create: `ui/src/components/graph/node-types/topic-node.tsx`
- Create: `ui/src/components/graph/node-types/decision-node.tsx`
- Create: `ui/src/components/graph/node-types/task-node.tsx`
- Create: `ui/src/components/graph/node-types/artifact-node.tsx`

- [ ] **Step 1: Create all 6 node type components**

Each follows the spec visual language (color, shape, displays). All use React Flow's `Handle` for edge connections. Each accepts `data` props matching the `NodeRow` shape. Message nodes display sender label. Task nodes show status badge. Artifact nodes show type from metadata.

Interactive states per spec:
- Default: background at 0.15 opacity, 1px border at 0.4 opacity
- Hover: background at 0.25, 2px border at 0.7, subtle glow
- Selected: background at 0.3, 2px solid border, stronger glow

Decision nodes use CSS `transform: rotate(45deg)` for diamond shape with inner content rotated back.

- [ ] **Step 2: Register node types in graph-canvas**

Update `graph-canvas.tsx` to include the `nodeTypes` mapping:

```typescript
const nodeTypes = {
  message: MessageNode,
  conversation: ConversationNode,
  topic: TopicNode,
  decision: DecisionNode,
  task: TaskNode,
  artifact: ArtifactNode,
};
```

- [ ] **Step 3: Build check**

Run: `cd ui && npm run build 2>&1 | tail -20`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add ui/src/components/graph/node-types/
git commit -m "feat: add 6 custom React Flow node type components"
```

---

### Task 15: Create graph toolbar and side panels

**Files:**
- Create: `ui/src/components/graph/graph-toolbar.tsx`
- Create: `ui/src/components/graph/graph-minimap.tsx`
- Create: `ui/src/components/graph/node-detail-panel.tsx`
- Create: `ui/src/components/graph/context-builder-panel.tsx`

- [ ] **Step 1: Create graph-toolbar.tsx**

Depth slider (1-5), direction toggle (outgoing/incoming/both), edge kind filter checkboxes, sender filter dropdown. All values come in via props (controlled component).

- [ ] **Step 2: Create graph-minimap.tsx**

React Flow MiniMap wrapper with node-kind coloring (maps kind → hex color).

- [ ] **Step 3: Create node-detail-panel.tsx**

Displays selected node's title, body, metadata, sender, timestamps, edge list (in/out). Accepts `selectedNode` prop. Shows "expand" button for unexpanded Claude Code assistant turns (checks `metadata.expanded === false`).

- [ ] **Step 4: Create context-builder-panel.tsx**

"Assemble Context" button, token budget bar (used/total with gradient fill), sections list with heading + relevance score bar + token estimate. Calls `/memory/context` API. Highlights included `node_ids` in graph via callback. Legend for gold ring vs dimmed.

- [ ] **Step 5: Build check**

Run: `cd ui && npm run build 2>&1 | tail -20`
Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
git add ui/src/components/graph/graph-toolbar.tsx ui/src/components/graph/graph-minimap.tsx ui/src/components/graph/node-detail-panel.tsx ui/src/components/graph/context-builder-panel.tsx
git commit -m "feat: add graph toolbar, minimap, detail panel, and context builder"
```

---

## Phase 5: Frontend Pages

### Task 16: Add header nav bar

**Files:**
- Create: `ui/src/components/nav-header.tsx`
- Modify: `ui/src/app/layout.tsx`

- [ ] **Step 1: Create nav-header.tsx**

Simple nav with links: **Conversations** (`/`), **Memory Explorer** (`/memory`). Highlight active route. Styled to match existing dark theme.

- [ ] **Step 2: Add to layout.tsx**

Import and render `NavHeader` above the main content in `layout.tsx`. Remove any existing ad-hoc navigation.

- [ ] **Step 3: Build check and manual verify**

Run: `cd ui && npm run build`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add ui/src/components/nav-header.tsx ui/src/app/layout.tsx
git commit -m "feat: add header nav bar with Conversations and Memory Explorer links"
```

---

### Task 17: Build Memory Explorer page with React Flow

**Files:**
- Create: `ui/src/components/memory-explorer/memory-explorer-page.tsx`
- Create: `ui/src/components/memory-explorer/memory-search-panel.tsx`
- Create: `ui/src/components/memory-explorer/traversal-log-panel.tsx`
- Create: `ui/src/hooks/use-traversal-logs.ts`
- Modify: `ui/src/app/memory/page.tsx`
- Modify: `ui/src/lib/memory-api.ts`

- [ ] **Step 1: Add traversal log API functions to memory-api.ts**

```typescript
export async function getTraversalLogs(repo: string, limit = 20): Promise<TraversalLog[]>
export async function getTraversalLog(id: string): Promise<TraversalLog>
export async function expandNode(id: string): Promise<ExpandResult>
```

- [ ] **Step 2: Create use-traversal-logs hook**

Fetches traversal logs for a repo, tracks loading/error state.

- [ ] **Step 3: Create memory-search-panel.tsx**

Left panel top: search input, mode selector (keyword/semantic/hybrid), kind filter chips, sender filter dropdown. Results as scored list with match type badges (K/S/H). Click result → callback to center graph on node.

- [ ] **Step 4: Create traversal-log-panel.tsx**

Left panel bottom: list of recorded traversals with agent name, operation, start node, depth, timestamp. Click → loads traversal subgraph into the graph.

- [ ] **Step 5: Create memory-explorer-page.tsx**

Three-column layout wiring: search panel + traversal log panel (left), graph canvas (center), node detail panel + context builder panel (right). Search results highlight nodes with score badges. Non-results dimmed at 0.4 opacity.

- [ ] **Step 6: Update memory/page.tsx**

Replace current content with the new `MemoryExplorerPage` component.

- [ ] **Step 7: Build check**

Run: `cd ui && npm run build`
Expected: Build succeeds

- [ ] **Step 8: Commit**

```bash
git add ui/src/components/memory-explorer/ ui/src/hooks/use-traversal-logs.ts ui/src/app/memory/page.tsx ui/src/lib/memory-api.ts
git commit -m "feat: rebuild Memory Explorer with React Flow three-column layout"
```

---

### Task 18: Build Conversation Graph page

**Files:**
- Create: `ui/src/app/conversation/[id]/layout.tsx`
- Create: `ui/src/app/conversation/[id]/graph/page.tsx`
- Create: `ui/src/components/conversation-graph/conversation-graph-page.tsx`
- Create: `ui/src/components/conversation-graph/conversation-node-list.tsx`
- Modify: `ui/src/app/conversation/[id]/page.tsx`

- [ ] **Step 1: Create shared conversation layout**

Create `ui/src/app/conversation/[id]/layout.tsx` with conversation header and tab nav (Timeline | Graph). Fetches conversation data shared by both views.

- [ ] **Step 2: Move existing timeline into layout**

Update `page.tsx` (timeline view) to work within the shared layout — it becomes the default tab.

- [ ] **Step 3: Create conversation-node-list.tsx**

Left panel: list of nodes filtered by kind, sorted by timestamp. Click to select and center in graph. Supports sender filter dropdown.

- [ ] **Step 4: Create conversation-graph-page.tsx**

Three-column layout: node list (left), graph canvas (center), detail panel + context builder (right). Loads the conversation's subgraph via `/memory/traverse/:id`. Expand button on unexpanded turns calls `/memory/node/:id/expand`.

- [ ] **Step 5: Create graph route page**

Create `ui/src/app/conversation/[id]/graph/page.tsx` rendering `ConversationGraphPage`.

- [ ] **Step 6: Build check**

Run: `cd ui && npm run build`
Expected: Build succeeds

- [ ] **Step 7: Commit**

```bash
git add ui/src/app/conversation/[id]/ ui/src/components/conversation-graph/
git commit -m "feat: add conversation graph page with tab navigation"
```

---

### Task 19: Build path replay system

**Files:**
- Create: `ui/src/hooks/use-path-replay.ts`
- Create: `ui/src/components/graph/path-replay.tsx`

- [ ] **Step 1: Create use-path-replay hook**

State machine for replay: `idle` → `playing` → `paused` → `idle`. Tracks current step index, timer interval, speed (0.5x-3x). Exposes: `play()`, `pause()`, `stepForward()`, `stepBack()`, `setSpeed()`, `loadTraversal(log)`, `currentStep`, `highlightedNodes`, `highlightedEdges`.

Visited nodes: green glow + step number. Current node: pulsing ring. Pending: dimmed.

- [ ] **Step 2: Create path-replay.tsx**

Playback controls panel: play/pause button, step forward/back buttons, speed slider, progress indicator. Current node detail with "reached via [edge_kind] from [parent]". Visit order list.

- [ ] **Step 3: Wire into Memory Explorer**

Update `memory-explorer-page.tsx` to pass replay state into the graph canvas (overrides default node styling when replay is active).

- [ ] **Step 4: Build check**

Run: `cd ui && npm run build`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add ui/src/hooks/use-path-replay.ts ui/src/components/graph/path-replay.tsx ui/src/components/memory-explorer/memory-explorer-page.tsx
git commit -m "feat: add traversal path replay system"
```

---

### Task 20: Remove deprecated memory-graph.tsx

**Files:**
- Delete: `ui/src/components/memory-graph.tsx`

- [ ] **Step 1: Verify no remaining imports**

Run: `cd ui && grep -r "memory-graph" src/ --include="*.tsx" --include="*.ts"`
Expected: No results (already replaced by graph-canvas)

- [ ] **Step 2: Delete the file**

```bash
rm ui/src/components/memory-graph.tsx
```

- [ ] **Step 3: Build check**

Run: `cd ui && npm run build`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add -u ui/src/components/memory-graph.tsx
git commit -m "refactor: remove deprecated memory-graph.tsx"
```

---

## Phase 6: Update sender in existing ingestion paths

> **Note:** This task can be done any time after Task 1. It's placed in Phase 6 because the new ingestion services (generic, Claude Code) set sender from the start, and existing paths work correctly without it — they just insert `sender: null`. Do this task early if you want sender filtering to work for bridge/transcript/git data immediately.

### Task 21: Populate sender in bridge and transcript ingestion

**Files:**
- Modify: `mcp-bridge/src/application/services/ingest-bridge.ts`
- Modify: `mcp-bridge/src/application/services/ingest-transcript.ts`
- Modify: `mcp-bridge/src/application/services/ingest-git.ts`
- Modify: `mcp-bridge/tests/ingest-bridge.test.ts`
- Modify: `mcp-bridge/tests/ingest-transcript.test.ts`
- Modify: `mcp-bridge/tests/ingest-git.test.ts`

- [ ] **Step 1: Write failing tests — sender populated on ingestion**

In each test file, add assertions that `sender` is set correctly:
- Bridge message: `sender = message.sender`
- Bridge task: `sender = task.assigned_to`
- Transcript: `sender = record.type` (e.g., "human", "assistant")
- Git: `sender = commit.author`

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd mcp-bridge && npx vitest run tests/ingest-bridge.test.ts tests/ingest-transcript.test.ts tests/ingest-git.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: FAIL — sender assertions fail (currently null)

- [ ] **Step 3: Update each ingestion service to pass sender to insertNode**

In `ingest-bridge.ts`: pass `sender: message.sender` to `insertNode` for message nodes, `sender: task.assigned_to` for task nodes.
In `ingest-transcript.ts`: pass `sender: record.type === "human" ? "human" : "assistant"` to `insertNode`.
In `ingest-git.ts`: pass `sender: commit.author` to `insertNode`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd mcp-bridge && npx vitest run tests/ingest-bridge.test.ts tests/ingest-transcript.test.ts tests/ingest-git.test.ts --reporter=verbose 2>&1 | tail -20`
Expected: All PASS

- [ ] **Step 5: Run full suite**

Run: `cd mcp-bridge && npx vitest run --reporter=verbose 2>&1 | tail -10`
Expected: All PASS

- [ ] **Step 6: Commit**

```bash
git add mcp-bridge/src/application/services/ingest-bridge.ts mcp-bridge/src/application/services/ingest-transcript.ts mcp-bridge/src/application/services/ingest-git.ts mcp-bridge/tests/ingest-bridge.test.ts mcp-bridge/tests/ingest-transcript.test.ts mcp-bridge/tests/ingest-git.test.ts
git commit -m "feat: populate sender column in all ingestion paths"
```

---

## Phase 7: Documentation & Cleanup

### Task 22: Update API contract documentation

**Files:**
- Modify: `planning/API_CONTRACT.md`

- [ ] **Step 1: Add documentation for new endpoints**

Document:
- `GET /memory/traversals` — query params, response shape, examples
- `GET /memory/traversals/:id` — params, response shape
- `POST /memory/node/:id/expand` — params, response shape, behavior notes
- `POST /memory/ingest` — updated body schema with all source types
- `ingest_conversation` MCP tool — parameters, description

Also document the updated schemas: `sender` field on `NodeResponse`, `node_ids` on `ContextSection`, `agent` param on traverse/context endpoints.

- [ ] **Step 2: Commit**

```bash
git add planning/API_CONTRACT.md
git commit -m "docs: update API contract with new endpoints and schema changes"
```

---

### Task 23: Final integration test and coverage check

- [ ] **Step 1: Run full backend test suite**

Run: `cd mcp-bridge && npx vitest run --reporter=verbose 2>&1 | tail -30`
Expected: All tests PASS

- [ ] **Step 2: Run typecheck**

Run: `cd mcp-bridge && npx tsc --noEmit 2>&1 | tail -20`
Expected: Zero errors

- [ ] **Step 3: Build UI**

Run: `cd ui && npm run build 2>&1 | tail -20`
Expected: Build succeeds

- [ ] **Step 4: Run coverage audit (if available)**

Run: `cd mcp-bridge && npx vitest run --coverage 2>&1 | tail -30`
Check for any files below 80%.

- [ ] **Step 5: Start servers and smoke test**

Run: `cd /Users/thor/repos/agentic-workflow/.claude/worktrees/issue-7-dag-visualization && ./start.sh`
Verify: Bridge on :3100, UI on :3000, nav bar visible, Memory Explorer loads, Conversation Graph tab works.
