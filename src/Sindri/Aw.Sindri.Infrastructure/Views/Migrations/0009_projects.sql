-- The Projects page's owner state (projects page spec §5.2), the same shape as 0008's (a key, the row as JSON text and the ledger sequence that last wrote it).
-- view_project_attached holds one row per attached or hand-made project, keyed by its prj_ id; view_project_hidden holds one row per led Linear project the owner
-- ever hid or unhid, keyed by its Linear key. Schema version 21 (ViewSchema.Version): a views.db written at version 20 gains these tables here and the projector
-- rebuilds the views from the ledger, because the version differs.
CREATE TABLE view_project_attached (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
CREATE TABLE view_project_hidden (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
