-- What the Linear hub's API needs of the pickers' lists (Part of #2077): a team's display name and when its lists were last replaced, whether a member is still active (a member who left is kept, not dropped),
-- and a label's colour. Rows written before this migration read with the defaults: no name, no sync time, active, no colour; the next tick fills them in.
ALTER TABLE linear_team ADD COLUMN name TEXT NOT NULL DEFAULT '';
ALTER TABLE linear_team ADD COLUMN synced_at TEXT NULL;
ALTER TABLE linear_member ADD COLUMN active INTEGER NOT NULL DEFAULT 1;
ALTER TABLE linear_label ADD COLUMN color TEXT NOT NULL DEFAULT '';

-- The workspace's URL key ('acme' in linear.app/acme), read from 'organization { urlKey }' on the Linear issue tick: one row, so a second write replaces the first.
CREATE TABLE linear_workspace (
    id      INTEGER PRIMARY KEY NOT NULL CHECK (id = 1),
    url_key TEXT NOT NULL
) STRICT;

-- The by-key issue read filters on 'identifier' ('ENG-12'); it scanned the table before.
CREATE INDEX linear_issue_by_identifier ON linear_issue (identifier);
