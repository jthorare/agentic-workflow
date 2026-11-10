-- Resources (agent-first PR 14): bookkeeping only; the data is the files under $AW_SINDRI_HOME/resources.
-- 'owner' is workflow:<slug>, node:<slug>/<id> or toolkit:<name>; 'kind' is database, disk, sandbox or connection.
-- 'path' is empty for a sandbox or a connection (they have no files). The number is renumbered at the final main merge.
CREATE TABLE resource (
    owner TEXT NOT NULL,
    kind TEXT NOT NULL,
    name TEXT NOT NULL,
    path TEXT,
    size_limit INTEGER,
    created_at TEXT NOT NULL,
    last_used_at TEXT,
    archived_at TEXT,
    PRIMARY KEY (owner, kind, name)
);
