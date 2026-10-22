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

/// <summary>The Work page's Closed fold (F3 spec work §1 Q6, slice 4): collapsed by default, reads done and cancelled once, newest first, rows with no actions.</summary>
[Lane(TestLane.Unit)]
public sealed class ClosedFoldTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ItemSummaryView Closed(string id, string state, int hoursAgo) =>
        new(id, "Item " + id, "alpha", state, Step: null, A.Noon.AddHours(-hoursAgo), PrimaryAction: "CloseItem:done", Actions: ["CloseItem:done"]);

    private QueueViewModel Queue()
    {
        _server.ListItemsBody = state =>
        {
            var items = string.Equals(state, "done", StringComparison.Ordinal) ? new[] { Closed("ITM-1", "done", 5), Closed("ITM-3", "done", 1) } : [Closed("ITM-2", "cancelled", 3)];
            var list = new ItemListView(items, Empty: null, new AsOfView(1, 1, A.Noon, Rebuilding: false, Stalled: false));
            return new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(list, SindriApiClient.Json));
        };
        return new QueueViewModel(_server, new CatalogViewModel(new FakeClipboard(), _clock), _clock);
    }

    [Fact]
    public void The_fold_is_collapsed_and_reads_nothing_until_opened()
    {
        var queue = Queue();

        Assert.False(queue.IsClosedOpen);
        Assert.Empty(_server.ListedStates);
        Assert.Equal(Labels.ClosedHeader(count: null), queue.ClosedHeader);
    }

    [Fact]
    public async Task Opening_reads_done_and_cancelled_once_and_lists_them_newest_first()
    {
        var queue = Queue();

        await queue.ToggleClosed(_api, Token);
        await queue.ToggleClosed(_api, Token);
        await queue.ToggleClosed(_api, Token);

        Assert.Equal(["done", "cancelled"], _server.ListedStates, StringComparer.Ordinal);
        Assert.Equal(["ITM-3", "ITM-2", "ITM-1"], queue.ClosedRows.Select(r => r.Id), StringComparer.Ordinal);
        Assert.Equal(Labels.ClosedHeader(3), queue.ClosedHeader);
        Assert.True(queue.IsClosedOpen);
    }

    [Fact]
    public async Task Closed_rows_carry_no_actions()
    {
        var queue = Queue();

        await queue.ToggleClosed(_api, Token);

        Assert.All(queue.ClosedRows, r => Assert.False(r.HasPrimaryAction));
    }
}
