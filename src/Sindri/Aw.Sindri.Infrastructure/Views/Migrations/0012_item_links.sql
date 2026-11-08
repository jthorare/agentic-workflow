-- What each item is linked to (Sindri v2 PR 4b): one row per issue, pull request or graph run, the same shape as the other views (a key, the row as JSON text and
-- the ledger sequence that last wrote it). The key is '<item>/<kind>/<record>'. The recipe-stats cache goes: the recipe engine is gone and nothing reads or writes it.
-- Schema version 27 (ViewSchema.Version): a views.db written at version 26 gains this table here and the projector rebuilds the views from the ledger, because the
-- version differs; that replay is what fills view_item_links from the old 'item.linked' rows and folds the retired 'workflow.*', 'line.*', 'recipe.*' and 'scope.*'
-- rows into the timeline only. The rebuilt-through marker (0010) is left alone: the reset the version change causes rewrites it with the ledger's newest sequence.
CREATE TABLE view_item_links (
    key TEXT PRIMARY KEY,
    row_json TEXT NOT NULL,
    seq INTEGER NOT NULL
);
DROP TABLE IF EXISTS view_recipe_stats;
