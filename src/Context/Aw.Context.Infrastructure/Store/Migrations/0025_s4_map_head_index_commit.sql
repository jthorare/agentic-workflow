-- The code index commit a map version's symbol citations were resolved against (issue #1704), per repo head. NULL when no index held the build's commit
-- (missing, still building, or at another commit), and for every version built before this column: those count as built without their index.
ALTER TABLE map_version_head ADD COLUMN index_commit TEXT NULL CHECK (index_commit IS NULL OR (length(index_commit) BETWEEN 7 AND 64 AND index_commit NOT GLOB '*[^0-9a-f]*'));
