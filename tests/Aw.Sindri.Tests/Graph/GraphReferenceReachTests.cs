using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

/// <summary>Issue 3005: a reference can name an upstream node and still resolve to null at run time. These shapes must be refused.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphReferenceReachTests
{
    private static readonly IReadOnlyDictionary<string, IGraphBlockType> _blocks = new Dictionary<string, IGraphBlockType>(GraphFixtures.Blocks, StringComparer.Ordinal)
    {
        ["for_each"] = new ForEachBlock(new DefaultForEachLimits()),
        ["collect"] = new CollectBlock(),
    };

    private static GraphNode Each(string id) => new(NodeId.From(id), "for_each", Title: null, new JsonObject { ["items"] = "{{src.facts}}" });

    private static GraphNode Gather(string id) => new(NodeId.From(id), "collect", Title: null, new JsonObject { ["value"] = "{{item}}" });

    [Fact]
    public void A_reference_to_a_node_on_the_untaken_branch_of_a_condition_is_refused()
    {
        // daily -> if -true-> a -> c, if -false-> b -> c: c runs on both paths, a runs on one
        var spec = Spec(
            [Trigger("daily"), Condition("if", "yes"), Search("a"), Search("b"), Search("c", "after {{a.facts}}")],
            [Edge("daily", "if"), Edge("if", "a", "true"), Edge("if", "b", "false"), Edge("a", "c"), Edge("b", "c")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var refused = Assert.Single(result.Findings, static f => f.Code == "GRAPH-REF-001");
        Assert.Equal(["c"], refused.Nodes);
        Assert.Equal("config.query", refused.Field);
        Assert.Contains("'a'", refused.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_reference_to_a_loop_body_node_from_after_the_collect_is_refused()
    {
        var spec = Spec(
            [Trigger("daily"), Search("src"), Each("each"), Search("body"), Gather("gather"), Search("after", "see {{body.facts}}")],
            [Edge("daily", "src"), Edge("src", "each"), Edge("each", "body"), Edge("body", "gather"), Edge("gather", "after")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        var refused = Assert.Single(result.Findings, static f => f.Code == "GRAPH-REF-001");
        Assert.Equal(["after"], refused.Nodes);
        Assert.Equal("config.query", refused.Field);
        Assert.Contains("'body'", refused.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("item")]
    [InlineData("itemIndex")]
    public void The_item_names_outside_a_for_each_body_are_refused(string name)
    {
        var spec = Spec([Trigger("daily"), Search("a", "for {{" + name + "}}")], [Edge("daily", "a")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        var refused = Assert.Single(result.Findings, static f => f.Code == "GRAPH-REF-001");
        Assert.Equal(["a"], refused.Nodes);
        Assert.Equal("config.query", refused.Field);
        Assert.Contains("'" + name + "'", refused.ToString(), StringComparison.Ordinal);
    }
}
