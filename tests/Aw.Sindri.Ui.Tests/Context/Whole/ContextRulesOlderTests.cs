using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The map status says the live map was built under older map rules (rulesOlder, #1125): the Context page says so beside its head, as the CLI does, and stays quiet when the field is false or absent (#1153).</summary>
[Lane(TestLane.Unit)]
public sealed class ContextRulesOlderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Uri _url = new("http://127.0.0.1:1/");

    private static RouteAnswer Status(bool? rulesOlder)
    {
        var status = JsonNode.Parse(GoldenFiles.Api("context-map", "map-tabs-anchor"))!.AsObject();   // why: the golden is a JSON object
        if (rulesOlder is { } value)
        {
            status["rulesOlder"] = value;
        }

        return new RouteAnswer(HttpStatusCode.OK, status.ToJsonString());
    }

    private static async Task<ContextPageViewModel> Shown(RouteAnswer status)
    {
        var api = new FakeSindriApi { MapStatusAnswer = status };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        catalog.Show(_url, api, (await api.Commands(_url, Token)).Value);
        var follows = new List<Func<Uri, CancellationToken, Task>>();
        var page = new ContextPageViewModel(new ContextPorts(api, () => _url, clipboard, clock, catalog, follows.Add));
        await page.Opened(Token);
        foreach (var follow in follows)
        {
            await follow(_url, Token);
        }

        return page;
    }

    [Fact]
    public async Task A_map_built_under_older_rules_says_so_in_the_CLI_words()
    {
        var page = await Shown(Status(rulesOlder: true));

        Assert.Equal((true, "Built under older map rules; a refresh will rebuild it (sindri context map refresh)"), (page.ShowRulesOlder, page.RulesOlderText));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task A_status_without_older_rules_stays_quiet(bool? rulesOlder)
    {
        var page = await Shown(Status(rulesOlder));

        Assert.False(page.ShowRulesOlder);
    }
}
