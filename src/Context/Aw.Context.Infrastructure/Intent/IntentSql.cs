namespace Aw.Context.Infrastructure.Intent;

/// <summary>The intent layer's SQL (context.db, migration 0006_s5b_intent_links, created_at from 0007_s5b_intent_created). The cursor lives in source_cursor under repo '*'.</summary>
internal static class IntentSql
{
    internal const string CursorRepo = "*";

    /// <summary>A record that is new, or whose version moved, is queued for extraction again; the same version changes nothing (a replay writes no rows).</summary>
    internal const string UpsertRecord =
        "INSERT INTO intent_record (key, source, version, title, aliases, fetched_at, extracted_version, extraction_state, created_at) " +
        "VALUES (@key, @source, @version, @title, @aliases, @at, NULL, 'queued', @created) " +
        "ON CONFLICT (key) DO UPDATE SET source = excluded.source, title = excluded.title, aliases = excluded.aliases, fetched_at = excluded.fetched_at, created_at = excluded.created_at, " +
        "extracted_version = NULL, extraction_state = 'queued', version = excluded.version WHERE intent_record.version <> excluded.version";

    // A record moving to another map version is queued in the same statement (a done one included; its decisions stay until the extraction replaces them): only a finished extraction marks it done again,
    // so a run that stops or fails between this commit and the extraction cannot leave it done with the old map's decisions. SQLite evaluates every right-hand side against the old row.
    internal const string MarkLinked =
        "UPDATE intent_record SET extracted_version = CASE WHEN linked_map <> @map THEN NULL ELSE extracted_version END, " +
        "extraction_state = CASE WHEN linked_map <> @map THEN 'queued' ELSE extraction_state END, linked_map = @map WHERE key = @record";

    internal const string LinkedKeys = "SELECT key FROM intent_record WHERE source = @source AND linked_map = @map ORDER BY key";

    internal const string DeleteLinks = "DELETE FROM intent_link WHERE record = @record";

    internal const string InsertLink =
        "INSERT INTO intent_link (record, target, rule, confidence, span_hash, synced_at) VALUES (@record, @target, @rule, @confidence, @span, @at) ON CONFLICT DO NOTHING";

    internal const string Synced =
        "INSERT INTO source_cursor (source, repo, cursor, last_sync, last_error) VALUES (@source, '*', @cursor, @at, NULL) " +
        "ON CONFLICT (source, repo) DO UPDATE SET cursor = COALESCE(excluded.cursor, source_cursor.cursor), last_sync = excluded.last_sync, last_error = NULL";

    internal const string Failed =
        "INSERT INTO source_cursor (source, repo, cursor, last_sync, last_error) VALUES (@source, '*', NULL, NULL, @code) " +
        "ON CONFLICT (source, repo) DO UPDATE SET last_error = excluded.last_error";

    internal const string Records = "SELECT key, source, version, title, aliases, created_at, fetched_at FROM intent_record ORDER BY key";

    internal const string Links = "SELECT record, target, rule, confidence, span_hash FROM intent_link ORDER BY record, target, rule";

    internal const string Decisions = "SELECT record, quote, wording, provenance, at FROM intent_decision ORDER BY key";

    internal const string Edges = "SELECT from_key, to_key, type, provenance, confidence, at FROM intent_edge ORDER BY from_key, to_key, type";

    internal const string Conflicts = "SELECT a, b, verdict, at, tries FROM intent_conflict ORDER BY a, b";

    internal const string QueuedKeys = "SELECT key FROM intent_record WHERE source = @source AND extraction_state = 'queued' ORDER BY key";

    internal const string MarkExtracted =
        "UPDATE intent_record SET extracted_version = version, extraction_state = 'done' WHERE key = @record AND version = @version";

    // A pending save (the conflict checks are still to run) queues the record again, a done one included: a map change re-extracts it, and a stopped check is retried by the next sync.
    internal const string Touch = "UPDATE intent_record SET extracted_version = NULL, extraction_state = 'queued' WHERE key = @record AND version = @version";

    internal const string SetAliases = "UPDATE intent_record SET aliases = @aliases WHERE key = @record AND version = @version";

    // The record's machine decisions the new batch no longer holds (@keys: a JSON array of the batch's keys). A decision that survives keeps its verdicts; a human one is never replaced.
    private const string Gone = "SELECT key FROM intent_decision WHERE record = @record AND provenance <> 'human' AND key NOT IN (SELECT value FROM json_each(@keys))";

    internal const string DropConflicts = "DELETE FROM intent_conflict WHERE a IN (" + Gone + ") OR b IN (" + Gone + ")";

    internal const string DropEdges = "DELETE FROM intent_edge WHERE from_key IN (SELECT key FROM intent_decision WHERE record = @record AND provenance <> 'human')";

    internal const string DropDecisions = "DELETE FROM intent_decision WHERE record = @record AND key IN (" + Gone + ")";

    internal const string InsertDecision =
        "INSERT INTO intent_decision (key, record, quote, wording, provenance, at) VALUES (@key, @record, @quote, @wording, @provenance, @at) ON CONFLICT (key) DO UPDATE SET wording = excluded.wording WHERE provenance <> 'human'";

    internal const string InsertEdge =
        "INSERT INTO intent_edge (from_key, to_key, type, provenance, confidence, at) VALUES (@from, @to, @type, @provenance, @confidence, @at) ON CONFLICT DO NOTHING";

    // A pair is stored once; only an 'unread' row (answers that could not be read yet) is overwritten, by the next try or the verdict.
    internal const string InsertConflict =
        "INSERT INTO intent_conflict (a, b, verdict, at, tries) VALUES (@a, @b, @verdict, @at, @tries) " +
        "ON CONFLICT (a, b) DO UPDATE SET verdict = excluded.verdict, at = excluded.at, tries = excluded.tries WHERE intent_conflict.verdict = 'unread'";

    internal const string State =
        "SELECT (SELECT cursor FROM source_cursor WHERE source = @source AND repo = '*'), " +
        "(SELECT last_sync FROM source_cursor WHERE source = @source AND repo = '*'), " +
        "(SELECT last_error FROM source_cursor WHERE source = @source AND repo = '*'), " +
        "(SELECT count(*) FROM intent_record WHERE source = @source), " +
        "(SELECT count(*) FROM intent_link l JOIN intent_record r ON r.key = l.record WHERE r.source = @source), " +
        "(SELECT count(*) FROM intent_decision d JOIN intent_record r ON r.key = d.record WHERE r.source = @source), " +
        "(SELECT count(*) FROM intent_record WHERE source = @source AND extraction_state = 'queued')";
}
