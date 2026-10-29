using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// The time cap cancels a pull's token (#1688): a run cancelled in the middle of page 3 keeps every page it committed before, and the next run resumes from the last committed cursor without reading those pages again. What a cancelled
/// pull returns is never committed (the run commits with the cancelled token), so progress comes from the bounded pulls, not from salvage.
/// </summary>
[Trait("Lane", "unit")]
public sealed class RecordSyncRunCancelTests
{
    private const int PageSize = 2;
    private const int CancelledPage = 3;
    private static readonly SourceId _source = RecordSamples.Linear;
    private readonly FakeRecordStore _store = new();
    private readonly FakeTimeProvider _clock = new(RecordSamples.Now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static string Id(int n) => string.Create(CultureInfo.InvariantCulture, $"i{n:00}");

    private static RecordItem Item(int n) => new(
        RecordKey.From("linear:issue/" + Id(n)), RecordKind.Issue, "Title " + N(n), "Text " + N(n), Author: null, Url: null, RecordState.Open, StateRaw: null, CreatedAt: null,
        RecordSamples.Now.AddDays(-1), ClosedAt: null, SourceVersion.From("v" + N(n)), Links: [], Repo: null, Audience: null);

    private RecordSyncRun Run(RecordFeed feed) =>
        new(feed, _store, (new FakeScrubberFactory().For([]).Value, new ContextIndexSettings()), _clock, PageSize);

    private static List<RecordItem> Items(int count) => [.. Enumerable.Range(1, count).Select(Item)];

    private async Task<HashSet<string>> StoredKeys()
    {
        var listed = await _store.List(new RecordFilter(), after: null, 1000, Ct);
        return [.. listed.Value.Select(static r => r.Key.Value)];
    }

    [Fact]
    public async Task A_run_cancelled_mid_pull_keeps_every_committed_page_and_the_next_run_resumes()
    {
        using var feed = new CancellingFeed(_source, Items(7), CancelledPage, returnItemsWhenCancelled: false);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Run(feed).Execute(feed.Token));

        Assert.Equal(Enumerable.Range(1, (CancelledPage - 1) * PageSize).Select(n => "linear:issue/" + Id(n)).Order(StringComparer.Ordinal), (await StoredKeys()).Order(StringComparer.Ordinal));
        var position = (await _store.Position(_source, Ct)).Value;
        Assert.Equal(((CancelledPage - 1) * PageSize).ToString(CultureInfo.InvariantCulture), position.Cursor?.Value);

        feed.Resume();
        var row = await Run(feed).Execute(Ct);

        Assert.Null(row.Code);
        Assert.Equal(Enumerable.Range(1, 7).Select(n => "linear:issue/" + Id(n)).Order(StringComparer.Ordinal), (await StoredKeys()).Order(StringComparer.Ordinal));
        Assert.Equal(((CancelledPage - 1) * PageSize).ToString(CultureInfo.InvariantCulture), feed.PullsAfterResume[0].After?.Value);   // the first pull after the cancel starts at page 3, no page before it is read again
    }

    [Fact]
    public async Task A_page_a_cancelled_pull_returns_is_not_committed_and_the_next_run_still_gets_it()
    {
        using var feed = new CancellingFeed(_source, Items(7), CancelledPage, returnItemsWhenCancelled: true);

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Run(feed).Execute(feed.Token));

        var kept = await StoredKeys();
        Assert.Equal((CancelledPage - 1) * PageSize, kept.Count);
        Assert.DoesNotContain("linear:issue/" + Id(((CancelledPage - 1) * PageSize) + 1), kept);

        feed.Resume();
        _ = await Run(feed).Execute(Ct);

        Assert.Equal(7, (await StoredKeys()).Count);
    }

    // A FakeRecordFeed that cancels its own token once it has read page 'cancelledPage' (after reading it, before returning): optionally it still returns that page's items, as a feed salvaging what it has would.
    private sealed class CancellingFeed(SourceId source, IReadOnlyList<RecordItem> items, int cancelledPage, bool returnItemsWhenCancelled) : RecordFeed(source), IDisposable
    {
        private readonly FakeRecordFeed _inner = new(source, items);
        private readonly CancellationTokenSource _cancel = new();
        private int _pulls;
        private bool _resumed;

        public CancellationToken Token => _cancel.Token;

        public List<RecordPull> PullsAfterResume { get; } = [];

        public void Dispose() => _cancel.Dispose();

        public void Resume() => _resumed = true;

        public override async ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
        {
            if (_resumed)
            {
                PullsAfterResume.Add(pull);
                return await _inner.Pull(pull, cancellationToken);
            }

            _pulls++;
            var page = await _inner.Pull(pull, cancellationToken);
            if (_pulls != cancelledPage)
            {
                return page;
            }

            await _cancel.CancelAsync();
            if (returnItemsWhenCancelled)
            {
                return page;
            }

            throw new OperationCanceledException(_cancel.Token);
        }
    }
}
