using System.Collections.Concurrent;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// The record-feed side of <see cref="SyncSourcesService"/>: the profile's feeds, one <see cref="RecordSyncRun"/> tick per feed into the record store, and the
/// per-source lock both the feed tick and the intent walk take (a second sync of a source that is running is SND-CONTEXT-056).
/// </summary>
/// <param name="feeds">The feeds, built per call.</param>
/// <param name="store">The record store the pages commit into.</param>
/// <param name="clock">The clock the ticks read.</param>
internal sealed class RecordFeedSync(IRecordFeedCatalog feeds, IRecordStore store, TimeProvider clock)
{
    private readonly SyncLocks _locks = new();
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, string? Code)> _outcomes = new(StringComparer.Ordinal);

    /// <summary>The feeds for the repos in scope, in source order.</summary>
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers) =>
        [.. feeds.For(settings, repos, scrubbers).OrderBy(f => f.Source.Value, StringComparer.Ordinal)];

    /// <summary>One tick of a feed; the row carries a feed failure as its code. The tick's end and code are kept for <see cref="LastOutcome"/>.</summary>
    public async ValueTask<Result<SourceSync>> Run(RecordFeed feed, (Scrubbers Scrubbers, ContextSettings Settings) context, CancellationToken cancellationToken)
    {
        var row = await Locked(feed.Source, () => new RecordSyncRun(feed, store, (context.Scrubbers, context.Settings.Index), clock).Execute(cancellationToken)).ConfigureAwait(false);
        if (!row.IsError)
        {
            _outcomes[feed.Source.Value] = (clock.GetUtcNow(), row.Value.Code);
        }

        return row;
    }

    /// <summary>When this process last finished a tick of the source's feed and the code it ended with; null when it has not.</summary>
    public (DateTimeOffset At, string? Code)? LastOutcome(SourceId source) => _outcomes.TryGetValue(source.Value, out var outcome) ? outcome : null;

    /// <summary>Runs <paramref name="run"/> while holding the source's lock.</summary>
    public async ValueTask<Result<SourceSync>> Locked(SourceId id, Func<ValueTask<SourceSync>> run)
    {
        if (!_locks.TryEnter(id.Value))
        {
            return ContextErrors.SyncRunning.With($"{id.Value} is being synced already (fix: wait for it; sindri context sources status)");
        }

        try
        {
            return await run().ConfigureAwait(false);
        }
        finally
        {
            _locks.Leave(id.Value);
        }
    }
}
