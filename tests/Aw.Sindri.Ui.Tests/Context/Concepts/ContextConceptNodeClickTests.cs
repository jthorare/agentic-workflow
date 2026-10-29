using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>A concept node on the whole map opens that concept's three bands, as a concept tile does (#1281): after a starting point is typed, choosing a node of kind concept goes to OpenConcept.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptNodeClickTests
{
    private const string Concept = "concept:tabs";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    // The small whole map with one concept node on it; a scoped read answers with the scoped golden (or the one given).
    private static FakeSindriApi Serving(List<string>? asked = null, Func<RouteAnswer>? scoped = null) => new()
    {
        WholeMapAnswer = q => q.Concept is not null ? (scoped ?? (() => RouteAnswer.Api("context-whole", "concept-open")))() : q.Area is null && q.Locate.Count == 0 ? WithConcept() : ContextAnswers.WholeMap(q),
        LensesAnswer = q =>
        {
            asked?.AddRange(q.Anchors);
            return RouteAnswer.Api("context-lenses", "map-tabs");
        },
    };

    private static RouteAnswer WithConcept()
    {
        var golden = GoldenFiles.Node("context-whole", "nodes-small");
        golden["nodes"]!.AsArray().Insert(0, new JsonObject
        {
            ["key"] = Concept,
            ["label"] = "Tabs",
            ["kind"] = "concept",
            ["area"] = "area:unassigned",
            ["alsoIn"] = new JsonArray(),
            ["trust"] = "trusted",
            ["provenance"] = "model",
            ["confidence"] = 0.8,
            ["confirmed"] = false,
            ["repo"] = "aw-split-panes",
        });
        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    // The scoped answer of a concept whose evidence is records only: no code node and no link to one.
    private static RouteAnswer NoCode()
    {
        var golden = GoldenFiles.Node("context-whole", "concept-open");
        var code = golden["nodes"]!.AsArray().Where(n => string.Equals((string?)n!["kind"], "code", StringComparison.Ordinal)).Select(n => (string)n!["key"]!).ToHashSet(StringComparer.Ordinal);
        foreach (var node in golden["nodes"]!.AsArray().Where(n => code.Contains((string)n!["key"]!)).ToList())
        {
            _ = golden["nodes"]!.AsArray().Remove(node);
        }

        foreach (var edge in golden["edges"]!.AsArray().Where(e => code.Contains((string)e!["from"]!) || code.Contains((string)e!["to"]!)).ToList())
        {
            _ = golden["edges"]!.AsArray().Remove(edge);
        }

        return new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
    }

    private static async Task<Aw.Sindri.Ui.ViewModels.Context.ContextPageViewModel> Queried(FakeSindriApi api)
    {
        var page = await ConceptPage.Shown(api);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        return page;
    }

    [Fact]
    public async Task Choosing_a_concept_node_after_a_starting_point_opens_that_concepts_three_bands()
    {
        var asked = new List<string>();
        var api = Serving(asked);
        var page = await Queried(api);
        Expect.True(page.IsZoomedIn);
        asked.Clear();

        await page.ChooseWholeNode(Concept, Token);

        Expect.Equal((Concept, Concept, true, false), (page.OpenedConceptId, api.WholeMapAsked[^1].Concept, page.ShowWhole, page.InspectorOpen));
        Expect.Equal(0, asked.Count);   // not read as a new starting point
    }

    [Fact]
    public async Task A_concept_node_with_no_code_opens_the_same_way_and_draws_its_bands_without_code()
    {
        var api = Serving(scoped: NoCode);
        var page = await Queried(api);

        await page.ChooseWholeNode(Concept, Token);

        Expect.Equal((Concept, true, false), (page.OpenedConceptId, page.Whole.IsReady, page.Whole.IsFailed));
        Expect.True(!page.Whole.Geometry!.Nodes.Any(n => string.Equals(n.Kind, "code", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_node_that_is_not_a_concept_is_still_the_new_starting_point()
    {
        var api = Serving();
        var page = await Queried(api);
        var other = page.Whole.Geometry!.Nodes.First(n => !string.Equals(n.Kind, "concept", StringComparison.Ordinal) && !page.Whole.HitKeys.Contains(n.Key)).Key;

        await page.ChooseWholeNode(other, Token);

        Expect.Equal((null, other), (page.OpenedConceptId, page.AnchorText));
    }
}
