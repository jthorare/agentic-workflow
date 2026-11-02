using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The persisted backoff of a source's feed (migration 0022_s4_source_backoff), beside its last outcome.</summary>
internal sealed partial class SqliteRecordStore
{
    public async ValueTask<Result<int>> RecordAttempt(SourceId source, SyncAttempt attempt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        // why: the caller holds the source's lock around a tick, so the read and the write below are not raced by another attempt for the same source
        var previous = await Backoff(source, cancellationToken).ConfigureAwait(false);
        if (previous.IsError)
        {
            return previous.Error;
        }

        var next = SourceBackoff.Apply(previous.Value, attempt);
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var saved = await Run(begun.Value, RecordSql.SaveBackoff, c => c.Bind("@source", source.Value).Bind("@failures", next.Failures).BindAt("@nextTry", next.NextTry).BindAt("@lastCommit", next.LastCommit).Bind("@errorText", next.LastErrorText), cancellationToken).ConfigureAwait(false);
            return saved.IsError ? saved.Error : await Finish(begun.Value, deleted: false, saved.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<Result<SourceBackoffState>> Backoff(SourceId source, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : SourceBackoffState.None;
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ReadBackoff, c => c.Bind("@source", source.Value)), RecordMappers.Backoff, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value.Count == 0 ? SourceBackoffState.None : rows.Value[0];
        }
    }
}
