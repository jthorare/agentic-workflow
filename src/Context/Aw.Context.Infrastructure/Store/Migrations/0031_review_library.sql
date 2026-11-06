-- The design-doc library (PR review page spec 2026-11-04 §5.3): a doc record of the repo-docs or library feed is itemized into design_doc, design_doc_item and design_doc_ref in the same transaction
-- that stores the record, and a PR record's 'Covers:' refs are stored as design_doc_claim rows. Records are soft-deleted, so no cascade fires on removal: the store deletes these rows itself with 'removed_at'.
-- Nothing is copied here: the repo-docs feed's fingerprint and per-doc check carry the parser version, so every doc is read again on the first tick after the upgrade and itemized then.
CREATE TABLE design_doc (
    doc_key        TEXT PRIMARY KEY NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    root           TEXT NOT NULL,
    scope          TEXT NOT NULL,
    doc_id         TEXT NULL,
    kind           TEXT NULL,
    status         TEXT NULL,
    project        TEXT NULL,
    repos          TEXT NOT NULL DEFAULT '[]',
    issues         TEXT NOT NULL DEFAULT '[]',
    parser_version INTEGER NOT NULL,
    errors         TEXT NOT NULL DEFAULT '[]'
) STRICT;

-- Not unique: who owns a shared doc id is decided on read (spec §4.2).
CREATE INDEX design_doc_by_scope_id ON design_doc (scope, doc_id);

CREATE TABLE design_doc_item (
    doc_key    TEXT NOT NULL REFERENCES design_doc (doc_key) ON DELETE CASCADE,
    item_id    TEXT NOT NULL,
    item_kind  TEXT NOT NULL,
    parent_id  TEXT NULL,
    line_start INTEGER NOT NULL,
    line_end   INTEGER NOT NULL,
    text       TEXT NOT NULL CHECK (length(text) <= 4000),
    PRIMARY KEY (doc_key, item_id)
) STRICT;

CREATE TABLE design_doc_ref (
    doc_key   TEXT NOT NULL REFERENCES design_doc (doc_key) ON DELETE CASCADE,
    from_item TEXT NULL,
    to_ref    TEXT NOT NULL,
    kind      TEXT NOT NULL CHECK (kind IN ('verifies', 'relates', 'blocks'))
) STRICT;

CREATE INDEX design_doc_ref_by_to_ref ON design_doc_ref (to_ref);
CREATE INDEX design_doc_ref_by_doc ON design_doc_ref (doc_key);

-- No resolved columns: a claim resolves on read (spec §4.4), so a PR synced before its doc, or a doc that renames an item, is right on the next read.
CREATE TABLE design_doc_claim (
    pr_key TEXT NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    ref    TEXT NOT NULL,
    PRIMARY KEY (pr_key, ref)
) STRICT;

CREATE INDEX design_doc_claim_by_ref ON design_doc_claim (ref);

-- A PR's claims come from its body when the PR record is written, and a PR that has not changed is never written again, so this is the one-time re-read 0030 did: GitHub PR rows are invalidated
-- (version '', which the store's unchanged-record check never matches) and the GitHub record feed goes back to the start of Recent with no watermark. Nothing else is touched.
UPDATE source_record SET version = '' WHERE source = 'github' AND kind = 'pr';

UPDATE source_cursor SET phase = 'recent', cursor = NULL, backfill_cursor = NULL, backfill_before = NULL WHERE source = 'github' AND repo = 'records';
