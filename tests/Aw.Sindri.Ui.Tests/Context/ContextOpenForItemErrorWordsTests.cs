using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>An item link on the Context page words each failed read by what happened: only a missing answer reads "not reachable", a refused id reads as unknown, and any other answered error says Sindri couldn't read it.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextOpenForItemErrorWordsTests
{
    private const string Item = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a305";

    private const string CouldNotRead = "Could not read the context of " + Item + ": Sindri couldn't read it";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static void AssertPlainFailure(ContextPageViewModel page)
    {
        Assert.Equal(CouldNotRead, page.ItemNote);
        Assert.True(page.IsItemFailed);
        Assert.DoesNotContain("not reachable", page.ItemNote, StringComparison.Ordinal);
        Assert.DoesNotContain("/sindri/", page.ItemNote, StringComparison.Ordinal);
        Assert.DoesNotContain("answered", page.ItemNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_item_id_with_a_problem_body_is_named_as_unknown()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-CLI-003", "Not an item id.");
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(Labels.ContextItemUnknown(Item), page.ItemNote);
        Assert.False(page.IsItemFailed);
    }

    [Fact]
    public async Task A_bare_400_on_the_item_read_is_named_as_unknown()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = new RouteAnswer(HttpStatusCode.BadRequest, string.Empty);
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(Labels.ContextItemUnknown(Item), page.ItemNote);
        Assert.False(page.IsItemFailed);
    }

    [Fact]
    public async Task A_bare_500_on_the_item_read_says_sindri_could_not_read_it()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = new RouteAnswer(HttpStatusCode.InternalServerError, string.Empty);
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        AssertPlainFailure(page);
    }

    [Fact]
    public async Task A_500_with_a_problem_body_says_sindri_could_not_read_it()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-ITEM-500", "The read failed.");
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        AssertPlainFailure(page);
    }

    [Fact]
    public async Task A_control_plane_that_is_down_still_reads_as_not_reachable()
    {
        var api = new FakeSindriApi();
        api.GoDown();
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(Labels.ContextItemFailed(Item, Labels.ContextItemUnreachable), page.ItemNote);
        Assert.True(page.IsItemFailed);
    }

    [Fact]
    public async Task An_answer_this_build_cannot_parse_says_sindri_could_not_read_it_with_retry()
    {
        var api = new FakeSindriApi();
        api.Items[Item] = new RouteAnswer(HttpStatusCode.OK, "{ not json");   // a newer serve's shape an older app can't read: SND-UI-003, Kind Invalid
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        AssertPlainFailure(page);
    }
}
