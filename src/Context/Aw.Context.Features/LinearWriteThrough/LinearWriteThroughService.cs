using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.LinearWriteThrough;

/// <summary>
/// Stores one Linear issue's state right after a write changed it (Linear hub spec 2026-11-04 D4), so a page that reads the store shows it at once. It re-reads the issue with the feed's own query and mapper
/// (<see cref="ILinearIssueReads"/>), builds the batch with <see cref="RecordBatches"/> (scrubbed, fenced, ruled and chunked like a tick's page, with no position, so the cursor stays) and commits it with
/// <see cref="RecordBatch.Guard"/>, so a tick that fetched before the write cannot put the old value back and the write cannot revert a newer one. It takes no lock: a tick holds the source's for minutes and the
/// write cannot wait on it. While a purge holds the source it skips with SND-LINEAR-013 ("Saved in Linear; Sindri shows it after the next sync").
/// </summary>
internal sealed class LinearWriteThroughService(ILinearIssueReads reads, IRecordStore store, IScrubberFactory scrubberFactory, ISourcePurges purges, TimeProvider clock)
    : IService<LinearWriteThroughRequest, LinearWriteThroughResponse>
{
    public async ValueTask<Result<LinearWriteThroughResponse>> Handle(LinearWriteThroughRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        if (settings.Value.Linear is not { } linear)
        {
            return ContextErrors.SourceNotConfigured.With("context.sources.linear: not configured, so there is no Linear issue to store");
        }

        if (purges.IsPurging(LinearSourceIds.Issues))
        {
            return LinearHubErrors.NotStored.With("a purge of the linear source is running; the change shows after the next sync");
        }

        var scrubbers = scrubberFactory.For(settings.Value.ExtraPatterns);
        var read = scrubbers.IsError ? scrubbers.Error : await reads.Read(linear, settings.Value.GitHub?.Repos ?? [], request.IssueId, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value is not { } item ? new LinearWriteThroughResponse(Key: null, Written: false) : await Store(item, request.Written, scrubbers.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<LinearWriteThroughResponse>> Store(RecordItem item, LinearWrittenFields written, Scrubbers scrubbers, CancellationToken cancellationToken)
    {
        var stamped = item.Side is LinearSideRows rows ? item with { Side = rows with { Written = written } } : item;
        var page = new RecordPage([stamped], [], SourceCursor.From("write-through"), CursorReset: false);
        var batch = RecordBatches.From(LinearSourceIds.Issues, page, scrubbers, clock.GetUtcNow()) with { Guard = true };
        var committed = await store.Commit(batch, cancellationToken).ConfigureAwait(false);
        return committed.IsError ? committed.Error : new LinearWriteThroughResponse(item.Key, committed.Value.Written > 0);
    }
}
