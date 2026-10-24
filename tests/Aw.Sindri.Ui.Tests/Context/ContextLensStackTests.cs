using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The lens stack (U9a, now the map of F3 spec 1): the anchor box reads the lenses and draws the head and the map. Every value is read from a context-lenses golden.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextLensStackTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static System.Text.Json.Nodes.JsonNode Golden(string scenario) => GoldenFiles.Node("context-lenses", scenario);

    [Fact]
    public async Task One_anchor_reads_the_map_version_and_the_anchor_count_into_the_head()
    {
        var golden = Golden("map-tabs");
        var page = Page(new FakeSindriApi());
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        Assert.Equal(Labels.ContextHead(1), page.Head);
        Assert.True(page.HasHead);
    }

    [Fact]
    public async Task A_draft_read_has_only_the_tag_on_its_head()
    {
        var golden = Golden("map-tabs");
        golden["draft"] = true;
        golden["mapCommit"] = "abc1234";
        var api = new FakeSindriApi { LensesAnswer = _ => new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString()) };
        var page = Page(api);
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        Assert.Equal(Labels.ContextHead(1), page.Head);
        Assert.True(page.IsDraft);
    }

    [Fact]
    public async Task Two_anchors_read_the_count_of_the_set()
    {
        var golden = Golden("set-two-anchors");
        var page = Page(new FakeSindriApi());
        page.AnchorText = string.Join(' ', golden["anchors"]!.AsArray().Select(a => (string)a!["key"]!));

        await page.Submit(Token);

        Assert.Equal(Labels.ContextHead(golden["anchors"]!.AsArray().Count), page.Head);
    }

    [Fact]
    public async Task The_answer_is_laid_out_as_a_map_of_its_nodes_and_edges()
    {
        var golden = Golden("map-tabs");
        var page = Page(new FakeSindriApi());
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        var bands = golden["bands"]!;
        var map = page.Map!;
        Assert.True(page.HasStack);
        Assert.Equal(3, map.Bands.Count);
        Assert.Equal(bands["intent"]!["records"]!.AsArray().Count + bands["intent"]!["decisions"]!.AsArray().Count + bands["product"]!["own"]!.AsArray().Count + bands["code"]!["files"]!.AsArray().Count, map.Nodes.Count);
        Assert.Equal(bands["product"]!["edges"]!.AsArray().Count + golden["edges"]!.AsArray().Count, map.Edges.Count);   // the conflict replaces one edge
        Assert.Equal(Labels.ContextMapCoverageLead(1, 4), map.CoverageLead);
        Assert.False(page.IsDraft);
    }

    [Fact]
    public async Task A_model_link_is_a_dashed_edge_whose_pill_says_how_sure_the_tool_is_and_a_suggested_file_is_a_suggestion()
    {
        var golden = Golden("map-tabs");
        var edge = golden["edges"]!.AsArray().Single(e => (string)e!["provenance"]! == "model")!;
        var page = Page(new FakeSindriApi());
        page.AnchorText = (string)golden["anchor"]!["key"]!;

        await page.Submit(Token);

        var confidence = (int)Math.Round((double)edge["confidence"]! * 100, MidpointRounding.AwayFromZero);
        var pill = page.Map!.Pills.Single(p => p.Key == (string)edge["link"]!);
        Assert.True(pill.Dashed);
        Assert.EndsWith(Labels.ContextMapSuggestedPct(confidence), pill.Text, StringComparison.Ordinal);
        Assert.Contains(page.Map.Nodes, n => n.IsSuggested && n.Caption.Contains(Labels.ContextMapSuggestedPct(confidence), StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_anchor_is_named_not_found_and_the_rest_still_draw()
    {
        var golden = Golden("set-missing-anchor");
        var page = Page(new FakeSindriApi());
        page.AnchorText = string.Join(' ', golden["anchors"]!.AsArray().Select(a => (string)a!["key"]!).Concat(golden["missingAnchors"]!.AsArray().Select(m => (string)m!)));

        await page.Submit(Token);

        var missing = Assert.Single(page.Missing);
        Assert.Contains((string)golden["missingAnchors"]![0]!, missing.Text, StringComparison.Ordinal);
        Assert.Contains(Labels.ContextNotFound, missing.Text, StringComparison.Ordinal);
        Assert.True(page.HasStack);
    }

    [Fact]
    public async Task An_unknown_anchor_lists_the_served_suggestions_as_choices_and_picking_one_reads_it()
    {
        var golden = Golden("unknown-anchor-003");
        var suggested = golden["suggestedAnchors"]!.AsArray().Select(a => (string)a!).ToList();
        var page = Page(new FakeSindriApi());
        page.AnchorText = "feature:tabs.nope";

        await page.Submit(Token);

        Assert.Equal(suggested, page.Choices);
        Assert.False(page.HasStack);
        Assert.True(page.HasProblem);

        await page.Pick("feature:tabs.tab-groups", Token);
        Assert.Equal("feature:tabs.tab-groups", page.AnchorText);
    }

    [Fact]
    public async Task Repeated_anchors_collapse_and_fifty_anchors_go_to_the_lenses_read_unrefused()
    {
        var sent = new List<int>();
        var server = new FakeSindriApi { LensesAnswer = q => { sent.Add(q.Anchors.Count); return ContextAnswers.Lenses(q); } };
        var page = Page(server);
        var key = (string)Golden("map-tabs")["anchor"]!["key"]!;
        page.AnchorText = $"{key} {key}";
        await page.Submit(Token);
        Assert.Equal(Labels.ContextHead(1), page.Head);

        page.AnchorText = string.Join(' ', Enumerable.Range(0, 50).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a:{i}")));
        await page.Submit(Token);

        Assert.Equal([1, 50], sent);
    }

    [Fact]
    public async Task A_request_line_the_server_refuses_as_too_long_says_so_in_plain_words()
    {
        var server = new FakeSindriApi { LensesAnswer = _ => new RouteAnswer(HttpStatusCode.RequestUriTooLong, string.Empty) };
        var page = Page(server);
        page.AnchorText = string.Join(' ', Enumerable.Range(0, 400).Select(i => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a:{i}")));

        await page.Submit(Token);

        Assert.Equal(Labels.ContextTooLongForOneRead, page.Problem);
        Assert.False(page.HasStack);
    }
}
