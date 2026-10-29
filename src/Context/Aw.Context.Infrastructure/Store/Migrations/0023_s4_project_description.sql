-- A project's description (issue #1659): kept apart from the chunked text so the Projects card's sheet can show it. The column is scrubbed and capped when written; a tombstone or a blocked row keeps NULL.
-- A one-time re-read of Linear projects follows, narrower than 0020's: only Linear project rows are invalidated (version set to '', which the store's unchanged-record check never matches, so the next
-- commit of each project rewrites it with its description) and only the project feed's cursor goes back to the start of Recent with no watermark (the project feed is cursored: an incremental pull
-- reads only projects updated after the watermark, so unchanged projects would never come back). The issue feed's cursor and the intent sync's row are not touched.
ALTER TABLE source_record ADD COLUMN description TEXT NULL;

UPDATE source_record SET version = '' WHERE source = 'linear-projects' AND kind = 'project';

UPDATE source_cursor SET phase = 'recent', cursor = NULL, backfill_cursor = NULL, backfill_before = NULL WHERE source = 'linear-projects' AND repo = 'records';
