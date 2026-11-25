using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'home-rows' and 'home-links' fold (home spec §3.1, §8): a row per object from the 'home.*' events, the same after a rebuild.</summary>
public sealed class HomeProjectionTests
{
    private static readonly HomeRowId _id = HomeRowId.From("gh:pr:jthorare/agentic-workflow#3120");
    private static readonly DateTimeOffset _until = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly HomeEdge[] _edges =
    [
        new("gh:pr:jthorare/agentic-workflow#3120", "gh:pr:jthorare/agentic-workflow#3121", HomeEdgeKind.Stacked),
        new("gh:pr:jthorare/agentic-workflow#3120", "linear:issue:ENG-412", HomeEdgeKind.Closes),
    ];

    private readonly ViewsHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // rows hold lists, which records compare by reference: compare what they write
    private static string Json<T>(T value) => Encoding.UTF8.GetString(AwJson.Serialize(value, SliceHarness.Json));

    [Fact, Trait("Lane", "unit")]
    public async Task Raised_fills_home_rows()
    {
        var row = HomeRowSamples.Row();

        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(row, _edges));

        Assert.Equal(Json(row), Json(await Row()));
        Assert.Equal(2, (await Edges(_id.Value)).Count);
        Assert.Empty(await Edges("gh:pr:jthorare/agentic-workflow#3121"));   // an edge belongs to the object it leaves
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changed_bumps_version()
    {
        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row(), _edges));
        var rank = new HomeRank(Blocks: 4, Weight: 5, SinceRank: 1, Position: 0);

        await _h.Fold(EventTypes.HomeRowChanged, new HomeRowChanged(_id, Version: 2, Title: null, Facts: null, WaitsOn: "CI", rank, BlocksList: null, Links: null, Actions: null, Source: null, Edges: null));

        var row = (await Row())!;
        Assert.Equal((2L, "CI", rank), (row.Version, row.WaitsOn, row.Rank));
        Assert.Equal("Review requested: the Home inbox spec", row.Title);   // a null member leaves the field as it was
        Assert.Equal(2, (await Edges(_id.Value)).Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Changed_with_edges_replaces_the_row_edges()
    {
        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row(), _edges));
        HomeEdge[] next = [new(_id.Value, "gh:pr:jthorare/agentic-workflow#3122", HomeEdgeKind.MergeOrder)];

        await _h.Fold(EventTypes.HomeRowChanged, new HomeRowChanged(_id, Version: 2, Title: null, Facts: null, WaitsOn: null, Rank: null, BlocksList: null, Links: null, Actions: null, Source: null, next));

        var edges = await Edges(_id.Value);
        Assert.Equal([HomeEdgeKind.MergeOrder], edges.Select(static e => e.Edge));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cleared_removes_row()
    {
        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row(), _edges));

        await _h.Fold(EventTypes.HomeRowCleared, new HomeRowCleared(_id));

        Assert.Null(await Row());
        Assert.Empty(await Edges(_id.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Snoozed_sets_until_and_woke_clears_it()
    {
        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row(), _edges));

        await _h.Fold(EventTypes.HomeRowSnoozed, new HomeRowSnoozed(_id, Version: 2, _until, UnlessReplied: false));
        var snoozed = (await Row())!;
        await _h.Fold(EventTypes.HomeRowWoke, new HomeRowWoke(_id, Version: 3));
        var woke = (await Row())!;

        Assert.Equal((HomeState.Snoozed, _until, 2L), (snoozed.State, snoozed.SnoozeUntil, snoozed.Version));
        Assert.Equal((HomeState.Open, null, 3L), (woke.State, woke.SnoozeUntil, woke.Version));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Done_then_revived_reopens_the_row_and_muted_silences_it()
    {
        await _h.Fold(EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row(), _edges));

        await _h.Fold(EventTypes.HomeRowDone, new HomeRowDone(_id, Version: 2, At: null));
        var done = (await Row())!;
        await _h.Fold(EventTypes.HomeRowRevived, new HomeRowRevived(_id, Version: 3));
        var revived = (await Row())!;
        await _h.Fold(EventTypes.HomeRowMuted, new HomeRowMuted(_id, Version: 4));
        var muted = (await Row())!;

        Assert.Equal((HomeState.Done, _h.Slice.Clock.GetUtcNow()), (done.State, done.DoneAt));   // no 'At' in the event: the event's time
        Assert.Equal((HomeState.Open, null), (revived.State, revived.DoneAt));
        Assert.Equal((HomeState.Muted, 4L), (muted.State, muted.Version));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_event_for_a_row_the_view_does_not_hold_is_skipped_and_act_and_brief_events_change_nothing()
    {
        await _h.Fold(EventTypes.HomeRowSnoozed, new HomeRowSnoozed(_id, Version: 2, _until, UnlessReplied: false));
        await _h.Fold(EventTypes.HomeActSent, new HomeActSent(_id, ActId.From("act-1"), "merge", "0f"));
        await _h.Fold(EventTypes.HomeBriefWritten, new HomeBriefWritten(_until, "facts", ["1 PR is ready"]));
        await _h.Fold(EventTypes.HomeCursorMoved, new HomeCursorMoved(_until));

        Assert.Null(await Row());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rebuild_replays_to_same_rows()
    {
        var events = _h.Slice.Events;
        LedgerEvent[] ledger =
        [
            Recorded.Of(events, EventTypes.HomeRowRaised, new HomeRowRaised(HomeRowSamples.Row(), _edges), sequence: 1),
            Recorded.Of(events, EventTypes.HomeRowChanged, new HomeRowChanged(_id, Version: 2, Title: "Review requested: renamed", Facts: null, WaitsOn: null, Rank: null, BlocksList: null, Links: null, Actions: null, Source: null, Edges: null), sequence: 2),
            Recorded.Of(events, EventTypes.HomeRowSnoozed, new HomeRowSnoozed(_id, Version: 3, _until, UnlessReplied: true), sequence: 3),
        ];
        await _h.Apply(ledger);
        var before = (Json(await Row()), EdgesJson(await Edges(_id.Value)));

        _ = (await _h.Views.Reset([ViewNames.HomeRows, ViewNames.HomeLinks], ViewSchema.Version, _h.Slice.Clock.GetUtcNow(), 0, Token)).Value;
        Assert.Null(await Row());
        await _h.Apply(ledger);

        Assert.Equal(before, (Json(await Row()), EdgesJson(await Edges(_id.Value))));
    }

    private static string EdgesJson(IReadOnlyList<HomeEdge> edges) => string.Join('|', edges.Select(static e => Json(e)));

    private async Task<HomeRow?> Row() => (await _h.Views.Get<HomeRow>(ViewNames.HomeRows, _id.Value, Token)).Value;

    private async Task<IReadOnlyList<HomeEdge>> Edges(string from) => (await _h.Views.Where<HomeEdge>(ViewNames.HomeLinks, ViewColumn.Item, from, Token)).Value;
}
