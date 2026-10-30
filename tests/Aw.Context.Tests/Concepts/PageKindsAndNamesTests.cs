using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "unit")]
public sealed class PageKindsAndNamesTests
{
    private static GraphNode Node(string key, string? label = null) => new(NodeKey.From(key), label ?? key);

    private static GraphEdge Implements(string code, string target) => new(NodeKey.From(code), NodeKey.From(target), EdgeType.Implements);

    [Theory]
    [InlineData("screen", true)]
    [InlineData("dialog", true)]
    [InlineData("pane", false)]
    [InlineData("api", false)]
    [InlineData("command", false)]
    [InlineData("keybinding", false)]
    [InlineData("setting", false)]
    [InlineData("menu", false)]
    [InlineData("job", false)]
    [InlineData("notification", false)]
    [InlineData("permission", false)]
    [InlineData("report", false)]
    [InlineData("flag", false)]
    public void Only_a_screen_or_a_dialog_is_a_page_by_kind(string kind, bool expected) =>
        Assert.Equal(expected, PageKinds.IsPage(Node($"surface:{kind}/x"), []));

    [Theory]
    [InlineData("web/Week.tsx", true)]
    [InlineData("web/Week.jsx", true)]
    [InlineData("web/week.ts", false)]
    [InlineData("api/Week.cs", false)]
    public void A_route_is_a_page_only_from_a_component_file(string path, bool expected) =>
        Assert.Equal(expected, PageKinds.IsPage(Node("surface:route/GET:/schedule/week"), [Implements("code:" + path, "surface:route/GET:/schedule/week")]));

    [Fact]
    public void A_route_with_no_code_is_not_a_page() => Assert.False(PageKinds.IsPage(Node("surface:route/GET:/x"), []));

    [Fact]
    public void An_action_is_not_a_page() => Assert.False(PageKinds.IsPage(Node("action:pay"), []));

    [Theory]
    [InlineData("GET:/schedule/week", "Schedule week")]
    [InlineData("/schedule/:id/week", "Schedule week")]
    [InlineData("ScheduleWeekPage", "Schedule week")]
    [InlineData("EditShiftDialog", "Edit shift")]
    [InlineData("Settings", "Settings")]
    [InlineData("the week view", "the week view")]
    public void A_name_is_offered_from_the_route_or_component(string label, string expected) =>
        Assert.Equal(expected, PageNames.Offer(Node("surface:screen/x", label)));

    [Fact]
    public void An_expresses_edge_joins_an_action_or_surface_to_a_concept_only()
    {
        Assert.True(EdgeRules.Allows(EdgeType.Expresses, NodeKind.Action, NodeKind.Concept));
        Assert.True(EdgeRules.Allows(EdgeType.Expresses, NodeKind.Surface, NodeKind.Concept));
        Assert.False(EdgeRules.Allows(EdgeType.Expresses, NodeKind.Feature, NodeKind.Concept));
        Assert.False(EdgeRules.Allows(EdgeType.Expresses, NodeKind.Action, NodeKind.Feature));
        Assert.Equal("expresses", EdgeRules.Name(EdgeType.Expresses));
    }

    [Fact]
    public void At_most_three_concepts_are_suggested_the_most_files_first_then_by_id()
    {
        var graph = new ConceptGraph().Node("action:a");
        foreach (var c in new[] { "d", "c", "b", "a" })
        {
            _ = graph.Concept($"concept:{c}", c).Node($"feature:{c}").Member($"feature:{c}", $"concept:{c}")
                .Edge($"code:src/{c}.cs", EdgeType.Implements, $"feature:{c}").Edge($"code:src/{c}.cs", EdgeType.Implements, "action:a");
        }

        _ = graph.Edge("code:src/extra.cs", EdgeType.Implements, "feature:d").Edge("code:src/extra.cs", EdgeType.Implements, "action:a");
        var made = PageExpresses.Apply(graph.Build()).Edges.Where(e => e.Type == EdgeType.Expresses).Select(e => e.To.Value).ToList();

        Assert.Equal(["concept:d", "concept:a", "concept:b"], made);
    }

    [Fact]
    public void A_screen_with_no_code_gets_no_suggestion()
    {
        var graph = new ConceptGraph().Concept("concept:a", "a").Node("surface:screen/home");
        Assert.DoesNotContain(PageExpresses.Apply(graph.Build()).Edges, e => e.Type == EdgeType.Expresses);
    }

    [Fact]
    public void A_pane_gets_no_suggestion()
    {
        var graph = new ConceptGraph().Concept("concept:a", "a").Node("feature:f").Member("feature:f", "concept:a").Node("surface:pane/x")
            .Edge("code:src/x.cs", EdgeType.Implements, "feature:f").Edge("code:src/x.cs", EdgeType.Implements, "surface:pane/x");
        Assert.DoesNotContain(PageExpresses.Apply(graph.Build()).Edges, e => e.Type == EdgeType.Expresses);
    }
}
