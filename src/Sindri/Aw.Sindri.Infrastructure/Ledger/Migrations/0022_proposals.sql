-- Proposals (issue 3787, piece 3): typed proposals to change the toolkit, mined from local agent sessions. Ledger migration 22.
-- Everything here stays local. 'body' is the proposal as the model drafted it (JSON); 'proposal_source' holds the turns the model saw for it,
-- recorded by code; 'proposal_public' holds the generalized version that passed the leak gate, the only text that may leave the box.
-- 'state' is proposed, staged, held, published, merged or rejected; 'held_by' names the gate layer that held it last, never matched text.
CREATE TABLE proposal (
    id TEXT PRIMARY KEY,
    kind TEXT NOT NULL,
    target TEXT NOT NULL,
    norm_title TEXT NOT NULL,
    state TEXT NOT NULL,
    tier TEXT NOT NULL,
    attempts INTEGER NOT NULL DEFAULT 0,
    held_by TEXT,
    body TEXT NOT NULL,
    origin TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX proposal_by_key ON proposal (target, norm_title);
CREATE INDEX proposal_by_state ON proposal (state, updated_at);

CREATE TABLE proposal_source (
    proposal_id TEXT NOT NULL REFERENCES proposal (id),
    session TEXT NOT NULL,
    turns TEXT NOT NULL,
    PRIMARY KEY (proposal_id, session)
);

CREATE TABLE proposal_public (
    proposal_id TEXT PRIMARY KEY REFERENCES proposal (id),
    body TEXT NOT NULL,
    tier TEXT NOT NULL,
    created_at TEXT NOT NULL
);
