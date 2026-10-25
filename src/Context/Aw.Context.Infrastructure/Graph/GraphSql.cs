namespace Aw.Context.Infrastructure.Graph;

/// <summary>The graph store's SQL (tables in Store/Migrations/0002_s3a_graph.sql).</summary>
internal static class GraphSql
{
    public const string InsertVersion =
        "INSERT INTO map_version (repo, commit_sha, key_version, created, status, ruleset) VALUES ($repo, $commit, $keyVersion, $created, 'building', $ruleset) RETURNING id";

    public const string InsertNode =
        "INSERT INTO node (map_version, key, label, attributes, provenance, confidence, trust, written_at) VALUES ($v, $key, $label, $attributes, $provenance, $confidence, $trust, $at)";

    public const string InsertNodeEvidence =
        "INSERT INTO node_evidence (map_version, node_key, position, record, span) VALUES ($v, $key, $position, $record, $span)";

    public const string InsertAlias = "INSERT INTO node_alias (map_version, alias, target) VALUES ($v, $alias, $target)";

    public const string InsertEdge =
        "INSERT INTO edge (map_version, from_key, to_key, type, provenance, confidence, trust, step_order, written_at) VALUES ($v, $from, $to, $type, $provenance, $confidence, $trust, $order, $at)";

    public const string InsertEdgeEvidence =
        "INSERT INTO edge_evidence (edge_id, position, record, span) SELECT id, $position, $record, $span FROM edge WHERE map_version = $v AND from_key = $from AND to_key = $to AND type = $type";

    public const string InsertDecision =
        "INSERT INTO decision (map_version, key, quote, wording, provenance, trust, written_at) VALUES ($v, $key, $quote, $wording, $provenance, $trust, $at)";

    public const string InsertOverride =
        "INSERT INTO map_override (id, action, target, label, other, who, at, rejection, link, undoes, payload) VALUES ($id, $action, $target, $label, $other, $who, $at, $rejection, $link, $undoes, $payload) ON CONFLICT (id) DO NOTHING";

    public const string DeleteBuilding = "DELETE FROM map_version WHERE status = 'building'";

    public const string CountGraphTables = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'map_version'";

    public const string RetireCurrent = "UPDATE map_version SET status = 'retained' WHERE status = 'current'";

    public const string IsBuilding = "SELECT count(*) FROM map_version WHERE id = $v AND status = 'building'";

    public const string MakeCurrent = "UPDATE map_version SET status = 'current' WHERE id = $v AND status = 'building'";

    public const string DeleteRetained = "DELETE FROM map_version WHERE id = $v AND status = 'retained'";

    public const string DeleteBuildingVersion = "DELETE FROM map_version WHERE id = $v AND status = 'building'";

    public const string SelectVersions =
        "SELECT id, repo, commit_sha, key_version, created, status, ruleset FROM map_version WHERE status <> 'building' ORDER BY id DESC";

    // A store not yet upgraded to 0012 (a graph-store read doesn't migrate): the same columns, the ruleset as NULL.
    public const string SelectVersionsBefore0012 =
        "SELECT id, repo, commit_sha, key_version, created, status, NULL FROM map_version WHERE status <> 'building' ORDER BY id DESC";

    public const string HasRulesetColumn = "SELECT count(*) FROM pragma_table_info('map_version') WHERE name = 'ruleset'";

    public const string SelectNodes =
        "SELECT key, label, attributes, provenance, confidence, written_at FROM node WHERE map_version = $v ORDER BY key";

    public const string SelectNodeEvidence =
        "SELECT node_key, record, span FROM node_evidence WHERE map_version = $v ORDER BY node_key, position";

    public const string SelectEdges =
        "SELECT id, from_key, to_key, type, provenance, confidence, step_order, written_at FROM edge WHERE map_version = $v ORDER BY id";

    public const string SelectEdgeEvidence =
        "SELECT ee.edge_id, ee.record, ee.span FROM edge_evidence ee JOIN edge e ON e.id = ee.edge_id WHERE e.map_version = $v ORDER BY ee.edge_id, ee.position";

    public const string SelectAliases = "SELECT alias, target FROM node_alias WHERE map_version = $v ORDER BY alias";

    // The quoted record lives on the decision's 'quotes' edge ('record:' + canonical key), not in the decision row.
    public const string SelectDecisions =
        "SELECT substr(e.to_key, 8), d.quote, d.wording, d.provenance, d.written_at FROM decision d " +
        "JOIN edge e ON e.map_version = d.map_version AND e.from_key = d.key AND e.type = 'quotes' WHERE d.map_version = $v ORDER BY d.key";

    public const string SelectOverrides =
        "SELECT action, target, who, at, label, other, rejection, link, undoes, payload FROM map_override ORDER BY at, id";

    // A store not yet upgraded to 0004 (a graph-store read doesn't migrate: SqliteGraphStore.OpenRead uses the session factory as it is, so a read before any write in a process can meet one): the same columns, the new ones as NULL.
    public const string SelectOverridesBefore0004 =
        "SELECT action, target, who, at, label, other, rejection, NULL, NULL, NULL FROM map_override ORDER BY at, id";

    public const string UpsertRun =
        "INSERT INTO proposal_run (run, repo, surfaces) VALUES ($run, $repo, $surfaces) ON CONFLICT (run) DO UPDATE SET repo = excluded.repo, surfaces = excluded.surfaces";

    public const string SelectRun = "SELECT run, repo, surfaces FROM proposal_run WHERE run = $run";

    // A store not yet upgraded to 0005 (a graph-store read doesn't migrate: SqliteGraphStore.OpenRead) has no run table: no run was ever saved.
    public const string HasRunTable = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'proposal_run'";

    public const string HasReviewColumns = "SELECT count(*) FROM pragma_table_info('map_override') WHERE name = 'payload'";

    public const string DeleteDrafts = "DELETE FROM map_version WHERE status = 'draft'";

    public const string DeleteDraft = "DELETE FROM map_version WHERE id = $v AND status = 'draft'";

    public const string MakeDraft = "UPDATE map_version SET status = 'draft' WHERE id = $v AND status = 'building'";

    public const string VersionStatus = "SELECT count(*) FROM map_version WHERE id = $v AND status = 'draft'";

    public const string MakeCurrentDraft = "UPDATE map_version SET status = 'current' WHERE id = $v AND status = 'draft'";

    public const string SelectVersion =
        "SELECT id, repo, commit_sha, key_version, created, status, ruleset FROM map_version WHERE id = $v";

    public const string CopyVersion =
        "INSERT INTO map_version (repo, commit_sha, key_version, created, status, ruleset) SELECT repo, commit_sha, key_version, $created, 'draft', ruleset FROM map_version WHERE id = $from RETURNING id";

    public const string InsertHead =
        "INSERT INTO map_version_head (version, repo, commit_sha, on_default) VALUES ($v, $repo, $commit, $onDefault)";

    public const string CopyHeads =
        "INSERT INTO map_version_head (version, repo, commit_sha, on_default) SELECT $to, repo, commit_sha, on_default FROM map_version_head WHERE version = $from ORDER BY rowid";

    public const string SelectHeads = "SELECT repo, commit_sha, on_default FROM map_version_head WHERE version = $v ORDER BY rowid";

    public const string SelectHold = "SELECT h.version, c.since FROM map_hold h JOIN map_publish_chain c ON c.id = 1";

    public const string SelectChain = "SELECT since FROM map_publish_chain WHERE id = 1";

    public const string UpsertChain = "INSERT INTO map_publish_chain (id, since) VALUES (1, $since) ON CONFLICT (id) DO UPDATE SET since = excluded.since";

    public const string DeleteHolds = "DELETE FROM map_hold";

    public const string InsertHold = "INSERT INTO map_hold (version) VALUES ($v)";

    public const string DeleteHold = "DELETE FROM map_hold WHERE version = $v";

    public const string DeleteChain = "DELETE FROM map_publish_chain";

    // A store not yet upgraded to 0010 (a graph-store read doesn't migrate): nothing was ever held and no version has heads.
    public const string HasPublishTables = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name IN ('map_hold', 'map_publish_chain', 'map_version_head')";

    public const string LatestDraft = "SELECT id FROM map_version WHERE status = 'draft' ORDER BY id DESC LIMIT 1";

    public static readonly string[] CopyGraph =
    [
        "INSERT INTO node (map_version, key, label, attributes, provenance, confidence, trust, written_at) SELECT $to, key, label, attributes, provenance, confidence, trust, written_at FROM node WHERE map_version = $from",
        "INSERT INTO node_evidence (map_version, node_key, position, record, span) SELECT $to, node_key, position, record, span FROM node_evidence WHERE map_version = $from",
        "INSERT INTO node_alias (map_version, alias, target) SELECT $to, alias, target FROM node_alias WHERE map_version = $from",
        "INSERT INTO edge (map_version, from_key, to_key, type, provenance, confidence, trust, step_order, written_at) SELECT $to, from_key, to_key, type, provenance, confidence, trust, step_order, written_at FROM edge WHERE map_version = $from ORDER BY id",
        "INSERT INTO edge_evidence (edge_id, position, record, span) SELECT n.id, ee.position, ee.record, ee.span FROM edge_evidence ee JOIN edge o ON o.id = ee.edge_id " +
        "JOIN edge n ON n.map_version = $to AND n.from_key = o.from_key AND n.to_key = o.to_key AND n.type = o.type WHERE o.map_version = $from",
        "INSERT INTO decision (map_version, key, quote, wording, provenance, trust, written_at) SELECT $to, key, quote, wording, provenance, trust, written_at FROM decision WHERE map_version = $from",
        "INSERT INTO map_version_head (version, repo, commit_sha, on_default) SELECT $to, repo, commit_sha, on_default FROM map_version_head WHERE version = $from ORDER BY rowid",
    ];
}
