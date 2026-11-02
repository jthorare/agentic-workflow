# Sindri's own index: one database for records, chunks, vectors and the map (and Onyx goes away)

**Status:** design, revision 2: the owner agreed on 2026-10-27 (answers in §13; Q18 changed the first ingest to a time window, D12) · **Date:** 2026-10-27 · **Owner decision:** 2026-10-27, drop Onyx; build a Sindri-owned index from ConnectAI's code, in the owner's words: "pulling out the code of connectAI but it becoming fully Sindri owned. not just another swap for another brain. a change to how we organize this stuff by making it a singular vectorized db that we can own and create the maps from" · **Builds on:** the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`: D2, D3, §5, §6.2, §7, §8.2), the Onyx managed spec (`2026-10-16-sindri-onyx-managed-design.md`, retired by this one), the code index spec (`2026-10-19-sindri-code-index-design.md`), the whole-map spec (`2026-10-23-sindri-context-whole-map-design.md`), and the map-structure note (`2026-10-27-sindri-context-map-structure-design.md`: #1365, #1372, #1373) · **Lane:** context · **Next:** /specToProvenPR takes §12's phases to PRs.

ConnectAI paths below are relative to its repo root (a private TypeScript monorepo, read at a shallow clone on 2026-10-27; read only, nothing was run and no secret was read or copied). Sindri paths are relative to this repo.

## 0. Why

What is taken from ConnectAI, said plainly so the owner can confirm it is what they meant: its ideas and mechanics for two connectors (GitHub, Slack), its chunk size and overlap, its cursor boundary rule, its hybrid-search shape and its constants. Nothing of its code is copied (§5.1), its data model is not used (§14.4) and 16 of its 18 connectors are not ported. The "fully Sindri owned" part is the store, the ids, the ranking and the map.

Onyx was meant to be a recall engine behind a port, with Sindri doing the ranking (retrieval spec D2, D3, `2026-10-10-sindri-context-retrieval-design.md:64-65`). Four things make that the wrong shape now:

| What | Today | Why it hurts |
|---|---|---|
| Sindri doesn't hold the text | A synced record keeps only its key, version, one-line title and aliases ("A record's text is never stored", `src/Context/Aw.Context.Infrastructure/Store/Migrations/0006_s5b_intent_links.sql:2`; `IntentRecord.cs:11`). Text is fetched live from Onyx or Linear (retrieval spec `:337`) | The map build cannot read it (map-structure note §11.1: "the map build does not see it"), and no embedding of it can join the graph |
| Sync is search-driven, not a feed | `IntentSyncRun` searches each source with terms taken from the map (`src/Context/Aw.Context.Features/SyncSources/IntentSyncRun.cs:134`) and fetches the hits (`:171`). `IChangeFeed` (`Aw.Context.Core/Sources/IChangeFeed.cs:6`) has no implementer | Sindri only ever sees records a term found. A record nobody searched for never arrives |
| Onyx is a second product to run | `context/onyx.lock:61-62`: 11.26 GB of disk and 5.9 GB of Docker memory, plus Docker, an OpenSearch, a Postgres, its own admin account and a personal access token for a dedicated user (`2026-10-16-sindri-onyx-managed-design.md` O7) | A one-user, one-box tool carries a multi-service stack it uses for one HTTP route (`POST /api/admin/search`, `OnyxSource.cs`) |
| Onyx can't be given Sindri's ids | Its hits are Onyx documents; Sindri maps them to keys after the fact (`OnyxRecordKeys.cs:30-55`) | Vectors and graph nodes can't share ids, so "find the records near this feature" is a join Sindri can't do |

**This is not an adapter swap.** ConnectAI is a company-brain server (Postgres, pgvector, a loop, an MCP server). What Sindri takes from it is the part that pulls data out of tools and turns it into searchable chunks: connector mechanics (paging, cursors, rate-limit handling, per-tool field maps), the text cleaner, the chunker, and the idea of hybrid dense plus lexical recall. What Sindri does **not** take is its data model (it deliberately keeps no structured record mirror, §14.4), its multi-tenant plumbing, or its ranking. The index is a part of Sindri's own context database, so the map is built from it and its ids are the graph's ids.

## 1. Decisions

Decisions are D-numbers; phases (§12) are I-numbers.

| # | Decision | Reason |
|---|---|---|
| D1 | **One store: new tables in the existing `$AW_SINDRI_HOME/context/context.db`** (SQLite, WAL, through `Aw.Kernel.Data`): structured source records, chunks (with FTS5), vectors. Not a second file, not a server | The graph is already in this file (`0002_s3a_graph.sql`). A join between a record, its chunks, its vector and its node is one SQL statement and one transaction (§4.1) |
| D2 | **Vectors are float32 blobs in SQLite, searched by an in-process scan** over a RAM copy, not sqlite-vec and not Postgres with pgvector. The scan is behind a port, so an index structure can replace it when a measured number says so (§4.4) | No new native library or server. It is how the code index already stores and reads vectors (`src/CodeIndex/Aw.CodeIndex.Infrastructure/Store/VectorCodec.cs:42`, `.../SqliteIndexReader.Clones.cs:104`) |
| D3 | **The text we store is scrubbed text**, under the profile's ruleset, with the ruleset recorded per record (the repo-docs pattern, `0003_s2w_scrub_ruleset.sql`) | Retrieval spec §7 "Stored text". This **reverses** "record text is fetched live, never stored" (`:337`) and needs the owner's yes (Q7) |
| D4 | **Connectors are a new tier-1 package, `Connectors`**, a port of ConnectAI's connector mechanics in idiomatic C#. First: GitHub (issues and pull requests) and Slack. Linear stays where it is (the existing native adapter becomes a feed) | §5.1 to §5.3. GitHub issues and Slack reach Sindri today only through Onyx (`docs/sindri/onyx.md:177,183-186`) |
| D5 | **Embeddings come from a new tier-0 package, `Embeddings`**: the loopback Ollama client, the vector codec and the scan. The model stays the code index's pinned `nomic-embed-text` (`context/code-index.lock`) | One model for code and records, nothing new to install (the model is already pulled by `scripts/install-deps.sh:155-195`) |
| D6 | **Ranking is Sindri's and uses the graph.** Search is two legs over the store (dense, lexical) plus the graph legs that exist today, fused by reciprocal rank fusion, then the feature boost. One new graph signal (reach from the top hits) and two record signals (state, freshness) are added to the five that exist (§6) | The five features are `RankFeatures.cs:12-55`; fusion is `RankFusion.cs:13` and `FeatureRanker.cs:41` |
| D7 | **The map builds from the store.** Doc records, cited paths, record age and the tracker half of #1373 read the store, not `IDocIndex` and not the read-time overlay alone (§7) | Closes map-structure note §11.1 at the root |
| D8 | **Credentials stay Sindri's own pattern**: a secret pointer in the profile (`env:`, `file:`, `keychain:`, `op:`), a read-only token the owner makes, scopes limited to the profile's repos, teams and channels. ConnectAI's vault, GitHub App private key and service-account approach are not copied (§5.4) | `LinearSettings.cs:17`, `ISecretPointers.cs:10`. This reverses a line of the retrieval spec (below), so the reason is the owner's decision of 2026-10-27, not that line |
| D9 | **Onyx is removed** in this order: the new index serves and backtests at parity, then the `sindri onyx` verbs, the Onyx adapter, the pins, the setup step and the docs go in a sequence of small PRs (§8) | The backtest arms still read Onyx (`Aw.Context.Infrastructure/Eval/OnyxBriefSource.cs`) |
| D10 | **No Docker for the index.** Docker stays on the box for Serena only (`setup.sh:268-290`) | The owner's goal: nothing to start, nothing to log in to |
| D11 | **No model call on ingest**: no summary, fact, entity or procedure extraction. ConnectAI makes up to five per item; Sindri makes none (§5.7) | Box cost, egress, and the graph's own linker already does the useful part |
| D12 | **Ingest is bounded by time: `context.index.windowDays`, default 183 (six months back)**, as ConnectAI bounds its cold start (`COBRAIN_COLD_START_WINDOW_DAYS`, `phases.ts:40`). Only records updated inside the window are synced and embedded; nothing older is ever fetched. `context.index.maxChunks` stays only as a safety ceiling (§5.5, §5.6) | Owner, 2026-10-27 (Q18): "it shouldn't take hours for first embedding, it should be time driven like 6 months in the past as a config option" |

**What this spec reverses** (each is an owner decision, Q21; nothing is reversed silently):

| Earlier text | Said | Now |
|---|---|---|
| Retrieval spec D2 (`:64`) | Onyx is the business-intent search engine, "Not ConnectAI" | Sindri's own store is, built from ConnectAI's ideas |
| Retrieval spec D3 (`:65`) | Onyx for recall only, Sindri owns ranking | Sindri owns recall and ranking |
| Retrieval spec §3, "Company OAuth" (`:85`) | The owner connects Onyx to company Linear and Slack with sign-off; Sindri never connects a business source itself | Sindri connects GitHub and Slack (Linear already) with read-only tokens the owner makes, same sign-off |
| Retrieval spec `:337` | Record text is fetched live, never stored | Scrubbed text is stored (Q7) |
| Whole-map decision of 2026-10-23 (Onyx GitHub issues search-only, no adapter) | no GitHub-issue adapter | GitHub issues and PRs are records (Q3) |

## 2. Goals and non-goals

Goals: the owner connects GitHub, Slack and Linear with read-only tokens and runs `sindri index sync`; every record in scope arrives, with its state, links and attachments; a query returns it by exact words at once and by meaning once its vectors exist, ranked with the graph; the map build reads the same rows the search does; no Docker stack and no server run for the index. The one other process is the loopback Ollama the code index already needs; with it down, words-and-graph search still answers (§5.8).

**First usable milestone** (the thing the owner can try before any vector exists): the store, the Linear and GitHub feeds and the lexical leg: phases I1, I2, I3 and I4a, about 19 PRs (§12). The map reading the store (S7) follows it, in parallel from the day I1's port merges. Vectors, Slack, the new ranking signals and Onyx's removal follow and are each gated on a number (§12).

Non-goals:
- No multi-tenant anything: one owner, one box, one `context.db` (README X61 keeps "shared later, not foreclosed": a `Shared` source placement stays in the contract, `SourcePlacement.cs`).
- No model call on ingest. ConnectAI also writes a derived summary line per record and embeds it beside the raw windows (`apps/api/src/application/services/ingest/chunk-synthesize.ts:1-23`: a derived path and a raw path); Sindri embeds the scrubbed source text only. Model egress stays behind the profile switches of retrieval spec §3.
- No write-back to any tool (ConnectAI's `act()`, `packages/connectors-core/src/types.ts:310-336`, is not ported).
- No history of a record's versions. The store keeps the latest version and the times the source gives (created, updated, closed).
- No change to graphify, to the map-structure steps 3 to 5 (S3, S4, S5), or to the code index's own files (§7.4).
- No rewrite of ConnectAI. It keeps running as it is; nothing here changes it.

## 3. Terms

- **Record:** one thing a source holds: a Linear issue, a GitHub pull request, a Slack thread, a repo doc. It has a **record key** (the existing `RecordKey`, `Aw.Context.Core/Sources/RecordKey.cs:12`), such as `linear:issue/<uuid>` or `repo:<repo>/<path>`.
- **Chunk:** a window of a record's scrubbed text, about 400 tokens, with a small overlap. Its key is `<record key>#<seq>`.
- **Vector:** the embedding of a chunk (or of a graph node), stored under the same key as what it embeds.
- **Source:** where records come from (`linear`, `github`, `slack`, `repo-docs`). A **connector** is the code that reads one.
- **Feed:** a connector's way to hand over what changed since a cursor, a page at a time.
- **Leg:** one ranked list in a query (dense, lexical, graph, code).
- **Seed:** one of the top hits of the dense and lexical legs, used as a starting point for the graph leg.

## 4. The store

### 4.1 Where, and why this engine

Measured on the dev box for this spec: 16 cores, 31 GB RAM, 73 GB free disk (`nproc`, `free -g`, `df -h`, 2026-10-27). Cited for the rest.

| Option | Needs | Verdict |
|---|---|---|
| **A. SQLite + FTS5 + float32 blobs, scanned in process** | Nothing new. `Microsoft.Data.Sqlite` with its bundled `e_sqlite3` is already the engine (`Directory.Packages.props:36-38`; AGENTS.md Tech Stack "Database"). FTS5 is already proven in the bundled build (`0001_s1a_repo_docs.sql`) | **Chosen** |
| B. SQLite + the sqlite-vec loadable extension | A native library per OS and CPU (four builds) shipped beside `e_sqlite3`, pinned and verified like the other binaries; extension loading enabled on the connection; AOT packaging to prove. As I read it, sqlite-vec is also a full scan with SIMD, so it is not a different algorithm, only faster code (verify in phase I0) | Held back: adopt only if A's measured p95 misses its budget (§4.4) |
| C. Postgres + pgvector (ConnectAI's engine: `packages/database/prisma/manual-indexes.sql:25,34-35`, HNSW on 1,024-dimension cosine; image `pgvector/pgvector:pg16`, `README.md:179`) | A server process and a second engine beside the SQLite stores (`planning/ERD.md` is all SQLite); Docker or a native install; a graph in another file from its vectors. Cost at our size: the server idles at a few hundred MB (the image alone is several hundred MB; the Onyx stack that includes a Postgres is 5.9 GB of Docker memory and 11.26 GB of disk, `context/onyx.lock:61-62`), HNSW adds an in-memory-resident graph of about 1.5 to 2 times the vector bytes (≈ 300 MB at 50,000 chunks of 768 floats; my estimate from pgvector's published sizing, not measured here), and an HNSW build over 250,000 vectors is minutes of CPU | Rejected: it brings back what we are removing, and the join with the graph is gone |
| D. LanceDB or another embedded vector store | A new native dependency and file format; the factory addendum lists it as an option (`2026-10-09-sindri-factory-addendum.md:2135`) | Rejected for now: one more thing to pin, and no number says A is too slow |

**Where:** the same `context.db`, not a new file. One transaction can write a record, its chunks and its vector rows; one query can join `source_record` to `node` through the key (§4.3). The cost is a bigger file with big blob rows beside small graph rows; WAL checkpoints and `VACUUM` run in the existing purge service. The code index's per-repo immutable files stay as they are (they are derived data published by pointer, code index spec §4.1); the store is the one place for everything else.

### 4.2 Schema (migration M-add, `planning/ERD.md` gets the same tables)

```sql
-- One row per record. The structured facts a tracker gives: not only text.
CREATE TABLE source_record (
    key         TEXT PRIMARY KEY NOT NULL,        -- RecordKey: linear:issue/<uuid> | github:pr/<owner>/<repo>/<n> | github:issue/... | slack:<channel>/<ts> | repo:<repo>/<path>
    source      TEXT NOT NULL,                    -- linear | github | slack | repo-docs
    kind        TEXT NOT NULL,                    -- doc | issue | pr | project | message | ...; validated in C# (an enum and a contract test), not a CHECK, so a new source adds no table rebuild
    repo        TEXT NULL,                        -- the profile repo it belongs to, when it has one. One GitHub host per profile in v1 (github.com or one Enterprise host), so the key needs no host; a second host would need one
    audience_key TEXT NULL,                       -- who may see it, set at ingest: `repo:<owner>/<repo>` | `linear:<teamId>` | `slack:<channelId>`; resolved against the approved profile (§4.4); re-set when a record moves team. NULL = visible to no session
    title       TEXT NOT NULL,                    -- scrubbed, one line
    url         TEXT NULL,                        -- only an allowed scheme and host (Permalinks)
    author      TEXT NULL,                        -- scrubbed
    state       TEXT NULL CHECK (state IS NULL OR state IN ('open','closed','merged','draft','canceled','done')),
    state_raw   TEXT NULL,                        -- the source's own word ("In Review"), scrubbed
    created_at  TEXT NULL, updated_at TEXT NULL, closed_at TEXT NULL,
    version     TEXT NOT NULL,                    -- the source's version: updatedAt, or a git blob id for a doc
    text_hash   TEXT NOT NULL,                    -- sha-256 of the scrubbed full text; a vector is recomputed only when it changes
    ruleset     TEXT NOT NULL,                    -- the scrub ruleset it was cleaned with
    blocked     INTEGER NOT NULL CHECK (blocked IN (0,1)),   -- the scrubber withheld the text: the row stays, it has no chunks
    removed_at  TEXT NULL,                        -- the source deleted it: a tombstone, so links to it stay readable
    synced_at   TEXT NOT NULL
) STRICT;
CREATE INDEX source_record_by_source ON source_record (source, updated_at);
CREATE INDEX source_record_by_state ON source_record (repo, state, updated_at);   -- the state and freshness signals and the scope mask (§4.4)

-- What the source itself says connects two records: a pull request that closes an issue, an issue's parent, a Linear attachment.
CREATE TABLE record_link (
    from_key   TEXT NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    to_ref     TEXT NOT NULL,                     -- the other record's key when known; `path:<repo>/<path>` for a file a PR touched (resolved at map build, §7.5); else the url as the source gave it (scrubbed)
    to_key     TEXT NULL,                         -- filled when to_ref resolves to a stored record
    kind       TEXT NOT NULL,                     -- parent | blocks | duplicates | relates | closes | attachment | review-of | replies-to | in-project | touches; validated in C#
    title      TEXT NULL,
    PRIMARY KEY (from_key, to_ref, kind)
) STRICT;

CREATE TABLE chunk (
    id         INTEGER PRIMARY KEY,
    key        TEXT NOT NULL UNIQUE,              -- <record key>#<seq>
    record     TEXT NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    seq        INTEGER NOT NULL,
    text       TEXT NOT NULL,                     -- scrubbed
    tokens     INTEGER NOT NULL,
    text_hash  TEXT NOT NULL,                     -- sha-256 of `text`: what an embedding's text_hash is compared with
    line_start INTEGER NULL, line_end INTEGER NULL,  -- docs keep their line span (as doc_chunk does)
    UNIQUE (record, seq)
) STRICT;
CREATE INDEX chunk_by_record ON chunk (record, seq);
CREATE VIRTUAL TABLE chunk_fts USING fts5 (text, content = 'chunk', content_rowid = 'id', tokenize = 'unicode61');
-- Three triggers, named chunk_fts_ai, chunk_fts_ad and chunk_fts_au, keep the index in step (`doc_chunk` has two, insert and delete, because docs are deleted and re-inserted; `chunk` is rewritten in place when a record is re-chunked or its text is withheld, so it needs the update one too):
--   AFTER INSERT                       INSERT INTO chunk_fts (rowid, text) VALUES (new.id, new.text)
--   AFTER DELETE                       INSERT INTO chunk_fts (chunk_fts, rowid, text) VALUES ('delete', old.id, old.text)
--   AFTER UPDATE OF text               the 'delete' row for old, then the insert for new
-- A query joins source_record and keeps `ruleset = @ruleset AND blocked = 0 AND removed_at IS NULL` (the shape of DocSql.Search, DocSql.cs:35-41).

-- One vector per thing embedded, under the same key as the thing.
CREATE TABLE embedding (
    seq       INTEGER PRIMARY KEY AUTOINCREMENT,  -- monotonic: the cache loads `WHERE seq > :last` (§4.4)
    space     TEXT NOT NULL,                      -- 'chunk' | 'node' (| 'summary' later, §5.7)
    key       TEXT NOT NULL,                      -- a chunk key, or a graph NodeKey
    model     TEXT NOT NULL,                      -- name and digest from the pin
    text_hash TEXT NOT NULL,                      -- of what was embedded; a row with another hash is stale
    dim       INTEGER NOT NULL,
    vec       BLOB NOT NULL,                      -- float32 little-endian, L2-normalised (so cosine is a dot product)
    UNIQUE (space, key, model)
) STRICT;     -- a rowid table: WITHOUT ROWID is for rows under a few hundred bytes, and these are ~3 KB
-- No foreign key (the key is polymorphic), so triggers do the cascade:
--   CREATE TRIGGER chunk_ad AFTER DELETE ON chunk BEGIN
--     DELETE FROM embedding WHERE space = 'chunk' AND key = old.key; END;
--   CREATE TRIGGER chunk_au AFTER UPDATE OF text ON chunk BEGIN        -- the vector of the old text is stale
--     DELETE FROM embedding WHERE space = 'chunk' AND key = old.key; END;
--   CREATE TRIGGER embedding_ad AFTER DELETE ON embedding BEGIN         -- every vector removal, from any cause (a chunk trigger, a re-embed, a retired node), moves the epoch
--     UPDATE store_epoch SET delete_epoch = delete_epoch + 1 WHERE id = 1; END;
-- The embed job writes a vector only as `INSERT ... SELECT ... WHERE EXISTS (SELECT 1 FROM chunk WHERE key = :key AND text_hash = :hash)`, so a batch that
-- finishes after its chunk was re-chunked or deleted inserts nothing (no orphans, no stale vector). A re-embed is a DELETE then an INSERT, so it gets a new seq.
-- Node vectors are deleted by the map build that retires the node (the same trigger fires). An integration test asserts zero orphans after a purge, a tombstone and a re-chunk.
CREATE TABLE store_epoch (id INTEGER PRIMARY KEY CHECK (id = 1), delete_epoch INTEGER NOT NULL, record_stamp INTEGER NOT NULL) STRICT;
INSERT INTO store_epoch VALUES (1, 0, 0);   -- one row; record_stamp is bumped (WHERE id = 1) once per committed page or embed batch

-- source_cursor (0001) gains: phase TEXT NULL, backfill_cursor TEXT NULL.   (No lease column: SyncLocks and one serve host already give single flight.)
-- A new counter, record_stamp, is bumped once per committed page and once per committed embed batch; the caches in §4.4 watch it (not change_stamp, which every graph write moves).
```

How the old tables map: a `doc` becomes a `source_record` of kind `doc` (`blob` becomes `version`; `title`, `blocked`, `ruleset` carry over); `doc_chunk` becomes `chunk` (line spans carry over). `planning/ERD.md` has no `context.db` section today (it covers the Sindri ledger and the code index), so it gains its first one, covering these tables and the existing `doc`, `node`, `intent_*` and `change_stamp` tables. Migration numbers are "the next free number at merge": `ctx-1365-s5` already carries a `0016_s5_map_checks.sql` and main's last is `0015`, so this spec's migrations are written as **M-add** (creates the tables, copies nothing) and **M-drop** (drops `doc` and `doc_chunk`) and rebase onto whatever number is free. There is no SQL copy of `doc`/`doc_chunk`: `text_hash` has no SQLite function to compute it and `tokens` is not stored today. Instead the repo-docs feed (the one writer from phase I1) re-reads the docs on its first run, which is cheap (git blobs) and fills the real hashes and token counts; until it has run, search falls back to the old tables, which stay until M-drop, a release later. Docs have no vectors until the embed job (§5.6) fills them. `intent_record` and its links, decisions and conflicts (`0006`) stay exactly as they are; its `key` is the same string as `source_record.key`. It keeps the linker's bookkeeping (`extraction_state`, `linked_map`). Its `title`, `version` and `aliases` repeat what `source_record` holds; folding them is a later clean-up (Q13).

### 4.3 Ids shared with the graph

One rule: **the graph node of a record is `record:` plus its record key** (`IntentOverlay.cs:123`, `RecordNode`; repo docs already follow it, `RepoDocs.Key`, `RepoDocs.cs:17`). A chunk's key is `<record key>#<seq>`; its vector row has that key. A graph node's vector (`space = 'node'`, §7.3) has the node's own key (`feature:...`, `concept:...`) and is rebuilt when a map version is promoted, not carried across versions. A code node is `code:<path>` and is not stored in `source_record` (code text is read from git at the sha, retrieval spec `:337`); its vectors stay in the code index (§7.4).

So from a chunk to its node: cut at the **last** `#` (a record key may itself contain `#`, e.g. a doc path `repo:x/a#b.md`; `seq` must be numeric or the key is not a chunk key), prepend `record:`. From a node to its chunks: strip `record:`, `WHERE record = ?`. No lookup table, no second id space.

### 4.4 Search cost and the budget

Vectors are L2-normalised on write, so a query is one dot product per stored vector. The existing code index loops in `double` with three accumulators per pair (`VectorCodec.cs:42-50`); the store uses a `float` loop over `System.Numerics.Vector<float>` (BCL, NativeAOT-safe) on a RAM copy of the matrix, loaded once by `serve`. It is **append-only**: the cache remembers the highest `embedding.seq` it has read and each refresh loads only `WHERE seq > last`; it rebuilds fully only on a delete (purge, tombstone, re-chunk), a ruleset change or a model change. It watches `store_epoch` (§4.2): `record_stamp` moves once per committed page or embed batch (append), `delete_epoch` moves on any chunk delete or text change (full reload). So an embed job that commits every 4,096 chunks for hours does not invalidate the graph's caches and does not re-read the whole matrix, and a purged or tombstoned vector leaves RAM at the next query, not the next restart. The matrix is memory-only and is never written to disk. (`StampedCache`, `Store/StampedCache.cs`, reloads whole values on any stamp move; it is the pattern for the stamp, not for this cache.) Memory is the limit, not arithmetic. V1 keeps float32 only; the int8 tier and an approximate index are what the budget below names as the next step, not v1 work. `serve`'s resident size today is not measured in this spec; phase I0 prints it beside the matrix size so the owner can see the delta.

Matrix sizes are arithmetic (n chunks times 768 floats times 4 bytes). The scan times are **measured** (phase I0, 2026-10-27): a throwaway harness on this box (Ubuntu 24.04, 4 cores, `Vector<float>` with 8 lanes), a random float32 matrix in RAM, one full scan plus a top-10 per query, 200 queries per size, one query at a time. The corpus size is still an assumption that I0's remaining half replaces with the owner's real counts (printed from the sources' list endpoints):

| Chunks (assumed corpus) | Vector matrix, float32 | One scan plus top-10, p95 (measured unless noted) | Store file on disk (vector 3 KB + text about 2 KB + FTS about 1.5 KB per chunk) |
|---|---|---|---|
| 25,000 (the six-month window, D12) | 77 MB | about 18 ms (half of the 50,000 row, not run) | about 0.15 GB |
| 50,000 (three Linear projects, a year of PRs) | 146 MB | 36 ms (p50 20, max 47; the scalar loop: p95 85) | about 0.3 GB |
| 150,000 | 440 MB | 141 ms (p50 68, max 190) | about 0.9 GB |
| 250,000 (plus Slack threads) | 732 MB | 235 ms (p50 115, max 306): over the budget | about 1.6 GB |
| 1,000,000 | 2.9 GB | not run; about 940 ms at the measured rate | about 6.5 GB |

The measured rate is about 6.7 GB/s of matrix per second (7.6 GB/s at 50,000 rows), not the 10 to 20 GB/s this section assumed before I0; the scan is memory-bound on this box. Peak working set of the harness was 1.5 GB.

**Budget:** p95 of the dense leg's scan and merge under 150 ms at the owner's real size on this box. The query embedding (tens of milliseconds to a second on CPU) is measured and reported beside it, not inside it. I0 measured the budget as holding to about 150,000 chunks in one thread (141 ms p95); the six-month window (about 25,000) is about 18 ms. Past 150,000 the fix order is the one below: first a parallel scan over the cores, then int8, then an index structure. Up to about 100,000 chunks the matrix is kept as float32 in RAM (307 MB); with the default cap of 60,000 chunks (Q18) the larger tiers apply only if the owner raises the cap. Above that, the cache holds int8 vectors (a scale per vector, a quarter of the memory) and the top 200 are re-scored exactly from the float32 rows. Past 1,000,000, or when the measured p95 misses the budget, option B or an approximate index goes behind the same `IVectorScan` port; no other code changes. Where each step starts is now a measured number (above): float32 in one thread to about 150,000 chunks (Q2).

The scope rule (retrieval spec §3, "Repo scope") applies **inside** the scan: the caller's allowed record set is a mask over the matrix before the top 50 are taken, never a filter after it, or a session whose repo owns few records would get an empty leg. How the mask is built (it decides what leaks, §10):

- The matrix carries a parallel array of record ids, loaded with it. The mask is computed from `source_record` (`repo`, `audience_key`, `source`, `ruleset`, `removed_at`, `blocked`) and the approved profile, per scope, cached by `(record_stamp, delete_epoch, approved-profile hash)`, so re-approving a profile that narrows an audience takes effect at once.
- **Sessions keep the existing rule (retrieval spec §3, "Repo scope", owner decision 2026-10-15: B), applied to a bigger set of records.** A session sees a record only if it is (a) a doc or GitHub record of its own repo, (b) a Linear record of a team the profile maps to that repo (`context.repoTeams`, which the retrieval spec describes and the code does not yet have: `grep repoTeams src` is empty, so until it is built (b) is empty), or (c) a record linked to graph nodes its repo implements, or a decision quoting one (rule B). Every other record answers as an unknown anchor (`SND-CONTEXT-003`) and is never hinted at. A Slack thread therefore reaches a session only through (c). `ScopeRules` (`Aw.Context.Core/Engine/ScopeRules.cs`) still decides which repo a session names and `context.sessions.enabled` still gates every leg. A human caller is not scoped. Retrieval spec §8.2 (and the whole-map line `:160`, "`ScopeRules.MayRead` denies every `Intent` source to a session", which q93 on 2026-10-23 amended: sessions now search the intent sources) are unchanged by this spec; the change is that the search now runs over the store.
- **Audience is computed from `audience_key` and the approved profile, never from raw link-graph state alone.** `audience_key` (§4.2) is `repo:<owner>/<repo>`, `linear:<teamId>` or `slack:<channelId>`; rule (c) uses the links in the approved overlay at the stamp the mask was built. A record with no match is visible to no session (default deny); it still counts in `sindri index status`.
- The mask applies to every leg: dense, lexical, graph (anchors) and the seed expansion (§6.1), so a hit's neighbours are masked too, and an out-of-scope hit gives the same empty answer as no hit (the behaviour of `SND-CONTEXT-045`).

### 4.5 What is stored, and what stays out

Stored: scrubbed text, titles, authors, urls (allowed hosts only), states, times, the links a source states. Not stored: unscrubbed text (a record the scrubber blocks keeps its row, `blocked = 1`, and no chunks), attachments' contents (their title and url only), comments' author handles beyond the scrubbed display name, direct messages (never read: the Slack allowlist names channels, §5.4), tokens (never, §5.4).

The file is `0600` in a `0700` directory, as `context.db` is now (retrieval spec §3 "State"), and so are `context.db-wal` and `context.db-shm` (the process runs with umask 077; `sindri doctor` checks all three files and every parent directory); full-disk encryption is assumed, as there.

**A tombstone removes the text.** `removed_at` keeps the row (key, title, url, state) so links to it stay readable, and deletes its chunks, their FTS rows and their vectors in the same transaction. The database runs with `PRAGMA secure_delete = ON`; a purge or a reconcile that deleted anything ends with an FTS5 `optimize`, a WAL truncate checkpoint, and an invalidation of the in-memory matrix. A profile key `context.index.retainDays` (default none; `slack` can set its own) makes the daily reconcile tombstone older records.

**What a purge or tombstone of a source removes, as a list:** `source_record` text columns (to key, kind, state, times), `chunk` and `chunk_fts`, `embedding`, the `to_key` and `title` of inbound `record_link` rows in other records, `intent_record`, `intent_link` and `intent_edge` rows for the keys, the matching graph nodes (the map is marked for rebuild), and the in-memory matrix. After any transaction that removed chunks (a purge, a tombstone, the ruleset job, a re-chunk), SQLite runs an FTS5 `optimize` and a TRUNCATE checkpoint, because `secure_delete` zeroes freed pages but old FTS5 segments persist until merged and the WAL keeps old page images. `secure_delete` is a per-connection pragma: it is set in the context database's connection options, not in the shared connection factory. A heavy test plants a string, removes its record, and scans the raw `.db` and `-wal` bytes for it. Authors are stored as the scrubbed display name only.

**One rule for every stored string.** Title, author, text, `state_raw`, `source_record.url` (query string and fragment stripped), `record_link.title`, `record_link.to_ref` (a url cut down as below; `path:` forms are checked against the path-character set) and any `key` part that comes from free text (a Slack channel name) pass the scrubber or the url cut-down before they are hashed or chunked. A `blocked` row or a tombstone keeps only key, kind, state and times: its title becomes the key and its outgoing `record_link` titles and `to_ref` are nulled. A column-by-column test plants a secret in each column.

**The scrubber fails closed.** An exception or a timeout inside it means `blocked = 1`, no text and the title replaced by the key, never raw text stored. Slack and GitHub comments are free text from many people, so each connector has a regression corpus of planted secrets (Slack `xoxb-` and `xoxp-` tokens, GitHub tokens, keys in URLs, JWTs) that must come out redacted or blocked. When the ruleset changes, rows under the old ruleset lose their chunks and vectors at once in a job that runs before the next pull, rather than waiting to be skipped.

**Links in text are cut down.** A `record_link.to_ref` that is a url is kept only when its host is the source's own or a listed docs host, with query string and fragment removed; any other url is reduced to its host or dropped (pre-signed and login links carry secrets in the query). When the profile's scrub patterns change, the ruleset id changes, every record under the old one is skipped by search (as docs are, `0003`) and marked for a re-pull; the next sync fetches it again, scrubs it, and re-embeds only when `text_hash` changed.

## 5. Sources, connectors and sync

### 5.1 What ConnectAI has, and what we do with each

ConnectAI's 18 connector directories are in `packages/connections/src/connectors/` (its README lists 17 (`README.md`, "Connectors"), plus `paperclip`, an agent-history source).

Confidence: **code** = the transport and connector files were read for this spec or the ideas lane's inventory; **spec** = only the connector's `SPEC.md` and header comments were read (inventory `connectai-inventory.md` §2.1 note: "treat their field lists as spec-level, not verified in code"). Lines are transport, server, connector (`wc -l`, 2026-10-27).

| # | Connector | What it reads | Lines | Confidence | Decision |
|---|---|---|---|---|---|
| 1 | **GitHub** | Issues and pull requests: state, label and comment **counts**, review counts, change size (`github-connector.ts:41-55`); `/issues` with `since`, `/pulls` filtered locally (`github/transport.ts:473-522`). No comment text, review text, diffs or links | 618, 231, 201 | code | **Port first.** No Sindri adapter exists and it is where the owner's issues and PRs live |
| 2 | **Slack** | Channel history, each thread root expanded through `conversations.replies`, a permalink per thread through `chat.getPermalink`; per-channel `ts` watermark (`slack/transport.ts:7-13,110-153`; `types.ts:219-228`) | 304, 147, 210 | code for the history and replies loop and the rate-limit note; spec for the rest | **Port second.** Reaches Sindri only through Onyx today (`docs/sindri/onyx.md:177`). Watch the rate limit in §5.3 |
| 3 | Linear | Issues and comments, `orderBy: updatedAt` (`linear/transport.ts:28-36`), state `{id,type}`, no relations or attachments | 420, 253, 229 | code | **Not ported.** Sindri's native adapter reads more (projects, documents, updates, attachments, relations: `Aw.Context.Infrastructure/Linear/Operations/*.graphql`). It becomes a feed (§5.2) |
| 4 | Jira | Issues via JQL, ADF flattened to text, every comment (an extra call per issue), parent and epic links (`jira/transport.ts:264-267`) | 524, 197, 215 | code | Out (owner, Q4: GitHub, Slack and Linear only) |
| 5 | Notion | Pages and database rows, block text | 266, 140, 211 | spec | Out (owner, Q4: GitHub, Slack and Linear only) |
| 6 | Granola | Meeting notes: summary and transcript | `granola/*` | spec | Later; transcripts are long, so the chunker matters more than the connector |
| 7 | Zoom | Cloud-recording transcripts (VTT) | `zoom/*` | spec | Later, same |
| 8 | Gmail | Mail threads | `gmail/*` | spec | **Not ported.** Business mail is not product context |
| 9 | Google Calendar | Events | `calendar/*` | spec | **Not ported** |
| 10 | Microsoft 365 | Mail, calendar, OneDrive, Teams | `microsoft-365/*` | spec | **Not ported.** Its tenant-wide "sovereign" mode (`oauth/SOVEREIGN.md:1-20`) is what we do not copy (§5.4) |
| 11 | Calendly | Events and invitees | `calendly/*` | spec | **Not ported** |
| 12 | Discord | Channel messages | `discord/*` | spec | **Not ported.** The only connector with an in-transport retry on `retry_after`; Slack's client takes that idea (§5.3) |
| 13 | HubSpot | CRM deals | `hubspot/*` | spec | **Not ported** (CRM) |
| 14 | Attio | CRM records | `attio/*` | spec | **Not ported** (CRM) |
| 15 | Intercom | Support conversations | `intercom/*` | spec | **Not ported** (helpdesk) |
| 16 | Stripe | Invoices, subscriptions | `stripe/*` | spec | **Not ported** (payments) |
| 17 | QuickBooks Online | Accounting records | `quickbooks-online/*` | spec | **Not ported** (accounting) |
| 18 | Paperclip | An agent-orchestration product's issues, runs, decisions | `paperclip/*` | spec | **Not ported.** Sindri's own agent sessions are `sindri agents` |

Why this order: GitHub first because it is the one tracker with no Sindri reader, it holds the PRs that connect issues to code (§7.5), and its transport (paging, rate limit, the cursor boundary rule) is fully readable in code. Slack second because it is the other source that reached Sindri only through Onyx and its keys already match (§8.3). Linear is already read natively, so porting ConnectAI's weaker reader would be a step back. Everything else waits for the owner to name a tool they use.

What else we take, and how:

| ConnectAI piece | Where | In Sindri |
|---|---|---|
| The connector contract: `read(window) -> { records, nextCursor, cursorReset }`, read kinds, `healthCheck` | `packages/connectors-core/src/types.ts:162-204,292-305,339-348` | `IConnector.Pull(cursor, limit)` returning a page of full records and the next cursor, in `Aw.Connectors.Core` (§5.2). No `act` |
| Sync phases: cold start on a recent window, resumable backfill, incremental by cursor, daily reconcile | `apps/loop-svc/src/sync/phases.ts:40-50` (30 days, pages of 10, 90 days) | The same four phases (§5.5); sized from the owner's profile, not from ConnectAI's cost limits |
| Single flight per connection with a fencing lease | `apps/loop-svc/src/sync/claim.ts`, `schema.prisma:463-480` | Not ported: Sindri has `SyncLocks` and one `serve` host (`SyncLocks.cs`) |
| Token-aware windowing, 400 tokens with 40 overlap | `connectors/raw-window.ts:34-36,46-88` | `Chunker` in `Aw.Context.Core` (§5.6) |
| Text cleaning: quoted-printable, HTML to text, control characters, whitespace | `connectors/raw-clean.ts:43-266` | `TextCleaner` in `Aw.Connectors.Core`, the HTML and whitespace parts only (no mail decoding) |
| GitHub rate-limit handling, the `since` cursor with a seen-id set at the boundary | `github/transport.ts:111-135,137-210` | Read as the spec for the C# client's error mapping and cursor, not translated |
| Reciprocal rank fusion, k = 60 | `apps/api/.../retrieval/rrf.ts:47-89` | Already in Sindri, same constant (`RankFusion.cs:13`) |
| Spreading activation over the entity graph: 2 hops, keep 7, fan-out 50 | `retrieval/spreading-activation.ts:43-62` | Ported as the reach signal, over Sindri's typed graph instead of entity edges (§6.2) |
| ACT-R recency re-rank: 0.7 similarity + 0.3 activation, decay 0.5, with a write on every read | `retrieval/actr-rerank.ts:45-52,113-122` | **Not ported.** A read that changes the next ranking makes results unrepeatable and breaks the backtest (D13); Sindri's recency is the age of the link and the record, computed, never written (§6.2) |
| Entity edges with weight and reinforcement | `schema.prisma:1081-1112`, `edge-reinforce.ts` | Not ported: Sindri's edges are typed and carry provenance; weights come from type and provenance (`RankFeatures.cs:12-33`) |
| Vault, inference gateway with budgets, MCP server, billing, RLS | `packages/vault`, `packages/inference`, `apps/api/src/routes/mcp` | Not ported: Sindri has secret pointers, a loopback Ollama and its own MCP |

ConnectAI is under the Business Source License 1.1 and is the owner's own code (`README.md`, "Licensing"). Nothing here copies a file: each item is a reimplementation in idiomatic C# with the TypeScript as a guide, the rule in `.agents/rules/csharp.md` "Porting a TS package" (Q17).

### 5.2 The contract, and what changes in Sindri's own

```
Aw.Connectors.Core
  IConnector       Describe() ; Pull(SourceCursor? after, int limit, ct) -> ConnectorPage ; Versions(keys, ct) ; Probe(ct)
  ConnectorPage    Items: IReadOnlyList<ConnectorItem> ; Removed: IReadOnlyList<string> ; Next: SourceCursor ; CursorReset: bool
  ConnectorItem    Key, Kind, Title, Text, Author, Url, State, StateRaw, CreatedAt, UpdatedAt, ClosedAt, Version, Links[], Repo?
```

It is neutral on purpose: `Connectors` is tier 1 and a tier-1 package cannot reference another tier-1 package (§9.1), so it cannot use `Aw.Context.Core`'s `SourceRecord`. **Sindri's Host is the glue**, as it already is for the code index (`ContextFeaturesServiceCollectionExtensions.cs:95` is the `ISymbolVectors` seam): a small adapter maps a `ConnectorItem` onto Context's `RecordBatch` (§5.5). This also means the existing `SourceRecord` (`SourceRecord.cs:11`: title, text, author, two dates, no state, no links) is extended in Context with `State`, `StateRaw`, `ClosedAt` and `Links`, for the sources that live inside Context (Linear, repo docs).

Linear stays an `IContextSource` inside Context. It gains a `Pull`: a new list operation (`Issue.graphql` fetches one issue by id; `IssueHits.graphql` searches by terms), filtered on `updatedAt > cursor` and ordered by `updatedAt`, plus the issue state (`Issue.graphql:4` already asks for `state { name type }`; `LinearIssue` has no field for it, `LinearIssue.cs:3-17`). Linear's `orderBy: updatedAt` sorts newest first (ConnectAI reads it so, `linear/transport.ts:30`; the inventory flags that a capped read could then skip older items, open question 3), so `Pull` pages newest first until it passes the cursor and commits the new cursor only when it has reached the old one; a run cut short repeats work and loses nothing. `IChangeFeed`, which has no implementer today, is replaced by a `RecordFeed` port in `Aw.Context.Core/Sync/` with the same page shape as `IConnector.Pull`; Linear and repo docs implement it inside Context, and the Host adapter implements it over each `IConnector`. So there is **one sync driver** in `Aw.Context.Features` (the existing `SyncSources` slice, rewritten to pull feeds), for every source.

### 5.3 The first two connectors

**GitHub** (`Aw.Connectors.Infrastructure/GitHub/`): one `HttpClient` to `api.github.com` (or an `https` Enterprise host, its own profile key that `profile approve` shows the owner; IP literals and hosts that resolve to loopback, link-local or private ranges are refused unless the profile marks them), per repo in the profile's `context.sources.github.repos`:
- Issues and pull requests through `GET /repos/{o}/{r}/issues?state=all&sort=updated&direction=asc&since=<cursor>&per_page=100` (this endpoint returns both; a record with a `pull_request` field is a PR; ConnectAI's transport reads `/issues` this way and reads `/pulls` separately, newest first, filtering locally, `github/transport.ts:473-522`; Sindri needs one list, so it uses `/issues` and fetches the PR detail only for PRs), then for a PR `GET /pulls/{n}` once for `merged`, `draft`, `merged_at` and the change counts (ConnectAI asks the `/pulls` *list* for those counts, `transport.ts:340-343`, which I believe omits them: inventory open question 2, settled by taking them from the single-PR route) and `GET /pulls/{n}/files` (100 per page, at most 3 pages) for the paths it changed (§7.5). Comments and review bodies, which ConnectAI never fetches (inventory §2.1: counts only), are fetched for records updated since the cursor, newest first, capped per record by a profile key (default 100 comments).
- State: `open`, `closed`, `merged`, `draft`. Links: `closes`/`fixes #n` in the PR body and the PR's own issue (`relates`), a cross-reference in a comment (`relates`); the per-issue timeline is not read.
- Cursor: the newest `updated_at` seen plus the ids seen at exactly that instant (ConnectAI's boundary rule, `github/transport.ts:137-210`), so a page boundary inside one timestamp loses nothing and repeats nothing.
- Rate limit: on `x-ratelimit-remaining: 0` or a secondary-limit reply the run stops cleanly with the cursor it has, records `Unavailable` with the reset time, and the next run continues. At 5,000 requests an hour (authenticated REST), the first pull reads only the window (D12, `since=` today minus `windowDays`). Assume six months is a quarter of a 20,000-record history: 5,000 records, 1,250 of them PRs. That is about 50 list pages, 1,250 PR detail calls, about 1,900 file-list pages (1 to 3 each), and about 200 pages of comments read repo-wide through `GET /repos/{o}/{r}/issues/comments?since=` and `/pulls/comments?since=` (100 a page) instead of one call per issue: about 3,400 calls, about 40 minutes at the limit (estimate; I0 counts the owner's real window). A day's changes are tens of calls.
- Key: `github:issue/<owner>/<repo>/<n>` and `github:pr/<owner>/<repo>/<n>`. (ConnectAI keys by GitHub's `node_id`, `github-connector.ts`, for cross-API stability; Sindri's keys are read by people and by the linker's `url` and `issue-key` rules, so they carry the repo and number, and the `node_id` goes in `version`'s companion, not the key. A renamed repo re-keys through `IKeyMigration`, `Aw.Context.Core/Graph/IKeyMigration.cs`.)

**Slack** (`Aw.Connectors.Infrastructure/Slack/`): `conversations.history` and `conversations.replies` for the channels in `context.sources.slack.channels`, never a direct message or a channel not listed. A thread (a `thread_ts` chain) is one record; a busy channel's unthreaded messages are bucketed by day. Key `slack:<channel>/<ts>`, **the same key Onyx's Slack connector produces** (`OnyxRecordKeys.cs:48-52`), so every stored link and decision on a Slack thread stays valid (§8.3). Cursor: the newest `ts` per channel, as ConnectAI's per-channel watermark map (`types.ts:219-228`). The thread link comes from `chat.getPermalink` on the root (`slack/transport.ts:7-9`).

**A rate limit that can decide the connector's value.** ConnectAI's transport assumes Slack's regime for apps outside the Marketplace: 1 request a minute and at most 15 objects per `conversations.history` call (`slack/transport.ts:13-15,25-26`). At that rate a backfill reads 900 messages an hour: a channel with 20,000 messages takes about a day, each thread's `conversations.replies` costs another request, and 90 days of ten busy channels is a week. As I understand Slack's policy the limit applies to commercially distributed apps and not to an internal app the owner creates in their own workspace, which keeps the normal tier (about 50 requests a minute, 999 messages per call). I have not verified that, so phase I0 makes one call with the owner's token and reads the `Retry-After` header and the page cap; The owner's answer (Q16): an internal app in their own workspace, so the normal tier is expected and I0's probe confirms it. The client honours `Retry-After` and resumes from its cursor (as the Discord connector does, inventory §2.1).

### 5.4 Credentials

| | ConnectAI | Sindri |
|---|---|---|
| Where a token lives | A vault (Infisical, or a local encrypted file) under `cred_<id>`, one per workspace (`packages/vault/src/vault.ts:20-45`); a platform-wide GitHub App private key plus a per-workspace installation id (`github/app-auth.ts:1-5`) | A **secret pointer** in the profile: `env:NAME`, `file:/path` (mode 0600), `keychain:service/account`, `op:vault/item/field` (`LinearSettings.cs:17`; resolved by `ISecretPointers.Resolve`, `ISecretPointers.cs:10`). A pointer that isn't one is refused and never echoed (`LinearSettings.cs:53-58`) |
| What the token can do | GitHub: an App with permissions across installations; Google: a service account with domain-wide delegation that can read any mailbox of the domain; Microsoft 365: tenant-wide app permissions (`SOVEREIGN.md:11-18`) | **A read-only token the owner makes, limited to what the profile lists.** GitHub: a fine-grained personal access token with Issues, Pull requests and Metadata read, on the listed repos only. Slack: a bot token with `channels:history`, `channels:read` (and `groups:*` only for listed private channels), in an app the owner installs. Linear: the read-only key the profile already points at |
| Who connects | A user clicks Connect and consents | The owner, with company sign-off where the tool is the company's (retrieval spec §3 "Company OAuth"). Sindri never creates an OAuth app, never asks for a grant, never stores a password |
| Scope | The workspace | The profile: `context.sources.github.repos`, `.slack.channels`, `.linear.teams` (`LinearSettings.cs:33`); a record outside them is dropped at the connector, before it reaches the store |
| Printing | | `ToString` withholds the pointer (`LinearSettings.cs:66`); no error repeats a token; a test plants a token in the environment and asserts it reaches no log, no span and no file (retrieval spec §7 "Logs and spans") |

A `sindri index connect <source>` verb, a human-only one (`SND-AUTH-002` for a session, as `onyx up` is), asks for the token once at the terminal and writes it with the existing `KeychainSecretWriter` (`Aw.Sindri.Infrastructure/Secrets/KeychainSecretWriter.cs`), then writes the pointer into the profile; it is the one part of the Onyx machinery that stays (§8.1).

### 5.5 Sync

Phases per source, from ConnectAI's four (`phases.ts:5-17`), run by the one driver:

| Phase | What it reads | Default |
|---|---|---|
| Recent | Records updated in the last N days, newest first, so the first answers come in minutes | 30 days (`phases.ts:40`) |
| Backfill | Older records **back to the window's start** (`windowDays`, D12) and no further, one page per tick, resumable from `backfill_cursor` | pages of 100 (ConnectAI uses 10 because each page costs a model call; Sindri's page costs an HTTP call and an embed). Default window 183 days; there is no budget gate |
| Incremental | Changes since `cursor` | every 5 minutes while `serve` runs |
| Reconcile | A bounded full window, to catch deletes and edits the cursor missed; marks records the source no longer returns as `removed_at` | daily, 90 days (`phases.ts:50`), never wider than `windowDays` |

**The window (D12).** `context.index.windowDays` (default 183, at least 1) is the one bound on what a first run reads: a record whose `updated_at` is older than today minus the window is never fetched, chunked or embedded. An old record that is edited later comes in through Incremental, because its `updated_at` is then inside the window. A record that ages out of the window stays in the store (retention is `retainDays`, Q7). Raising the window starts a Backfill from the old start back to the new one; lowering it fetches nothing and deletes nothing. `profile approve` shows the window and the estimate for it (§5.8).

A page commits in **one `BEGIN IMMEDIATE` transaction**: `source_record`, `record_link`, `chunk` (the FTS rows follow by trigger), the matching `intent_record` rows and the moved cursor, all or none. Today `IIntentStore.CommitPage` writes `intent_record` one record at a time with `Cursor: null` (`IntentSyncRun.cs:183`), so `Commit(RecordBatch)` is a new method that writes both tables together, or they drift; a kill-between test covers each pair (record and cursor, record and chunks, record and `intent_record`). The phase names are internal; the owner sees two milestones, **usable** (the recent window is synced) and **complete** (backfill and embedding done), §5.8. Embedding is a **separate, resumable job** (§5.6), so a slow embedder never holds a sync: a record is searchable by words the moment its page commits, and by meaning when its vectors land. A replay of a page changes nothing (compare `text_hash` and `version`). Sync runs as the engine, like `SyncSources` does today (`SyncSourcesService.cs`), and uses the same `SyncLocks`. A `sindri index sync [source]` verb and a built-in service run it; a source that is down is a row with its code, not a failed call (as now).

### 5.6 Chunking and embedding

`Chunker` (pure, `Aw.Context.Core`): clean, then window at 400 tokens with 40 overlap (`raw-window.ts:34-36`). ConnectAI counts tokens with `js-tiktoken` (cl100k) and says itself it is an approximation for the embedder's real tokenizer (`raw-window.ts:14-19`). Sindri adds no tokenizer package: it estimates a chunk's tokens as the larger of words times 1.3 and characters divided by 3.5, which overcounts dense text (base64, long urls: the case that broke ConnectAI's fixed 2,048-character window, `raw-window.ts:8-12`), and sends each batch to Ollama with `truncate: false` so an over-long input is an error, not a silent cut; on that error the chunk is halved and retried once (ConnectAI's "embed-fit backstop", `embed-fit.ts`). **Measured in phase I0 (Ollama 0.20.5):** the model's `context_length` is 2,048 tokens. `truncate: false` returns HTTP 400 "the input length exceeds the context length" on inputs of about 3,000 and about 7,800 tokens, as documented. `truncate: true` returns the same 400 on this version; it does not cut. A 400-token control input is fine. So the halve-and-retry backstop is required, not optional, and chunks must stay well under 2,048 tokens (the 400-token window does).

`Embeddings` (tier 0): `IEmbedder.Embed(texts, ct)` against `POST {url}/api/embed`, loopback only (`127.0.0.1` or `[::1]`, never `localhost`), no proxy, no redirect, no cloud model, the model's digest checked against the pin and never downloaded: the rules of `IEmbedder` in the code index (`Aw.CodeIndex.Core/Ports/IEmbedder.cs:7-9`; code index spec §5.6). Batches of 32; the embed job works newest-updated first in chunks of 4,096 chunks until none is missing, and is restartable. **Cost** (measured in phase I0, 2026-10-27, on a GPU box: Ollama 0.20.5, `nomic-embed-text` fully in VRAM, 4 cores): 122 chunks a second, batches of 32, chunks of about 300 words (2,000 chunks in 16.4 s; a batch takes 0.26 s at p50, 0.29 s at p95). That is 25,000 chunks in 3.4 minutes and 4,000 in 33 seconds. The older estimate (0.8 to 4 chunks a second, derived from the code index's CPU figure) was for a box without a GPU; **a CPU-only box is still unmeasured** and stays gated by the one-hour bar below. **With the six-month window (D12):** the 50,000-chunk size in §4.4 is three Linear projects and a year of PRs, so six months is about 25,000 chunks: 3.4 minutes at the measured GPU rate, 1.7 to 8.7 hours at the old CPU estimate. The owner's bar ("it shouldn't take hours") is a gate: **the first embed of the owner's real window finishes inside one hour on this box.** It is met on a GPU box; for a CPU-only box it stays open until measured. The order makes most of it invisible anyway. Recent (30 days, about 4,000 chunks) is embedded first, 33 seconds on the GPU box (17 minutes to 1.4 hours at the old CPU estimate), and every record is searchable by words the moment its page commits. If a box's measured rate misses the hour, the fix is chosen in this order: a smaller embedding model (Q6's 384-dimension alternative, roughly 2 to 3 times faster on CPU); then a shorter default window. Never a cap that quietly leaves part of the window word-search only. `context.index.maxChunks` (default 60,000) stays as a safety ceiling against a misconfigured window, not as the bound. A day's changes (a few hundred records, about a thousand chunks) are minutes. Apple silicon is faster (Ollama uses the GPU). If a CPU-only run lands under 1 chunk a second, Q6's smaller-model alternative comes back on the table.

### 5.7 LLM enrichment: the call is no

ConnectAI runs up to five model calls on every item after it is chunked: entity resolution, fact extraction (0 to 3 facts, up to 768 output tokens), an episode summary, procedure extraction and preference mining (`ING/ingest-gmail.ts:700-831`, inventory §3 step 6). On self-host the model is `qwen2.5:1.5b` through Ollama, in the cloud Claude Haiku through a gateway (`deploy/selfhost/.env.example`, inventory §1).

| | Cost for the owner's size (my arithmetic from the figures above; assumes 20,000 records) |
|---|---|
| Calls | up to 100,000 (20,000 x 5) on the first sync, then 5 per changed record |
| Time on CPU | a 1.5-billion-parameter model produces roughly 10 to 20 tokens a second on 16 cores; one call with a 300-token prompt and a 200-token answer is 10 to 20 seconds, so 100,000 calls are 12 to 23 days of CPU. Even at one call per record it is 2 to 5 days |
| Memory | a second model resident beside the embedder: about 1 GB of weights for the 1.5B model, more for a model good enough to extract facts reliably |
| Egress | the cloud variant sends every record's text to a hosted model: this reverses retrieval spec §3 (model egress is off unless the profile turns it on) |

What the owner gets back is entities, facts and summaries that Sindri's graph already produces another way: the rules linker makes deterministic links from the record text (retrieval spec D3), the optional model linker proposes links as untrusted `propose` rows (D15), and a feature's summary is the map's own `describe` step. ConnectAI needs its facts because it has no structure to hang records on; Sindri's map is that structure.

**Decision (D11): no model call on ingest, in v1.** Nothing in the store's design needs one: chunks and vectors are made from the scrubbed source text. If a later spec wants a one-line summary per record to embed beside the raw chunks (the "derived path", `chunk-synthesize.ts:1-23`), it is an opt-in job over records that have no summary yet, on the profile's model switch, bounded by a budget; the schema has room (`embedding.space` could gain `summary`) and nothing here blocks it. Q19.

### 5.8 What the owner sees

One progress surface serves the CLI and the app: `sindri index status [--json]` prints it, Home shows the same line live from the event stream.

```
github   usable · synced 1,900 / ~5,000 records (6-month window) · embedded 14,000 / 31,000 chunks · about 6 h left (estimate) · newest first
slack    waiting · back at 14:05 (rate limited; 312 threads read)
linear   complete · 1,940 records · embedded
```

Per source, one state, its text, its fix and where it shows:

| State | Text | Fix | Shows in |
|---|---|---|---|
| ok / complete | "complete" | none | status, Home |
| syncing | "usable" or "filling older history" | none | status, Home |
| waiting | "waiting, back at 14:05" (rate limit, with the reset time) | none | status, Home, doctor (ok) |
| needs token | "needs token: the token was refused" (401, or a pointer that does not resolve) | `sindri index connect <source>` | status, Home, doctor (fail), setup |
| needs access | "needs access: repo X is unreadable" (a listed repo or channel the token cannot read, or a renamed one) | widen the token or fix the profile | status, Home, doctor (warn), setup |
| error | the code and one line | per code | status, doctor |
| embeddings off | "meaning search is off: Ollama isn't running" | start Ollama | status, Home, every query's answer, doctor (warn) |
| cap reached | "cap reached: older chunks are word-search only" | raise `context.index.maxChunks` | status, Home |

`index connect <source>` validates the token **and** every listed repo or channel before it saves anything, so a wrong scope fails at connect, not hours into a sync. Embedding has pause and resume (`index embed pause|resume`, and a button on Home).

**Setup replaces the Onyx step** with ordered steps, each with the states not started, waiting on you, running, done, failed: 1 approve the profile; 2 connect GitHub (and Slack, if used): the app shows "waiting for the token in your terminal" while `index connect` reads it, because the token is typed at the terminal and never in the app; 3 first sync ("usable" when the recent window is in); 4 first embed ("complete"); and, only while `index:onyx-leftover` is not clean, 5 clear the Onyx leftovers (`onyx down --purge`). A box that never ran `onyx up` sees none of the Onyx wording. After the upgrade that removes Onyx, a banner and a `sindri setup` row say "GitHub and Slack need new tokens; searches are partial until then" (§8.2).

**A query while vectors are partial or missing** answers from what exists and says so in the answer, in plain words: "meaning search is off: Ollama isn't running" or "meaning search covers 69% of your records; the rest are word-search only". The dense leg is dropped, the lexical and graph legs answer, `index sync` keeps running (sync does not need Ollama), and a golden pins the wording. A wrong model digest is the same state with its own line.

**Doctor rows** (replacing `onyx:stack` and `context:onyx`):

| Row | ok | warn | fail | Fix line |
|---|---|---|---|---|
| `index:store` | file and `-wal`/`-shm` are `0600` in a `0700` directory | any mode or parent wider; file over 5 GB | cannot open or migrate | `chmod`; Q1 |
| `index:embeddings` | Ollama reachable, digest equals the pin, backlog moving | Ollama unreachable; backlog not moving for 1 h | digest differs from the pin | start Ollama; `install-deps.sh` |
| `index:github`, `index:slack`, `index:linear` | complete or syncing | waiting; needs access | needs token | `index connect <source>` |
| `index:onyx-leftover` | no Onyx volumes, files or keychain entries | present and acknowledged (`context.index.acknowledgeOnyxLeftover: true`) | present and not acknowledged: raw Slack and Linear text sit in them (§8.2 step 5) | `sindri onyx down --purge`; the row is gone only when nothing is left |

`index purge <source>` shows its counts first ("deletes 8,200 records, 31,000 chunks, 31,000 vectors, about 140 MB") and asks for the human's yes; it cannot be undone for anything the source has since deleted, which is why it is a human action (§10). Adding a repo or channel to the profile shows "backfilling 2 new repos" in the same progress line.

## 6. The query path and ranking

### 6.1 Legs

Today's pipeline (retrieval spec §8.2): anchors, cascade, per-source candidates, per-source fusion, reciprocal rank fusion across sources (k = 60, `RankFusion.cs:13`), the feature boost, an optional rerank, the stale check, packing. The store changes step 3 and what a "source" is:

| Leg | Built from | Ranked by |
|---|---|---|
| **Dense** | the query (and each anchor name) embedded with the pinned model, scanned over `embedding` rows of `space = 'chunk'` with the caller's scope mask (§4.4), top 50 chunks, a relevance floor, then the best chunk per record | cosine, best first |
| **Lexical** | FTS5 over `chunk_fts` with each term quoted as a phrase, never as query syntax (`DocSql.Phrase`, `src/Context/Aw.Context.Infrastructure/Docs/DocSql.cs:47`), terms of a free-text query joined with `OR`, the same scope mask, top 50 chunks, best chunk per record | `bm25`, best first (`DocSql.cs:41`). ConnectAI's lexical leg is `plainto_tsquery`, which ANDs every term, so a long question matches almost nothing (`hybrid-search.ts`, inventory §5 note); `OR` with `bm25` does not have that failure |
| **Graph (anchors)** | exists today: records linked to the cascade of the query's anchors, ordered by cascade distance then edge type (retrieval spec §8.2 step 3) | as today |
| **Graph (seeds)** | new: the owner nodes of the top 7 dense and lexical hits (Sindri's choice; ConnectAI's seeds are the fused candidates' episodes, `recall.ts:164`), expanded by the same cascade, 2 hops, at most 50 neighbours per node, and only the 7 best-scored nodes carried to the next hop (ConnectAI's `HOP_LIMIT = 2`, `FAN_OUT_CAP = 50` and `INHIBITION_KEEP = 7`, which is a per-hop cut, `spreading-activation.ts:43-55`); the records linked to what that reaches and not already in a list | hops, then edge type |
| Code | exists today: git grep and the code index's neighbours | as today |

**Retrieval spec §8.2 steps 7 and 8** (reformulate once; one batched `Versions` call per source for the cited refs) are kept: for a stored record the stale check compares the stored `version` with a live `Versions` answer for the cited refs only, so a hit that changed at the source is flagged, not served silently.

**The dense floor.** A nearest-neighbour scan always returns 50 rows, relevant or not, and a record at rank 1 of a leg of noise scores like one at rank 1 of a good leg. ConnectAI cuts the dense leg: everything within cosine distance 0.5 is kept, else the nearest 8 within 0.75, else nothing (`hybrid-search.ts:170-177`, applied at `:473`). Sindri does the same with its own numbers, because those two were tuned for a 1,024-dimension model and `nomic-embed-text` spreads its distances differently: the floor is two profile keys, starting at ConnectAI's 0.5 and 0.75 cosine distance and 8 rescued hits and then set by the I4b backtest, and a query whose dense leg is empty says so in the answer ("nothing close by meaning"), which is information, not an error.

Legs are fused exactly as sources are now: per leg, the best rank of a record across that leg's sub-queries; then reciprocal rank fusion across legs, `score = sum of 1 / (60 + rank)` (`RankFusion.cs:30-50`). Source scores are discarded; only ranks are kept (`SourceHit.cs`). A leg that is unavailable (Ollama down: no dense leg, `unavailable` with its code) drops out and the answer says so, as a degraded source does today.

### 6.2 Signals, and how they combine

`FeatureRanker.Apply` multiplies each fused score by `1 + weighted mean of the features` (`FeatureRanker.cs:23-27,41`), so features can at most double a score. The five existing features are scores from 0 to 1 (`RankFeatures.cs:12-55`): cascade distance (`1 / (1 + d)` from the query's anchors), edge type (decides 1, quotes 0.7, mentions 0.4, other 0.2), provenance (human 1, deterministic 0.7, model 0.4), supersession (1 stands, 0 replaced), and link recency (halves every 90 days from the day the link was written). Weights default to 1 each (`FeatureWeights.cs:10-22`).

Three are added. **Reach** is the graph signal: `1 / (1 + h)` where `h` is the fewest typed-edge hops from a seed's node to the record's node (0 when the record is itself a linked seed), and 0 for a record with no node (an unlinked record is not part of the graph, `IntentOverlay.cs:42`). **State** and **freshness** are record signals the store makes possible for every record, linked or not: state (open or no state 1, draft 0.75, merged or done 0.5, closed or canceled 0.5) and freshness (halves every 180 days from `updated_at`). New weights: reach 1, state 0.5, freshness 0.5. They are starting values, and (as retrieval spec D13 does for the reranker) they ship on only if a backtest arm with them beats the five-feature arm (§11); with the store off, the five-feature result is unchanged. Nothing here writes on read.

**Worked example.** Query: "why does split pane orientation reset on restore". The anchor is `feature:layout.split-pane.orientation`. Four records come back; ranks are within each leg (`-` = not in it):

| Record | Dense | Lexical | Graph | What the map knows |
|---|---|---|---|---|
| A: open Linear issue, "Orientation lost when restoring layout" | 3 | 1 | 1 | decides the feature; deterministic; linked 30 days ago; updated 10 days ago; a seed itself |
| B: repo doc chunk, `docs/layout.md` | 1 | 5 | - | not linked; doc updated 400 days ago |
| C: closed GitHub issue, similar words | 2 | 2 | - | not linked; closed, updated 800 days ago |
| D: merged GitHub PR, touches the feature's files | 4 | - | 2 | mentions the feature (cascade 1 from the anchor), deterministic, linked 200 days ago; one hop from A's node (the PR `closes` the issue); updated 60 days ago |

Fusion, `1 / (60 + rank)` summed: A = 1/63 + 1/61 + 1/61 = 0.0487; B = 1/61 + 1/65 = 0.0318; C = 1/62 + 1/62 = 0.0323; D = 1/64 + 1/62 = 0.0318. Order by fusion alone: A, C, B, D.

Features (cascade, edge, provenance, supersession, link recency, reach, state, freshness), weights 1, 1, 1, 1, 1, 1, 0.5, 0.5 (sum 7). A record with no state counts as 1 (neutral):

- A: 1, 1, 0.7, 1, 0.794 (0.5^(30/90)), 1, 1, 0.962 (0.5^(10/180)): weighted sum = 5.494 + 0.5 + 0.481 = 6.475; mean 0.925; score 0.0487 x 1.925 = **0.0937**.
- D: 0.5, 0.4, 0.7, 1, 0.214 (0.5^(200/90)), 0.5 (one hop: 1/(1+1)), 0.5 (merged), 0.794: sum = 3.314 + 0.25 + 0.397 = 3.961; mean 0.566; score 0.0318 x 1.566 = **0.0497**.
- B: 0, 0, 0, 1, 0, 0, 1 (a doc has no state), 0.214: sum = 1 + 0.5 + 0.107 = 1.607; mean 0.230; score 0.0318 x 1.230 = **0.0391**.
- C: 0, 0, 0, 1, 0, 0, 0.5 (closed), 0.046: sum = 1 + 0.25 + 0.023 = 1.273; mean 0.182; score 0.0323 x 1.182 = **0.0381**.

The same four with the five existing features only (weights 1, mean over 5): A 0.0924, D 0.0496, C 0.0387, B 0.0381. So the three orders are: fusion alone A, C, B, D; five features A, D, C, B; all eight A, D, B, C.

What that shows, honestly: the big move, D from last to second, comes from the graph features that exist today (it is linked to the feature, so cascade, edge type, provenance and link recency lift it). The three new signals only reorder the near-tie below it, putting B (a doc updated 400 days ago) ahead of C (a closed issue untouched for 800 days) because C is old and closed. That is a small effect, which is why the new signals ship only if their backtest arm beats the five-feature arm (§11).

## 7. The map builds from the store

### 7.1 What changes for the build

`BuildMapService` (`Aw.Context.Features/BuildMap/BuildMapService.cs:21-28`, already at the seven-constructor-parameter limit) gets its doc and record input from one new record, `MapRecordSources(IRecordStore Records, ICodeSymbolSource Symbols)`, the shape map-structure §11.2 gave `MapDocSources(IDocIndex Docs, ...)`. `IRecordStore` is a Context.Core port with a SQLite adapter in Context.Infrastructure; its read side is what the build and the search legs use, its write side is `Commit(RecordBatch)` for the sync driver.

- **The build sees every record in scope, and a record becomes a node only when it has a reason to.** Docs: every doc matching the record globs is a node, linked or not (map-structure §11.2 unchanged in rule; 583 today). Tracker and chat records: a node only when it has at least one edge into the map, that is a source-stated link to another stored node, a rule link, a resolved `touches` path or a resolved cited path; an isolated record stays in the store, searchable by the legs, and is not a node (the overlay's rule, `IntentOverlay.cs:42`: "stored ... but is not part of the graph"). Why: 20,000 records as 20,000 `record:` nodes would swamp the whole-map budget of 500 nodes per area (`2026-10-23-sindri-context-whole-map-design.md:122-124`) and push every area into its `truncated` fallback. A PR with 30 resolved files is one node with 30 edges that the step-4 roll-up folds into its features. My estimate is that a quarter of 20,000 records have an edge, about 5,000 nodes, which the area fallback absorbs; I5 prints the real number. Sessions see a record node only if the scope rules below let them (`context_map_whole` reads the same scope-cut view, whole-map spec `:160`).
- **The build records which store state it read**: a `commit stamp` (the last committed sync page) in the draft's version row, so a build says "records as of stamp N" and the check line that §11.2 words as `docs: index is N commits behind` becomes `records: store is N pages behind` (numbers only, as §4.3 of that note requires). A stale source's edges are skipped, not guessed.
- **Source-stated links become edges.** A `record_link` of kind `closes`, `parent`, `blocks`, `duplicates`, `relates`, `attachment` whose two ends are stored records is a deterministic `record mentions record` edge, with the link kind as the edge's detail (`EdgeDetail.cs`); no new edge type in v1 (Q9). A `closes` from a PR to an issue, and the PR's files via the tracker half of #1373, are what let a lens on a feature show the issue, the PR that fixed it and the docs that describe it, from one graph.
- **The key is shared** (§4.3), so no mapping layer exists between what was searched and what is graphed.

### 7.2 What changes for #1373 (map-structure §11)

§11 of the map-structure note is written as **pending this spec** (a banner is added in this PR; its rules are otherwise kept):

| §11 says | Now |
|---|---|
| Doc records come from the build, from `IDocIndex` and the path list (§11.2) | From the build, from `IRecordStore` for the doc text and from the path list for existence. The rules (record globs, directory-doc names, home-feature links, cited-path `mentions`, roll-up) do not change |
| "The map build does not see it" (§11.1), the doc index is `doc_chunk` behind `IDocIndex` | The doc index **is** the store (`source_record` kind `doc`, `chunk`): `IDocIndex` and `SqliteDocIndex` are retired after M-drop; `RefreshDocsService` becomes the repo-docs feed of §5.5 |
| `MapDocSources(IDocIndex Docs, ICodeSymbolSource Symbols)` | `MapRecordSources(IRecordStore Records, ICodeSymbolSource Symbols)` |
| Open question 18: a doc's age needs a per-file commit time the reader doesn't give | Unchanged for docs (the repo head's time first). Tracker records now have real `created_at`, `updated_at`, `closed_at` |
| "The tracker half (issues, projects, decisions, freshness) is the ideas lane's own design" | The tracker half's inputs exist in the store: state, links, attachments and times for Linear and GitHub. The ideas lane designs only what a lens shows, not how records arrive |
| Overlay overlap: S7 deduplicates the overlay's edges (§11.2) | Unchanged |
| S7 needs S3 (§7) | S7 needs S3 **and** this spec's phase I1 (the `IRecordStore` port, its fake and its contract tests). The ideas lane can start S7 against the fake the day I1's port merges; it should not start before |

S3 (home features), S4 (roll-up), S5 (checks) and S6 (pins) do not touch docs or records and are unaffected.

### 7.3 Vectors of graph nodes (optional, phase I7)

`embedding` rows with `space = 'node'` hold a vector per feature and concept (its label plus its top member paths, re-embedded when that text's hash changes). Two uses, both proposals and never silent edges: a record that is similar to a feature but linked to none becomes a `propose` row in map review (model provenance, untrusted until accepted, retrieval spec D15, `RulesLinker`'s rules stay the deterministic path); and a query with no anchor can start from the nearest features. Whether I7 is worth building depends on how many tracker records stay unlinked after I5; the backtest reports that number (Q10).

### 7.4 Code

Code vectors stay in the code index's per-repo files (`embeddings` table, code index spec §4.2, `:163`), reached through the existing `Neighbors` (code index spec §7.1). A query therefore runs two nearest-neighbour passes, the store's and the code index's, fused as two legs. Moving code vectors into the store would make one pass but couples the code index's publish-by-pointer files to `context.db`; revisit only if the two-pass cost shows in the backtest (Q14).

### 7.5 Records and the build: the ideas lane's two constraints, and tracker records that touch code

The ideas lane set two constraints for doc records (map-structure §11.2). Does the store give a real reason to change either? No.

1. **Doc-record nodes come from the build's path list, keyed `record:repo:<repo>/<path>`.** Kept. The store has a `source_record` row per doc, but the node must not depend on the store having synced: a doc that matches the record globs and is in the path list gets its node even when the store has no row yet (then it simply has no cited paths and a `records: not yet synced` line in the report). The store adds the doc's *text*, not its existence. **Per-doc freshness:** the cited-path pass uses a doc's chunks only when its `source_record.version` (the git blob id) equals that doc's blob in the build's path list; otherwise its citations are skipped and counted (`records: 4 docs older than HEAD`). The store-wide `records: store is N pages behind` stamp can't catch one doc edited since the last sync; this rule does. Tracker records are the other kind: they are not in any path list, so their node is `record:` plus their record key (`record:github:pr/<owner>/<repo>/<n>`, §4.3), and a record the store has but no rule or link has touched stays in the store, searchable by the legs, and is **not** a node (§7.1).
2. **Cited paths become Deterministic `mentions` edges at 1.0 only on an exact path match; an unresolved citation is a count in the build report.** Kept, and the store makes the count better: the build reads chunk text for every record (not only docs), extracts backticked repo paths and `file:line` forms, and an edge exists only when the path is in the build's path list for that repo. Anything else (a renamed file, a path two or more mapped repos hold, a symbol the code index can't place; a path in exactly one other mapped repo resolves to it, since the record's own repo is tried first) is counted by kind in the report's `citations` line: `citations: 212 resolved, 31 unresolved (26 missing, 5 ambiguous)`. A cited symbol resolves through the code index (`ICodeSymbolSource`, map structure spec §11.2) when the index is on and knows it, and is counted as unresolved otherwise. No fuzzy match, no basename match, no model. The extractor runs over tracker and chat text too, so a backticked path in a Slack thread makes a Deterministic edge; that is intended, since the match is exact. It also means a session can see that thread through the edge under scope rule (c) (records linked to the repo's nodes, §4.4). A thread that cites one of the repo's files is about the repo, so that's the point.

**Tracker to code.** ConnectAI has no record-to-record or record-to-file links at all (inventory §4: "no table linking one episode to another"). The ones we add are source-stated, not inferred:

- A merged or open PR's changed paths: `GET /pulls/{n}/files` (§5.3) writes `record_link` rows of kind `touches` with `to_ref = 'path:<repo>/<path>'`. The build resolves each against its path list by exact match: a hit is one Deterministic `record mentions code` edge at 1.0, which the step-4 roll-up turns into `record mentions feature` with a count, so a lens on a feature shows the PRs that touched it. A path that no longer exists (a deleted or renamed file) is normal history for a merged PR, not a quality problem, so it has its own bucket, `touched files since removed: N`, kept apart from the `citations` line so that line stays a quality signal.
- **A huge PR.** A 500-file refactor or rename would otherwise be one node with 500 edges that the roll-up ties to nearly every feature. Past `MapRecords.TouchCap` resolved paths (a named constant, starting value **50**), the PR keeps its node and gets one rolled-up `record mentions feature` edge per feature with the count, but no per-file edges are stored. The build report counts them: `records over the file cap: 3 (812 paths rolled up)`.
- **Which repo.** A GitHub `owner/name` becomes the profile's repo name when the connector writes the record: the `repo` column is set by matching `owner/name` against `context.sources.github.repos` (§5.4), and `to_ref` is `path:<profile repo name>/<path>`. A PR from a repo outside the profile is dropped at write time and counted in the sync report (`github: 12 PRs from repos not in the profile, skipped`); it never reaches the store.
- A PR that `closes` an issue and an issue's parent, blocks, duplicates, relations and attachments: `record_link` rows between records (§7.1).
- The result for the worked example in §6.2: A (the Linear issue) is `closes`-linked from D (the PR); D `touches` `src/layout/SplitPane.cs`, which is in the feature's files; so the feature, the issue, the PR and the doc that cites the file are one connected neighbourhood, with no model involved.

Cost: 1 to 3 file-list pages per PR on top of its detail call. The window's 1,250 PRs (§5.3) are about 3,100 calls, most of the 40 minutes in §5.3, spent over the backfill.

## 8. Onyx: removal and migration

### 8.1 What goes, and what stays

Counts are files that mention "onyx" (an upper bound on what is deleted; `grep -rli onyx` over the tree) and are rechecked when each PR is cut.

| Area | What | Action |
|---|---|---|
| CLI | `sindri onyx up/down/status/open`, `OnyxVerbs.cs`, `CommandTable.cs:104,167`, `Interrupt.cs:45`, the status row `Verbs.cs:135` | Delete, except `onyx down --purge`, which stays as a human-only, self-contained stub (U30) that lists and removes a leftover stack, its files and its keychain entries (§8.2 step 5, Q20), until `index:onyx-leftover` is clean. |
| API and setup | `ConnectOnyx`, `DeferOnyx`, `SetupOnyx`, `OnyxAccount`, `OnyxEnsure`, `OnyxStatus`, `OnyxUp`, `OnyxDown` slices; the endpoints `ConnectOnyxEndpoint`, `DeferOnyxEndpoint`, `SetupOnyxEndpoint`; the Onyx parts of `GetSetup` and `SetupSources`; the serve hooks `OnyxServeHooks.cs` | Delete. The setup screen's Onyx step is replaced by "connect your sources" (I8) |
| Sindri Core and Infrastructure | `Aw.Sindri.Core/Onyx/*` (about 50 files: compose, container runtime, provisioning, pins, login), `Aw.Sindri.Infrastructure/Onyx/*` (the Docker CLI runtime, HTTP provisioning, compose YAML, health), 93, 57, 34 and 23 files in Core, Features, Infrastructure and Host mention it | Delete; keep `KeychainSecretWriter` and `ISecretWriter` (§5.4) |
| UI | 20 files in `Aw.Sindri.Ui` | Delete the Onyx views and view models; the setup flow and its goldens change |
| Context | `Aw.Context.Core/Onyx/*`, `Aw.Context.Infrastructure/Onyx/*` (23 files), `OnyxSection`, `ContextSettings.Onyx` (`ContextSettings.cs:48`), `OnyxSources` in `ContextSourceCatalog.cs:34-37`, `Eval/OnyxBriefSource.cs`, the Onyx arm of the backtest (`Eval/Backtest/Arms.cs`), `Permalinks`' loopback Onyx origin | Delete after the backtest brief source reads the store (U17b, in I4a) |
| Doctor | `onyx:stack` (`RunDoctor/OnyxStackDoctor.cs:20`), `context:onyx` (`OnyxVersionProbe.cs:10`), the aot-smoke expected output | Delete the rows (the leftover row stays until clean); update `src/Sindri/aot-smoke/expected.txt` and run aot-smoke in that PR |
| Errors and profile | `SND-ONYX-*` (`SindriErrors.Onyx.cs`), `docs/sindri/errors.md`, profile keys `context.sources.onyx.*` | Codes retired. A profile that still has the key is **an error** for one release naming the fix, not silently ignored (Q15). Removing the key changes the profile's hash, so the owner re-approves once (`sindri profile approve`) |
| Pins and scripts | `context/onyx.lock`, `scripts/onyx-lock.sh`, `scripts/install-onyx.sh`, `scripts/tests/install-onyx.test.sh`, `scripts/tests/onyx-lock.test.sh`, the Onyx step in `setup.sh` (flags `:142-153`, step `:595-605`) and its `--no-onyx` / `--with-sindri` wording, the `deploy/images.lock` note (`:34`), AGENTS.md's command list and the merge-gate line for `onyx-lock.sh` | Delete; `--with-sindri` stays accepted and does nothing (as AGENTS.md says it already does); `--no-onyx` stays accepted for one release |
| Docs | `docs/sindri/onyx.md`, `SETUP.md`, `first-context-run.md`, `docs/csharp/sindri*.md` Onyx sections, `.env.example` (any `AW_*ONYX*` the code reads) | Rewrite or delete; `scripts/check-env-example.sh` in the gate. Old specs stay as history, each gaining a one-line "superseded by 2026-10-27" |
| Other tree references | `Dockerfile`, `ONBOARDING.md`, `scripts/{sindri-pod,context-first-run,changed-areas}.sh` and their tests and goldens, `scripts/lib/sindri-build-inputs.sh`, `scripts/env-scan-ignore.txt`, `docs/*.html` (the list in §8.2) | Delete or edit; this group is the last of the code groups |
| Tests | `tests/Aw.Sindri.Tests/{OnyxUp,OnyxAccount,OnyxStatus,SetupOnyx,OnyxEnsure,OnyxDown,OnyxPinsTests}`, `tests/Aw.Context.Tests/Onyx`, `FakeOnyxServer` in `Aw.Context.Testing/Fakes`, the API goldens (`tests/Aw.Sindri.Tests/Golden`, 37 files; `Aw.Sindri.Ui.Tests/Golden`, 8) | Delete with their code; regenerate goldens in the same PR |

### 8.2 Order (so main stays green and the backtest keeps a baseline)

1. Build the store, the feed driver, GitHub, Slack and Linear feeds, and the new legs with Onyx still in place (I1 to I4).
2. Run the backtest on both: Onyx arm and store arm, same corpus, same questions. Parity (store arm at least as good on the pass bars of retrieval spec §9.4) is the gate to delete.
3. Switch the default: sources in the catalog are the store's; Onyx stays buildable behind its profile key (one key switches it back) until I8 starts, then it is no longer built.
4. Delete leaf consumers first so each PR builds and tests alone, each taking its own tests and goldens with it: doctor rows and aot-smoke, the UI, API and setup slices and the CLI verbs (keeping the purge stub, written first as U30), then Context's Onyx code, then Sindri Core and Infrastructure's Onyx runtime last, then errors and profile (the reader keeps detecting a leftover `context.sources.onyx` key for `SND-INDEX-007` until M-drop plus one release), then pins, scripts and the setup step, then docs. A box that never ran `onyx up` is untouched at every step.
5. Owner actions, once, listed by `sindri setup`: paste the GitHub and Slack tokens; approve the profile; and **purge the Onyx leftovers**. Onyx's Postgres and OpenSearch volumes hold a raw, unscrubbed copy of the Slack and Linear text, which the new store deliberately does not, so leaving them is the largest exposure this migration leaves behind. `sindri onyx down --purge` (the one Onyx verb that stays, as a human-only, self-contained stub: U30; Q20) lists what it will delete (the containers and volumes carrying the compose project label Sindri recorded at `onyx up`, never matched by name prefix; `$AW_SINDRI_HOME/onyx/` including its generated `.env` with the admin and API secrets; and the keychain entries by the exact names Sindri recorded, none it did not create), asks for the human's yes, then removes them. Sindri never does this on its own. `index:onyx-leftover` (§5.8) is a fail while those exist (a warn once the owner acknowledges them in the profile) and goes away only when nothing is left.
6. **Rollback.** Before I8 (Onyx removal), Onyx is still switchable back on with one profile key and nothing has been dropped: M-add only adds tables and copies nothing. M-drop is the only destructive step; it ships a release after I8 and only when I8 has soaked. If the I5 parity run fails, the owner decides between fixing the store arm and keeping Onyx for the sources the store does not cover (Q4); nothing is deleted until the answer is yes.

**The last Onyx PR passes only when `grep -rli onyx` over the tree, minus history (old specs, git, `docs/superpowers`), finds nothing but four exempt things:** the purge stub and its test, the `index:onyx-leftover` doctor row, the `SND-INDEX-007` text, and the reserved `SND-ONYX-*` lines in `docs/sindri/errors.md` (those lines don't exist today, as the codes live only in `SindriErrors.Onyx.cs`: the errors-and-profile PR deletes that file and adds a "Retired (reserved, never reused)" block listing every `SND-ONYX-*` code to `errors.md`). Files that stay but name Onyx only as an example, such as the `keychain:onyx/sindri-onyx` pointer in `Secrets/SecretPointer.cs` and the secrets docs, get a neutral example in the docs PR, or the grep never passes. The stub's own removal is a later PR, merged once `index:onyx-leftover` is clean on the owner's box (not after a fixed release), and removes the first two exemptions. The §8.1 table is the plan; the grep is the proof, because the first count missed real files: `Dockerfile`, `scripts/sindri-pod.sh` and its test (`--onyx`, `AW_SINDRI_POD_ONYX_PORT`), `scripts/context-first-run.sh`, its test and the `context-first-run.up-dry-run.txt` golden, `scripts/lib/sindri-build-inputs.sh`, `scripts/ci/changed-areas.sh` and its test, `scripts/env-scan-ignore.txt`, `ONBOARDING.md`, `docs/sindri/{OPERATIONS,deploy,README,context,linear,SETUP}.md`, the `docs/*.html` pages, and in Context `Arm.Onyx` (`Core/Eval/Backtest/Arm.cs`), `Arms.NotAvailable`, `LensIntent.cs`, `SourceRef.cs`, `SourcesSection.cs` and `Features/QueryContext/ItemMiss.cs`. Each deletion PR deletes its code **and its tests together** and regenerates its goldens in the same PR (deleted code cannot build while its tests still name it). Bash tests that change with it: `sindri-pod`, `context-first-run`, `ci-changed-areas`, `install-onyx`, `onyx-lock`, `images-lock`. `ISecretWriter` lives in `Aw.Sindri.Core/Onyx/ISecretWriter.cs` today, so it moves out of `Onyx/` (to `Secrets/`, beside `ISecretPointers`) in the first connector PR, before any deletion.

**Before the switch, a usage check.** Onyx also connected Confluence and Drive spaces (`docs/sindri/onyx.md:179`), and the store does not. Phase I5 lists which Onyx sources returned hits in the backtest and in recent sessions; the owner reads that list (Q4) and either accepts the loss or names a connector to add before Onyx goes.

### 8.3 What happens to data Onyx already gave Sindri

Less than it sounds: Sindri never stored Onyx's text (§0). What it stored is in `intent_record`, `intent_link`, `intent_decision`, `intent_edge` and `intent_conflict` (`0006`, `0007`, `0008`, `0009`, `0011`), plus human overrides in `map_override`.

| Stored thing | Key before | Key after | Result |
|---|---|---|---|
| A Linear issue | `linear:issue/<uuid>` | the same | Nothing to do. The native adapter and Onyx's connector already wrote the same key (`LinearRecordKeys.cs`, `OnyxRecordKeys.cs:44-47`). `source` changes from `onyx` to `linear` when the Linear feed first rewrites the row |
| A Slack thread | `slack:<channel>/<ts>` | the same | Nothing to do (§5.3) |
| Other Onyx documents | `onyx:<type>/<id>`, including GitHub as `onyx:github/<id>` | GitHub: `github:issue\|pr/<owner>/<repo>/<n>`; others: none | Onyx's GitHub id "is whatever Onyx gives it" (`docs/sindri/onyx.md:183`), so a mapping is not guaranteed. Rows that can't be mapped become orphaned links (the overlay already reports them, `IntentOverlayResult`); their links are **derived** by rules from record text and are rebuilt from the new GitHub records at the next sync. **Human** decisions on them (overrides naming `record:onyx:...`) can't be re-derived: the migration writes them to a file, `sindri index migrate review` shows each with keep, remap (to a new GitHub key) or drop, and the default for an unanswered one is keep as an orphan, which the overlay already reports (§13, Q8) |
| Repo docs | `doc`, `doc_chunk` | `source_record`, `chunk` | Re-read by the repo-docs feed on its first run (§4.2), scrubbed again; vectors by the embed job |
| Onyx's own index (its Postgres and OpenSearch volumes) | | | Not migrated. It is a derived copy of data the connectors read again. Re-reading is the cost: GitHub about 3,400 calls for the six-month window (§5.3) |

## 9. Packages, tiers and edges

### 9.1 The rules that decide this

Project and compose edges point strictly down the tiers; a tier-1 package cannot reference another tier-1 package; Core and Infrastructure may use tier-0 packages only; another package is reached through its Core only (`deps.yaml:24-60`; `.agents/rules/csharp.md:56,83-85`). So connectors cannot sit beside Context and call its `SourceRecord`, and the code index and Context already meet only in Sindri's Host. The glue pattern exists; this spec uses it.

### 9.2 New packages and edges (`deps.yaml`)

```yaml
packages:
  - id: Embeddings
    kind: package
    tier: 0
    sublevel: 1                      # references Aw.Kernel
    projectPrefix: Aw.Embeddings
    src: src/Embeddings
    note: "the loopback Ollama embed client (digest pin, no proxy, no redirect), the float32 vector codec, L2 normalisation and the top-k scan; domain-free; BCL only (HttpClient, System.Numerics); a tier-0 library, so only project edges point at it"
  - id: Connectors
    kind: package
    tier: 1
    projectPrefix: Aw.Connectors
    src: src/Connectors
    note: "source readers (GitHub, Slack; later Notion, Jira): the IConnector contract, text cleaner, HTTP clients with a host allowlist; a port of ConnectAI's connector mechanics; no storage, no Context types"
edges:
  - { from: Embeddings,   to: Kernel,          kind: project, status: planned, note: "Result, IService, ids" }
  - { from: Connectors,   to: Kernel,          kind: project, status: planned, note: "Result, ids, IService" }
  - { from: Connectors,   to: Redaction,       kind: project, status: planned, note: "response bodies and urls go through it before any error row (§10); with Kernel, KernelTelemetry and Redaction, Connectors.Infrastructure is at the fan-out limit of 4" }
  - { from: Connectors,   to: KernelTelemetry, kind: project, status: planned, note: "logs and spans (codes and counts only, never record text)" }
  - { from: CodeIndex,    to: Embeddings,      kind: project, status: planned, note: "phase I6, blocked: Aw.CodeIndex.Infrastructure already has 4 project references, so this needs a time-boxed fan-out exception or a dropped reference; until then CodeIndex keeps its own copy and the two diverge by design" }
  - { from: Sindri,       to: Embeddings,      kind: project, status: planned, note: "Host only: binds Context's vector ports (IQueryEmbedder, IVectorScan) to Aw.Embeddings with its own registrations (a compose edge into tier 0 is illegal, DEPS-COMPOSE-001), as it binds ISymbolVectors" }
  - { from: Sindri,       to: Connectors,      kind: project, status: planned, note: "Host only (Aw.Sindri.Features already has 4 project references, the limit): the Host adapter maps IConnector onto Context's RecordFeed" }
  - { from: Sindri,       to: Connectors,      kind: compose, status: planned, note: "Host calls AddConnectorsFeatures and AddConnectorsInfrastructure and nothing else from them" }
  - { from: SindriUi,     to: Sindri,          kind: http,    status: current, note: "unchanged; the setup flow's API routes change (Onyx routes removed, source connect routes added)" }
```

Context does **not** reference `Embeddings`: `Aw.Context.Infrastructure` already has four in-repo project references and the limit is `projectFanOut: 4` (`deps.yaml:92`; Infrastructure is not exempt), and `Aw.Context.Features` may use `Aw.Kernel` among tier 0 and nothing else (`deps.yaml:37-60`, ARCH-DIR-005). So the query embedder and the scan are **ports in `Aw.Context.Core`** (`IQueryEmbedder`, `IVectorScan`, the way `ISymbolVectors` is a Core port bound in `ContextFeaturesServiceCollectionExtensions.cs:95`); the cache that loads rows from SQLite is in `Context.Infrastructure` and hands the matrix to the port; the Host binds the ports to `Aw.Embeddings`. `Aw.Connectors.Infrastructure` uses Kernel, KernelTelemetry and Redaction (tier 0) and is then at the fan-out limit. The pointer grammar (`env:`, `file:`, `keychain:`, `op:`) is private to `LinearSettings` today (`LinearSettings.cs:17`); `GitHubSettings` and `SlackSettings` in `Connectors.Core` carry their own copy of the grammar with a parity test against `LinearSettings` (a shared Kernel helper is a later clean-up). Packages `Context` and `Sindri` keep their present edges (`deps.yaml:421-436,507-514,629-636`). No package gains a new edge to `Context`. Nothing in `deps.yaml` names Onyx (its executables are external, as the managed-Onyx spec §9 notes), so removal edits no edge.

Why not put the connectors inside Context, as Linear and Onyx sit today (`Aw.Context.Infrastructure/Linear`, `/Onyx`)? That works under the tier rules with no adapter and keeps one contract (`IContextSource`). It was weighed and not chosen because Context is the retrieval engine and 58 of its files already mention Onyx (`grep -rli onyx src/Context`, 2026-10-27), and the owner asked for ConnectAI's code to be pulled out into something Sindri owns, which a package with its own contract and tests makes visible and reusable (a recipe block can use `Connectors` without Context). The cost is the adapter in the Host (one small class per connector) and two places to read when tracing a record. Q5 puts the choice to the owner (a decision on structure; the answer changes no behaviour).

### 9.3 Where the code goes (by the C# rules)

| Package | Layer | What |
|---|---|---|
| `Aw.Embeddings` | library | `OllamaEmbedder`, `VectorCodec`, `ScanInProcess` (implementing Context's `IQueryEmbedder` and `IVectorScan` ports through small adapters in the Host); fakes and contract tests in `Aw.Embeddings.Testing`; tests in `Aw.Embeddings.Tests` |
| `Aw.Connectors.Core` | Core | `IConnector`, `ConnectorPage`, `ConnectorItem`, `TextCleaner`, the profile records `GitHubSettings`, `SlackSettings` (a secret pointer and scope lists, with `Create` that validates, as `LinearSettings` does) |
| `Aw.Connectors.Features` | Features | `PullGitHub`, `PullSlack` slices (a request, a service, a fake client in tests) |
| `Aw.Connectors.Infrastructure` | Infrastructure | the HTTP clients, wire records, source-generated JSON context |
| `Aw.Context.Core` | Core | `IRecordStore`, `RecordBatch`, `RecordFeed`, `IQueryEmbedder`, `IVectorScan`, `Chunker`, `RankFeatures` additions, `ReachSignal` |
| `Aw.Context.Features` | Features | `SyncSources` rewritten around feeds, `EmbedRecords` (through the Core ports), the search legs, `BuildMap` reading `IRecordStore` |
| `Aw.Context.Infrastructure` | Infrastructure | `SqliteRecordStore` (M-add), `VectorMatrixCache`, the Linear feed |
| `Aw.Sindri.Host` | Host | the adapter `ConnectorFeed : RecordFeed` over `IConnector`; `sindri index connect\|sync\|status\|purge` |
| `Aw.Sindri.Features` | Features | the `index-sync` and `index-embed` built-in services (the shape of `code-index-embed`) |

## 10. Security and trust

- **Trust is unchanged** (retrieval spec §7): record text, titles and authors are `untrusted` and fenced on egress; stored text is scrubbed first. A new stored-text surface is why Q7 asks the owner's yes before the first line of code.
- **Egress.** Nothing leaves the machine except the connectors' own requests to their tool's API, over `https` to an exact-match allowed host (the forge adapter's rule, `src/Forge/Aw.Forge.Core/Ports/IForge.cs:9`), and the loopback embed call. Every connector client sets `AllowAutoRedirect = false` and `UseProxy = false` (as `LinearHttp.cs:6` does) and treats a 3xx as an error; the scheme and host of every follow-up url (a `Link` header's `next`, a file or permalink url) are compared with the allowlist before the request is sent, and the Authorization header is never sent anywhere else. A GitHub Enterprise host gets its own token pointer and the same host and redirect rules; it never shares the github.com token. A fake server per connector asserts each of these.
- **Callers.** Reads are any caller; `index sync` is any caller (it runs as the engine, as `sources sync` does); `index connect` writes a secret and is human-only; `index purge <source>` deletes a source's rows and vectors and is human-only (`SND-AUTH-002` for a session); a sync refills what the source still holds, but not what it has since deleted or aged out, so it is not reversible. Caller rules, one table, a test per row: `index sync`, status, reads: any caller; `index connect`, `index purge`, `onyx down --purge`, `onyx up` (until removed): human only.
- **Prompt injection in a record** is the existing threat (S-H3), larger now: the text includes GitHub comments (anyone on a public repo) and Slack threads (anyone in the workspace, bots and guests), which agents read as search results. On egress it is fenced as untrusted, as today, and each chunk's record carries its author's relation to the org where the source says (member, outside collaborator, bot, guest): an outside author's text, and any author whose relation the source does not give (Linear has none), is fenced with a stronger "unverified third party" label. "Record text never reaches a model on ingest" (§2, §5.7) is true of this index; the optional model linker (retrieval spec D15) does read record text, stays off by default and, when on, reads member-authored records that contain no outside-authored text (a thread quoting an outsider is skipped). An egress test plants an injection string in a Slack thread and in an outside PR comment.
- **Direct messages and unlisted channels are never read**: the Slack connector refuses a channel not in the profile before sending a request (`SND-INDEX-006`, a test with a fake Slack that fails the run on any other route). The allowlist is keyed by Slack channel **id**, with the name for display, so a renamed channel or a different channel reusing a name needs a new approval.
- **Disk.** The store holds more private text than any file Sindri has held; it and its `-wal` and `-shm` are `0600` in a `0700` directory, and `sindri doctor` warns when any mode or any parent directory is wider (the check the three SQLite files get today). Test fixtures never copy the real database.
- **Tokens.** A `file:` pointer wider than `0600` is refused (a Sindri code, no echo of the path's contents); `keychain:` is preferred; `index connect` reads the token without echo and passes it only in the Authorization header. Response bodies go through `Aw.Redaction` before they reach an error row, and HTTP spans capture no bodies; the planted-token test includes a fake server that reflects the token back.

## 11. Proof

- **Unit:** `Chunker` (windows, overlap, dense text, multi-byte at a cut: the cases ConnectAI's tests cover, `connections/src/connectors/__tests__`), `TextCleaner`, the cursor boundary rule, the reach and state and freshness scores, fusion with the new legs, the scope mask inside the scan (a session with three allowed records gets three, not zero).
- **Contract:** `IRecordStore`, `IConnector` and `IEmbedder` each get a fake and a contract suite the real adapter also passes (the repo's rule, `.agents/rules/csharp-testing.md`).
- **Integration:** `SqliteRecordStore` on temp files: M-add run on a copy of a database made by `0015` (new tables exist and are empty, `doc`, `doc_chunk`, intent tables and old `source_cursor` rows untouched; the first docs refresh fills `source_record` and `chunk` with real hashes and token counts); a page commit is atomic (a kill between any two of record, chunks, `intent_record` and cursor: none of them moves); a purge, tombstone and re-chunk leave zero orphan `embedding` rows; a `#` in a doc path round-trips between chunk key and node key; a replayed page writes nothing; a ruleset change marks rows outdated and a re-pull fixes them; FTS and a dense query agree on a planted record.
- **Fakes:** a fake GitHub server and a fake Slack server (`Aw.Connectors.Testing`), the way `FakeOnyxServer` is built today; a fake Ollama.
- **Heavy (nightly):** real Ollama with the pinned model: dense leg ranks a known near-duplicate first; the measured numbers of phase I0 (scan p95 at 50,000 and 250,000 synthetic chunks, embed throughput, store size).
- **Backtest (the gate for deleting Onyx, §8.2):** the existing arms plus a `store-lexical` arm (words and graph only), a `store` arm (plus dense) and a `store+features` arm, on the owner's three Linear projects' corpus and questions (retrieval spec §9) **plus a new question set whose answers live only in GitHub issues and PRs and in Slack** (at least 15 questions the owner writes or approves), because Linear never needed Onyx and a Linear-only corpus cannot show GitHub or Slack are covered. **The bar:** the `store` arm must meet every pass bar of retrieval spec §9.4 (`:800`; the numbers are that spec's: recall, precision at 5, latency and token figures) and be no worse than the Onyx arm by more than 0.02 on each ratio metric, and the GitHub/Slack question set must pass at least as many questions on the store arm as on the Onyx arm (both arms run it; Onyx holds the owner's GitHub and Slack today); if I3b is skipped, the Slack questions are left out of the bar and that loss is the owner's explicit acceptance (Q4, Q16). **Dense and the new signals ship on only if their arm beats `store-lexical` or `store`, respectively, on the same bars**; otherwise they stay off and the lexical store ships. The report prints, per arm, the numbers, the share of tracker records left unlinked (input to Q10), and which Onyx sources returned hits (§8.2).
- **Setup proof:** `./setup.sh --dry-run` shows no Onyx step; `scripts/check-env-example.sh`, `node scripts/check-deps.mjs`, `scripts/dotnet-verify-packages.sh` pass; `sindri doctor` lists the new rows (`index:store`, `index:embeddings`, `index:github`, `index:slack`, `index:linear`, and `index:onyx-leftover` until it is clean) and no `onyx:stack` or `context:onyx`.

## 12. Phases and parallel plan (for /specToProvenPR)

Lanes: api, engine, context, ui, ideas. Builders are Sonnet, in parallel, one project each (a builder does the one light build of its project; leads run the gates). Every phase's PR carries its own tests. PR counts for I1 to I5, I8 and S7 are the sum of the unit rows in §12.1 (a unit is one PR; a few touch two projects, noted); I6 and I7 are sized when they are scheduled.

| Phase | What | Needs | Lane | PRs |
|---|---|---|---|---|
| **I0: measure** | **Box measurements done 2026-10-27** (scan p95, embed rate, `truncate`, peak working set: results in §4.4, §5.6 and §12.2; still open: the Slack probe, the owner's real record counts inside the window and the time to embed them, the sqlite-vec check, and the CPU-only embed rate). A throwaway harness, never merged: the scan's p95 at 50,000 chunks and at 3 times the owner's real count on this box (float32; int8 only if float32 misses); `serve`'s resident size; Ollama `truncate: false`; embed throughput; sqlite-vec's load on the bundled `e_sqlite3` and under NativeAOT; one Slack `conversations.history` call with the owner's token to read its page cap and `Retry-After`; the owner's real record counts **inside the six-month window** (D12) from the Linear and GitHub list endpoints, and the time to embed them. Results go into §4.4 and §12.2. **Gate:** I4's dense half and the model choice (Q6) wait for it; the first embed of the real window must fit in one hour, else the smaller model, then a shorter default window (§5.6); I1 to I3 do not. A one-day task | nothing | context | 0 (a spec amendment) |
| **I1: store, port, ports** | M-add, `SqliteRecordStore`, `IRecordStore`, `RecordBatch`, `Chunker`, the repo-docs feed; `IQueryEmbedder` and `IVectorScan` ports. The `IRecordStore` fake and contract land first so I2 and the ideas lane start on it | nothing | context | 6 |
| **I1e: `Embeddings`** | The tier-0 package after its scaffold: `OllamaEmbedder`, `VectorCodec`, `ScanInProcess`; Host adapters to Context's ports | the scaffold unit | engine | 3 |
| **I2: feed driver and Linear** | `RecordFeed`, `SyncSources` rewritten, the Linear feed with issue state, `index-sync` service, `sindri index sync\|status` and the progress surface (§5.8) | I1 | context | 4 |
| **I3: GitHub** | `Connectors` package, GitHub client, settings, the Host adapter, `sindri index connect github`, Permalinks host, `ISecretWriter` moved out of `Onyx/` | I1 (the port), fake server | api | 6 |
| **I3b: Slack** | The Slack client, settings, `index connect slack`. Runs as an internal app in the owner's workspace (Q16). It starts once the owner has created the app and named the channels; I0's probe confirms the normal rate limit, and if it shows the 1-a-minute throttle instead, Slack goes back to the owner before I3b builds | I3, I0 | api | 1 |
| **I4a: lexical leg** | Lexical leg over the store with the scope mask and audience table, the degraded-answer wording, backtest `store-lexical` arm | I1, I2 | context | 3 |
| **I4b: dense leg and embed job** | `EmbedRecords`, `index-embed`, `VectorMatrixCache`, dense leg with the floor, `store` arm | I1e, I4a, I0 | context | 3 |
| **I4c: graph signals** | Seed leg, `Reach`/`State`/`Freshness`, `FeatureWeights`, `store+features` arm. Ships on only if its arm wins (§11) | I4a | context | 1 |
| **I5: parity run, usage check, switch** | The parity run on the bar in §11 (the brief source from the store is U17b, in I4a); the Onyx-source usage list; the catalog's default switches, with Onyx still buildable behind its profile key. **The gate to I8** | I2, I3, I4a (I3b, I4b, I4c if built) | context | 2 |
| **I6: `CodeIndex` uses `Embeddings`** | Move `OllamaEmbedder` and `VectorCodec`; optional, can wait | I1e | engine | 1 |
| **I7: node vectors and similar-link proposals** | `space = 'node'`, proposals in map review. Only if I5's unlinked share justifies it (Q10) | I5 | context, ideas | 2 |
| **I8: Onyx removal** | The eleven row groups of §8.1 (U19 to U29), each deleting its code and its tests together, after the `onyx down --purge` stub exists (U30, §8.2 step 5); then **M-drop** (U31) one release later; and the stub's removal (U32), merged only once `index:onyx-leftover` is clean on the owner's box | I5 and the usage-check answer | engine, ui, context, api | 11 + 3 |
| **S7 (map-structure §11)** | Doc records, `context.docs.directoryNames` (profile key, `ProfileDiff`, `check-profile-doc.mjs`, the `RefreshDocsService` change) and the tracker half of #1373 (units U18 and U18b) | S3, I1, the repo-docs feed | ideas | 2 |

**First usable milestone** = I1 + I2 + I3 + I4a, 19 PRs (6 + 4 + 6 + 3), and nothing in it needs Ollama, vectors, Slack or a ranking experiment. Vectors (I4b) follow as soon as I0 has its numbers.

I0, I1 and the scaffold unit start together. I2 starts when I1's port and fake merge; I3 starts the same day against the fake. I4a starts when I1 and I2 merge. I4b needs I0's numbers. I5 waits for the built subset of I2 to I4. I8's PRs are independent after I5 and run in parallel by area; the ui lane's goldens, the engine lane's `src/Sindri/aot-smoke/expected.txt`, `Verbs.cs` and `CommandTable.cs` are the shared files, so those PRs merge in turn. Shared files with the map lane: `BuildMapService.cs` (S7's wiring) and `EdgeRules.cs` (no change here: Q9 adds no type).

### 12.1 Work units (builders, Sonnet, one project each)

Every unit is a red test plus its code in one project, so the lane lead can run many at once. Phase and gates are columns; "gates" are the extra checks that PR must run (§12.3).

| Unit | Phase | What | Project | After | Gates |
|---|---|---|---|---|---|
| U3 | I1 | `deps.yaml` entries for `Embeddings` and `Connectors`, scaffold of both packages and their `.Testing` and `.Tests` projects, `check-deps` pass | repo root | none | deps |
| U5 | I1 | `IRecordStore`, `RecordBatch`, `RecordFeed`, the audience port, `IQueryEmbedder`, `IVectorScan`, fakes and contract suites | `Aw.Context.Core`, `.Testing` | none | arch |
| U6a | I1 | `Chunker` (pure) | `Aw.Context.Core` | none | arch |
| Uprof | I1 | Profile surface: `context.sources.github.{repos,host}`, `.slack.channels[{id,name}]`, `.linear.teams`, `context.index.*` (windowDays, maxChunks, retainDays, floors, acknowledgeOnyxLeftover), `ProfileDiff`, `docs/csharp/sindri-profile.md`, `check-profile-doc` | `Aw.Sindri.Core` | none | profile-doc, re-approval |
| U6b | I3 | `TextCleaner` (pure) | `Aw.Connectors.Core` | U3 | arch |
| U4 | I1 | M-add (next free number), schema, triggers, `SqliteRecordStore` (commit page in one transaction, read, tombstone, secure-delete) | `Aw.Context.Infrastructure` | U5 | integration |
| U7 | I1 | repo-docs feed (implements `RecordFeed`) writing `source_record`/`chunk` | `Aw.Context.Features/Infrastructure` | U4, U5 | integration |
| U1 | I1e | `VectorCodec`, L2 normalise, `ScanInProcess` (float, `Vector<float>`), contract suite for `IVectorScan` | `Aw.Embeddings` | U3 | arch |
| U2 | I1e | `OllamaEmbedder` (loopback guard, digest check, batches, `truncate:false`), fake Ollama | `Aw.Embeddings` | U3 | |
| U2b | I1e | Host adapters binding the Core ports to `Aw.Embeddings` | `Aw.Sindri.Host` | U1, U2, U5 | arch |
| U8 | I2 | Rewritten `SyncSources` driver over `RecordFeed`, phases and cursor columns | `Aw.Context.Features` | U5 | arch, engine tests if notice text moves |
| U9 | I2 | Linear `Pull` + issue state + new list query | `Aw.Context.Infrastructure` | U5 | |
| U14a | I2 | `index-sync` built-in service and the progress surface's data (through Context's ports only; `Sindri.Features` takes no reference to `Aw.Connectors.Core`) | `Aw.Sindri.Features` | U8 | aot-smoke |
| U14b | I2 | `sindri index sync\|status\|purge` verbs (`purge` human-only) | `Aw.Sindri.Host` | U14a | aot-smoke, Ui.Tests (DTOs), env-example |
| U11 | I3 | GitHub client, cursor boundary, rate limit, redirect and host rules, fake server | `Aw.Connectors.Infrastructure`, `.Testing` | U3, U6b | |
| U12a | I3 | GitHub settings, `PullGitHub` slice | `Aw.Connectors.Core/Features` | U11 | arch |
| U12b | I3 | Host adapter over `IConnector` (the only reference to `Aw.Connectors.Core`), `index connect github` | `Aw.Sindri.Host` | U12a, U12c, U12d, U5, Uprof | aot-smoke, arch |
| U12c | I3 | Permalinks: add the GitHub and Slack hosts | `Aw.Context.Core` | none | arch |
| U12d | I3 | `ISecretWriter` moved from `Aw.Sindri.Core/Onyx/` to `Secrets/` | `Aw.Sindri.Core` | none | arch |
| U13 | I3b | Slack client, thread rules, rate limit, channel-id allowlist, fake server, `index connect slack` | `Aw.Connectors.Infrastructure` | U11, U12b | |
| U15a | I4a | Lexical leg, scope mask with the audience table, degraded wording | `Aw.Context.Features/Infrastructure` | U4, U5, Uprof | integration |
| U17a | I4a | Backtest `store-lexical` arm: the arm enum and `Arms` | `Aw.Context.Core` | U15a | |
| U17b | I4a | Backtest brief source from the store | `Aw.Context.Infrastructure/Eval` | U15a | |
| U10a | I4b | `EmbedRecords` job (guarded insert, §4.2), `VectorMatrixCache` (§4.4) | `Aw.Context.Features/Infrastructure` | U2b, U4 | integration |
| U10b | I4b | `index-embed` built-in service, `sindri index embed [pause\|resume]` | `Aw.Sindri.Features` | U10a | aot-smoke |
| U15b | I4b | Dense leg with the floor; `store` arm | `Aw.Context.Features` | U10a, U17a | |
| U16 | I4c | Seed graph leg, `Reach`, `State`, `Freshness`, weights; `store+features` arm | `Aw.Context.Core` | U15a | arch |
| U18 | S7 | `MapRecordSources`, build reads the store, citation counts, `touches` edges, record-stamp line | `Aw.Context.Features` | U5, U7, S3 | arch |
| U18b | S7 | `context.docs.directoryNames` and the `RefreshDocsService` change | `Aw.Sindri.Core`, `Aw.Context.Features` | U7, Uprof | profile-doc |
| U31 | I8 | M-drop and the retirement of `IDocIndex`/`SqliteDocIndex` and the search fallback to the old tables | `Aw.Context.*` | U19 to U29 and one release | integration |
| U32 | I8 | Remove the purge stub and its test, and the `index:onyx-leftover` row | `Aw.Sindri.Host` | `index:onyx-leftover` clean on the owner's box | aot-smoke |
| U33 | I5 | Catalog default switches to the store (Onyx stays buildable behind its profile key); the Onyx-source usage list | `Aw.Context.Infrastructure` | U17b, U15a | integration |
| U34 | I5 | The parity run and its report (bar of §11, GitHub/Slack set, unlinked share, node count) | `Aw.Context.Infrastructure/Eval` | U33 | |
| U30 | I8 | The `onyx down --purge` stub: a self-contained verb (direct `docker` calls matched by the recorded compose project label, keychain entries by exact recorded names), written before the Onyx runtime code is deleted | `Aw.Sindri.Host` | I5 | aot-smoke |
| U19 to U29 | I8 | Eleven Onyx-removal PRs, one per §8.1 row group (CLI; API and setup; Sindri Core and Infra; UI; Context; doctor and aot-smoke; errors and profile; pins, scripts and setup; docs; tests and goldens that survive the others; other tree references: `Dockerfile`, `ONBOARDING.md`, CI scripts and fixtures), each with its own tests and goldens, in the leaf-first order of §8.2 step 4 | per area | U30, I5 | aot-smoke, Ui.Tests, Engine tests, bash tests |

U3, U5, U6a and Uprof start on day one (they touch different projects and none depends on another): four builders; U3 is the first merge because `Aw.Embeddings` and `Aw.Connectors` do not exist until it lands (`ls src`), so U1, U2, U6b and U11 wait on it. U4 follows U5. U9, U11 and U16 start once their "After" merge. The lane leads run the gates; builders do the one light build of their project.

### 12.2 Box cost, in one place

| What | Number | Where it comes from |
|---|---|---|
| Disk, Onyx removed | frees up to 11.26 GB (`context/onyx.lock:61`): the volumes by `onyx down --purge` (§8.2), the images by `docker image prune` (the verb does not touch images) | cited |
| Disk, store | 0.15 GB at 25,000 chunks, 0.3 GB at 50,000, 1.6 GB at 250,000 (§4.4) | arithmetic |
| Memory, Onyx removed | frees 5.9 GB of Docker memory (`context/onyx.lock:62`) | cited |
| Memory, store | 146 MB (50,000 chunks, float32) to 732 MB (250,000) for the vector matrix in `serve`; int8 above 100,000 chunks is a quarter. The I0 harness peaked at 1.5 GB working set | arithmetic, harness peak measured in I0 |
| First embed (GPU box) | 122 chunks a second: 3.4 minutes for the six-month window's ~25,000 chunks, 33 seconds for the newest 4,000; the one-hour bar is met | measured in I0 |
| First embed (CPU-only box) | not measured; the old estimate was 0.8 to 4 chunks a second, 1.7 to 8.7 hours for ~25,000 chunks; still gated by the one-hour bar, else a smaller model (§5.6) | derived from a cited figure (§5.6) |
| CPU, a query | about 18 ms p95 for the scan at 25,000 chunks, 36 ms at 50,000, 141 ms at 150,000, 235 ms at 250,000 (over the 150 ms budget), plus one query embed (tens of ms to a second on CPU) | measured in I0 (25,000 scaled from 50,000) |
| Network | GitHub about 3,400 calls for the six-month window (§5.3), about 40 minutes at the rate limit; Slack, an internal app (Q16), about 50 requests a minute, confirmed by I0's probe | cited and arithmetic |
| Model on ingest | none (§5.7) | decision |

### 12.3 Gates and errors

Gates that stay sequential: any PR that edits a recipe or the shipped catalog runs the Sindri integration lane; any PR that changes a doctor row or adds a verb updates `src/Sindri/aot-smoke/expected.txt` and runs aot-smoke; any PR that changes notice or banner text runs `Aw.Sindri.Engine.Tests`; any PR that changes a DTO, the setup flow or a golden runs `Aw.Sindri.Ui.Tests`; any PR that adds a `using Aw.` or a Core sub-namespace runs `Aw.Architecture.Tests`; any new environment variable goes in `.env.example` (`scripts/check-env-example.sh`); any new profile key (`context.index.maxChunks`, `context.index.retainDays`, `context.sources.github.repos`, `.host`, `context.sources.slack.channels`, the vector floor keys) goes in `docs/csharp/sindri-profile.md` and passes `scripts/tests/check-profile-doc.test.sh`, and the owner re-approves the profile once. I0 names no new environment variable; the Ollama URL is the code index's existing one.

New error codes (each gets a row in `docs/sindri/errors.md` and a test; retired `SND-ONYX-*` codes stay reserved and are never reused):

| Code | Meaning | Exit | Fix line |
|---|---|---|---|
| `SND-INDEX-001` | A token pointer does not resolve, or a `file:` pointer is wider than 0600 | 2 | `sindri index connect <source>` |
| `SND-INDEX-002` | A listed repo or channel is unreadable with this token | 1 | widen the token or fix the profile |
| `SND-INDEX-003` | A source is rate limited (carries the reset time) | 1 | wait; automatic |
| `SND-INDEX-004` | The embed model's digest differs from the pin | 1 | `scripts/install-deps.sh` |
| `SND-INDEX-005` | Ollama is unreachable (meaning search off) | 1 | start Ollama |
| `SND-INDEX-006` | A channel or host is not in the profile; no request was sent | 2 | add it to the profile and approve |
| `SND-INDEX-007` | The profile still names Onyx | 2 | remove `context.sources.onyx` and re-approve |
| `SND-INDEX-008` | A cursor was reset by the source; a bounded re-read follows | 1 | none; automatic |

## 13. The owner's answers (2026-10-27)

The owner answered every question on 2026-10-27, in their words: "yes only those 3, yes store record text, yes slack is our workspace i can make an app. yes rewrite in C#, it shouldn't take hours for first embedding, it should be time driven like 6 months in the past as a config option like connectai does, yes skip ai summaries during ingest, everything. take all the others". The numbers are the ones revision 1 asked under.

### Answered

| # | Question | Answer | What it changes |
|---|---|---|---|
| 3 | GitHub issues as real records? | **Yes** | Kinds `issue` and `pr`, with state and links, searchable and in the map. Never turned into a Sindri item automatically; `sindri item add` stays the only way |
| 4 | Which sources? | **GitHub, Slack and Linear only** | Confluence, Drive, Notion and Jira are out. I5's usage list is still printed before Onyx goes, but it can no longer add a source |
| 7 | Store scrubbed record text? | **Yes** | D3 as written: scrub then store, `blocked` records kept without text, no retention limit by default (`context.index.retainDays` exists) |
| 8 | When does Onyx go? | **Yes, as recommended** | After the parity backtest and I5's usage list (§8.2). Human decisions on `onyx:` keys go to a review file |
| 16 | Slack: which app? | **An internal app the owner creates in their own workspace** | Listed channels only, by id, never a DM; private channels only if listed and the bot is a member. I0 keeps its one-call probe to confirm the normal rate limit (about 50 a minute). I3b is no longer conditional, unless that probe shows the 1-a-minute throttle anyway |
| 17 | Copy any ConnectAI file? | **No: rewrite in C#** | Reimplemented, nothing copied |
| 18 | How much to embed, and how long? | **Changed: bounded by time, not hours** | D12: `context.index.windowDays`, default 183 (six months back). Only records updated in the window are synced and embedded. `maxChunks` stays only as a safety ceiling. §5.6 re-estimates the window (about 25,000 chunks) and makes "under one hour" an I0 gate |
| 19 | Any model call on ingest? | **None** | D11 as written |
| 20 | May `onyx down --purge` delete Onyx's volumes and files? | **Yes** | Lists everything first, the owner's yes, never automatic |
| 21 | Accept the reversals at the end of §1? | **Yes** | All of them, with the company sign-off kept |

### Accepted as written

Every "decided unless you object" item is accepted as revision 1 wrote it ("take all the others"):

1. The index goes into the same `context.db`, and splits to `index.db` only past about 5 GB.
2. Vector search is an in-process scan with a 150 ms budget, decided by I0's measurement.
5. A new tier-1 `Connectors` package.
6. The embedding model is `nomic-embed-text`. A smaller model is the first fix if I0 misses the one-hour bar (§5.6).
9. Record-to-record edges are `mentions` with a detail.
10. No node vectors or similar-link proposals (I7) unless I5's unlinked share passes 20%.
11. Reach, state and freshness ship only if they beat the five-feature backtest arm.
12. Six state words.
13. `intent_record` and `source_record` both stay in v1.
14. Code vectors stay in the code index.
15. A profile that still names Onyx after removal is an error for one release.

## 14. Where the code differs from what you'd assume

1. **Sindri keeps no Onyx data to migrate.** The intent tables store keys, titles and links, never text (`0006_s5b_intent_links.sql:2`). Onyx's index is a copy of what the tools hold. "Migrating Onyx" is a key mapping (§8.3) and a re-read, not a data copy.
2. **The source contract is already a feed in name only.** `IChangeFeed` exists (`IChangeFeed.cs:6`) and nothing implements it; `IntentSyncRun` finds records by searching with map terms (`IntentSyncRun.cs:134`). The new driver is a different design, not an activation of the old one.
3. **Linear is further along than ConnectAI's.** Sindri's native adapter reads projects, documents, updates, attachments and relations (`LinearIssue.cs:3-17`, `Operations/`); ConnectAI's reads issues and comments (`linear/SPEC.md`, `transport.ts`). The one gap is issue state: the query asks for it (`Issue.graphql:4`) and the record drops it. So Linear is the one connector that is **not** ported.
4. **ConnectAI keeps no structured records.** Its own rule: "ConnectAI deliberately does not own a normalized per-vendor record mirror" (`CLAUDE.md:16`). Its GitHub connector turns a PR's state, priority, blockers and counts into one synthesized sentence (`github-connector.ts:87-96`) and keeps only that sentence, the raw text windows and a hash of the participants (`:155-167`). The structured `source_record` and `record_link` tables are new design; what is ported is the fetching, the paging, the cursors and the chunker.
5. **ConnectAI's ranking writes on read.** Its recency re-rank bumps an access count on every retrieved chunk (`actr-rerank.ts`, "WRITE-BACK"). A ranking that changes with how often you asked can't be backtested, so it is not ported (§5.1).
6. **The code index's vector loop is too slow for this.** It is a scalar loop in `double` with three accumulators per pair (`VectorCodec.cs:42-50`), fine for one repo's symbols and likely too slow for 250,000 chunks (§4.4; I0 measures it). The store's scan is new code in `Embeddings`.
7. **Tier rules, not taste, decide the package split.** A tier-1 package can't reference another tier-1 package and Core and Infrastructure may use tier 0 only (`deps.yaml:24-60`), so `Connectors` can't speak Context's types; Sindri's Host is the glue, as for the code index (`ContextFeaturesServiceCollectionExtensions.cs:95`). The embedder is tier 0 so the code index and the store share one client; Context reaches it only through ports the Host binds.
8. **Docker stays on the box.** Serena runs in a container (`setup.sh:268-290`), so removing Onyx does not remove Docker from setup. What goes is Docker as a requirement of Sindri.
9. **The backtest still depends on Onyx.** The eval arms read Onyx for the intent brief (`Eval/OnyxBriefSource.cs`, `Eval/Backtest/Arms.cs`), which is why parity is run before deletion (§8.2) and not after.
10. **A doc's key and a node's key are already one thing.** Repo docs are `repo:<repo>/<path>` and their node `record:repo:<repo>/<path>` (`RepoDocs.cs:17`, `IntentOverlay.cs:123`). "Ids shared with the graph" is mostly extending that rule to chunks and vectors, not a new scheme.
11. **Slack keys already match.** Onyx's Slack connector writes `slack:<channel>/<ts>` (`OnyxRecordKeys.cs:48-52`); our connector writes the same, so stored Slack links need no migration. GitHub is the one that doesn't map (§8.3).
12. **Permalinks allow one host today.** `https` on `linear.app` (`Aw.Context.Core/Trust/Permalinks.cs:14`); a result's GitHub and Slack links are dropped until I3 adds their hosts.
13. **Slack's reader was read in code, and it is rate-limited by design.** The inventory has Slack as spec-level only. Its transport (`slack/transport.ts`) is 304 lines and assumes Slack's non-Marketplace limits, 1 request a minute and 15 messages per call (`:13-15,25-26`). Whether that binds an app the owner creates in their own workspace is unverified (§5.3, Q16).
14. **ConnectAI re-embeds a whole item whenever it changes, with no hash check.** `clearEpisodeChunks` then re-insert (inventory §3 step 5, a guess that unchanged items are skipped only because incremental reads return changed ones). Sindri's `text_hash` per record and per chunk is new (§4.2) and is what makes a reconcile run cheap.
15. **ConnectAI's lexical leg ANDs every term.** `plainto_tsquery` needs all words, so a long question finds little (inventory §5). Sindri's `OR` with `bm25` is a deliberate change (§6.1).
16. **Embed speed was quoted for symbols, not chunks.** The code index's "hour or two" is for short symbols; a 400-token chunk is far longer, so the first full embed is slower per item than the code index's figure suggests (§5.6).
17. **ConnectAI's GitHub reader cannot link anything.** No comment text, no review text, no cross-references, no permalink in the breadcrumb (`github-connector.ts:11`, `github/SPEC.md` §5); its `/pulls` list is asked for change counts it probably does not return (`transport.ts:340-343`, inventory open question 2). Every link in §7.5 is new work, not a port.
18. **Features cannot reach the embedder, and Context cannot either.** `Aw.Context.Features` may use `Aw.Kernel` among tier 0 only, and `Aw.Context.Infrastructure` is already at the fan-out limit of 4 (`deps.yaml:92`), so Context gets ports in Core and the Host binds them to `Aw.Embeddings` (§9.2). An earlier draft had Context reference `Embeddings` directly; that would have failed `DEPS-FANOUT-002`.
19. **The existing cache pattern does not fit.** `StampedCache` reloads a whole value when its stamp moves, and the existing stamp moves on every graph write (`0008`, `0009`); an embed job commits for hours, so the vector cache is append-only on its own `record_stamp` (§4.4).
20. **`doc_chunk` has two triggers, not three.** Docs are deleted and re-inserted, so it has no update trigger (`0001_s1a_repo_docs.sql`); `chunk` is rewritten in place and needs one (§4.2).
21. **Migration numbers are not ours to pick.** `ctx-1365-s5` already has a `0016`; the spec says "next free number at merge" (§4.2).
