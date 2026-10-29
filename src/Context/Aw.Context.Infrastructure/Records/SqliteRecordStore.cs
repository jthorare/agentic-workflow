using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>
/// The record store in context.db (migration 0018_s4_record_store, spec §4): reads, vectors, the position and the purge are here, the page commit is in the other part of this class. Every write
/// is one 'BEGIN IMMEDIATE' transaction on its own connection with 'secure_delete' on.
/// </summary>
internal sealed partial class SqliteRecordStore(ContextDatabase database) : IRecordStore
{
    public async ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<StoredRecord?>(value: null);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.Get, c => c.Bind("@key", key.Value)), RecordMappers.Record, cancellationToken).ConfigureAwait(false);
            if (rows.IsError)
            {
                return ContextDatabase.Translate(rows.Error);
            }

            if (rows.Value.Count == 0)
            {
                return Results.Success<StoredRecord?>(value: null);
            }

            var full = await WithLinks(session, rows.Value[0], cancellationToken).ConfigureAwait(false);
            return full.IsError ? full.Error : Results.Success<StoredRecord?>(full.Value);
        }
    }

    public async ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<RecordChunk>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ChunksOf, c => c.Bind("@key", key.Value)), RecordMappers.Chunk, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    public async ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<StoredRecord>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var statement = ListStatement(filter, after, limit);
            var rows = await SqliteQueries.List(session, statement, RecordMappers.Record, cancellationToken).ConfigureAwait(false);
            if (rows.IsError)
            {
                return ContextDatabase.Translate(rows.Error);
            }

            return await AllWithLinks(session, rows.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : SyncPosition.Start;
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ReadPosition, c => c.Bind("@source", source.Value)), RecordMappers.Position, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value.Count == 0 ? SyncPosition.Start : rows.Value[0];
        }
    }

    public async ValueTask<Result<int>> RecordOutcome(SourceId source, SyncOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var saved = await Run(begun.Value, RecordSql.SaveOutcome, c => c.Bind("@source", source.Value).Bind("@at", outcome.At).Bind("@code", outcome.Code), cancellationToken).ConfigureAwait(false);
            return saved.IsError ? saved.Error : await Finish(begun.Value, deleted: false, saved.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<Result<SyncOutcome?>> LastOutcome(SourceId source, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<SyncOutcome?>(value: null);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ReadOutcome, c => c.Bind("@source", source.Value)), RecordMappers.Outcome, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
        }
    }

    public async ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : new StoreStamps(0, 0);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ReadStamps), RecordMappers.Stamps, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value.Count == 0 ? new StoreStamps(0, 0) : rows.Value[0];
        }
    }

    public async ValueTask<Result<LiveRecordCounts>> LiveCounts(CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : new LiveRecordCounts(0, 0);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.CountLive), RecordMappers.Live, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value[0];
        }
    }

    public async ValueTask<Result<SourceCounts>> Count(SourceId source, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : new SourceCounts(0, 0, 0);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.CountSource, c => c.Bind("@source", source.Value)), RecordMappers.Counts, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value[0];
        }
    }

    public ValueTask<Result<IReadOnlyList<ProjectRow>>> ProjectFacts(SourceId source, AudienceScope audience, CancellationToken cancellationToken) => throw new NotSupportedException();

    public async ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken)
    {
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            void Bind(Microsoft.Data.Sqlite.SqliteCommand c) => c.Bind("@source", source.Value);
            foreach (var sql in RecordSql.Purge)
            {
                var step = await Run(begun.Value, sql, Bind, cancellationToken).ConfigureAwait(false);
                if (step.IsError)
                {
                    return step.Error;
                }
            }

            var records = await Run(begun.Value, RecordSql.DeleteRecords, Bind, cancellationToken).ConfigureAwait(false);
            var stamped = records.IsError ? records : await Run(begun.Value, RecordSql.BumpRecordStamp, bind: null, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (stamped.IsError)
            {
                return stamped.Error;
            }

            return await Finish(begun.Value, deleted: true, records.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SqlStatement ListStatement(RecordFilter filter, RecordKey? after, int limit) => new(RecordSql.List, c => c
        .Bind("@source", filter.Source?.Value)
        .Bind("@repo", filter.Repo?.Value)
        .Bind("@kind", filter.Kind is { } kind ? RecordWire.Of(kind) : null)
        .Bind("@removed", filter.IncludeRemoved)
        .Bind("@after", after?.Value)
        .Bind("@limit", limit));

    private static async ValueTask<Result<IReadOnlyList<StoredRecord>>> AllWithLinks(SqliteReadSession session, IReadOnlyList<StoredRecord> rows, CancellationToken cancellationToken)
    {
        var page = new List<StoredRecord>(rows.Count);
        foreach (var row in rows)
        {
            var full = await WithLinks(session, row, cancellationToken).ConfigureAwait(false);
            if (full.IsError)
            {
                return full.Error;
            }

            page.Add(full.Value);
        }

        return page;
    }

    private static async ValueTask<Result<StoredRecord>> WithLinks(SqliteReadSession session, StoredRecord record, CancellationToken cancellationToken)
    {
        var links = await SqliteQueries.List(session, new SqlStatement(RecordSql.LinksOf, c => c.Bind("@key", record.Key.Value)), RecordMappers.Link, cancellationToken).ConfigureAwait(false);
        return links.IsError ? ContextDatabase.Translate(links.Error) : record with { Links = links.Value };
    }

    // A reader on the file, or null when nothing was ever written (a store never created has no records, chunks or vectors).
    private async ValueTask<Result<SqliteReadSession?>> BeginRead(CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : (SqliteReadSession?)null;
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        return read.IsError ? ContextDatabase.Translate(read.Error) : read.Value;
    }

    // A write transaction (BEGIN IMMEDIATE) with secure_delete on, so every page a delete frees is zeroed, not just unlinked.
    private async ValueTask<Result<SqliteUnitOfWork>> BeginWrite(CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        var pragma = await Run(begun.Value, RecordSql.SecureDelete, bind: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (pragma.IsError)
        {
            await begun.Value.DisposeAsync().ConfigureAwait(false);
            return pragma.Error;
        }

        return begun.Value;
    }

    // Commits, then, when chunks went, compacts the full-text segments and truncates the WAL, so no old page image or segment keeps the deleted words (spec §4.5).
    private async ValueTask<Result<int>> Finish(SqliteUnitOfWork session, bool deleted, int result, CancellationToken cancellationToken)
    {
        var optimized = deleted ? await Run(session, RecordSql.OptimizeFts, bind: null, cancellationToken: cancellationToken).ConfigureAwait(false) : Results.Success(0);
        var committed = optimized.IsError ? optimized.Error : await session.Commit(cancellationToken).ConfigureAwait(false);
        database.RestrictFiles();
        if (committed.IsError)
        {
            return ContextDatabase.Translate(committed.Error);
        }

        if (deleted)
        {
            database.TruncateWal();
        }

        return result;
    }

    // A statement's failure is the store being unavailable (SND-CONTEXT-004), whatever the Kernel.Data code was.
    private static async ValueTask<Result<int>> Run(ISqliteSession session, string sql, Action<Microsoft.Data.Sqlite.SqliteCommand>? bind, CancellationToken cancellationToken)
    {
        var done = await SqliteQueries.Execute(session, new SqlStatement(sql, bind), cancellationToken).ConfigureAwait(false);
        return done.IsError ? ContextDatabase.Translate(done.Error) : done;
    }
}
