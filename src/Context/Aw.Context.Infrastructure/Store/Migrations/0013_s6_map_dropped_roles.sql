-- Issue 1161: a map version records how many tracked files each ignored role's globs dropped when it was built (the files are never indexed,
-- so nothing can count them afterwards). One line per (repo, role): repo, role and count, separated by U+001F. NULL = built before the counts were
-- recorded; '' = built with no ignored role.
ALTER TABLE map_version ADD COLUMN dropped TEXT;
