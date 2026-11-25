using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

/// <summary>Issue 3005, the other side: a reference to a node that has run every time the reader runs must still validate.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphReferenceReachPassTests
{
    private static readonly IReadOnlyDictionary<string, IGraphBlockType> _blocks = new Dictionary<string, IGraphBlockType>(GraphFixtures.Blocks, StringComparer.Ordinal)
    {
        ["for_each"] = new ForEachBlock(new DefaultForEachLimits()),
        ["collect"] = new CollectBlock(),
    };

    private static GraphNode Each(string id) => new(NodeId.From(id), "for_each", Title: null, new JsonObject { ["items"] = "{{src.facts}}" });

    private static GraphNode Gather(string id, string value = "{{item}}") => new(NodeId.From(id), "collect", Title: null, new JsonObject { ["value"] = value });

    // why: only the reach rule (GRAPH-REF-001) is under test; the fixture condition block declares no outputs, so {{if.result}} is a GRAPH-REF-002 here
    private static void NoReachFinding(GraphSpec spec)
    {
        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);
        Assert.DoesNotContain(result.Findings, static f => f.Code == "GRAPH-REF-001");
    }

    [Fact]
    public void A_fan_out_that_merges_back_may_read_either_parent()
    {
        // daily -> a, daily -> b, a -> c, b -> c: c waits for both, and both always run
        NoReachFinding(Spec(
            [Trigger("daily"), Search("a"), Search("b"), Search("c", "after {{a.facts}} and {{b.facts}}")],
            [Edge("daily", "a"), Edge("daily", "b"), Edge("a", "c"), Edge("b", "c")]));
    }

    [Fact]
    public void A_node_on_a_branch_may_read_the_node_before_it_on_that_branch()
    {
        NoReachFinding(Spec(
            [Trigger("daily"), Condition("if", "yes"), Search("a"), Search("d", "after {{a.facts}}")],
            [Edge("daily", "if"), Edge("if", "a", "true"), Edge("a", "d")]));
    }

    [Fact]
    public void A_node_after_a_join_may_read_the_condition_and_the_trigger()
    {
        NoReachFinding(Spec(
            [Trigger("daily"), Condition("if", "yes"), Search("a"), Search("b"), Search("c", "after {{if.result}} at {{daily.scheduledFor}}")],
            [Edge("daily", "if"), Edge("if", "a", "true"), Edge("if", "b", "false"), Edge("a", "c"), Edge("b", "c")]));
    }

    [Fact]
    public void A_fan_out_below_a_branch_that_merges_back_may_read_either_parent()
    {
        // if -true-> x, x -> a, x -> b, a -> c, b -> c: a and b run every time x runs, and c runs only when x ran
        NoReachFinding(Spec(
            [Trigger("daily"), Condition("if", "yes"), Search("x"), Search("a"), Search("b"), Search("c", "after {{a.facts}}")],
            [Edge("daily", "if"), Edge("if", "x", "true"), Edge("x", "a"), Edge("x", "b"), Edge("a", "c"), Edge("b", "c")]));
    }

    [Fact]
    public void A_reader_no_start_reaches_gets_the_wiring_finding_not_a_reach_finding()
    {
        // x -> y -> x hangs off nothing: GRAPH-WIRING-001 and GRAPH-CYCLE-001 already say why it never runs
        var spec = Spec(
            [Trigger("daily"), Search("a"), Search("x"), Search("y", "after {{x.facts}}")],
            [Edge("daily", "a"), Edge("x", "y"), Edge("y", "x")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, static f => f.Code == "GRAPH-REF-001");
        Assert.Contains(result.Findings, static f => f.Code == "GRAPH-WIRING-001");
    }

    [Fact]
    public void Loop_body_nodes_and_the_collect_may_read_the_body_the_item_and_the_for_each()
    {
        NoReachFinding(Spec(
            [Trigger("daily"), Search("src"), Each("each"), Search("one", "for {{item}} of {{each.count}}"), Search("two", "after {{one.facts}} at {{itemIndex}}"), Gather("gather", "{{two.facts}} {{item}}")],
            [Edge("daily", "src"), Edge("src", "each"), Edge("each", "one"), Edge("one", "two"), Edge("two", "gather")]));
    }
}
