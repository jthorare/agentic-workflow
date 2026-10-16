-- API P4's request, decision and needs-you views, the same shape as 0001's (a key, the row as JSON text and the ledger sequence
-- that last wrote it) plus the columns the projections and queries look rows up by. Each is a virtual column generated from the
-- row's JSON and indexed, so a filtered read never scans the table and the JSON stays the one place a row's data lives.
-- Schema version 3 (ViewSchema.Version): a views.db written at version 2 gains these tables here and the projector rebuilds it
-- from the ledger, because the version differs.
CREATE TABLE view_requests (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL,
    item TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.item')) VIRTUAL,
    state TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.state')) VIRTUAL,
    repo TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.repo')) VIRTUAL
);
CREATE INDEX view_requests_item ON view_requests (item);
CREATE INDEX view_requests_state ON view_requests (state);
CREATE INDEX view_requests_repo ON view_requests (repo);

CREATE TABLE view_decisions (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL,
    item TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.item')) VIRTUAL,
    state TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.state')) VIRTUAL,
    repo TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.repo')) VIRTUAL
);
CREATE INDEX view_decisions_item ON view_decisions (item);
CREATE INDEX view_decisions_state ON view_decisions (state);
CREATE INDEX view_decisions_repo ON view_decisions (repo);

-- The needs-you view stores a NeedsYouRowRecord ({row, sequence}), so its columns read the wire row inside it.
CREATE TABLE view_needs_you (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL,
    item TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.row.item')) VIRTUAL,
    repo TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.row.repo')) VIRTUAL,
    kind TEXT GENERATED ALWAYS AS (json_extract(row_json, '$.row.kind')) VIRTUAL
);
CREATE INDEX view_needs_you_item ON view_needs_you (item);
CREATE INDEX view_needs_you_repo ON view_needs_you (repo);
CREATE INDEX view_needs_you_kind ON view_needs_you (kind);
