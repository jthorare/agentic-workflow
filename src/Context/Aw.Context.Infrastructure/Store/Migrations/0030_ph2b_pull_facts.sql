-- The PR review page (spec 2026-11-04-sindri-pr-review-page-design, 5.6, D21): a GitHub PR record now carries its requested reviewer logins, its head sha and each reviewer's latest reviewed commit
-- with the number of commits the head is ahead of it, as one JSON text in 'pull_facts' (NULL for every other record, and for a blocked record or a tombstone). Rows stored before keep NULL and an
-- unchanged PR would never get them, so this is a one-time re-read the way 0029 did it: GitHub PR rows are invalidated (version '', which the store's unchanged-record check never matches) and the
-- GitHub record feed goes back to the start of Recent with no watermark. Linear's feeds, GitHub issues and the intent sync's row (repo '*') are not touched.
ALTER TABLE source_record ADD COLUMN pull_facts TEXT;

UPDATE source_record SET version = '' WHERE source = 'github' AND kind = 'pr';

UPDATE source_cursor SET phase = 'recent', cursor = NULL, backfill_cursor = NULL, backfill_before = NULL WHERE source = 'github' AND repo = 'records';
