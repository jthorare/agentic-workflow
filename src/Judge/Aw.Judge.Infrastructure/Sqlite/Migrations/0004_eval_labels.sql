-- The eval tables (cutover unit 6.3): one row per item a question can be scored on, and one row per label an item has
-- from each source. Final schema: a fix after this has merged is a new migration, never an edit of this file.
-- 'source' of an item is its origin and what keeps an import idempotent ('decision:<id>' or 'transcript:<session>:<turn>');
-- 'source' of a label is who gave it, and an item has at most one label per source.
CREATE TABLE eval_items (
  id TEXT PRIMARY KEY,
  question TEXT NOT NULL,
  input_json TEXT NOT NULL,
  source TEXT NOT NULL UNIQUE,
  model_decision TEXT,
  created_at TEXT NOT NULL
);
CREATE INDEX eval_items_question ON eval_items(question, created_at);
CREATE TABLE labels (
  item_id TEXT NOT NULL REFERENCES eval_items(id),
  source TEXT NOT NULL CHECK (source IN ('outcome', 'adjudicator', 'override')),
  label TEXT NOT NULL,
  labeled_at TEXT NOT NULL,
  PRIMARY KEY (item_id, source)
);
