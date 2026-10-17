-- API P10a's launches view, the same shape as 0004's (a key, the row as JSON text and the ledger sequence that last wrote it) plus the columns
-- the launch queries look rows up by (an item's launches, a repo's, those in a state), each a virtual column generated from the row's JSON and indexed.
-- Schema version 6 (ViewSchema.Version): a views.db written at version 5 gains this table here and the projector rebuilds it from the
-- ledger, because the version differs.
CREATE TABLE view_launches (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL,
    item TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.item')) VIRTUAL,
    state TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.state')) VIRTUAL,
    repo TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.repo')) VIRTUAL
);
CREATE INDEX view_launches_item ON view_launches (item);
CREATE INDEX view_launches_state ON view_launches (state);
CREATE INDEX view_launches_repo ON view_launches (repo);
