-- Backfill stops where Recent started (#1512): the sync driver stores that instant when Recent ends, so a later tick on a later day still pulls only what is older than it.
-- A row with phase 'backfill' and no value gets Recent's start from the driver on its next pull.
ALTER TABLE source_cursor ADD COLUMN backfill_before TEXT NULL;
