-- Which failed workflow runs the owner has seen (home spec §3.3, E1): a failed live run with no row here is a 'run-failed' row on Home.
-- Every run that already failed counts as seen, so the first start after this migration raises no row for old failures.
CREATE TABLE workflow_run_seen (
    run_id TEXT NOT NULL PRIMARY KEY,
    seen_at TEXT NOT NULL
);
INSERT INTO workflow_run_seen (run_id, seen_at) SELECT id, COALESCE(ended_at, started_at) FROM workflow_run WHERE status = 'failed';
