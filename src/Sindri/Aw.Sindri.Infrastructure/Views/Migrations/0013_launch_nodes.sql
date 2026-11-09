-- Graph node launches (issue 2258, PR 7): the launch row's step carries the run and node a 'run_agent' launch belongs to, and these virtual columns let the
-- readers (the idle sweep, the health count) look a node's launches up by them. Schema version 30 (ViewSchema.Version): an older views.db gains the columns here
-- and the projector rebuilds the launches view from the ledger, because the version differs. Numbered after 0012_workflow_runs (no gaps, AW-DATA-005).
ALTER TABLE view_launches ADD COLUMN run_id TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.step.run')) VIRTUAL;
ALTER TABLE view_launches ADD COLUMN node_id TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.step.node')) VIRTUAL;
CREATE INDEX view_launches_run ON view_launches (run_id, node_id);
