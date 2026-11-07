-- A ref's line in its doc, so the library read's SND-LIB-015 names the real line the parser gave (#2189). 0 = written before this column; the scope check treats it as no line.
-- Nothing is copied here: DesignDocParser.Version went from 2 to 3 with this migration, so every doc is read again on the first tick after the upgrade and the line is filled then.
ALTER TABLE design_doc_ref ADD COLUMN line INTEGER NOT NULL DEFAULT 0;
