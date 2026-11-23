-- The Home page's rows and their edges (Home S0, #3733; home spec §3.1, §8), the same base shape as the other views (a key, the row as JSON text and the ledger
-- sequence that last wrote it) plus virtual columns generated from the row's JSON, so a read can filter by them. view_home_rows holds one row per object keyed by its
-- id (state: open, snoozed, done or muted; grp: needs-you or moving); view_home_links holds one row per edge keyed '<from>|<to>|<edge>', with its source object in
-- the item column (the generated 'from') so a row's edges are one lookup. Schema version 39 (ViewSchema.Version): an older views.db gains the tables here and the
-- projector rebuilds the views from the ledger, because the version differs. Numbered after 0016_items_state (no gaps, AW-DATA-005).
CREATE TABLE view_home_rows (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL,
    kind TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.kind')) VIRTUAL,
    grp TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.group')) VIRTUAL,
    state TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.state')) VIRTUAL,
    since TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.since')) VIRTUAL,
    snooze_until TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.snoozeUntil')) VIRTUAL,
    done_at TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.doneAt')) VIRTUAL
);
CREATE INDEX view_home_rows_state ON view_home_rows (state, grp);
CREATE INDEX view_home_rows_since ON view_home_rows (since);
CREATE TABLE view_home_links (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL,
    item TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.from')) VIRTUAL,
    to_id TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.to')) VIRTUAL,
    edge TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.edge')) VIRTUAL
);
CREATE INDEX view_home_links_item ON view_home_links (item);
CREATE INDEX view_home_links_to ON view_home_links (to_id);
