-- Graph runs (issue 2258, PR 6): one row per run, keyed by the run id, the row as JSON text and the ledger sequence that last wrote it.
-- Folded from 'workflow.run.*' and 'workflow.node.*'. Schema version 26 (ViewSchema.Version): an older views.db gains this table here and the
-- projector rebuilds it from the ledger. The number is a placeholder: the lead renumbers it at the final main merge (no gaps, AW-DATA-005).
CREATE TABLE view_workflow_runs (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
