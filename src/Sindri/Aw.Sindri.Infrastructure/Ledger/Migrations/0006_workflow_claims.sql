-- The item claims (engine spec §5.1, §5.7, §5.9, §12; engine P6 unit (d)): who holds each item's claim. 'effect.claim.ref'
-- takes it (and creates the marker ref 'sindri/{user}/{item}' in the item's repo, which holds no owner); '--attempt next'
-- is refused while the holder is live (SND-WF-012). One row per item; an ended holder's row is taken over in place.
CREATE TABLE workflow_claims (
    item TEXT PRIMARY KEY,
    owner TEXT NOT NULL,
    attempt INTEGER NOT NULL CHECK (attempt >= 1),
    claimed_at TEXT NOT NULL
);
