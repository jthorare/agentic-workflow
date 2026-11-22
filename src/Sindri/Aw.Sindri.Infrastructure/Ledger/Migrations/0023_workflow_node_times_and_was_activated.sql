-- The Workflows list and run pages (#3683). A node row says when it began and when it ended, so a run page shows seconds per node: the store stamps 'started_at' on
-- the first write of a node (its 'running' row) and 'ended_at' on its final one. Rows written before this have neither. 'workflow.was_activated' is set the first time a
-- revision is pinned and never cleared, so "paused" (was on, now off) differs from "off" (never turned on). Workflows that are off now stay "off": the flag starts
-- from the pinned ones only.
ALTER TABLE workflow_node_run ADD COLUMN started_at TEXT;
ALTER TABLE workflow_node_run ADD COLUMN ended_at TEXT;
ALTER TABLE workflow ADD COLUMN was_activated INTEGER NOT NULL DEFAULT 0;
UPDATE workflow SET was_activated = 1 WHERE active_revision IS NOT NULL;
