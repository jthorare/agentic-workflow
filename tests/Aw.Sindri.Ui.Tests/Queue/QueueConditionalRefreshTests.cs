using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>#2862: a refresh whose answer is the view it already shows (the client's 304) changes nothing and reads nothing more.</summary>
[Lane(TestLane.Unit)]
public sealed class QueueConditionalRefreshTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ItemSummaryView Item(string id) =>
        new(id, "Title " + id, "alpha", "new", Step: null, A.Noon.AddMinutes(-5), PrimaryAction: null, Actions: []);

    [Fact]
    public async Task A_second_answer_that_is_the_same_view_keeps_the_rows_and_does_not_read_closed_again()
    {
        _server.QueueSame = new QueueView(
            [new QueueGroupView("queued", [Item("ITM-1"), Item("ITM-2")], "Nothing is queued.")],
            Empty: null,
            new AsOfView(1, 1, A.Noon, Rebuilding: false, Stalled: false),
            Tag: "t1");
        // The closed read fails both times, so today every refresh tries it again (a read that learned something is cached by the row ids).
        _server.ListItemsBody = _ => new RouteAnswer(HttpStatusCode.InternalServerError, "{}");
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var queue = new QueueViewModel(_server, catalog, _clock);

        await queue.Refresh(_api, Token);
        var rows = queue.Groups.Single().Rows.ToList();
        var listed = _server.ListedStates.Count;
        await queue.Refresh(_api, Token);

        var again = queue.Groups.Single().Rows.ToList();
        Assert.Equal(rows.Count, again.Count);
        Assert.All(rows.Zip(again), pair => Assert.Same(pair.First, pair.Second));
        Assert.Equal(listed, _server.ListedStates.Count);
    }
}
