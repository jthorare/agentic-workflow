-- Idle launches (issue 2786): status counts the sessions that ended idle with no relaunch of their Step, and read every launch ever made to do it. These virtual
-- columns let it look the idle launches up by how they ended, and a Step's launches by its instance. Schema version 34 (ViewSchema.Version): an older views.db gains
-- the columns here and the projector rebuilds the launches view from the ledger, because the version differs. Numbered after 0014_launch_nodes (no gaps, AW-DATA-005).
ALTER TABLE view_launches ADD COLUMN reason TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.ended.reason')) VIRTUAL;
ALTER TABLE view_launches ADD COLUMN instance TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.step.instance')) VIRTUAL;
CREATE INDEX view_launches_reason ON view_launches (reason);
CREATE INDEX view_launches_instance ON view_launches (instance);
