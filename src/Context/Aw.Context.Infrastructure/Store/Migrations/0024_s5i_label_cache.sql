-- The labeler's answers (issue #1723, q149), one row per cluster: the key is a sha256 of the scrubbed file list the model was shown (never the positional cluster id),
-- so a rebuild asks the model only about clusters whose files are new.
CREATE TABLE label_cache (
    key TEXT NOT NULL PRIMARY KEY,
    label TEXT NOT NULL,
    saved TEXT NOT NULL
);
