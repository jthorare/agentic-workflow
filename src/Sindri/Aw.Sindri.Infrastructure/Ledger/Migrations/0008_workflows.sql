-- Workflow graphs and their revisions (issue 2258, PR 5). The source of truth for what a workflow is, so they live in
-- ledger.db and are not rebuilt from events. A save is a new immutable revision; 'active_revision' (null = off) is the one
-- triggers run; 'name' is the latest revision's. Run tables follow in 0009 and 0010.
CREATE TABLE workflow (
    id TEXT PRIMARY KEY,
    slug TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    active_revision INTEGER,
    archived_at TEXT,
    created_by TEXT NOT NULL
);

CREATE TABLE workflow_revision (
    workflow_id TEXT NOT NULL REFERENCES workflow (id),
    revision INTEGER NOT NULL,
    spec TEXT NOT NULL,
    spec_hash TEXT NOT NULL,
    created_by TEXT NOT NULL,
    created_at TEXT NOT NULL,
    PRIMARY KEY (workflow_id, revision)
);
