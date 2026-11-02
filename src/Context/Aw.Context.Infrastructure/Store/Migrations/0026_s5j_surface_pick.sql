-- Jev's decisions on product-surface groups (issue #1567, q149), one row per group: the key is the group's sha256 (node keys, kinds, passes and evidence paths),
-- so a rebuild asks the model only about groups it has not decided. candidate_keys is a JSON array of node keys, never names or paths; model is the identity that decided.
CREATE TABLE surface_pick (
    group_key TEXT NOT NULL PRIMARY KEY,
    choice TEXT NOT NULL CHECK (choice IN ('separate', 'merge', 'reject')),
    survivor_key TEXT,
    merged_name TEXT,
    candidate_keys TEXT NOT NULL,
    model TEXT,
    decided_at TEXT NOT NULL
) STRICT;
