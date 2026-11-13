using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3391 review: with no item open, serve's own building line keeps the repository head, and a long list of repositories reads as a count.</summary>
[Lane(TestLane.Unit)]
public sealed class MapBuildingServedCopyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Refusal(string member) => new(HttpStatusCode.NotFound,
        "{\"type\":\"about:blank\",\"title\":\"SND-CONTEXT-030\",\"status\":404,\"detail\":\"there is no map yet\"," + member + ",\"traceId\":\"<trace>\"}");

    private static async Task<(ContextPageViewModel Page, FakeSindriApi Api)> Page(RouteAnswer first)
    {
        var api = new FakeSindriApi { WholeMapAnswer = _ => first };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        await page.Opened(Token);
        return (page, api);
    }

    [Fact]
    public async Task Serves_own_building_line_keeps_the_repository_in_the_title()
    {
        var (page, api) = await Page(Refusal("\"noMap\":{\"repos\":[\"ui-3391\"]}"));
        await page.BuildControl!.Press(Token);
        Assert.Equal("Building the map for ui-3391", page.BuildingTitle);

        api.WholeMapAnswer = _ => Refusal("\"building\":{\"startedAt\":\"2026-01-01T02:00:00.0000000Z\"}");
        await page.Opened(Token);

        Assert.True(page.ShowBuilding);
        Assert.Equal("Building the map for ui-3391", page.BuildingTitle);
        Assert.StartsWith("started", page.BuildingBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Three_repositories_read_as_a_count_not_a_list()
    {
        var (page, _) = await Page(Refusal("\"noMap\":{\"repos\":[\"a-very-long-repository-name\",\"b\",\"c\"]}"));

        await page.BuildControl!.Press(Token);

        Assert.Equal("Building the map for 3 repos", page.BuildingTitle);
    }

    [Fact]
    public async Task Opening_for_an_item_changes_the_building_words()
    {
        var (page, _) = await Page(Refusal("\"noMap\":{\"repos\":[\"ui-3391\"]}"));
        await page.BuildControl!.Press(Token);
        var changed = new List<string?>();
        page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await page.OpenForItem(A.NewItem, Token);

        Assert.Contains(nameof(page.BuildingTitle), changed, StringComparer.Ordinal);
        Assert.Contains(nameof(page.BuildingBody), changed, StringComparer.Ordinal);
    }
}
