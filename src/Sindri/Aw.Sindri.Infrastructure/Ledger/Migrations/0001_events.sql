-- The ledger's event log (factory addendum §17.20–17.21): the §17.21 envelope, the writer's lease epoch and the
-- idempotency key that makes a replay a no-op. Append-only; seq is every consumer's cursor.
CREATE TABLE events (
    seq INTEGER PRIMARY KEY AUTOINCREMENT,
    id TEXT NOT NULL UNIQUE,
    type TEXT NOT NULL,
    schema_version INTEGER NOT NULL,
    occurred_at TEXT NOT NULL,
    actor TEXT NOT NULL,
    tenant TEXT NOT NULL,
    ring INTEGER NOT NULL,
    item TEXT,
    correlation_id TEXT NOT NULL,
    causation_id TEXT,
    trace_id TEXT,
    epoch INTEGER NOT NULL,
    idempotency_key TEXT NOT NULL UNIQUE,
    payload_json TEXT NOT NULL
);
CREATE INDEX events_type_seq ON events (type, seq);
