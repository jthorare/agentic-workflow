-- Toolkits (agent-first PR 16): the toolkits Sindri builds and runs, and a log of every tool call. 'state' is
-- off, built, running, paused or failed; 'source_hash' is the hash of the source the last good build used (set only when
-- build -warnaserror and the tests passed). 'input' and 'output' are scrubbed and capped at 16 KB; 'truncated' says one was cut.
CREATE TABLE toolkit (
    name TEXT NOT NULL PRIMARY KEY,
    dir TEXT NOT NULL,
    state TEXT NOT NULL,
    source_hash TEXT,
    built_at TEXT,
    manifest_json TEXT,
    created_by TEXT NOT NULL
);

CREATE TABLE toolkit_call (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    toolkit TEXT NOT NULL,
    tool TEXT NOT NULL,
    caller TEXT NOT NULL,
    input TEXT,
    output TEXT,
    truncated INTEGER NOT NULL DEFAULT 0,
    error TEXT,
    duration_ms INTEGER NOT NULL,
    at TEXT NOT NULL
);

CREATE INDEX toolkit_call_by_toolkit ON toolkit_call (toolkit, at);
