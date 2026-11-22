-- A failed run the owner has not opened is a Home row (Home spec 2026-11-12 §3.2, E1); seen_at is when the owner first opened it.
-- Runs that failed before this column existed count as seen, so an upgrade does not put every old failure on Home.
ALTER TABLE workflow_run ADD COLUMN seen_at TEXT;
UPDATE workflow_run SET seen_at = COALESCE(ended_at, started_at) WHERE status = 'failed';
CREATE INDEX workflow_run_failed_unseen ON workflow_run (started_at) WHERE status = 'failed' AND seen_at IS NULL AND mode = 'live';
