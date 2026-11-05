-- The newest 'views.rebuilt' ledger sequence this views.db has already acted on (#2054), so a rebuild serve accepted but never ran survives a restart.
-- Nullable on purpose: NULL means a views.db from before this column (the projector adopts the ledger's newest sequence without rebuilding), and
-- ViewSchema.Version stays put so an upgrade does not rebuild every box.
ALTER TABLE projector_state ADD COLUMN rebuilt_through INTEGER;
