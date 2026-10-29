using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;
using Aw.Context.Tests.Concepts;

namespace Aw.Context.Tests.WholeMap;

/// <summary>#1701: a concept's map keeps the product pages and actions under what its decisions explain, forward only and capped.</summary>
public sealed class ConceptScopeProductClosureTests
{
    private static readonly WholeMapBasis _basis = new("v7", MapCommit: null, Draft: false, DateTimeOffset.UnixEpoch, Repo: null);

    private static WholeMapView Ask(ConceptGraph world)
    {
        var graph = world.Build();
        var ask = new WholeMapAsk(_basis, WholeMapDetail.Auto, [], Area: null) { Concept = "concept:staffing" };
        return WholeMapBuilder.Build(ask, graph, MapAreas.Assign(graph), WholeMapRules.Default, ConceptGraph.Text).Value;
    }

    private static ConceptGraph Staffing() => new ConceptGraph()
        .Concept("concept:staffing", "Staffing")
        .Node("decision:goal-rule").Member("decision:goal-rule", "concept:staffing")
        .Node("thing:staffing-goal").Edge("decision:goal-rule", EdgeType.Decides, "thing:staffing-goal")
        .Node("action:set-goal").Edge("action:set-goal", EdgeType.Writes, "thing:staffing-goal")
        .Node("surface:goals").Edge("action:set-goal", EdgeType.On, "surface:goals");

    private static HashSet<string> Keys(WholeMapView view) => [.. view.Nodes!.Select(n => n.Key.Value)];

    [Fact, Trait("Lane", "unit")]
    public void A_decision_that_decides_a_thing_brings_the_action_that_writes_it_and_that_actions_surface()
    {
        var view = Ask(Staffing());

        Assert.Contains("thing:staffing-goal", Keys(view));
        Assert.Contains("action:set-goal", Keys(view));
        Assert.Contains("surface:goals", Keys(view));
        Assert.Contains(view.Edges!, e => e.Type == EdgeType.Writes && e.From.Value == "action:set-goal" && e.To.Value == "thing:staffing-goal");
        Assert.Contains(view.Edges!, e => e.Type == EdgeType.On && e.From.Value == "action:set-goal" && e.To.Value == "surface:goals");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hub_surface_reached_forward_does_not_pull_in_the_other_actions_on_it()
    {
        var world = Staffing().Node("surface:settings").Edge("action:set-goal", EdgeType.On, "surface:settings");
        for (var i = 0; i < 10; i++)
        {
            var other = $"action:other-{i.ToString(CultureInfo.InvariantCulture)}";
            _ = world.Node(other).Edge(other, EdgeType.On, "surface:settings");
        }

        var keys = Keys(Ask(world));

        Assert.Contains("surface:settings", keys);
        Assert.DoesNotContain(keys, k => k.StartsWith("action:other-", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_closure_follows_forward_edges_past_level_two_and_stops_at_a_non_member_feature_and_at_code()
    {
        var world = Staffing()
            .Node("feature:swaps").Member("feature:swaps", "concept:staffing")
            .Node("surface:board").Edge("surface:board", EdgeType.PartOf, "feature:swaps")
            .Node("action:offer").Edge("action:offer", EdgeType.On, "surface:board")
            .Node("surface:offers").Edge("action:offer", EdgeType.On, "surface:offers")
            .Node("feature:payroll", "Payroll").Edge("surface:offers", EdgeType.PartOf, "feature:payroll").Edge("feature:payroll", EdgeType.DependsOn, "feature:swaps")
            .Node("surface:payslips").Edge("surface:payslips", EdgeType.PartOf, "feature:payroll")
            .Node("code:src/Offer.cs").Edge("code:src/Offer.cs", EdgeType.Implements, "action:offer")
            .Edge("action:set-goal", EdgeType.StepOf, "flow:onboarding").Node("flow:onboarding").Node("surface:welcome").Edge("surface:welcome", EdgeType.StepOf, "flow:onboarding");

        var keys = Keys(Ask(world));

        // Levels from the members (decision:goal-rule, feature:swaps): thing:staffing-goal and surface:board are 1, action:set-goal and action:offer 2. code:src/Offer.cs is 3 (past the walk) and never added by the closure.
        // The member feature's own chain is level 2 and so already in; the closure takes action:offer's other surface from there.
        Assert.Contains("action:offer", keys);
        Assert.Contains("surface:offers", keys);
        Assert.Contains("flow:onboarding", keys);
        Assert.DoesNotContain("feature:payroll", keys);
        Assert.DoesNotContain("surface:payslips", keys);
        Assert.DoesNotContain("code:src/Offer.cs", keys);
        Assert.DoesNotContain("surface:welcome", keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_closure_adds_at_most_its_cap_in_key_order()
    {
        // feature:f is the member; surface:s is level 1 and thing:t level 2, so the 250 writers of thing:t are level 3 and only the closure's back-hop can add them.
        var world = new ConceptGraph()
            .Concept("concept:staffing", "Staffing")
            .Node("feature:f").Member("feature:f", "concept:staffing")
            .Node("surface:s").Edge("surface:s", EdgeType.PartOf, "feature:f")
            .Node("thing:t").Edge("surface:s", EdgeType.Writes, "thing:t");
        for (var i = 0; i < 250; i++)
        {
            var writer = $"action:w-{i.ToString("000", CultureInfo.InvariantCulture)}";
            _ = world.Node(writer).Edge(writer, EdgeType.Writes, "thing:t");
        }

        var keys = Keys(Ask(world));
        var writers = keys.Where(k => k.StartsWith("action:w-", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(200, writers.Count);
        Assert.Contains("action:w-000", writers);
        Assert.DoesNotContain("action:w-249", writers);
    }
}
