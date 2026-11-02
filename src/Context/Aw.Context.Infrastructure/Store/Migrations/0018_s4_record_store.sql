-- Stage I1 (U4): the Sindri-owned record store (index spec §4.2). Source records, their links, chunks with a full-text index, vectors and the counters
-- the vector cache watches. It creates the tables and copies nothing: `doc` and `doc_chunk` stay until the repo-docs feed has re-read the docs.
-- Numbered 0018 because 0016 is the roll-up count and 0017 belongs to the map checks (S5).
CREATE TABLE source_record (
    key          TEXT PRIMARY KEY NOT NULL,
    source       TEXT NOT NULL,
    kind         TEXT NOT NULL,
    repo         TEXT NULL,
    audience_key TEXT NULL,
    title        TEXT NOT NULL,
    url          TEXT NULL,
    author       TEXT NULL,
    state        TEXT NULL CHECK (state IS NULL OR state IN ('open', 'closed', 'merged', 'draft', 'canceled', 'done')),
    state_raw    TEXT NULL,
    created_at   TEXT NULL,
    updated_at   TEXT NULL,
    closed_at    TEXT NULL,
    version      TEXT NOT NULL,
    text_hash    TEXT NOT NULL,
    ruleset      TEXT NOT NULL,
    blocked      INTEGER NOT NULL CHECK (blocked IN (0, 1)),
    removed_at   TEXT NULL,
    synced_at    TEXT NOT NULL
) STRICT;

CREATE INDEX source_record_by_source ON source_record (source, updated_at);
CREATE INDEX source_record_by_state ON source_record (repo, state, updated_at);

CREATE TABLE record_link (
    from_key TEXT NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    to_ref   TEXT NOT NULL,
    to_key   TEXT NULL,
    kind     TEXT NOT NULL,
    title    TEXT NULL,
    PRIMARY KEY (from_key, to_ref, kind)
) STRICT;

CREATE INDEX record_link_by_to_key ON record_link (to_key);
CREATE INDEX record_link_by_to_ref ON record_link (to_ref);

CREATE TABLE chunk (
    id         INTEGER PRIMARY KEY,
    key        TEXT NOT NULL UNIQUE,
    record     TEXT NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    seq        INTEGER NOT NULL,
    text       TEXT NOT NULL,
    tokens     INTEGER NOT NULL,
    text_hash  TEXT NOT NULL,
    line_start INTEGER NULL,
    line_end   INTEGER NULL,
    UNIQUE (record, seq)
) STRICT;

CREATE INDEX chunk_by_record ON chunk (record, seq);

CREATE VIRTUAL TABLE chunk_fts USING fts5 (text, content = 'chunk', content_rowid = 'id', tokenize = 'unicode61');

-- `doc_chunk` has two triggers because docs are deleted and re-inserted; `chunk` is rewritten in place, so it has the update one too.
CREATE TRIGGER chunk_fts_ai AFTER INSERT ON chunk BEGIN
    INSERT INTO chunk_fts (rowid, text) VALUES (new.id, new.text);
END;

CREATE TRIGGER chunk_fts_ad AFTER DELETE ON chunk BEGIN
    INSERT INTO chunk_fts (chunk_fts, rowid, text) VALUES ('delete', old.id, old.text);
END;

CREATE TRIGGER chunk_fts_au AFTER UPDATE OF text ON chunk BEGIN
    INSERT INTO chunk_fts (chunk_fts, rowid, text) VALUES ('delete', old.id, old.text);
    INSERT INTO chunk_fts (rowid, text) VALUES (new.id, new.text);
END;

CREATE TABLE embedding (
    seq       INTEGER PRIMARY KEY AUTOINCREMENT,
    space     TEXT NOT NULL,
    key       TEXT NOT NULL,
    model     TEXT NOT NULL,
    text_hash TEXT NOT NULL,
    dim       INTEGER NOT NULL,
    vec       BLOB NOT NULL,
    UNIQUE (space, key, model)
) STRICT;

CREATE TABLE store_epoch (
    id           INTEGER PRIMARY KEY CHECK (id = 1),
    delete_epoch INTEGER NOT NULL,
    record_stamp INTEGER NOT NULL
) STRICT;

INSERT INTO store_epoch (id, delete_epoch, record_stamp) VALUES (1, 0, 0);

-- `embedding.key` is polymorphic (a chunk key or a graph node key), so no foreign key can cascade: triggers do.
CREATE TRIGGER chunk_ad AFTER DELETE ON chunk BEGIN
    DELETE FROM embedding WHERE space = 'chunk' AND key = old.key;
END;

CREATE TRIGGER chunk_au AFTER UPDATE OF text ON chunk BEGIN
    DELETE FROM embedding WHERE space = 'chunk' AND key = old.key;
END;

-- Every vector removal, from any cause, moves the epoch the in-memory matrix watches.
CREATE TRIGGER embedding_ad AFTER DELETE ON embedding BEGIN
    UPDATE store_epoch SET delete_epoch = delete_epoch + 1 WHERE id = 1;
END;

-- source_cursor (0001) gains the sync driver's place: the phase and the backfill cursor. Its `repo` is '*' for a source's one position.
ALTER TABLE source_cursor ADD COLUMN phase TEXT NULL;
ALTER TABLE source_cursor ADD COLUMN backfill_cursor TEXT NULL;
