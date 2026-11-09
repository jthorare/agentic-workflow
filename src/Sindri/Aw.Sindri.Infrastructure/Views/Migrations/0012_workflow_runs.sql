-- Graph runs (issue 2258, PR 6): one row per run, keyed by the run id, the row as JSON text and the ledger sequence that last wrote it.
-- Folded from 'workflow.run.*' and 'workflow.node.*'. Schema version 27 (ViewSchema.Version): an older views.db gains this table here and the
-- projector rebuilds it from the ledger. Numbered after main's 0011_todos (no gaps, AW-DATA-005); a later merge takes main's next free number.
CREATE TABLE view_workflow_runs (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
