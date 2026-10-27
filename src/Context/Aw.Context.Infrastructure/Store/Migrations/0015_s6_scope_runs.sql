-- Issue 1005: the scoping graph a workflow instance's agent Steps work from, and the context reads their sessions made against it.
-- scope_graph: one row per instance (the newest graph replaces the old); scope_graph_node: its node ids, so a read is checked with one lookup.
-- scope_read: one row per checked read, per instance and run, oldest first by id.
CREATE TABLE scope_graph (
    instance     TEXT PRIMARY KEY,
    map_version  TEXT NOT NULL,
    graph        TEXT NOT NULL,
    at           TEXT NOT NULL
) STRICT;

CREATE TABLE scope_graph_node (
    instance  TEXT NOT NULL REFERENCES scope_graph (instance) ON DELETE CASCADE,
    id        TEXT NOT NULL,
    PRIMARY KEY (instance, id)
) STRICT, WITHOUT ROWID;

CREATE TABLE scope_read (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    instance  TEXT    NOT NULL,
    run       TEXT    NOT NULL,
    node      TEXT    NOT NULL,
    tool      TEXT    NOT NULL,
    outside   INTEGER NOT NULL CHECK (outside IN (0, 1)),
    reason    TEXT,
    at        TEXT    NOT NULL
) STRICT;

CREATE INDEX scope_read_run ON scope_read (instance, run, id);
