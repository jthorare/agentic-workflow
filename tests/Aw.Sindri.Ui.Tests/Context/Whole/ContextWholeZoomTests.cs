using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A query zooms into the whole map (whole-map spec §3.1 "Zoomed in"): the answer's nodes stand out on the map behind it, the camera goes to them, Esc and "Whole map" go back.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeZoomTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    // By feature: the overview, the answer's nodes located in the one feature the goldens hold in full, and that feature read with area=.
    private static FakeSindriApi ByArea() => new()
    {
        WholeMapAnswer = q => q switch
        {
            { Locate.Count: > 0 } => Located(q),
            { Area: not null } => ContextAnswers.WholeMap(q),
            _ => RouteAnswer.Api("context-whole", "areas-over-budget"),
        },
    };

    private static RouteAnswer Located(WholeMapQuery query)
    {
        var golden = GoldenFiles.Node("context-whole", "locate");
        golden["located"] = new JsonArray([.. query.Locate.Select(k => (JsonNode)new JsonObject { ["key"] = k, ["area"] = ContextAnswers.OpenArea })]);
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public async Task A_query_keeps_the_whole_map_on_the_stage_and_zooms_to_the_answers_nodes_that_are_on_it()
    {
        var page = Page(new FakeSindriApi());
        await page.Opened(Token);
        var asked = new List<WholeRect>();
        page.Whole.ZoomRequested += (_, e) => asked.Add(e.Value);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((true, false, true), (page.ShowWhole, page.MapVisible, page.IsZoomedIn));
        Assert.NotEmpty(page.Whole.HitKeys);
        Assert.All(page.Whole.HitKeys, key => Assert.Contains(page.Map!.Nodes, n => n.Key == key));
        Assert.Equal(WholeZoomBox.Around([.. page.Whole.Geometry!.Nodes.Where(n => page.Whole.HitKeys.Contains(n.Key)).Select(n => n.Rect)]), Assert.Single(asked));
        Assert.Equal("Context · the whole map · v1", page.Head);
    }

    [Fact]
    public async Task An_answer_with_nothing_on_the_map_says_so_and_moves_nothing()
    {
        var api = new FakeSindriApi { WholeMapAnswer = _ => RouteAnswer.Api("context-whole", "draft") };
        var page = Page(api);
        await page.Opened(Token);
        var moved = false;
        page.Whole.ZoomRequested += (_, _) => moved = true;
        page.Whole.Show(WholeMapLayoutTests.Draft("nodes-small") with { Nodes = [], Edges = [] });
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((false, false, Labels.ContextWholeNoMatch), (moved, page.IsZoomedIn, page.Problem));
    }

    [Fact]
    public async Task Esc_closes_the_inspector_first_then_goes_back_to_the_whole_map_and_fits_again()
    {
        var api = new FakeSindriApi();
        var page = Page(api);
        await page.Opened(Token);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        await page.Select(page.Map!.Nodes[0].Key, Token);
        var fits = 0;
        page.Whole.FitRequested += (_, _) => fits++;

        Assert.True(page.Back());
        Assert.Equal((false, true, 0), (page.InspectorOpen, page.IsZoomedIn, fits));

        Assert.True(page.Back());
        Assert.Equal((false, 1, false, true), (page.IsZoomedIn, fits, page.HasStack, page.ShowWhole));
        Assert.Empty(page.Whole.HitKeys);
        Assert.False(page.Back());
    }

    [Fact]
    public async Task The_whole_map_button_does_the_same_in_one_step()
    {
        var page = Page(new FakeSindriApi());
        await page.Opened(Token);
        page.AnchorText = Anchor;
        await page.Submit(Token);

        Assert.True(page.BackToWholeMap());

        Assert.Equal((false, false, "Context · the whole map · v1"), (page.IsZoomedIn, page.HasStack, page.Head));
        Assert.False(page.BackToWholeMap());
    }

    [Fact]
    public async Task The_card_asks_for_a_starting_point_until_one_is_chosen()
    {
        var page = Page(new FakeSindriApi());
        await page.Opened(Token);

        Assert.Equal(Labels.ContextWholePick, page.Whole.CardText);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        Assert.False(string.Equals(Labels.ContextWholePick, page.Whole.CardText, StringComparison.Ordinal));
        _ = page.BackToWholeMap();
        Assert.Equal(Labels.ContextWholePick, page.Whole.CardText);
    }

    [Fact]
    public async Task By_feature_the_answers_nodes_are_located_and_their_feature_is_opened_in_full_with_the_other_tiles_staying_tiles()
    {
        var api = ByArea();
        var page = Page(api);
        await page.Opened(Token);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal(ContextAnswers.OpenArea, api.WholeMapAsked.Single(q => q.Area is not null).Area);
        Assert.Equal(("areas", true), (api.WholeMapAsked.Single(q => q.Locate.Count > 0).Detail, page.IsZoomedIn));
        Assert.NotEmpty(page.Whole.Geometry!.Nodes);
        Assert.NotEmpty(page.Whole.Geometry.Tiles);
        _ = page.BackToWholeMap();
        Assert.Equal((true, 0), (page.Whole.ByArea, page.Whole.Geometry!.Nodes.Count));
    }

    [Fact]
    public async Task By_feature_every_touched_feature_is_opened_not_just_the_first_three()
    {
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q => q switch
            {
                { Locate.Count: > 0 } => LocatedInAreas(q, 5),
                { Area: not null } => ContextAnswers.WholeMap(q),
                _ => RouteAnswer.Api("context-whole", "areas-over-budget"),
            },
        };
        var page = Page(api);
        await page.Opened(Token);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((5, false), (api.WholeMapAsked.Count(q => q.Area is not null), page.Whole.HasMoreTouched));
    }

    private static RouteAnswer LocatedInAreas(WholeMapQuery query, int areas)
    {
        var golden = GoldenFiles.Node("context-whole", "locate");
        golden["located"] = new JsonArray(
            [.. Enumerable.Range(0, areas).Select(i => (JsonNode)new JsonObject { ["key"] = query.Locate[i % query.Locate.Count], ["area"] = "area:a" + i.ToString(CultureInfo.InvariantCulture) })]);
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    [Fact]
    public async Task A_second_query_while_zoomed_by_feature_zooms_again_and_is_not_refused()
    {
        var page = Page(ByArea());
        await page.Opened(Token);
        page.AnchorText = Anchor;
        await page.Submit(Token);

        await page.Submit(Token);

        Assert.Equal((string.Empty, true), (page.Problem, page.IsZoomedIn));
        _ = page.BackToWholeMap();
        Assert.Equal((true, 0), (page.Whole.ByArea, page.Whole.Geometry!.Nodes.Count));
    }

    [Fact]
    public async Task A_query_keeps_every_node_of_the_map_drawn_behind_the_answer_to_be_dimmed_and_Whole_map_restores_it()
    {
        var page = Page(new FakeSindriApi());
        await page.Opened(Token);
        var nodes = page.Whole.Geometry!.Nodes.Count;
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal(nodes, page.Whole.Geometry!.Nodes.Count);
        Assert.Contains(page.Whole.Geometry.Nodes, n => !page.Whole.HitKeys.Contains(n.Key));
        _ = page.BackToWholeMap();
        Assert.Empty(page.Whole.HitKeys);
    }

    [Fact]
    public async Task An_answer_whose_nodes_are_in_no_feature_is_a_refusal_over_the_overview()
    {
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q => q.Locate.Count > 0
                ? new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Node("context-whole", "locate").ToJsonString().Replace("\"area\": \"area:unassigned\"", "\"area\": null", StringComparison.Ordinal))
                : RouteAnswer.Api("context-whole", "areas-over-budget"),
        };
        var page = Page(api);
        await page.Opened(Token);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((false, Labels.ContextWholeNoMatch, true), (page.IsZoomedIn, page.Problem, page.ShowWhole));
    }

    // Any answer the lenses give: the map-tabs golden, whatever starting point was asked.
    private static FakeSindriApi Recording(List<string> asked) => new()
    {
        LensesAnswer = q =>
        {
            asked.AddRange(q.Anchors);
            return RouteAnswer.Api("context-lenses", "map-tabs");
        },
    };

    [Fact]
    public async Task A_click_on_a_dimmed_node_makes_it_the_new_starting_point_and_the_camera_goes_to_its_answer()
    {
        var asked = new List<string>();
        var page = Page(Recording(asked));
        await page.Opened(Token);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        var dimmed = page.Whole.Geometry!.Nodes.First(n => !page.Whole.HitKeys.Contains(n.Key)).Key;
        var moved = new List<WholeRect>();
        page.Whole.ZoomRequested += (_, e) => moved.Add(e.Value);
        asked.Clear();

        await page.ChooseWholeNode(dimmed, Token);

        Assert.Equal([dimmed], asked);
        Assert.Equal(dimmed, page.AnchorText);
        _ = Assert.Single(moved);
        Assert.True(page.IsZoomedIn);
    }

    [Fact]
    public async Task A_click_on_a_node_of_the_answer_opens_it_in_the_inspector_and_reads_nothing_new()
    {
        var asked = new List<string>();
        var page = Page(Recording(asked));
        await page.Opened(Token);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        var hit = page.Whole.HitKeys.First();
        asked.Clear();

        await page.ChooseWholeNode(hit, Token);

        Assert.Equal((true, 0), (page.InspectorOpen, asked.Count));
    }

    [Fact]
    public async Task With_nothing_zoomed_to_a_click_on_any_node_is_the_starting_point()
    {
        var asked = new List<string>();
        var page = Page(Recording(asked));
        await page.Opened(Token);
        var key = page.Whole.Geometry!.Nodes[0].Key;

        await page.ChooseWholeNode(key, Token);

        Assert.Equal([key], asked);
        Assert.Equal(key, page.AnchorText);
    }
}
