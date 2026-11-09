-- The tdd system database (D17, D23, D24): one Postgres for the whole TDD system, local first, never one per TDD.
-- Filled only by `tdd load-db` after lint has run on a build; the compiler never writes here. Nothing in it is a source
-- of truth: the packet folder and the published page are, and this database indexes them for cross-packet queries.
--
-- Idempotent: every statement can run again on a database that already has the schema (the loader applies this file
-- before each load). Tables follow the eight schema cards of the architecture packet (schema/*.json), corrected where
-- the cards disagreed with each other or with the data the producers emit; each correction is marked "card:" below.
--
-- Grain: `revision` is one row per BUILD (packet, rev, content.json `generated`), not per publish. Child rows hang off
-- a revision and are replaced in one transaction when that build is loaded again, so a reload never duplicates.
-- `decision` is per packet and updated in place (UNIQUE packet_id, prefix, number).

SET client_min_messages = warning;

CREATE TABLE IF NOT EXISTS schema_version (
  one boolean PRIMARY KEY DEFAULT true CHECK (one),        -- at most one row
  version integer NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);
INSERT INTO schema_version (version) VALUES (1) ON CONFLICT (one) DO NOTHING;

-- One row per TDD packet: the container D17 asked for.
CREATE TABLE IF NOT EXISTS packet (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),           -- card: no default
  repo_slug text NOT NULL,                                 -- the packet path's parent-parent folder (or `house`)
  slug text NOT NULL,                                      -- the packet folder name
  title text NOT NULL,
  owner text NOT NULL,                                     -- meta.owner, else the Author in meta.people
  status text NOT NULL CHECK (status IN ('draft','in_review','approved','as_built','superseded')),
  status_label text NULL,                                  -- card: added; meta.status verbatim ("Draft for review")
  packet_path text NOT NULL,                               -- home-relative
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NULL,                             -- meta.updated
  UNIQUE (repo_slug, slug)
);

-- One row per build of a revision. The compile counts, the lint counts and the page size of that build.
CREATE TABLE IF NOT EXISTS revision (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),           -- card: no default
  packet_id uuid NOT NULL REFERENCES packet(id) ON DELETE CASCADE,
  rev integer NOT NULL,
  generated timestamptz NOT NULL,                          -- card: added; content.json `generated`, the build key
  rev_note text NOT NULL,
  sha text NULL,
  sections integer NOT NULL,
  diagrams integer NOT NULL,
  decisions integer NOT NULL,
  prs integer NOT NULL,
  schema_tables integer NOT NULL,
  refs_linked integer NOT NULL,                            -- card: added; the compiler's last line
  refs_unlinked integer NOT NULL,                          -- card: added; distinct unlinked tokens
  citations integer NOT NULL,                              -- card: added
  lint_errors integer NOT NULL,                            -- card: added; lint.json counts
  lint_warnings integer NOT NULL,                          -- card: added
  page_bytes integer NULL,                                 -- card: NOT NULL; NULL when build/tdd.html is absent
  bundle_gzip_bytes integer NULL,
  published_at timestamptz NULL,                           -- card: NOT NULL; NULL until a publish is recorded
  created_at timestamptz NOT NULL DEFAULT now(),
  loaded_at timestamptz NOT NULL DEFAULT now(),            -- card: added; moves on every reload
  UNIQUE (packet_id, rev, generated),                      -- card: UNIQUE (packet_id, rev)
  UNIQUE (id, packet_id)                                   -- target of decision's composite key
);

-- Every owner decision of a packet, in the owner's words, updated in place by the newest loaded build.
CREATE TABLE IF NOT EXISTS decision (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),           -- card: no default
  packet_id uuid NOT NULL REFERENCES packet(id) ON DELETE CASCADE,
  revision_id uuid NULL,                                   -- card: added; the build that last wrote it
  token text NOT NULL,                                     -- card: added; the id as the page writes it (D17, R2)
  prefix text NOT NULL,                                    -- card: added; D, R, Q ...
  number integer NOT NULL,
  title text NOT NULL,
  status text NOT NULL,                                    -- card: added; the status line verbatim
  state text NOT NULL CHECK (state IN ('done','open','held')),  -- card: added
  quote text NULL,                                         -- card: NOT NULL; NULL for open questions, or before the
                                                           -- compiler emits decisions[].quote
  reading text NULL,
  decided_by text NULL,                                    -- card: NOT NULL; NULL while the decision is open
  decided_on date NULL,                                    -- card: NOT NULL; same
  source text NULL,                                        -- card: NOT NULL; where it was said, when the page says
  first_rev integer NOT NULL,                              -- the lowest LOADED rev that carried it (not derived
                                                           -- from the packet's history)
  section_id text NULL,                                    -- card: added
  anchor text NOT NULL,                                    -- card: added
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NULL,
  UNIQUE (packet_id, prefix, number),                      -- card: UNIQUE (packet_id, number); the same for D-rows,
                                                           -- but a packet may carry R2 beside D2
  FOREIGN KEY (revision_id, packet_id) REFERENCES revision (id, packet_id) ON DELETE SET NULL (revision_id)
);
CREATE INDEX IF NOT EXISTS decision_decided_on_idx ON decision (decided_on);

-- Every reference token the compiler linked or failed to link in a build (D5).
CREATE TABLE IF NOT EXISTS reference_link (
  id bigserial PRIMARY KEY,
  revision_id uuid NOT NULL REFERENCES revision(id) ON DELETE CASCADE,
  token text NOT NULL,
  family text NOT NULL,
  section_id text NOT NULL,                                -- content.json refs.links[].section
  anchor text NULL,                                        -- NULL when unlinked
  linked boolean NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS reference_link_revision_idx ON reference_link (revision_id);
CREATE INDEX IF NOT EXISTS reference_link_unlinked_idx ON reference_link (revision_id) WHERE NOT linked;

-- Every path:line citation in a build and where lint resolved it (the CITES rule).
CREATE TABLE IF NOT EXISTS citation (
  id bigserial PRIMARY KEY,
  revision_id uuid NOT NULL REFERENCES revision(id) ON DELETE CASCADE,
  text text NOT NULL,                                      -- card: added; the citation as written
  path text NOT NULL,
  line_from integer NULL,                                  -- card: NOT NULL; a path without a line is a citation too
  line_to integer NULL,
  section_id text NOT NULL,
  anchor text NULL,                                        -- card: added
  resolved_at text NOT NULL CHECK (resolved_at IN ('sha','head','working_tree','packet','missing','unknown')),
                                                           -- card: no 'packet' (a file in the packet or its build,
                                                           -- as lint.json citations[] says) and no 'unknown' (lint.json
                                                           -- without citations[])
  checked_sha text NULL,
  checked_on date NULL,                                    -- card: NOT NULL; NULL when resolved_at is 'unknown'
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS citation_revision_idx ON citation (revision_id);
CREATE INDEX IF NOT EXISTS citation_path_idx ON citation (path);

-- One row per linter finding per build.
CREATE TABLE IF NOT EXISTS lint_finding (
  id bigserial PRIMARY KEY,
  revision_id uuid NOT NULL REFERENCES revision(id) ON DELETE CASCADE,
  family text NOT NULL,                                    -- lint.json `rule`: STRUCT, REFS, CITES ...
  rule text NOT NULL,                                      -- lint.json `check`: REFS/title-token
  severity text NOT NULL CHECK (severity IN ('error','warning','info')),
  where_ text NULL,                                        -- lint.json `file`
  line integer NULL,                                       -- card: added; lint.json `line`
  message text NOT NULL,
  linted_at timestamptz NOT NULL,                          -- lint.json's mtime
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS lint_finding_revision_idx ON lint_finding (revision_id);
CREATE INDEX IF NOT EXISTS lint_finding_rule_idx ON lint_finding (rule, severity);

-- One row per eval scorer result for a build (D10, D12, D18).
CREATE TABLE IF NOT EXISTS eval_run (
  id bigserial PRIMARY KEY,
  revision_id uuid NOT NULL REFERENCES revision(id) ON DELETE CASCADE,
  kind text NOT NULL,
  scorer text NOT NULL,
  value numeric NULL,
  status text NOT NULL CHECK (status IN ('ok','not_run','error')),
  method text NOT NULL,
  reason text NULL,                                        -- card: added; why a scorer did not run
  metrics jsonb NULL,
  braintrust_experiment text NULL,                         -- the experiment name from push.json
  ran_at timestamptz NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (revision_id, kind, scorer)                       -- card: added; a reload cannot duplicate a score
);
CREATE INDEX IF NOT EXISTS eval_run_scorer_idx ON eval_run (kind, scorer);

-- Where each build is published: the claude.ai artifact (the source of truth) and its mirrors.
CREATE TABLE IF NOT EXISTS publication (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),           -- card: no default
  revision_id uuid NOT NULL REFERENCES revision(id) ON DELETE CASCADE,
  surface text NOT NULL CHECK (surface IN ('artifact','notion','other')),
  url text NOT NULL,
  is_source_of_truth boolean NOT NULL DEFAULT false,
  visibility text NULL CHECK (visibility IN ('private','org','public')),  -- card: NOT NULL; meta.json rarely says
  published_at timestamptz NULL,                           -- card: NOT NULL; NULL when meta.json has no `published`
  created_at timestamptz NOT NULL DEFAULT now(),
  UNIQUE (revision_id, surface)
);
