-- messages.db (messages spec §8.1): its own file and its own migration sequence, a cache plus owner state and a work queue, never a view of the ledger, so
-- `sindri views rebuild` does not touch it. Message text lives only here. Cache tables (conversations, users, messages, messages_fts) and the derived
-- columns of thread_state can be rebuilt by a re-sync; owner state (the owner columns of thread_state, drafts, outbox rows not yet sent) cannot.

-- One row per Slack conversation. read_ts is the local pointer, last_read_ts Slack's value; newest_ts and gaps (a JSON array) are the sync's position in it.
-- The poll position per (connection, kind) stays in the ledger's poll_cursor.
CREATE TABLE conversations (
    id TEXT PRIMARY KEY,
    workspace TEXT NOT NULL,
    kind TEXT NOT NULL CHECK (kind IN ('channel', 'private', 'dm', 'mpim')),
    name TEXT NOT NULL,
    ext_shared INTEGER NOT NULL DEFAULT 0,
    starred INTEGER NOT NULL DEFAULT 0,
    muted INTEGER NOT NULL DEFAULT 0,
    last_read_ts TEXT,
    read_ts TEXT,
    latest_ts TEXT,
    newest_ts TEXT,
    gaps TEXT NOT NULL DEFAULT '[]'
) STRICT;

-- Slack people. The workspace id and my user id sit on the slack:messages connection row, so there is no workspaces table.
CREATE TABLE users (
    id TEXT PRIMARY KEY,
    workspace TEXT NOT NULL,
    name TEXT NOT NULL,
    real_name TEXT,
    display_name TEXT,
    time_zone TEXT,
    title TEXT,
    status TEXT,
    presence TEXT,
    bot INTEGER NOT NULL DEFAULT 0,
    deleted INTEGER NOT NULL DEFAULT 0
) STRICT;

-- One row per message. thread_root is the unit triage and retention work on: the root ts of a channel thread (a top-level message is its own root) and ''
-- for a DM or group DM, whose row is the conversation. mine and names_me are set by the writer, which knows my user id from auth.test.
CREATE TABLE messages (
    id INTEGER PRIMARY KEY,
    workspace TEXT NOT NULL,
    conversation TEXT NOT NULL REFERENCES conversations (id) ON DELETE CASCADE,
    ts TEXT NOT NULL,
    thread_ts TEXT,
    thread_root TEXT NOT NULL,
    user_id TEXT NOT NULL,
    text TEXT NOT NULL,
    blocks TEXT,
    reactions TEXT NOT NULL DEFAULT '[]',
    subtype TEXT,
    edited_ts TEXT,
    reply_count INTEGER NOT NULL DEFAULT 0,
    latest_reply_ts TEXT,
    partial INTEGER NOT NULL DEFAULT 0,
    file_ids TEXT NOT NULL DEFAULT '[]',
    mine INTEGER NOT NULL DEFAULT 0,
    names_me INTEGER NOT NULL DEFAULT 0,
    UNIQUE (conversation, ts)
) STRICT;

CREATE INDEX messages_by_thread ON messages (conversation, thread_root, ts);
CREATE INDEX messages_by_ts ON messages (ts);

-- Local search: FTS5 over the text, kept in step with messages by the triggers below, so a delete (retention, purge, a Slack delete) takes its FTS row too.
CREATE VIRTUAL TABLE messages_fts USING fts5 (text, content = 'messages', content_rowid = 'id', tokenize = 'unicode61 remove_diacritics 2');

CREATE TRIGGER messages_fts_insert AFTER INSERT ON messages BEGIN
    INSERT INTO messages_fts (rowid, text) VALUES (new.id, new.text);
END;

CREATE TRIGGER messages_fts_delete AFTER DELETE ON messages BEGIN
    INSERT INTO messages_fts (messages_fts, rowid, text) VALUES ('delete', old.id, old.text);
END;

CREATE TRIGGER messages_fts_update AFTER UPDATE OF text ON messages BEGIN
    INSERT INTO messages_fts (messages_fts, rowid, text) VALUES ('delete', old.id, old.text);
    INSERT INTO messages_fts (rowid, text) VALUES (new.id, new.text);
END;

-- One row per thread (thread_ts '' for a DM). Owner columns: follow, later, title_override. Derived columns: my_last_ts, newest_ts, newest_user,
-- needs_reply (§7.1 items 1 to 3; Done, snooze and linked issues are Home's row state and apply on read), gist and gist_key, and text_expired_at, set when
-- retention took the text of a thread it had kept. rev rises on every write of the row, so a test sees which rows a rebuild touched (W7).
-- No foreign key: a partial search hit can flag a thread whose root was never fetched.
CREATE TABLE thread_state (
    conversation TEXT NOT NULL,
    thread_ts TEXT NOT NULL,
    follow INTEGER NOT NULL DEFAULT 0,
    later INTEGER NOT NULL DEFAULT 0,
    title_override TEXT,
    my_last_ts TEXT,
    newest_ts TEXT,
    newest_user TEXT,
    needs_reply INTEGER NOT NULL DEFAULT 0,
    gist TEXT,
    gist_key TEXT,
    text_expired_at TEXT,
    rev INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (conversation, thread_ts)
) STRICT;

CREATE INDEX thread_state_needs_reply ON thread_state (conversation, thread_ts) WHERE needs_reply = 1;

-- Unsent text the ledger must not hold (Release 2 writes it; retention reads it now, so a thread with a draft keeps its messages).
CREATE TABLE drafts (
    id TEXT PRIMARY KEY,
    conversation TEXT NOT NULL,
    thread_ts TEXT NOT NULL DEFAULT '',
    text TEXT NOT NULL,
    origin TEXT NOT NULL,
    version INTEGER NOT NULL DEFAULT 1,
    updated_at TEXT NOT NULL
) STRICT;

CREATE INDEX drafts_by_thread ON drafts (conversation, thread_ts);

-- The write-through queue (§8.3, Release 2). origin is the workflow id or 'human' and created_at its time: together they serve the daily send cap (§9.7
-- item 6). A landed row keeps its text only until retention.
CREATE TABLE outbox (
    act_id TEXT PRIMARY KEY,
    origin TEXT NOT NULL,
    created_at TEXT NOT NULL,
    conversation TEXT NOT NULL,
    thread_ts TEXT NOT NULL DEFAULT '',
    kind TEXT NOT NULL,
    arguments TEXT NOT NULL,
    state TEXT NOT NULL CHECK (state IN ('pending', 'sending', 'sent', 'failed', 'unknown', 'discarded')),
    tries INTEGER NOT NULL DEFAULT 0,
    reason TEXT,
    version INTEGER NOT NULL DEFAULT 1
) STRICT;

CREATE INDEX outbox_by_origin ON outbox (origin, created_at);
CREATE INDEX outbox_by_thread ON outbox (conversation, thread_ts);
