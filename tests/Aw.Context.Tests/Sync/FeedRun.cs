using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;

namespace Aw.Context.Tests.Sync;

/// <summary>What the sync driver will do (U8) for one pull: read the stored cursor, pull, build the batch, commit it.</summary>
/// <param name="Page">The page the feed answered.</param>
/// <param name="Report">What the commit did, or null when the page was empty and nothing was committed.</param>
internal sealed record FeedRun(RecordPage Page, CommitReport? Report)
{
    public static async Task<FeedRun> Once(RecordFeed feed, IRecordStore store, Scrubbers scrubbers, int limit = 100)
    {
        var position = (await store.Position(feed.Source, TestContext.Current.CancellationToken)).Value;
        var page = (await feed.Pull(new RecordPull(position.Cursor, limit, NotBefore: null), TestContext.Current.CancellationToken)).Value;
        if (page.Items.Count == 0 && page.Removed.Count == 0)
        {
            return new FeedRun(page, Report: null);
        }

        var batch = RecordBatches.From(feed.Source, page, SyncPhase.Incremental, scrubbers, new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        return new FeedRun(page, (await store.Commit(batch, TestContext.Current.CancellationToken)).Value);
    }

    /// <summary>Pulls until a pull has nothing to commit; returns the runs that did.</summary>
    public static async Task<IReadOnlyList<FeedRun>> UntilQuiet(RecordFeed feed, IRecordStore store, Scrubbers scrubbers, int limit = 100)
    {
        var runs = new List<FeedRun>();
        while (true)
        {
            var run = await Once(feed, store, scrubbers, limit);
            if (run.Report is null)
            {
                return runs;
            }

            runs.Add(run);
        }
    }
}
