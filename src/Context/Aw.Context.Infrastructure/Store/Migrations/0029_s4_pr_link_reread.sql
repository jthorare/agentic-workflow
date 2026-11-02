-- The Projects page's PR counts (spec 2026-10-29-sindri-projects-page-design §4.5): a Linear issue now carries its GitHub PR attachments and its own key ('known-as'), and a GitHub PR the
-- Linear keys it names. Records stored before keep their old version, which a replay never rewrites, so this is a one-time re-read the way 0023 did it: Linear issue and GitHub PR rows are
-- invalidated (version '', which the store's unchanged-record check never matches) and both record feeds go back to the start of Recent with no watermark. The project feed's cursor,
-- GitHub issues and the intent sync's row (repo '*') are not touched.
UPDATE source_record SET version = '' WHERE (source = 'linear' AND kind = 'issue') OR (source = 'github' AND kind = 'pr');

UPDATE source_cursor SET phase = 'recent', cursor = NULL, backfill_cursor = NULL, backfill_before = NULL WHERE source IN ('linear', 'github') AND repo = 'records';
