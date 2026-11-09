-- The recipe engine is gone (issue 2258, PR 3). Its four tables go; 'events' stays, so old rows still read as history.
-- The migration runner copies ledger.db to ledger.db.bak-v<N> before this runs, N being the version it upgrades FROM (11 when 0011 was the
-- last applied; MigrationRunner, backupBeforeUpgrade). That file is the only copy of the four tables, and MigrationBackups.Prune deletes it at
-- the next upgrade that succeeds, so keep it by hand if the recipe rows matter.
DROP TABLE IF EXISTS pr_comment_marks;
DROP TABLE IF EXISTS workflow_claims;
DROP TABLE IF EXISTS recipe_approvals;
DROP TABLE IF EXISTS recipes;
