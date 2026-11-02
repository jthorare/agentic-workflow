-- Concept synthesis (issue #1597, part B): the cross-source concepts a model synthesized from the embedded records, kept by a stable slug so a relabel updates a row
-- instead of replacing it and the map's nodes for it don't churn. concept_synthesis_cursor holds the embedding.seq high-water the last pass ran up to.
CREATE TABLE concept_synthesis (
    key             TEXT PRIMARY KEY,
    name            TEXT NOT NULL,
    summary         TEXT NOT NULL,
    members         TEXT NOT NULL,
    membership_hash TEXT NOT NULL,
    state           TEXT NOT NULL CHECK (state IN ('queued', 'done')),
    watermark       INTEGER NOT NULL,
    created_at      TEXT NOT NULL,
    updated_at      TEXT NOT NULL
) STRICT;

CREATE TABLE concept_synthesis_cursor (
    id         INTEGER PRIMARY KEY CHECK (id = 1),
    watermark  INTEGER NOT NULL,
    updated_at TEXT NOT NULL
) STRICT;

-- A concept the map reads moves the map's counter (0008), like every other table a map read loads.
CREATE TRIGGER concept_synthesis_stamp_ai AFTER INSERT ON concept_synthesis BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER concept_synthesis_stamp_au AFTER UPDATE ON concept_synthesis BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
CREATE TRIGGER concept_synthesis_stamp_ad AFTER DELETE ON concept_synthesis BEGIN UPDATE change_stamp SET seq = seq + 1 WHERE id = 1; END;
