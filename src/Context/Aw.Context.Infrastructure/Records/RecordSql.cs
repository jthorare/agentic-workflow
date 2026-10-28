namespace Aw.Context.Infrastructure.Records;

/// <summary>The record store's SQL (context.db, migration 0018_s4_record_store). Values are always bound, never formatted in.</summary>
internal static class RecordSql
{
    /// <summary>A source's one place in its record feed lives in source_cursor under this repo, apart from the intent sync's own row at '*' (IntentSql.Synced merges its cursor with COALESCE; this row is replaced whole), so neither clobbers the other.</summary>
    internal const string CursorRepo = "records";

    /// <summary>The page commit's transaction is a write session, so the freed pages of every delete are zeroed (spec §4.5).</summary>
    internal const string SecureDelete = "PRAGMA secure_delete = ON";

    internal const string Existing = "SELECT version, text_hash, ruleset, blocked, removed_at FROM source_record WHERE key = @key";

    internal const string UpsertRecord =
        "INSERT INTO source_record (key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at) " +
        "VALUES (@key, @source, @kind, @repo, @audience, @title, @url, @author, @state, @stateRaw, @created, @updated, @closed, @version, @hash, @ruleset, @blocked, NULL, @synced) " +
        "ON CONFLICT (key) DO UPDATE SET source = excluded.source, kind = excluded.kind, repo = excluded.repo, audience_key = excluded.audience_key, title = excluded.title, url = excluded.url, " +
        "author = excluded.author, state = excluded.state, state_raw = excluded.state_raw, created_at = excluded.created_at, updated_at = excluded.updated_at, closed_at = excluded.closed_at, " +
        "version = excluded.version, text_hash = excluded.text_hash, ruleset = excluded.ruleset, blocked = excluded.blocked, removed_at = NULL, synced_at = excluded.synced_at";

    internal const string DeleteLinks = "DELETE FROM record_link WHERE from_key = @key";

    internal const string InsertLink =
        "INSERT INTO record_link (from_key, to_ref, to_key, kind, title) " +
        "VALUES (@key, @toRef, COALESCE(@toKey, (SELECT key FROM source_record WHERE key = @toRef)), @kind, @title) ON CONFLICT DO NOTHING";

    /// <summary>A link that named this record before it was stored now resolves.</summary>
    internal const string ResolveInbound = "UPDATE record_link SET to_key = @key WHERE to_ref = @key AND to_key IS NULL";

    /// <summary>A chunk whose text hash is unchanged is left alone, so its vector stays; a changed one is rewritten in place and its vector goes by trigger.</summary>
    internal const string UpsertChunk =
        "INSERT INTO chunk (key, record, seq, text, tokens, text_hash, line_start, line_end) VALUES (@chunk, @key, @seq, @text, @tokens, @hash, @lineStart, @lineEnd) " +
        "ON CONFLICT (record, seq) DO UPDATE SET text = excluded.text, tokens = excluded.tokens, text_hash = excluded.text_hash, line_start = excluded.line_start, line_end = excluded.line_end " +
        "WHERE chunk.text_hash <> excluded.text_hash";

    internal const string DeleteChunksFrom = "DELETE FROM chunk WHERE record = @key AND seq >= @from";

    /// <summary>The queue row for the intent layer: new, or its version moved, so extraction runs again; the aliases the intent sync found stay.</summary>
    internal const string UpsertIntentRecord =
        "INSERT INTO intent_record (key, source, version, title, aliases, fetched_at, extracted_version, extraction_state, created_at) " +
        "VALUES (@key, @source, @version, @title, '', @at, NULL, 'queued', @created) " +
        "ON CONFLICT (key) DO UPDATE SET source = excluded.source, title = excluded.title, fetched_at = excluded.fetched_at, created_at = excluded.created_at, " +
        "extracted_version = NULL, extraction_state = 'queued', version = excluded.version WHERE intent_record.version <> excluded.version";

    /// <summary>A tombstone and a blocked row keep only key, kind, state and times: the title becomes the key and the free text goes.</summary>
    internal const string Tombstone =
        "UPDATE source_record SET removed_at = @at, title = key, url = NULL, author = NULL, state_raw = NULL, synced_at = @at WHERE key = @key AND source = @source AND removed_at IS NULL";

    internal const string ClearInboundTitles = "UPDATE record_link SET title = NULL WHERE to_key = @key";

    internal const string DeleteChunks = "DELETE FROM chunk WHERE record = @key";

    internal const string SavePosition =
        "INSERT INTO source_cursor (source, repo, cursor, last_sync, last_error, phase, backfill_cursor, backfill_before) VALUES (@source, 'records', @cursor, @at, NULL, @phase, @backfill, @before) " +
        "ON CONFLICT (source, repo) DO UPDATE SET cursor = excluded.cursor, last_sync = excluded.last_sync, last_error = NULL, phase = excluded.phase, backfill_cursor = excluded.backfill_cursor, backfill_before = excluded.backfill_before";

    internal const string BumpRecordStamp = "UPDATE store_epoch SET record_stamp = record_stamp + 1 WHERE id = 1";

    /// <summary>Compacts the full-text segments, so a deleted chunk's words don't outlive it in an old segment.</summary>
    internal const string OptimizeFts = "INSERT INTO chunk_fts (chunk_fts) VALUES ('optimize')";

    internal const string Columns =
        "key, source, kind, repo, audience_key, title, url, author, state, state_raw, created_at, updated_at, closed_at, version, text_hash, ruleset, blocked, removed_at, synced_at";

    internal const string Get = "SELECT " + Columns + " FROM source_record WHERE key = @key";

    internal const string LinksOf = "SELECT to_ref, kind, title, to_key FROM record_link WHERE from_key = @key ORDER BY to_ref, kind";

    internal const string ChunksOf = "SELECT seq, text, tokens, text_hash, line_start, line_end FROM chunk WHERE record = @key ORDER BY seq";

    /// <summary>The list page: every filter is optional (a NULL parameter matches all), the keys come in ordinal order.</summary>
    internal const string List =
        "SELECT " + Columns + " FROM source_record WHERE (@source IS NULL OR source = @source) AND (@repo IS NULL OR repo = @repo) AND (@kind IS NULL OR kind = @kind) " +
        "AND (@removed = 1 OR removed_at IS NULL) AND (@after IS NULL OR key > @after) ORDER BY key LIMIT @limit";

    internal const string ReadPosition = "SELECT cursor, phase, backfill_cursor, backfill_before FROM source_cursor WHERE source = @source AND repo = 'records'";

    internal const string ReadStamps = "SELECT record_stamp, delete_epoch FROM store_epoch WHERE id = 1";

    internal const string ReadVectors =
        "SELECT seq, key, model, text_hash, dim, vec FROM embedding WHERE space = @space AND seq > @after ORDER BY seq LIMIT @limit";

    /// <summary>The embed job's work list: chunks of live records with no vector of this model made from their current text, newest-updated record first.</summary>
    internal const string ChunksToEmbed =
        "SELECT c.key, c.text, c.text_hash FROM chunk c JOIN source_record r ON r.key = c.record " +
        "WHERE r.blocked = 0 AND r.removed_at IS NULL " +
        "AND NOT EXISTS (SELECT 1 FROM embedding e WHERE e.space = 'chunk' AND e.key = c.key AND e.model = @model AND e.text_hash = c.text_hash) " +
        "ORDER BY r.updated_at IS NULL, r.updated_at DESC, c.record, c.seq LIMIT @limit";

    /// <summary>A re-embed replaces the row (a new seq), but only for a chunk that still has the hash.</summary>
    internal const string ReplaceChunkVector =
        "DELETE FROM embedding WHERE space = 'chunk' AND key = @key AND model = @model AND EXISTS (SELECT 1 FROM chunk WHERE key = @key AND text_hash = @hash)";

    internal const string InsertChunkVector =
        "INSERT INTO embedding (space, key, model, text_hash, dim, vec) SELECT 'chunk', @key, @model, @hash, @dim, @vec WHERE EXISTS (SELECT 1 FROM chunk WHERE key = @key AND text_hash = @hash)";

    internal const string ReplaceNodeVector = "DELETE FROM embedding WHERE space = 'node' AND key = @key AND model = @model";

    internal const string InsertNodeVector = "INSERT INTO embedding (space, key, model, text_hash, dim, vec) VALUES ('node', @key, @model, @hash, @dim, @vec)";

    internal const string DeleteVector = "DELETE FROM embedding WHERE space = @space AND key = @key";

    // A source's records, as the purge sees them: what the store holds and what the intent sync queued for it.
    private const string SourceKeys = "(SELECT key FROM source_record WHERE source = @source UNION SELECT key FROM intent_record WHERE source = @source)";

    private const string DecisionKeys = "(SELECT key FROM intent_decision WHERE record IN " + SourceKeys + ")";

    /// <summary>The purge's statements, in order; each binds only '@source'. Chunks are deleted explicitly so their triggers (full-text, vectors, epoch) run.</summary>
    internal static readonly string[] Purge =
    [
        "UPDATE record_link SET to_key = NULL, title = NULL WHERE to_key IN (SELECT key FROM source_record WHERE source = @source) AND from_key NOT IN (SELECT key FROM source_record WHERE source = @source)",
        "DELETE FROM chunk WHERE record IN (SELECT key FROM source_record WHERE source = @source)",
        "DELETE FROM intent_conflict WHERE a IN " + DecisionKeys + " OR b IN " + DecisionKeys,
        "DELETE FROM intent_edge WHERE from_key IN " + DecisionKeys + " OR to_key IN " + DecisionKeys,
        "DELETE FROM intent_decision WHERE record IN " + SourceKeys,
        "DELETE FROM intent_link WHERE record IN " + SourceKeys,
        "DELETE FROM intent_record WHERE source = @source",
        "DELETE FROM source_cursor WHERE source = @source",
    ];

    /// <summary>What a purge would delete: the source's records, their chunks and the chunk vectors of those chunks.</summary>
    internal const string CountSource =
        "SELECT (SELECT COUNT(*) FROM source_record WHERE source = @source), " +
        "(SELECT COUNT(*) FROM chunk WHERE record IN (SELECT key FROM source_record WHERE source = @source)), " +
        "(SELECT COUNT(*) FROM embedding WHERE space = 'chunk' AND key IN (SELECT key FROM chunk WHERE record IN (SELECT key FROM source_record WHERE source = @source)))";

    internal const string DeleteRecords = "DELETE FROM source_record WHERE source = @source";

    /// <summary>
    /// The lexical leg (spec §6.1): chunks that match, of records that are stored, not blocked and in an allowed audience. The mask is part of the query, so the limit
    /// is taken after it. A record with no audience matches only when @records lists its key (rule (c)).
    /// </summary>
    internal const string LexicalSearch =
        "SELECT c.key, r.title, c.line_start, c.line_end, bm25(chunk_fts) " +
        "FROM chunk_fts " +
        "JOIN chunk c ON c.id = chunk_fts.rowid " +
        "JOIN source_record r ON r.key = c.record " +
        "WHERE chunk_fts MATCH @match AND r.removed_at IS NULL AND r.blocked = 0 AND (r.audience_key IN (SELECT value FROM json_each(@audiences)) OR r.key IN (SELECT value FROM json_each(@records))) " +
        "ORDER BY bm25(chunk_fts), c.id " +
        "LIMIT @limit";

    /// <summary>Whether the store holds any chunk at all, whatever its audience.</summary>
    internal const string AnyChunk = "SELECT EXISTS (SELECT 1 FROM chunk)";
}
