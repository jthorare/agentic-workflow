using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Work page's states (F3 spec work §3, slice 3): the empty box, a filter that matches nothing, a rebuild or a stall shown in the groups and the footer.</summary>
[Lane(TestLane.Unit)]
public sealed class QueueStatesTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ItemSummaryView Item(string id, string title) =>
        new(id, title, "agentic-workflow", "running", "plan", A.Noon.AddMinutes(-5), PrimaryAction: null, Actions: []);

    private async Task<QueueViewModel> Queue(IReadOnlyList<ItemSummaryView> running, AsOfView? asOf = null)
    {
        var view = new QueueView(
            [new QueueGroupView("waitingOnYou", [], "Nothing waits on you."), new QueueGroupView("running", running, "Nothing is running."), new QueueGroupView("queued", [], "Nothing is queued.")],
            "Nothing in the queue yet.",
            asOf ?? new AsOfView(214, 214, A.Noon, Rebuilding: false, Stalled: false));
        _server.QueueView = new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(view, SindriApiClient.Json));
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var queue = new QueueViewModel(_server, catalog, _clock);
        await queue.Refresh(_api, Token);
        return queue;
    }

    [Fact]
    public async Task An_empty_queue_shows_the_nothing_box_with_the_add_command()
    {
        var queue = await Queue([]);

        Assert.True(queue.ShowsNothing);
        Assert.False(queue.ShowsNoMatch);
        Assert.Equal((Labels.NothingInQueue, Labels.ItemAddCommand), (queue.NothingTitle, queue.AddCommand));
    }

    [Fact]
    public async Task A_filter_that_matches_nothing_shows_no_items_match_and_clear_filter_empties_it()
    {
        var queue = await Queue([Item("ITM-1", "Try the queue")]);

        queue.FilterText = "zzz";

        Assert.True(queue.ShowsNoMatch);
        Assert.False(queue.ShowsNothing);
        Assert.Equal(Labels.NoItemsMatch, queue.NoMatchTitle);
        queue.ClearFilter();
        Assert.Equal(string.Empty, queue.FilterText);
        Assert.False(queue.ShowsNoMatch);
    }

    [Fact]
    public async Task While_the_views_rebuild_each_group_but_queued_shows_that_as_its_empty_line()
    {
        var queue = await Queue([], new AsOfView(8, 8, A.Noon, Rebuilding: true, Stalled: false));

        var lines = queue.Groups.ToDictionary(g => g.Group, g => g.Empty, StringComparer.Ordinal);

        Assert.Equal(Labels.RebuildingViews(8, 8), lines["running"]);
        Assert.Equal(Labels.RebuildingViews(8, 8), lines["waitingOnYou"]);
        Assert.Equal("Nothing is queued.", lines["queued"]);
    }

    [Fact]
    public async Task The_footer_says_up_to_date_or_how_far_behind()
    {
        Assert.Equal(Labels.QueueUpToDate(214), (await Queue([])).FooterText);
        Assert.Equal(Labels.QueueBehind(8, 9), (await Queue([], new AsOfView(8, 9, A.Noon, Rebuilding: false, Stalled: false))).FooterText);
    }
}
