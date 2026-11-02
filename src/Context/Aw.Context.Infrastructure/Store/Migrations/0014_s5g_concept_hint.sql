-- Concept scan (owner, q110): the product concepts a model found in a draft map, kept per version as a JSON list of hints. They sit beside the map
-- versions, not in them: the graph, the overrides and the pins never change because of a scan, and a version that is purged leaves a small row behind.
CREATE TABLE map_concept_hint (
    version INTEGER PRIMARY KEY,
    scanned TEXT NOT NULL,
    hints   TEXT NOT NULL
) STRICT;
