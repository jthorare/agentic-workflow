-- The items view's state column (issue 2788): GET /sindri/queue reads only the open items, one lookup per open state, so the state
-- is a virtual column generated from the row's JSON and indexed. Schema version 35 (ViewSchema.Version): an older views.db gains the column
-- here and the projector rebuilds the views from the ledger, because the version differs. Numbered after 0015_launch_idle (no gaps, AW-DATA-005).
ALTER TABLE view_items ADD COLUMN state TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.state')) VIRTUAL;
CREATE INDEX view_items_state ON view_items (state);
