-- Connections (connections PR 10a): a name workflows and agents use instead of a token. A row holds only non-secret config
-- and a secret POINTER ('keychain:sindri-connection/linear.work'); the token is in the OS keychain, never in this file.
CREATE TABLE connection (
    name TEXT PRIMARY KEY,
    kind TEXT NOT NULL,
    preset TEXT,
    config_json TEXT NOT NULL,
    secret_pointer TEXT,
    status TEXT NOT NULL,
    status_reason TEXT,
    created_by TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    last_refreshed_at TEXT
);
