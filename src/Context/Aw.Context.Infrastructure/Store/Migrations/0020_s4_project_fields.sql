-- The Projects page (spec 2026-10-29-sindri-projects-page-design §4.1 to §4.3): a project record's health word and due day, the owner's identity per source, and a one-time
-- re-read of Linear. The cursor reset puts the Linear record feed back to the start of Recent with no watermark and no end cursor, so issues stored before the 'led-by' link and the
-- project filter are fetched again; the intent sync's own cursor row (repo '*') is not touched.
ALTER TABLE source_record ADD COLUMN health TEXT NULL;
ALTER TABLE source_record ADD COLUMN due_at TEXT NULL;

CREATE TABLE source_identity (
    source   TEXT PRIMARY KEY NOT NULL,
    user_ref TEXT NOT NULL,
    name     TEXT NOT NULL,
    read_at  TEXT NOT NULL
) STRICT;

UPDATE source_cursor SET phase = 'recent', cursor = NULL, backfill_cursor = NULL, backfill_before = NULL WHERE source = 'linear' AND repo = 'records';
