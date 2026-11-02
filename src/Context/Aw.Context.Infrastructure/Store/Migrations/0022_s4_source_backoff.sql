-- The owned index's per-source backoff and last commit (#1597 part A, piece 4): written on a feed's ('records') source_cursor row beside its last_sync and last_error, so a restart keeps them.
-- 'failures' counts failed ticks in a row, 'next_try' is when a scheduled tick may pull the source again, 'last_commit' is the end of the last tick that did not fail, and 'last_error_text' is one scrubbed line.
ALTER TABLE source_cursor ADD COLUMN failures INTEGER NOT NULL DEFAULT 0;
ALTER TABLE source_cursor ADD COLUMN next_try TEXT NULL;
ALTER TABLE source_cursor ADD COLUMN last_commit TEXT NULL;
ALTER TABLE source_cursor ADD COLUMN last_error_text TEXT NULL;
