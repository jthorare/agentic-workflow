-- trait-check's verdict cache (judge/trait-check/verdicts.sqlite): one row per cached Jev verdict on one trait question.
-- key: the SHA-256 (hex) of the trait file, the question, the excerpt and the model (TraitVerdictKeys); never the text itself.
-- probabilities: the model's probability per option, a JSON object ({} when it gave none).
-- stored_at: Unix milliseconds; rows older than the cache's keep time are never read and are deleted on the next write.
CREATE TABLE trait_verdicts (
    key           TEXT    NOT NULL PRIMARY KEY,
    choice        TEXT    NOT NULL,
    confidence    REAL    NOT NULL,
    probabilities TEXT    NOT NULL,
    stored_at     INTEGER NOT NULL
);

CREATE INDEX trait_verdicts_stored_at ON trait_verdicts (stored_at);
