-- 'judge approve' records when the user cleared a decision in its own column (null until approved; the first
-- approval's time is kept). Existing rows read back as not approved.
ALTER TABLE decisions ADD COLUMN approved_at TEXT;
