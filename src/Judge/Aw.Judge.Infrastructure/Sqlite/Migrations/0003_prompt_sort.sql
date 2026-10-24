-- The prompt sorter's tables (cutover unit 6.1): one row per sorted axis of a 'prompt-sort' decision, and one row per
-- run. Final schema: a fix after this has merged is a new migration, never an edit of this file. The eval tables
-- (eval_items, labels) come with the by-hand verbs, in the next migration.
CREATE TABLE prompt_sort_axes (
  decision_id TEXT NOT NULL REFERENCES decisions(id),
  axis TEXT NOT NULL,
  value TEXT NOT NULL,
  heuristic TEXT NOT NULL,
  source TEXT NOT NULL,
  status TEXT NOT NULL,
  probability REAL,
  confidence REAL,
  PRIMARY KEY (decision_id, axis)
);
CREATE INDEX prompt_sort_axes_axis ON prompt_sort_axes(axis, status);
CREATE TABLE prompt_sort_runs (
  decision_id TEXT PRIMARY KEY REFERENCES decisions(id),
  mode TEXT NOT NULL,
  reason TEXT NOT NULL,
  would_fire TEXT NOT NULL DEFAULT '[]',
  fired TEXT NOT NULL DEFAULT '[]',
  suppressed TEXT NOT NULL DEFAULT '[]'
);
