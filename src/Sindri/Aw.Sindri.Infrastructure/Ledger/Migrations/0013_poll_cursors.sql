-- Poll cursors (connections PR 11): where polling stands for one (connection, event kind). 'cursor' is the provider's own
-- position (an event id or a timestamp), saved only after the runs for the events before it started; 'backoff_until' and
-- 'failures' keep the rate-limit wait across a restart.
CREATE TABLE poll_cursor (
    connection TEXT NOT NULL,
    event_kind TEXT NOT NULL,
    cursor TEXT,
    updated_at TEXT,
    last_poll_at TEXT,
    backoff_until TEXT,
    failures INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (connection, event_kind)
);
