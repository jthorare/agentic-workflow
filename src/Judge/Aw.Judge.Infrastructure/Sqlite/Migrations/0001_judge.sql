-- The judge's C# schema (spec 4a: no TS state is read or migrated). Table and column names follow the TS judge so a
-- later reader (the Scorer port) finds what it expects; eval_items, labels and prompt_sort_* arrive with J2/J3.
CREATE TABLE decisions (
  id TEXT PRIMARY KEY,
  ts TEXT NOT NULL,
  question TEXT NOT NULL,
  content_class TEXT NOT NULL,
  provider TEXT NOT NULL,
  decision TEXT,
  confidence REAL NOT NULL,
  reason_code TEXT NOT NULL,
  latency_ms INTEGER NOT NULL,
  input_digest TEXT NOT NULL,
  undone_at TEXT,
  chain_position INTEGER NOT NULL DEFAULT 0,
  skipped TEXT NOT NULL DEFAULT '[]',
  outcome TEXT NOT NULL DEFAULT 'decided' CHECK (outcome IN ('decided', 'escalated', 'failed'))
);
CREATE INDEX decisions_ts ON decisions(ts);
CREATE INDEX decisions_question_ts ON decisions(question, ts);

CREATE TABLE failures (
  ts TEXT NOT NULL,
  question TEXT NOT NULL,
  provider TEXT NOT NULL,
  reason_code TEXT NOT NULL
);
CREATE INDEX failures_ts ON failures(ts);

CREATE TABLE briefs (
  tool_use_id TEXT PRIMARY KEY,
  agent_id TEXT,
  session_id TEXT NOT NULL,
  prompt_id TEXT NOT NULL,
  dispatch_name TEXT,
  subagent_type TEXT NOT NULL,
  goal TEXT NOT NULL,
  acceptance_criteria TEXT NOT NULL,
  proof_command TEXT NOT NULL,
  saved_at TEXT NOT NULL
);
CREATE INDEX briefs_agent_id ON briefs(agent_id);
CREATE INDEX briefs_dispatch_name ON briefs(dispatch_name, agent_id, saved_at);
CREATE INDEX briefs_dispatch_lookup ON briefs(session_id, prompt_id, subagent_type, agent_id, saved_at);

CREATE TABLE decision_details (
  id TEXT PRIMARY KEY REFERENCES decisions(id),
  input_json TEXT NOT NULL,
  probabilities TEXT,
  rules_opinion TEXT,
  agreement TEXT CHECK (agreement IN ('agreed', 'overrode', 'undecided')),
  session_id TEXT
);
