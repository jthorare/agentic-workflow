using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>Opening the Context page for an item: its frozen starting points fill the box and are read against the live map; there is no as-of choice.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextOpenForItemTests
{
    private const string Item = "ITM-0000000000000000000000000000000f";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static string AnchorKey => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static RouteAnswer Frozen()
    {
        var golden = GoldenFiles.Node("items-id-scope", "scope-found");
        var scope = golden["scope"]!;
        scope["anchors"] = new JsonArray(new JsonObject { ["key"] = AnchorKey, ["mapVersion"] = "v1" });
        scope["mapVersion"] = "v1";
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public async Task A_frozen_scope_fills_the_starting_points_and_reads_the_live_map()
    {
        var asked = new List<string?>();
        var api = new FakeSindriApi { ItemScopeAnswer = _ => Frozen() };
        var inner = api.LensesAnswer;
        api.LensesAnswer = query =>
        {
            asked.Add(query.MapVersion);
            return inner(query);
        };
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal([Item], api.ScopeAsked);
        Assert.Equal(AnchorKey, page.AnchorText);
        Assert.Equal(Labels.ContextHead(1), page.Head);
        Assert.Equal([null], asked);
    }

    [Fact]
    public async Task An_unknown_item_leaves_the_page_as_it_was()
    {
        var api = new FakeSindriApi { ItemScopeAnswer = _ => RouteAnswer.Problem("items-id-scope", "not-found") };
        var page = Page(api);

        await page.OpenForItem(Item, Token);

        Assert.Equal(string.Empty, page.AnchorText);
    }
}
