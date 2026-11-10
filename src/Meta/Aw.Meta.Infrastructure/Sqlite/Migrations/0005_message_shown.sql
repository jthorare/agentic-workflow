-- When `inbox --once` (the meta lane hook's view) last showed a message. NULL = never shown. A deferred message whose deferral ran out
-- after that time is shown once more.
ALTER TABLE messages ADD COLUMN shown_at TEXT;
