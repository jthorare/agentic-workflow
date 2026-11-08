-- The recipe engine is gone (issue 2258, PR 3). Its four tables go; 'events' stays, so old rows still read as history.
-- The migration runner copies ledger.db to ledger.db.bak-v8 before this runs (MigrationRunner, backupBeforeUpgrade).
DROP TABLE IF EXISTS pr_comment_marks;
DROP TABLE IF EXISTS workflow_claims;
DROP TABLE IF EXISTS recipe_approvals;
DROP TABLE IF EXISTS recipes;
