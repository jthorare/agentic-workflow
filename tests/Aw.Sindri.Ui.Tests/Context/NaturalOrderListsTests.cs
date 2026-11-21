using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>Issue 3658: the repo names and a drilled band's neighbour list order by their numbers (2 before 10), like the area cards, not as plain text.</summary>
[Lane(TestLane.Unit)]
public sealed class NaturalOrderListsTests
{
    private static readonly string[] _repos = ["repo-2", "repo-10", "repo-1"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public async Task Repo_names_order_by_their_numbers()
    {
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q =>
            {
                var node = JsonNode.Parse(GoldenFiles.Api("context-whole", "nodes-small"))!.AsObject();
                _ = node.Remove("repos");
                node["repos"] = new JsonArray([.. _repos.Select(r => (JsonNode)new JsonObject { ["name"] = r, ["nodeCount"] = 3 })]);
                return new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
            },
        };
        var page = Page(api);

        await page.Opened(Token);

        Assert.Equal(["repo-1", "repo-2", "repo-10"], page.Repos);
    }

    [Fact]
    public void Band_neighbours_are_kept_in_their_numeric_order_when_the_reach_cap_cuts_the_list()
    {
        // One Product hub, decided by a decision, is part-of-linked to 210 Product nodes p:1..p:210; the cap keeps 200 of them, and natural order keeps p:1..p:200.
        var map = WholeMapLayoutTests.Draft("nodes-small");
        var area = map.Areas[0] with { Unassigned = false };
        WholeNode Node(string key, string kind) => new(key, key, kind, area.Key, [], "trusted", "human", 1, Confirmed: true);
        string P(int i) => string.Create(CultureInfo.InvariantCulture, $"p:{i}");
        var nodes = new List<WholeNode> { Node("n:decision", "decision"), Node("n:hub", "feature") };
        nodes.AddRange(Enumerable.Range(1, 210).Select(i => Node(P(i), "feature")));
        var edges = new List<WholeEdge> { new("n:decision", "n:hub", "decides", "edge:d>h", "human", 1, Confirmed: true) };
        edges.AddRange(Enumerable.Range(1, 210).Select(i => new WholeEdge("n:hub", P(i), "part-of", string.Create(CultureInfo.InvariantCulture, $"edge:h>{i}"), "human", 1, Confirmed: true)));
        var whole = WholeMapLayout.Place(map with { Areas = [area], AreaEdges = [], Nodes = nodes, Edges = edges });

        var shown = WholeMapBandDrill.Cross(whole, WholeBand.Intent).Nodes.Select(n => n.Key).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("p:200", shown);
        Assert.DoesNotContain("p:201", shown);
    }
}
