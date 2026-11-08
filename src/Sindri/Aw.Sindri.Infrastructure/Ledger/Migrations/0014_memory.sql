-- Memory (agent-first PR 13): named stores of short facts, recalled by meaning. The number is the planned one; the lead renumbers at merge.
-- Text is scrubbed before it is written. 'embedding' is a VectorCodec blob (NULL when no model answered at write time).
CREATE TABLE memory (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    created_by TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE TABLE memory_fact (
    id TEXT PRIMARY KEY,
    memory_id TEXT NOT NULL REFERENCES memory (id),
    text TEXT NOT NULL,
    embedding BLOB,
    tags TEXT NOT NULL,
    workflow_id TEXT,
    run_id TEXT,
    item_id TEXT,
    session_id TEXT,
    expires_at TEXT,
    created_at TEXT NOT NULL,
    refreshed_at TEXT NOT NULL
);

CREATE INDEX memory_fact_by_memory ON memory_fact (memory_id);
CREATE INDEX memory_fact_by_expiry ON memory_fact (expires_at);
