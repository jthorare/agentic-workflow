# Prism Specialization Split

**Date:** 2026-03-27
**Status:** Approved
**Branch:** feat/prism-specialization-split

## Summary

Delegate persistent memory, semantic search, and conversation visualization to [prism-mcp](https://github.com/dcostenco/prism-mcp). Strip the agentic-bridge down to its core purpose: a coordination bus for multi-agent dialogue (message passing, task assignment, status reporting).

## Motivation

The agentic-bridge grew two responsibilities: coordination (5 tools) and memory (6 tools + ingestion pipeline + graph DB + embedding model + UI). prism-mcp does memory better (versioned snapshots, importance-weighted corrections, visual Mind Palace dashboard, 10× embedding compression) and is actively maintained as a dedicated product. Keeping our own memory layer is redundant maintenance burden.

The 5 coordination tools have no equivalent in prism-mcp. They remain ours.

## Architecture After

```
Claude Code ──► agentic-bridge (MCP) ──► Codex / other agents
                │
                ├─ send_context      (point-to-point message)
                ├─ get_messages      (conversation history)
                ├─ get_unread        (pull unread, mark read)
                ├─ assign_task       (structured task delegation)
                └─ report_status     (completion / feedback)

Claude Code ──► prism-mcp (MCP) ──► shared memory graph
                │
                ├─ session_load_context  (restore state on start)
                ├─ session_save_handoff  (persist state on end)
                ├─ knowledge_search      (semantic + FTS)
                ├─ agent_register        (Hivemind roster)
                └─ … (30+ tools)
```

The bridge is a **headless Fastify + MCP stdio server**. No UI. No memory DB. No embedding model.

## What Gets Removed

### mcp-bridge/src/

| Path | Reason |
|------|--------|
| `ingestion/` (entire dir) | embedding, queue, session-queue, claude-code-watcher, claude-code-parser, transcript-parser, secret-filter |
| `application/services/assemble-context.ts` | memory layer |
| `application/services/search-memory.ts` | memory layer |
| `application/services/traverse-memory.ts` | memory layer |
| `application/services/extract-decisions.ts` | memory layer |
| `application/services/infer-topics.ts` | memory layer |
| `application/services/ingest-bridge.ts` | memory layer |
| `application/services/ingest-claude-code.ts` | memory layer |
| `application/services/ingest-generic.ts` | memory layer |
| `application/services/ingest-git.ts` | memory layer |
| `application/services/ingest-transcript.ts` | memory layer |
| `application/events.ts` | EventBus — no subscribers remain |
| `db/memory-client.ts` | memory DB |
| `db/memory-schema.ts` | memory DB |
| `routes/events.ts` | SSE — no UI consumer |
| `routes/memory.ts` | memory REST API |
| `transport/controllers/memory-controller.ts` | memory layer |
| `transport/schemas/memory-schemas.ts` | memory layer |
| `mcp.ts` | strip 6 memory tools + all memory/embedding imports |
| `index.ts` | remove memoryDb, embedService, secretFilter, backfillBridge, sessionQueue, AsyncQueue, ClaudeCodeWatcher |

### Repo root

| Path | Reason |
|------|--------|
| `ui/` | entire Next.js app — visualization delegated to prism Mind Palace |
| `start.sh` | only existed to start bridge + UI together |
| `config/hooks/bridge-context.sh` | queries memory graph; prism supplies session context via its own hook |

## What Stays

### mcp-bridge/src/

| Layer | Files |
|-------|-------|
| `db/` | schema.ts, client.ts (bridge DB — messages + tasks only) |
| `application/services/` | send-context, get-messages, assign-task, report-status, get-conversations, result |
| `transport/` | types.ts; schemas: common, message-schemas, task-schemas, conversation-schemas; controllers: message, task, conversation |
| `routes/` | messages.ts, tasks.ts, conversations.ts |
| `server.ts`, `index.ts`, `mcp.ts` | slimmed |

### REST endpoints (kept for debuggability + future codex worker)

```
POST   /messages/send
GET    /messages/conversation/:id
GET    /messages/unread/:recipient
POST   /tasks
GET    /tasks/:id
PATCH  /tasks/:id/status
GET    /conversations
GET    /health
```

## setup.sh Changes

Remove:
- Memory DB path configuration
- UI build step (`cd ui && npm install && npm run build`)
- `bridge-context.sh` hook installation

Keep:
- Bridge build (`cd mcp-bridge && npm install && npm run build`)
- `claude mcp add agentic-bridge` registration
- `codex mcp add agentic-bridge` registration (if codex present)
- headroom registration

**Do not add prism-mcp registration** — prism's own setup does that.

## Documentation Updates

| Doc | Change |
|-----|--------|
| `CLAUDE.md` | Remove ui/ from directory structure, remove start.sh, update test baseline |
| `planning/ARCHITECTURE.md` | Remove memory graph, ingestion pipeline, embedding service, dual-DB sections |
| `planning/API_CONTRACT.md` | Remove all `/memory/*` endpoint specs |
| `planning/ERD.md` | Remove memory schema tables (nodes, edges, node_embeddings, traversal_logs, ingestion_cursors) |
| `planning/TESTING.md` | Update test counts, remove memory/ingestion test patterns |
| `planning/DEPENDENCY_GRAPH.md` | Remove sqlite-vec, @huggingface/transformers, ingestion deps |
| `planning/COMPETITIVE_ANALYSIS.md` | Update feature table — memory/graph delegated to prism-mcp |
| `planning/PRODUCT_ROADMAP.md` | Remove/update memory-related roadmap items |
| `planning/LOCAL_DEV.md` | Remove memory DB and UI startup steps |

Historical specs — leave as-is (don't update, don't delete):
- `2026-03-22-dag-visualization-design.md` — superseded by prism Mind Palace
- `2026-03-24-memory-recall-subskill-dispatch-design.md` — superseded
- `2026-03-26-rtk-headroom-bridge-context-design.md` — bridge-context.sh being removed

Active spec — keep and preserve:
- `2026-03-20-autonomous-codex-worker-design.md` — codex worker is the next step after this split

## Test Count Impact

Before: 341 bridge tests (39 files), 67 UI tests
After: ~150 bridge tests (memory + ingestion tests removed), 0 UI tests

CLAUDE.md test baseline must be updated to the actual post-removal count.

## Out of Scope

- Codex worker implementation — valid next step, covered by existing spec
- prism-mcp configuration or customization
- Any new coordination tools
