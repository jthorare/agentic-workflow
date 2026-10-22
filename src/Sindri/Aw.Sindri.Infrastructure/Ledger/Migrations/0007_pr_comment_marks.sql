-- The PR babysitter's comment marks (babysitter spec §5.4, W26): what was done with each comment of a pull request.
-- Append-only; the latest row per (comment_id, edited_at) by 'at' is the comment's state. 'edited_at' is '' for a comment
-- never edited. 'effect' is the delivering effect's identity: a re-delivered effect writes no second row.
CREATE TABLE pr_comment_marks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    forge TEXT NOT NULL,
    repo TEXT NOT NULL,
    pr INTEGER NOT NULL,
    comment_id TEXT NOT NULL,
    edited_at TEXT NOT NULL,
    instance TEXT NOT NULL,
    outcome TEXT NOT NULL,
    reason TEXT,
    "commit" TEXT,
    at TEXT NOT NULL,
    effect TEXT NOT NULL UNIQUE
);

CREATE INDEX pr_comment_marks_by_pr ON pr_comment_marks (forge, repo, pr, comment_id);
