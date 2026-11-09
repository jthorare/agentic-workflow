-- What an 'only_new' workflow node has passed on (agent-first PR 9). The key is the item's configured field; the first time a workflow saw it is kept.
-- A key is written only when the whole run succeeded, so a run that fails anywhere leaves its items unseen. Purged with the workflow's own state, never by age.
CREATE TABLE workflow_seen (
    workflow_id TEXT NOT NULL,
    key TEXT NOT NULL,
    first_seen_at TEXT NOT NULL,
    PRIMARY KEY (workflow_id, key)
);
