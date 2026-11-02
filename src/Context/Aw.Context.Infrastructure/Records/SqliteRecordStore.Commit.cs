using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

internal sealed partial class SqliteRecordStore
{
    /// <summary>
    /// One page in one 'BEGIN IMMEDIATE' transaction (spec §5.5): the records, their links and chunks (the full-text rows follow by trigger), the intent layer's queue rows, the tombstones and the moved
    /// position. Any failure rolls all of it back, so a cursor never moves past a record that did not commit.
    /// </summary>
    public async ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (Problem(batch) is { } problem)
        {
            return ContextErrors.RecordBatchInvalid.With(problem);
        }

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
            var failure = await Exec(begun.Value, RecordSql.SavePosition, PositionBinder(batch), cancellationToken).ConfigureAwait(false)
                ?? (report.Written + report.Tombstoned > 0 ? await Exec(begun.Value, RecordSql.BumpRecordStamp, bind: null, cancellationToken: cancellationToken).ConfigureAwait(false) : null);
            if (failure is not null)
            {
                return failure;
            }

            var finished = await Finish(begun.Value, removedText, result: 0, cancellationToken).ConfigureAwait(false);
            return finished.IsError ? finished.Error : report;
        }
    }

    // Why a page can't be committed, or null. Checked before anything is opened, so a refused page touches nothing.
    private static string? Problem(RecordBatch batch)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var write in batch.Records)
        {
            var record = write.Record;
            if (record.Source != batch.Source)
            {
                return $"record '{record.Key.Value}' belongs to source '{record.Source.Value}', not '{batch.Source.Value}'";
            }

            if (!keys.Add(record.Key.Value))
            {
                return $"record '{record.Key.Value}' is in the page twice";
            }

            if (record.Blocked && write.Chunks.Count > 0)
            {
                return $"record '{record.Key.Value}' is blocked and must have no chunks";
            }

            for (var i = 0; i < write.Chunks.Count; i++)
            {
                if (write.Chunks[i].Seq != i)
                {
                    return $"record '{record.Key.Value}' has chunk number {write.Chunks[i].Seq.ToString(CultureInfo.InvariantCulture)} where {i.ToString(CultureInfo.InvariantCulture)} was expected";
                }
            }
        }

        return batch.Removed.Any(k => keys.Contains(k.Value)) ? "a record is both written and removed in the page" : null;
    }

    private static Action<SqliteCommand> PositionBinder(RecordBatch batch) => c => c
        .Bind("@source", batch.Source.Value)
        .Bind("@cursor", batch.Position.Cursor?.Value)
        .Bind("@at", batch.At)
        .Bind("@phase", batch.Position.Phase is { } phase ? RecordWire.Of(phase) : null)
        .Bind("@backfill", batch.Position.BackfillCursor?.Value)
        .BindAt("@before", batch.Position.BackfillBefore);

    private static async ValueTask<Result<(CommitReport Report, bool RemovedText)>> WritePage(SqliteUnitOfWork session, RecordBatch batch, CancellationToken cancellationToken)
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
    private static async ValueTask<Result<(int Written, int Unchanged, bool ChangedOld)>> WriteRecords(SqliteUnitOfWork session, RecordBatch batch, CancellationToken cancellationToken)
    {
        int written = 0, unchanged = 0;
        var changedOld = false;
        foreach (var write in batch.Records)
        {
            var key = write.Record.Key.Value;
            var existing = await SqliteQueries.List(session, new SqlStatement(RecordSql.Existing, c => c.Bind("@key", key)), RecordMappers.Existing, cancellationToken).ConfigureAwait(false);
            if (existing.IsError)
            {
                return ContextDatabase.Translate(existing.Error);
            }

            var before = existing.Value.Count == 0 ? null : existing.Value[0];
            if (before is { Removed: false } && Same(before, write.Record))
            {
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

    private static async ValueTask<Result<int>> Tombstone(SqliteUnitOfWork session, RecordBatch batch, CancellationToken cancellationToken)
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
                ?? await Exec(session, RecordSql.ClearInboundTitles, c => c.Bind("@key", removed.Value), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            tombstoned++;
        }

        return tombstoned;
    }

    private static bool Same(ExistingRecord stored, StoredRecord record) =>
        string.Equals(stored.Version, record.Version.Value, StringComparison.Ordinal)
        && string.Equals(stored.TextHash, record.TextHash, StringComparison.Ordinal)
        && string.Equals(stored.Ruleset, record.Ruleset, StringComparison.Ordinal)
        && stored.Blocked == record.Blocked;

    private static async ValueTask<Error?> WriteRecord(SqliteUnitOfWork session, RecordBatch batch, RecordWrite write, bool isNew, CancellationToken cancellationToken)
    {
        var record = write.Record;
        var key = record.Key.Value;
        var failure = await Exec(session, RecordSql.UpsertRecord, c => BindRecord(c, record), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, RecordSql.DeleteLinks, c => c.Bind("@key", key), cancellationToken).ConfigureAwait(false);
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
        // A blocked record's row carries only its key, kind, state and times.
        var blocked = record.Blocked;
        _ = c.Bind("@key", record.Key.Value)
            .Bind("@source", record.Source.Value)
            .Bind("@kind", RecordWire.Of(record.Kind))
            .Bind("@repo", record.Repo?.Value)
            .Bind("@audience", record.Audience?.Value)
            .Bind("@title", blocked ? record.Key.Value : record.Title)
            .Bind("@url", blocked ? null : record.Url)
            .Bind("@author", blocked ? null : record.Author)
            .Bind("@state", record.State is { } state ? RecordWire.Of(state) : null)
            .Bind("@stateRaw", blocked ? null : record.StateRaw)
            .BindAt("@created", record.CreatedAt)
            .BindAt("@updated", record.UpdatedAt)
            .BindAt("@closed", record.ClosedAt)
            .Bind("@version", record.Version.Value)
            .Bind("@hash", record.TextHash)
            .Bind("@ruleset", record.Ruleset)
            .Bind("@blocked", blocked)
            .Bind("@synced", record.SyncedAt)
            .Bind("@health", record.Health)
            .Bind("@dueAt", record.DueAt?.ToString(RecordMappers.DueFormat, CultureInfo.InvariantCulture))
            .Bind("@description", blocked ? null : record.Description);
    }

    // A statement whose row count doesn't matter: its error, or null.
    private static async ValueTask<Error?> Exec(ISqliteSession session, string sql, Action<SqliteCommand>? bind, CancellationToken cancellationToken)
    {
        var done = await Run(session, sql, bind, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : null;
    }
}
