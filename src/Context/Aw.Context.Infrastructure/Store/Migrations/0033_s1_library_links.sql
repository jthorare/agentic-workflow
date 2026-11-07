-- A doc's parent and supersedes (PR review page spec 2026-11-04 §4.2), as written in its front matter. The library read decides SND-LIB-016 against the doc's scope from them (#2181), as it decides 012:
-- nothing is stored about the result, so a doc added or removed later changes the others' answers on the next read.
-- Nothing is copied here: DesignDocParser.Version went from 1 to 2 with this migration, so every doc is read again on the first tick after the upgrade and these columns are filled then.
ALTER TABLE design_doc ADD COLUMN parent TEXT NULL;
ALTER TABLE design_doc ADD COLUMN supersedes TEXT NULL;
