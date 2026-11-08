-- Workflow runs and their node runs (issue 2258, PR 6). The run id '<slug>@<revision>:<occurrence>' is the primary key, so a
-- re-delivered trigger or a catch-up overlap cannot record a second run. 'status' holds the lowercase status name. 'input' and
-- 'output' of a node run are cut at 64 KB; 'truncated' is 1 when either was.
CREATE TABLE workflow_run (
    id TEXT PRIMARY KEY,
    workflow_id TEXT NOT NULL,
    revision INTEGER NOT NULL,
    trigger TEXT NOT NULL,
    status TEXT NOT NULL,
    outputs TEXT,
    error TEXT,
    item_id TEXT,
    started_at TEXT NOT NULL,
    ended_at TEXT
);

CREATE INDEX workflow_run_by_workflow ON workflow_run (workflow_id, status);

CREATE TABLE workflow_node_run (
    run_id TEXT NOT NULL REFERENCES workflow_run (id),
    node_id TEXT NOT NULL,
    item_index INTEGER NOT NULL,
    status TEXT NOT NULL,
    input TEXT,
    output TEXT,
    truncated INTEGER NOT NULL,
    error TEXT,
    usage TEXT,
    PRIMARY KEY (run_id, node_id, item_index)
);
