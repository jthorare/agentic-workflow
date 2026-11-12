using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3391: pressing Build map on the bare Context page (no item open) says which repository is being built and makes no time promise; with an item open the item's words stay.</summary>
[Lane(TestLane.Unit)]
public sealed class MapBuildingCopyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer NoMap(string noMap) => new(HttpStatusCode.NotFound,
        "{\"type\":\"about:blank\",\"title\":\"SND-CONTEXT-030\",\"status\":404,\"detail\":\"there is no map yet\",\"noMap\":" + noMap + ",\"traceId\":\"<trace>\"}");

    private static async Task<ContextPageViewModel> Page(RouteAnswer answer)
    {
        var api = new FakeSindriApi { WholeMapAnswer = _ => answer };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        await page.Opened(Token);
        return page;
    }

    [Fact]
    public async Task With_no_item_open_the_building_line_names_the_repository_and_promises_no_time()
    {
        var page = await Page(RouteAnswer.Problem("context-whole", "no-map-030"));

        await page.BuildControl!.Press(Token);

        Assert.True(page.ShowBuilding);
        Assert.Equal("Building the map for aw-split-panes", page.BuildingTitle);
        Assert.DoesNotContain("minute", page.BuildingBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ticket", page.BuildingBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("this item", page.BuildingTitle, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task With_no_item_open_and_no_repository_named_the_line_is_just_Building_the_map()
    {
        var page = await Page(NoMap("{}"));

        await page.BuildControl!.Press(Token);

        Assert.Equal("Building the map", page.BuildingTitle);
        Assert.DoesNotContain("minute", page.BuildingBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task With_an_item_open_the_item_words_stay()
    {
        var page = await Page(RouteAnswer.Problem("context-whole", "no-map-030"));
        await page.OpenForItem(A.NewItem, Token);

        await page.BuildControl!.Press(Token);

        Assert.Equal((Labels.ContextStateBuilding, Labels.ContextStateBuildingBody), (page.BuildingTitle, page.BuildingBody));
    }
}
