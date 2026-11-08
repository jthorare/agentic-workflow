-- The owner's todos (todo spec §4.2), the same shape as 0008's (a key, the row as JSON text and the ledger sequence that last wrote it).
-- view_todo holds one row per todo, keyed by its td_ id; removed and done todos stay as rows (soft). Schema version 25 (ViewSchema.Version): a views.db
-- written at version 24 gains this table here and the projector rebuilds the views from the ledger, because the version differs.
CREATE TABLE view_todo (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
