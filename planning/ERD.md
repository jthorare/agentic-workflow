# Entity-Relationship Diagram -- SQLite stores

Every SQLite store in the toolkit is C#, opened through `Aw.Kernel.Data` (WAL, migrations embedded in the owning package
and never edited once merged, the schema version in `PRAGMA user_version`). The schemas are defined by those
migrations; this file is the map to them. The TypeScript ledger and code-index schemas that used to be drawn here left
with the TypeScript packages at the switch, and no TS state is migrated.

| Store | Path | Owner | Schema and contract |
|-------|------|-------|---------------------|
| Sindri ledger | `$AW_SINDRI_HOME/ledger.db` | `Aw.Sindri.Infrastructure` | `events`, `lease`, `outbox_commands`; Sindri is the only writer, hooks append to spool files instead (`docs/csharp/sindri.md` "State") |
| Sindri views | `$AW_SINDRI_HOME/views.db` | the projector | `projector_state` and one `view_<name>(key, row_json, seq)` table per view; derived, rebuilt from the ledger by `sindri views rebuild` (`docs/csharp/sindri.md`) |
| Temporal dev server | `$AW_SINDRI_HOME/temporal.db` | the pinned `temporal` binary | Not ours; the workflow engine's history (`docs/csharp/sindri-workflow.md`) |
| Code index | per repo, under the Sindri home | `src/CodeIndex` | Symbols, callers and callees; derived data (`docs/csharp/code-index.md`) |
| Context store | `$AW_SINDRI_HOME/context/context.db` | `Aw.Context.Infrastructure` | The graph, the intent layer, repo docs and the record store below; migrations `Store/Migrations/NNNN_*.sql` (spec `docs/superpowers/specs/2026-10-27-sindri-owned-index-design.md` §4) |
| Judge decisions | `$AW_STATE_DIR/judge/decisions.sqlite` | `Aw.Judge.Infrastructure` | `decisions`, `decision_details`, `failures`, `briefs` (`docs/csharp/judge.md` "State") |

`$AW_SINDRI_HOME` defaults to `$AW_STATE_DIR/sindri-app`.

## Context store (`context.db`)

Tables by migration: `source_cursor`, `doc`, `doc_chunk` (0001, the repo-docs snapshot, kept until the repo-docs feed has re-read the docs); `node`, `edge` and their evidence, aliases and the map versions (0002 and later: the graph); `intent_record`, `intent_link`, `intent_decision`, `intent_edge`, `intent_conflict` (0006, the intent layer; `intent_record.key` is the same string as `source_record.key`); `change_stamp` (0008) and `stamp_intent` (0009), the counters the graph and intent caches watch. The record store is migration 0018 (`s4_record_store`):

```mermaid
erDiagram
    source_record ||--o{ record_link : "from_key (cascade)"
    source_record ||--o{ chunk : "record (cascade)"
    chunk ||--o| embedding : "key, space = chunk (by trigger)"
    source_record {
        text key PK "linear:issue/uuid, github:pr/owner/repo/n, repo:repo/path"
        text source "linear, github, slack, repo-docs"
        text kind "doc, issue, pr, project, message, comment, code"
        text repo "profile repo, or null"
        text audience_key "repo:o/r, linear:team, slack:channel, owner:library; null is visible to no session; an owner: record is read only by the owner, never through a link"
        text title "scrubbed; the key when blocked or removed"
        text url "allowed scheme and host, no query or fragment"
        text author "scrubbed display name"
        text state "open, closed, merged, draft, canceled, done"
        text state_raw "the source's own word"
        text created_at
        text updated_at
        text closed_at
        text version "updatedAt, or a git blob id"
        text text_hash "sha-256 of the scrubbed text"
        text ruleset "the scrub ruleset"
        int blocked "0 or 1: text withheld, no chunks"
        text removed_at "tombstone"
        text synced_at
        text health "project health word (migration 0020), or null"
        text due_at "project due day, yyyy-MM-dd (migration 0020), or null"
        text pull_facts "a GitHub PR's JSON {requestedReviewers, headSha, reviews[{login, commit, newCommits}]} (migration 0030), or null"
    }
    source_identity {
        text source PK "linear"
        text user_ref "linear:user/id, what a led-by link points at"
        text name
        text read_at
    }
    record_link {
        text from_key PK
        text to_ref PK "a record key, path:repo/path, or a url"
        text kind PK "parent, blocks, duplicates, relates, closes, attachment, review-of, replies-to, in-project, touches, led-by"
        text to_key "set when to_ref resolves to a stored record"
        text title
    }
    chunk {
        int id PK
        text key UK "record key#seq"
        text record FK
        int seq "unique with record"
        text text "scrubbed"
        int tokens
        text text_hash "what an embedding's hash is compared with"
        int line_start
        int line_end
    }
    embedding {
        int seq PK "autoincrement: the cache reads seq greater than its last"
        text space "chunk or node (unique with key and model)"
        text key "a chunk key, or a graph node key"
        text model
        text text_hash
        int dim
        blob vec "float32 little-endian, L2-normalised"
    }
    store_epoch {
        int id PK "always 1"
        int delete_epoch "moves on any embedding delete"
        int record_stamp "moves once per committed page or embed batch"
    }
```

`chunk_fts` is an FTS5 table over `chunk.text` (external content, `unicode61`), kept by the triggers `chunk_fts_ai`, `chunk_fts_ad` and `chunk_fts_au`. `chunk_ad` and `chunk_au` delete a chunk's vector when the chunk goes or its text changes; `embedding_ad` moves `store_epoch.delete_epoch` on every vector removal. Migration 0020 (`project_fields`) adds `source_record.health` and `due_at`, the `source_identity` table (the owner's user per source, one row each), and resets the `linear` record feed's `source_cursor` row (`repo = 'records'`) to phase `recent` with no cursor, backfill cursor or backfill bound. `source_cursor` gains `phase` and `backfill_cursor`; a source's one position is its row with `repo = '*'`.

Migration 0030 (`s2b_pull_facts`) adds `source_record.pull_facts`: one JSON text on a GitHub PR's row with its requested reviewer logins (people only, never a team), its head sha and, for an open PR, each reviewer's latest reviewed commit with the number of commits the head is ahead of it (`newCommits` is null when a force-push dropped the commit). It is NULL for every other record, for a blocked record and for a tombstone, and `Text` and search don't see it. It is a column and not a table because the facts are one-to-one with the row and always read and written with it. The migration also invalidates GitHub PR rows (version `''`) and resets the `github` record feed's `source_cursor` row (`repo = 'records'`) to phase `recent`, so the next sync re-reads every PR once, the way 0029 did.

Migration 0031 (`s1_review_library`) adds the design-doc library's four tables (PR review page spec 2026-11-04 §5.3). They are written through the generic side-rows seam (`RecordWrite.Side`, one `ISideRowWriter` per source; the design-doc and PR-claim writers are the first) by `IRecordStore.Commit` in the same transaction as the record they hang off, and rewritten when they differ even if the record is unchanged, so no reader sees a doc without its items:

```mermaid
erDiagram
    source_record ||--o| design_doc : "doc_key (cascade)"
    design_doc ||--o{ design_doc_item : "doc_key (cascade)"
    design_doc ||--o{ design_doc_ref : "doc_key (cascade)"
    source_record ||--o{ design_doc_claim : "pr_key (cascade)"
    design_doc {
        text doc_key PK "the doc record's key: repo:repo/path or doc:root/path"
        text root "repo:name, or the library root"
        text scope "root; in the aw root also the repo slug, aw/slug"
        text doc_id "front matter id; null for a doc with errors or a blocked one"
        text kind "spec, plan, ..."
        text status
        text project
        text repos "json array of owner/name"
        text issues "json array"
        int parser_version "DesignDocParser.Version"
        text errors "json array of {code, line, message, fix}"
    }
    design_doc_item {
        text doc_key PK
        text item_id PK "REQ-7, Ph2, TP-1, or a section anchor"
        text item_kind "section, phase, requirement, acceptance, testPlan, task"
        text parent_id
        int line_start
        int line_end
        text text "capped at 4000 characters, from the scrubbed text"
    }
    design_doc_ref {
        text doc_key
        text from_item "null when the ref starts at the doc"
        text to_ref "item id, doc id#item, or doc:root/id#item"
        text kind "verifies, relates, blocks"
    }
    design_doc_claim {
        text pr_key PK "the PR record's key"
        text ref PK "as written in the PR's Covers: line"
    }
```

`design_doc` has `INDEX(scope, doc_id)`, not unique: who owns a shared doc id is decided on read. `design_doc_ref` has `INDEX(to_ref)` and `design_doc_claim` has `INDEX(ref)`. Claims carry no resolved columns: they resolve on read. Records are soft-deleted, so a foreign-key cascade never fires on removal: `Commit` deletes the rows itself in the transaction that sets `removed_at` (cascade covers only `Purge`). A doc record with no `aw-doc` front matter has no `design_doc` row. A doc the scrubber blocks has one, with the error "blocked" and no items. Itemized doc records (kind `doc` of `repo-docs` and `library`) store their scrub ruleset followed by `+aw-doc-p<parser version>`, so the repo-docs feed's per-doc check sees an old parse and re-emits the doc with no new commit. The migration also invalidates GitHub PR rows and resets the `github` record feed, as 0030 did, so every PR body is read for its `Covers:` line once.

The rows are read through `ILibraryStore` (`SqliteLibraryStore`), which joins `design_doc`, `design_doc_item` and `design_doc_claim` to their live `source_record` (`removed_at IS NULL`) and returns the record's `audience_key` beside each row; `IContextLibrary` applies `AudienceScope.Allows` to it, so the audience rule lives in one place. A doc, its items and its refs are read through the doc record's audience; a claim needs its PR record's audience and the audience of the doc it resolves to. A doc that holds `owner:library` (a `library` doc whose `repos:` does not name exactly one synced repo) is visible only to the owner's scope, which always adds that key.
