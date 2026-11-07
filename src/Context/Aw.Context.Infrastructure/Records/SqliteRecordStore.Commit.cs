using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

internal sealed partial class SqliteRecordStore
{
    /// <summary>
    /// One page in one 'BEGIN IMMEDIATE' transaction (spec §5.5): the records, their links and chunks (the full-text rows follow by trigger), the intent layer's queue rows, the tombstones and the moved
    /// position, and a design doc's items and refs or a PR's claims (spec 2026-11-04 §5.3). Any failure rolls all of it back, so a cursor never moves past a record that did not commit.
    /// </summary>
    public async ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (Problem(batch) is { } problem)
        {
            return ContextErrors.RecordBatchInvalid.With(problem);
        }

        return await Guarded(() => CommitPage(batch, cancellationToken), "commit").ConfigureAwait(false);
    }

    private async ValueTask<Result<CommitReport>> CommitPage(RecordBatch batch, CancellationToken cancellationToken)
    {
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var written = await WritePage(begun.Value, batch, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }

            var (report, removedText) = written.Value;
            var failure = (batch.Position is { } position ? await Exec(begun.Value, RecordSql.SavePosition, PositionBinder(batch.Source, batch.At, position), cancellationToken).ConfigureAwait(false) : null)
                ?? (report.Written + report.Tombstoned > 0 ? await Exec(begun.Value, RecordSql.BumpRecordStamp, bind: null, cancellationToken: cancellationToken).ConfigureAwait(false) : null);
            if (failure is not null)
            {
                return failure;
            }

            var finished = await Finish(begun.Value, removedText, result: 0, cancellationToken).ConfigureAwait(false);
            return finished.IsError ? finished.Error : report;
        }
    }

    private static Action<SqliteCommand> PositionBinder(SourceId source, DateTimeOffset at, SyncPosition position) => c => c
        .Bind("@source", source.Value)
        .Bind("@cursor", position.Cursor?.Value)
        .Bind("@at", at)
        .Bind("@phase", position.Phase is { } phase ? RecordWire.Of(phase) : null)
        .Bind("@backfill", position.BackfillCursor?.Value)
        .BindAt("@before", position.BackfillBefore);

    private async ValueTask<Result<(CommitReport Report, bool RemovedText)>> WritePage(SqliteUnitOfWork session, RecordBatch batch, CancellationToken cancellationToken)
    {
        var records = await WriteRecords(session, batch, cancellationToken).ConfigureAwait(false);
        if (records.IsError)
        {
            return records.Error;
        }

        var tombstones = await Tombstone(session, batch, cancellationToken).ConfigureAwait(false);
        return tombstones.IsError
            ? tombstones.Error
            : (new CommitReport(records.Value.Written, records.Value.Unchanged, tombstones.Value), records.Value.ChangedOld || tombstones.Value > 0);
    }

    // An old text, even a replaced one, may still sit in a freed page or an old full-text segment, hence ChangedOld.
    private async ValueTask<Result<(int Written, int Unchanged, bool ChangedOld)>> WriteRecords(SqliteUnitOfWork session, RecordBatch batch, CancellationToken cancellationToken)
    {
        int written = 0, unchanged = 0;
        var changedOld = false;
        foreach (var write in batch.Records)
        {
            var existing = await StoredBefore(session, write.Record.Key, cancellationToken).ConfigureAwait(false);
            if (existing.IsError)
            {
                return existing.Error;
            }

            var before = existing.Value;
            if (batch.Guard && Stale(before, write.Record))
            {
                unchanged++;   // a newer write is stored: this page read the record before it
                continue;
            }

            if (before is { Removed: false } && Same(before, write.Record))
            {
                var side = await WriteChangedSide(session, write, cancellationToken).ConfigureAwait(false);
                if (side.IsError)
                {
                    return side.Error;
                }

                unchanged++;
                continue;
            }

            if (await WriteRecord(session, batch, write, isNew: before is null, cancellationToken).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }

            written++;
            changedOld |= before is not null;
        }

        return (written, unchanged, changedOld);
    }

    private static async ValueTask<Result<ExistingRecord?>> StoredBefore(SqliteUnitOfWork session, RecordKey key, CancellationToken cancellationToken)
    {
        var existing = await SqliteQueries.List(session, new SqlStatement(RecordSql.Existing, c => c.Bind("@key", key.Value)), RecordMappers.Existing, cancellationToken).ConfigureAwait(false);
        return existing.IsError ? ContextDatabase.Translate(existing.Error) : Results.Success(existing.Value.Count == 0 ? null : existing.Value[0]);
    }

    private async ValueTask<Result<int>> Tombstone(SqliteUnitOfWork session, RecordBatch batch, CancellationToken cancellationToken)
    {
        var tombstoned = 0;
        foreach (var removed in batch.Removed)
        {
            var done = await Run(session, RecordSql.Tombstone, c => c.Bind("@key", removed.Value).Bind("@source", batch.Source.Value).Bind("@at", batch.At), cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return done.Error;
            }

            if (done.Value == 0)
            {
                continue;   // not stored, already a tombstone, or another source's: nothing to do
            }

            var failure = await Exec(session, RecordSql.DeleteChunks, c => c.Bind("@key", removed.Value), cancellationToken).ConfigureAwait(false)
                ?? await Exec(session, RecordSql.DeleteLinks, c => c.Bind("@key", removed.Value), cancellationToken).ConfigureAwait(false)
                ?? await Exec(session, RecordSql.ClearInboundTitles, c => c.Bind("@key", removed.Value), cancellationToken).ConfigureAwait(false)
                ?? await DeleteSide(session, removed, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            tombstoned++;
        }

        return tombstoned;
    }

    // The stored record is strictly newer than the incoming one; an equal time passes, and so does a record with no time on either side.
    private static bool Stale(ExistingRecord? stored, StoredRecord record) =>
        stored?.UpdatedAt is { } kept && record.UpdatedAt is { } incoming && incoming < kept;

    private static bool Same(ExistingRecord stored, StoredRecord record) =>
        string.Equals(stored.Version, record.Version.Value, StringComparison.Ordinal)
        && string.Equals(stored.TextHash, record.TextHash, StringComparison.Ordinal)
        && string.Equals(stored.Ruleset, record.Ruleset, StringComparison.Ordinal)
        && stored.Blocked == record.Blocked
        && string.Equals(stored.Audience, record.Audience?.Value, StringComparison.Ordinal);

    private async ValueTask<Error?> WriteRecord(SqliteUnitOfWork session, RecordBatch batch, RecordWrite write, bool isNew, CancellationToken cancellationToken)
    {
        var record = write.Record;
        var key = record.Key.Value;
        var failure = await Exec(session, RecordSql.UpsertRecord, c => BindRecord(c, record), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, RecordSql.DeleteLinks, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false)
            ?? await WriteSide(session, write, isNew, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        if (record.Blocked)
        {
            // A blocked row keeps no text, no links and no title a reader could take for the record's own (spec §4.5).
            return await Exec(session, RecordSql.DeleteChunks, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false)
                ?? await Exec(session, RecordSql.ClearInboundTitles, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false);
        }

        if (await WriteLinks(session, record, isNew, cancellationToken).ConfigureAwait(false) is { } linkFailure)
        {
            return linkFailure;
        }

        return await WriteChunks(session, write, cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, RecordSql.UpsertIntentRecord, c => c
                .Bind("@key", key)
                .Bind("@source", batch.Source.Value)
                .Bind("@version", record.Version.Value)
                .Bind("@title", record.Title)
                .Bind("@at", batch.At)
                .BindAt("@created", record.CreatedAt), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Error?> WriteChunks(SqliteUnitOfWork session, RecordWrite write, CancellationToken cancellationToken)
    {
        var key = write.Record.Key.Value;
        foreach (var chunk in write.Chunks)
        {
            var chunkKey = new ChunkKey(write.Record.Key, chunk.Seq).ToString();
            var failure = await Exec(session, RecordSql.UpsertChunk, c => c
                .Bind("@chunk", chunkKey)
                .Bind("@key", key)
                .Bind("@seq", chunk.Seq)
                .Bind("@text", chunk.Text)
                .Bind("@tokens", chunk.Tokens)
                .Bind("@hash", chunk.TextHash)
                .Bind("@lineStart", chunk.LineStart)
                .Bind("@lineEnd", chunk.LineEnd), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        return await Exec(session, RecordSql.DeleteChunksFrom, c => c.Bind("@key", key).Bind("@from", write.Chunks.Count), cancellationToken).ConfigureAwait(false);
    }

    // Links are rewritten with their record; a link named before its target was stored is resolved by the target's own first write.
    private static async ValueTask<Error?> WriteLinks(SqliteUnitOfWork session, StoredRecord record, bool isNew, CancellationToken cancellationToken)
    {
        var key = record.Key.Value;
        foreach (var link in record.Links)
        {
            var failure = await Exec(session, RecordSql.InsertLink, c => c
                .Bind("@key", key)
                .Bind("@toRef", link.ToRef)
                .Bind("@toKey", link.ToKey?.Value)
                .Bind("@kind", RecordWire.Of(link.Kind))
                .Bind("@title", link.Title), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        return isNew ? await Exec(session, RecordSql.ResolveInbound, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false) : null;
    }

    private static void BindRecord(SqliteCommand c, StoredRecord record)
    {
        _ = c.Bind("@key", record.Key.Value)
            .Bind("@source", record.Source.Value)
            .Bind("@kind", RecordWire.Of(record.Kind))
            .Bind("@repo", record.Repo?.Value)
            .Bind("@audience", record.Audience?.Value)
            .Bind("@state", record.State is { } state ? RecordWire.Of(state) : null)
            .BindAt("@created", record.CreatedAt)
            .BindAt("@updated", record.UpdatedAt)
            .BindAt("@closed", record.ClosedAt)
            .Bind("@version", record.Version.Value)
            .Bind("@hash", record.TextHash)
            .Bind("@ruleset", record.Ruleset)
            .Bind("@blocked", record.Blocked)
            .Bind("@synced", record.SyncedAt)
            .Bind("@health", record.Health)
            .Bind("@dueAt", record.DueAt?.ToString(RecordMappers.DueFormat, CultureInfo.InvariantCulture));
        BindText(c, record);
    }

    // A blocked record's row carries only its key, kind, state and times.
    private static void BindText(SqliteCommand c, StoredRecord record)
    {
        var blocked = record.Blocked;
        _ = c.Bind("@title", blocked ? record.Key.Value : record.Title)
            .Bind("@url", blocked ? null : record.Url)
            .Bind("@author", blocked ? null : record.Author)
            .Bind("@stateRaw", blocked ? null : record.StateRaw)
            .Bind("@description", blocked ? null : record.Description)
            .Bind("@pull", blocked ? null : PullFactsJson.Write(record.Pull));
    }

    // A statement whose row count doesn't matter: its error, or null.
    internal static async ValueTask<Error?> Exec(ISqliteSession session, string sql, Action<SqliteCommand>? bind, CancellationToken cancellationToken)
    {
        var done = await Run(session, sql, bind, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : null;
    }
}
