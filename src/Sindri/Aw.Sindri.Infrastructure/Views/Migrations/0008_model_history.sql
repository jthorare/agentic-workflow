-- Model-pick M-4's two views, the same shape as 0006's (a key, the row as JSON text and the ledger sequence that last wrote it). view_model_history holds one row per
-- (recipe, version, state, model); view_model_runs holds what the fold keeps to join a run to its model and recipe. Schema version 15 (ViewSchema.Version): a views.db
-- written at version 14 gains these tables here and the projector rebuilds them from the ledger, because the version differs.
CREATE TABLE view_model_history (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
CREATE TABLE view_model_runs (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
