using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

/// <summary>'onFailure: skip-item' drops one item of a loop (#3806), so it is valid only on a node of a for_each body; anywhere else it is GRAPH-NODE-005.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphValidatorSkipItemTests
{
    private static readonly IReadOnlyDictionary<string, IGraphBlockType> _blocks = new Dictionary<string, IGraphBlockType>(GraphFixtures.Blocks, StringComparer.Ordinal)
    {
        ["for_each"] = new ForEachBlock(new DefaultForEachLimits()),
        ["collect"] = new CollectBlock(),
    };

    private static GraphNode Each(string id) => new(NodeId.From(id), "for_each", Title: null, new JsonObject { ["items"] = "{{search.facts}}" });

    private static GraphNode Gather(string id) => new(NodeId.From(id), "collect", Title: null, new JsonObject { ["value"] = "{{item}}" });

    private static GraphNode Skipping(GraphNode node) => node with { OnFailure = "skip-item" };

    private static GraphSpec Loop(GraphNode tell, GraphNode gather, GraphNode after) => Spec(
        [Trigger("daily"), Search("search"), Each("each"), tell, gather, after],
        [Edge("daily", "search"), Edge("search", "each"), Edge("each", "tell"), Edge("tell", "gather"), Edge("gather", "after")]);

    [Fact]
    public void Skip_item_on_a_node_of_a_for_each_body_passes()
    {
        var spec = Loop(Skipping(Notify("tell", "{{item}}")), Gather("gather"), Notify("after", "{{gather.count}}"));

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, static f => f.Code is "GRAPH-NODE-003" or "GRAPH-NODE-005");
    }

    [Fact]
    public void Skip_item_on_a_node_outside_any_loop_is_refused_naming_the_node_and_field()
    {
        var spec = Loop(Notify("tell", "{{item}}"), Gather("gather"), Skipping(Notify("after", "{{gather.count}}")));

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, static f => f.Code == "GRAPH-NODE-005");
        Assert.Equal(["after"], finding.Nodes);
        Assert.Equal("onFailure", finding.Field);
        Assert.Contains("for_each", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Findings, static f => f.Code == "GRAPH-NODE-003");
        Assert.False(result.Passed);
    }

    [Fact]
    public void Skip_item_on_the_collect_is_refused()
    {
        var spec = Loop(Notify("tell", "{{item}}"), Skipping(Gather("gather")), Notify("after", "{{gather.count}}"));

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        Assert.Equal(["gather"], Assert.Single(result.Findings, static f => f.Code == "GRAPH-NODE-005").Nodes);
    }

    [Fact]
    public void An_unknown_onFailure_value_names_skip_item_in_its_fix()
    {
        var spec = Loop(Notify("tell", "{{item}}") with { OnFailure = "retry" }, Gather("gather"), Notify("after", "{{gather.count}}"));

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        Assert.Contains("skip-item", Assert.Single(result.Findings, static f => f.Code == "GRAPH-NODE-003").Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void The_collect_block_declares_a_skipped_output()
    {
        Assert.Equal("number", new CollectBlock().OutputsFor([]).Fields.GetValueOrDefault("skipped"));
    }
}
