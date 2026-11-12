-- aw-meta's append-only event log (docs/csharp/meta.md "Events"): read by a cursor, pruned to 7 days on every append.
CREATE TABLE meta_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  at TEXT NOT NULL,
  kind TEXT NOT NULL,
  lane TEXT,
  fields TEXT NOT NULL
);
CREATE INDEX meta_events_kind ON meta_events(kind, id);
CREATE INDEX meta_events_lane ON meta_events(lane, id);
