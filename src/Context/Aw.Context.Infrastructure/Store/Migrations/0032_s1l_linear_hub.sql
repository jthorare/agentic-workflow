-- The Linear hub (spec 2026-11-04 §4.1, §4.2): an issue's fields, its newest comments, the assignee and state changes between two reads, and the lists the pickers need. The first three are written by the 'linear'
-- side-row writer in the same transaction as the issue record. A tombstone deletes the issue's side rows (the writer's Delete, in the tombstone's transaction) and a purge cascades from source_record; every read still filters 'removed_at IS NULL'.
CREATE TABLE linear_issue (
    key         TEXT PRIMARY KEY NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    team_id     TEXT NULL,
    identifier  TEXT NOT NULL,
    state_id    TEXT NULL,
    state_type  TEXT NULL,
    assignee_id TEXT NULL,
    priority    INTEGER NOT NULL,
    estimate    REAL NULL,
    cycle_id    TEXT NULL,
    project_id  TEXT NULL,
    parent_id   TEXT NULL,
    labels      TEXT NOT NULL DEFAULT '[]',
    due_date    TEXT NULL,
    sort_order  REAL NULL,
    updated_at  TEXT NOT NULL,
    description TEXT NULL   -- scrubbed with the ingest scrubber, at most 65,536 characters; the issue sheet shows and edits it
) STRICT;

CREATE INDEX linear_issue_by_assignee_state ON linear_issue (assignee_id, state_type);
CREATE INDEX linear_issue_by_project ON linear_issue (project_id);
CREATE INDEX linear_issue_by_cycle ON linear_issue (cycle_id);
CREATE INDEX linear_issue_by_updated ON linear_issue (updated_at, key);   -- the list's order: updated_at DESC, key

CREATE TABLE linear_comment (
    id         TEXT PRIMARY KEY NOT NULL,
    issue_key  TEXT NOT NULL REFERENCES linear_issue (key) ON DELETE CASCADE,
    author_id  TEXT NULL,
    created_at TEXT NOT NULL,
    body       TEXT NOT NULL,
    sindri     INTEGER NOT NULL DEFAULT 0 CHECK (sindri IN (0, 1))
) STRICT;

CREATE INDEX linear_comment_by_issue ON linear_comment (issue_key, created_at);
CREATE INDEX linear_comment_by_created ON linear_comment (created_at);

-- 'old' and 'new' are Linear ids (an assignee's user id, a state's id); null is no assignee. 'via' is 'feed' or 'write-through'.
CREATE TABLE linear_issue_change (
    seq       INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    issue_key TEXT NOT NULL REFERENCES linear_issue (key) ON DELETE CASCADE,
    field     TEXT NOT NULL CHECK (field IN ('assignee', 'state')),
    old       TEXT NULL,
    new       TEXT NULL,
    at        TEXT NOT NULL,
    via       TEXT NOT NULL CHECK (via IN ('feed', 'write-through'))
) STRICT;

CREATE INDEX linear_issue_change_by_issue ON linear_issue_change (issue_key);
CREATE INDEX linear_issue_change_by_at ON linear_issue_change (at);

-- The pickers' lists (§4.2): one small table each, replaced per team in one transaction. A member or a label can belong to more than one team, so the team is part of the key.
-- 'linear_team' maps a team's key ('ENG'), which is what the profile, the audience and the owner use, to its Linear id.
CREATE TABLE linear_team (
    id  TEXT PRIMARY KEY NOT NULL,
    key TEXT NOT NULL
) STRICT;

CREATE UNIQUE INDEX linear_team_by_key ON linear_team (key);

CREATE TABLE linear_state (
    team_id  TEXT NOT NULL,
    id       TEXT NOT NULL,
    name     TEXT NOT NULL,
    type     TEXT NOT NULL,
    position REAL NOT NULL,
    PRIMARY KEY (team_id, id)
) STRICT;

CREATE TABLE linear_cycle (
    team_id   TEXT NOT NULL,
    id        TEXT NOT NULL,
    number    INTEGER NOT NULL,
    name      TEXT NULL,
    starts_at TEXT NOT NULL,
    ends_at   TEXT NOT NULL,
    PRIMARY KEY (team_id, id)
) STRICT;

CREATE TABLE linear_member (
    team_id      TEXT NOT NULL,
    id           TEXT NOT NULL,
    name         TEXT NOT NULL,
    display_name TEXT NULL,
    PRIMARY KEY (team_id, id)
) STRICT;

CREATE TABLE linear_label (
    team_id TEXT NOT NULL,
    id      TEXT NOT NULL,
    name    TEXT NOT NULL,
    PRIMARY KEY (team_id, id)
) STRICT;

-- The feed's version suffix moved to ';f3' (every stored issue is re-saved once with its fields and comments); this puts the Linear record feed back to the start of Recent with no watermark and no end cursor, as 0020 did,
-- so the re-read starts now. The re-save finds no stored 'linear_issue' row, so it logs no change rows.
UPDATE source_cursor SET phase = 'recent', cursor = NULL, backfill_cursor = NULL, backfill_before = NULL WHERE source = 'linear' AND repo = 'records';
