-- Test runs (Workflows page spec 2026-11-12 §3.1, §3.2; P4a): a run row says whether it was live or a test and how the test ran, and pinned step outputs get a table.
-- Existing rows are live. A pin is keyed by workflow, revision and node; taken_on_revision is the revision it was first taken on (the carry-forward rule keeps it when a pin moves to a later revision).
ALTER TABLE workflow_run ADD COLUMN mode TEXT NOT NULL DEFAULT 'live';
ALTER TABLE workflow_run ADD COLUMN test_options TEXT;
CREATE TABLE workflow_pin (
    workflow_id TEXT NOT NULL,
    revision INTEGER NOT NULL,
    node_id TEXT NOT NULL,
    output TEXT NOT NULL,
    taken_on_revision INTEGER NOT NULL,
    pinned_by TEXT NOT NULL,
    run_id TEXT NOT NULL,
    pinned_at TEXT NOT NULL,
    PRIMARY KEY (workflow_id, revision, node_id)
);
CREATE INDEX workflow_run_mode ON workflow_run (workflow_id, mode);
