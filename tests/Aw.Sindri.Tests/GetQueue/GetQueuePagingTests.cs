using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Tests.Items;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetQueue;

/// <summary>
/// GET /sindri/queue pages per group and answers 304 when nothing changed (issue #2788): today it lists the whole items view
/// and returns every open item (207 KB, 455 ms at 5,000 items). The tag moves with the items view, the projector's state and
/// whether the views are behind, and never with the projector's cursor or as-of time alone.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GetQueuePagingTests
{
    private const string SeedType = "test.seed";
    private readonly ItemQueryHarness _q = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_limit_caps_every_group_and_the_counts_keep_the_totals()
    {
        // 4 groups x 8 items; the waiting group alternates parked and needs-decision items (even n is parked).
        for (var n = 1; n <= 8; n++)
        {
            await Item(n, Title("wait", n), n % 2 == 0 ? ItemState.Parked : ItemState.NeedsDecision);
            await Item(100 + n, Title("with", n), ItemState.TakenOver);
            await Item(200 + n, Title("run", n), ItemState.Running);
            await Item(300 + n, Title("queued", n), ItemState.Queued);
        }

        var response = await Query(new GetQueueRequest(Limit: 5));

        Assert.All(response.Groups, static g => Assert.Equal(5, g.Items.Count));
        Assert.Equal(new QueueGroupCounts(8, 8, 8, 8), response.Counts);
        var waiting = response.Groups.Single(static g => g.Group == QueueGroup.WaitingOnYou);
        Assert.Contains(waiting.Items, static i => i.Parked);
        Assert.All(waiting.Items, static i => Assert.Equal(i.State == ItemState.Parked, i.Parked));
    }

    [Fact]
    public async Task Paging_a_group_walks_every_item_once_in_todays_order()
    {
        var expected = new List<string>();
        for (var n = 1; n <= 12; n++)
        {
            await Item(n, Title("queued", n));
            expected.Add(Id(n));
        }

        var pages = await Walk("queued", limit: 5);

        Assert.Equal([5, 5, 2], pages.Select(static p => p.Items.Count));
        Assert.Null(pages[^1].Next);
        Assert.Equal(expected, pages.SelectMany(static p => p.Items.Select(static i => i.Id.Value)), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_cursor_survives_an_item_leaving_an_earlier_page()
    {
        for (var n = 1; n <= 12; n++)
        {
            await Item(n, Title("queued", n));
        }

        var page1 = Assert.Single((await Query(new GetQueueRequest(Limit: 5, Group: "queued"))).Groups);
        Assert.NotNull(page1.Next);
        await Move(3, ItemState.Running);   // item 3 sat on page 1; an offset cursor would now skip item 6
        var page2 = Assert.Single((await Query(new GetQueueRequest(Limit: 5, Group: "queued", After: page1.Next))).Groups);

        Assert.Equal([Id(6), Id(7), Id(8), Id(9), Id(10)], page2.Items.Select(static i => i.Id.Value), StringComparer.Ordinal);
        Assert.Empty(page2.Items.Select(static i => i.Id.Value).Intersect(page1.Items.Select(static i => i.Id.Value), StringComparer.Ordinal));
    }

    [Fact]
    public async Task Closed_and_cancelled_items_stay_out_of_the_queue()
    {
        for (var n = 1; n <= 3; n++)
        {
            await Item(n, Title("open", n));
        }

        for (var n = 11; n < 61; n++)
        {
            await Item(n, Title("closed", n), n % 2 == 0 ? ItemState.Done : ItemState.Cancelled);
        }

        var response = await Query(new GetQueueRequest());

        Assert.Equal(3, response.Groups.Sum(static g => g.Items.Count));
    }

    [Fact]
    public async Task With_a_limit_the_rows_read_do_not_grow_with_the_closed_items()
    {
        var small = await RowsRead(closed: 20);
        var large = await RowsRead(closed: 2000);

        Assert.Equal(small, large);
    }

    [Fact]
    public async Task The_same_tag_answers_not_modified_with_no_body()
    {
        await Seed();
        var first = await Query(new GetQueueRequest());
        Assert.NotNull(first.Tag);

        var second = await Query(new GetQueueRequest(IfNoneMatch: first.Tag));

        Assert.True(second.NotModified);
        Assert.Empty(second.Groups);
    }

    [Fact]
    public async Task A_not_modified_answer_reads_no_item_rows()
    {
        await Seed();
        await SeedClosed(200);
        var first = await Query(new GetQueueRequest());
        Assert.NotNull(first.Tag);
        var calls = new List<ReadCall>();

        var second = await Query(new CountingViewReader(_q.Views.Views, calls), new GetQueueRequest(IfNoneMatch: first.Tag));

        Assert.True(second.NotModified);
        Assert.Equal(0, calls.Where(static c => c.View == ViewNames.Items).Sum(static c => c.Rows));   // #2852: today every open row is read to build the tag
    }

    [Fact]
    public async Task Closing_an_item_moves_the_tag()
    {
        await Seed();
        var first = await Query(new GetQueueRequest());
        Assert.NotNull(first.Tag);

        await Move(1, ItemState.Done);
        var second = await Query(new GetQueueRequest(IfNoneMatch: first.Tag));

        Assert.False(second.NotModified);
        Assert.NotEqual(first.Tag, second.Tag, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_event_that_writes_no_item_row_leaves_the_tag_alone()
    {
        await Seed();
        var first = await Query(new GetQueueRequest());
        Assert.NotNull(first.Tag);
        var before = _q.Views.Progress.Current;

        _q.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(5));
        await _q.Views.Fold(EventTypes.ServiceRan, new ServiceRan(_q.Views.Slice.Ids.New<CommandId>(), ServiceName.From("heartbeat"), _q.Views.Slice.Clock.GetUtcNow(), ServiceOutcome.Ok, Code: null));
        var after = _q.Views.Progress.Current;
        Assert.True(after.Cursor > before.Cursor && after.ProjectedAt > before.ProjectedAt, "the premise: the cursor and the as-of time moved");
        var second = await Query(new GetQueueRequest(IfNoneMatch: first.Tag));

        Assert.True(second.NotModified);
        Assert.Equal(first.Tag, second.Tag, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_item_write_moves_the_tag_and_brings_the_body()
    {
        await Seed();
        var first = await Query(new GetQueueRequest());
        Assert.NotNull(first.Tag);

        await Move(1, ItemState.Running);
        var second = await Query(new GetQueueRequest(IfNoneMatch: first.Tag));

        Assert.False(second.NotModified);
        Assert.NotEqual(first.Tag, second.Tag, StringComparer.Ordinal);
        Assert.NotEmpty(second.Groups.SelectMany(static g => g.Items));
    }

    [Fact]
    public async Task A_stalled_rebuilding_or_behind_projector_moves_the_tag_with_no_item_write()
    {
        await Seed();
        var baseline = _q.Views.Progress.Current;
        var first = await Query(new GetQueueRequest());
        Assert.NotNull(first.Tag);

        var variants = new[]
        {
            baseline with { LedgerHead = baseline.Cursor + 3 },
            baseline with { Rebuilding = true },
            baseline with { Stalled = true, StalledAt = baseline.Cursor + 1, StalledType = EventTypes.ServiceRan },
        };
        foreach (var variant in variants)
        {
            _q.Views.Progress.Publish(variant);
            var answer = await Query(new GetQueueRequest(IfNoneMatch: first.Tag));
            Assert.False(answer.NotModified, variant.ToString());
            Assert.NotEqual(first.Tag, answer.Tag, StringComparer.Ordinal);
        }

        _q.Views.Progress.Publish(baseline);
        Assert.True((await Query(new GetQueueRequest(IfNoneMatch: first.Tag))).NotModified, "back to the baseline, the tag is the first one again");
    }

    private static string Title(string prefix, int n) => prefix + " " + n.ToString(CultureInfo.InvariantCulture);

    private static string Id(int n) => "ITM-" + n.ToString("x32", CultureInfo.InvariantCulture);

    private static ItemId ItemIdOf(int n) => ItemId.From(Id(n));

    private async Task Seed()
    {
        await Item(1, "one");
        await Item(2, "two");
        await Item(3, "three", ItemState.Running);
    }

    /// <summary>Creates an item one minute after the last event, then moves it to <paramref name="state"/> when not new.</summary>
    private async Task Item(int n, string title, ItemState state = ItemState.New)
    {
        _q.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        var id = ItemIdOf(n);
        await _q.Views.Fold(EventTypes.ItemCreated, new ItemCreated(_q.Views.Slice.Ids.New<CommandId>(), id, title, Repo: null, Link: null, Body: "the body", Owner: null), id);
        if (state != ItemState.New)
        {
            await Move(n, state);
        }
    }

    private async Task Move(int n, ItemState to)
    {
        _q.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        var id = ItemIdOf(n);
        await _q.Views.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(_q.Views.Slice.Ids.New<CommandId>(), id, ItemState.New, to, 2, Reason: null), id);
    }

    private async Task<QueueResponse> Query(GetQueueRequest request) => await Query(_q.Views.Views, request);

    private async Task<QueueResponse> Query(IViewReader reader, GetQueueRequest request)
    {
        using var entered = _q.Human();
        var result = await new GetQueueService(reader, _q.Views.Progress, _q.Views.Slice.Callers).Handle(request, Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return result.Value;
    }

    /// <summary>Follows a group's cursor from the top to its last page (at most 10 pages).</summary>
    private async Task<List<QueueGroupView>> Walk(string group, int limit)
    {
        var pages = new List<QueueGroupView>();
        string? after = null;
        do
        {
            var page = Assert.Single((await Query(new GetQueueRequest(Limit: limit, Group: group, After: after))).Groups);
            pages.Add(page);
            after = page.Next;
        }
        while (after is not null && pages.Count < 10);

        return pages;
    }

    /// <summary>The rows the items view hands out for one limited queue call, with 8 queued items and <paramref name="closed"/> closed ones.</summary>
    private static async Task<int> RowsRead(int closed)
    {
        var test = new GetQueuePagingTests();
        for (var n = 1; n <= 8; n++)
        {
            await test.Item(n, Title("queued", n));
        }

        await test.SeedClosed(closed);
        var calls = new List<ReadCall>();
        _ = await test.Query(new CountingViewReader(test._q.Views.Views, calls), new GetQueueRequest(Limit: 5));
        return calls.Sum(static c => c.Rows);
    }

    /// <summary>Puts closed item rows straight into the items view, the way a fold would (one event, one projection).</summary>
    private async Task SeedClosed(int count)
    {
        var slice = _q.Views.Slice;
        var at = slice.Clock.GetUtcNow();
        var rows = Enumerable.Range(1000, count).Select(n => new ItemRow(ItemIdOf(n), Title("closed", n), Repo: null, n % 2 == 0 ? ItemState.Done : ItemState.Cancelled, Step: null, Version: 3,
            CreatedBy: "human", at.AddDays(-2), Since: at.AddDays(-1), at.AddDays(-1), Link: null, Alias: null, Body: null, WaitingOn: null, Instance: null, Owner: null, Sequence: 500 + n)).ToList();
        var seed = Recorded.Of(slice.Events, SeedType, new ServiceRan(slice.Ids.New<CommandId>(), ServiceName.From("heartbeat"), at, ServiceOutcome.Ok, Code: null), sequence: 100_000);
        _ = (await _q.Views.Views.Apply([seed], [new SeedItems(rows)], at, Token)).Value;
    }

    private sealed record ReadCall(string View, string Method, int Rows);

    private sealed class SeedItems(List<ItemRow> rows) : IProjection
    {
        public IReadOnlyList<string> Views => [ViewNames.Items];

        public bool Handles(string eventType) => eventType == SeedType;

        public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows target, CancellationToken cancellationToken)
        {
            foreach (var row in rows)
            {
                _ = await target.Put(ViewNames.Items, row.Id.Value, row, cancellationToken);
            }

            return Unit.Value;
        }
    }

    /// <summary>The real reader, counting every call and the rows it returns.</summary>
    private sealed class CountingViewReader(IViewReader inner, List<ReadCall> calls) : IViewReader
    {
        public async ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
            where TRow : class
        {
            var result = await inner.Get<TRow>(view, key, cancellationToken);
            calls.Add(new ReadCall(view, "Get", result.IsSuccess && result.Value is not null ? 1 : 0));
            return result;
        }

        public async ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
            where TRow : class => Count(view, "List", await inner.List<TRow>(view, cancellationToken));

        public async ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
            where TRow : class => Count(view, "Range", await inner.Range<TRow>(view, afterKey, beforeKey, max, cancellationToken));

        public async ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
            where TRow : class => Count(view, "Where", await inner.Where<TRow>(view, column, value, cancellationToken));

        public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) => inner.State(cancellationToken);

        private Result<IReadOnlyList<TRow>> Count<TRow>(string view, string method, Result<IReadOnlyList<TRow>> result)
        {
            calls.Add(new ReadCall(view, method, result.IsSuccess ? result.Value.Count : 0));
            return result;
        }
    }
}
