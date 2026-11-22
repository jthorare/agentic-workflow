using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Closed fold after a refresh (#3801): read again only when the queue's set of item ids changed, never when the same ids come back in another order or with other fields.</summary>
[Lane(TestLane.Unit)]
public sealed class ClosedFoldRefreshTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ItemSummaryView Item(string id, string state = "running", string title = "Item") =>
        new(id, title + " " + id, "alpha", state, Step: null, A.Noon.AddHours(-1), PrimaryAction: null, Actions: []);

    // The queue the next refresh reads: the running group holds these items, in this order.
    private void Serve(params ItemSummaryView[] running)
    {
        var view = new QueueView([new QueueGroupView("running", running, Empty: null)], Empty: null, new AsOfView(1, 1, A.Noon, Rebuilding: false, Stalled: false));
        _server.QueueView = new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(view, SindriApiClient.Json));
    }

    // A queue that has shown a, b, c and read its closed fold once.
    private async Task<QueueViewModel> Read()
    {
        Serve(Item("ITM-a"), Item("ITM-b"), Item("ITM-c"));
        var queue = new QueueViewModel(_server, new CatalogViewModel(new FakeClipboard(), _clock), _clock);
        await queue.Refresh(_api, Token);
        return queue;
    }

    private int ClosedReads => _server.ListedStates.Count(s => string.Equals(s, "done", StringComparison.Ordinal));

    [Fact]
    public async Task The_first_refresh_reads_the_closed_items_once()
    {
        _ = await Read();

        Assert.Equal(1, ClosedReads);
    }

    [Fact]
    public async Task The_same_ids_again_read_nothing_more_even_reordered_or_retitled()
    {
        var queue = await Read();

        await queue.Refresh(_api, Token);
        Serve(Item("ITM-c"), Item("ITM-a", title: "Renamed"), Item("ITM-b", state: "waiting"));
        await queue.Refresh(_api, Token);

        Assert.Equal(1, ClosedReads);
    }

    [Fact]
    public async Task An_added_id_reads_the_closed_items_again()
    {
        var queue = await Read();
        Serve(Item("ITM-a"), Item("ITM-b"), Item("ITM-c"), Item("ITM-d"));

        await queue.Refresh(_api, Token);

        Assert.Equal(2, ClosedReads);
    }

    [Fact]
    public async Task A_gone_id_reads_the_closed_items_again()
    {
        var queue = await Read();
        Serve(Item("ITM-a"), Item("ITM-c"));

        await queue.Refresh(_api, Token);

        Assert.Equal(2, ClosedReads);
    }

    [Fact]
    public async Task One_id_swapped_for_another_at_the_same_size_reads_the_closed_items_again()
    {
        var queue = await Read();
        Serve(Item("ITM-a"), Item("ITM-b"), Item("ITM-d"));   // c closed while d was added

        await queue.Refresh(_api, Token);

        Assert.Equal(2, ClosedReads);
    }
}
