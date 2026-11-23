-- The concepts and glossary answers (issue 3676): built once for a map version and kept, so a read doesn't load and walk the whole map (23.8 s and 19.6 s on a first
-- read of a 24k-node map). The key is a sha256 of everything the answer reads (ConceptAnswerKey); version and basis let a write drop the rows that can't be asked for again
-- (a version that is gone, or the same version under an older override or intent layer). body is the answer's JSON. A cache: deleting every row only makes the next reads slow.
CREATE TABLE concept_answer (
    key      TEXT    NOT NULL PRIMARY KEY,
    kind     TEXT    NOT NULL CHECK (kind IN ('concepts', 'glossary')),
    version  INTEGER NOT NULL,
    basis    TEXT    NOT NULL,
    body     TEXT    NOT NULL,
    saved    TEXT    NOT NULL
) STRICT;

CREATE INDEX concept_answer_by_version ON concept_answer (version);
