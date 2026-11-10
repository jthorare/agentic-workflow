using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

[Lane(TestLane.Unit)]
public sealed class GraphValidatorLoopTests
{
    private static readonly IReadOnlyDictionary<string, IGraphBlockType> _blocks = new Dictionary<string, IGraphBlockType>(GraphFixtures.Blocks, StringComparer.Ordinal)
    {
        ["for_each"] = new ForEachBlock(new DefaultForEachLimits()),
        ["collect"] = new CollectBlock(),
        ["wait"] = new WaitBlock(TimeProvider.System),
    };

    private static GraphNode Each(string id) => new(NodeId.From(id), "for_each", Title: null, new JsonObject { ["items"] = "{{search.facts}}" });

    private static GraphNode Pause(string id) => new(NodeId.From(id), "wait", Title: null, new JsonObject { ["until"] = "07:00", ["timezone"] = "America/Los_Angeles" });

    private static GraphNode Gather(string id) => new(NodeId.From(id), "collect", Title: null, new JsonObject { ["value"] = "{{item}}" });

    [Fact]
    public void A_for_each_wired_to_one_collect_passes_and_may_read_item_and_item_index()
    {
        var spec = Spec(
            [Trigger("daily"), Search("search"), Each("each"), Notify("tell", "{{item}} is number {{itemIndex}}"), Gather("gather")],
            [Edge("daily", "search"), Edge("search", "each"), Edge("each", "tell"), Edge("tell", "gather")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, static f => f.Code.StartsWith("GRAPH-LOOP", StringComparison.Ordinal) || f.Code.StartsWith("GRAPH-REF", StringComparison.Ordinal));
    }

    [Fact]
    public void A_for_each_with_no_collect_is_a_loop_finding_naming_it()
    {
        var spec = Spec([Trigger("daily"), Each("each"), Notify("tell", "x")], [Edge("daily", "each"), Edge("each", "tell")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, static f => f.Code == "GRAPH-LOOP-001");
        Assert.Equal(["each"], finding.Nodes);
        Assert.Contains("collect", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wait_inside_a_loop_is_a_loop_finding_naming_it()
    {
        // why: an item of a loop runs in its own child workflow, which never parks, so a wait or ask_owner there would end at once
        var spec = Spec(
            [Trigger("daily"), Search("search"), Each("each"), Pause("pause"), Gather("gather")],
            [Edge("daily", "search"), Edge("search", "each"), Edge("each", "pause"), Edge("pause", "gather")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, static f => f.Code == "GRAPH-LOOP-004");
        Assert.Equal(["pause"], finding.Nodes);
        Assert.Contains("run_workflow", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void A_collect_no_for_each_reaches_is_a_loop_finding_naming_it()
    {
        var spec = Spec([Trigger("daily"), Gather("gather")], [Edge("daily", "gather")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        Assert.Equal(["gather"], Assert.Single(result.Findings, static f => f.Code == "GRAPH-LOOP-002").Nodes);
    }

    [Fact]
    public void A_run_agent_in_a_loop_body_is_not_a_loop_finding()
    {
        // why: an agent runs once per item (#2470), so a run_agent in the body is a valid loop
        var spec = Spec(
            [Trigger("daily"), Each("each"), Agent("work", "app"), Gather("gather")],
            [Edge("daily", "each"), Edge("each", "work"), Edge("work", "gather")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, static f => f.Code == "GRAPH-LOOP-003");
    }

    [Fact]
    public void A_run_agent_before_the_for_each_is_not_a_loop_finding()
    {
        var spec = Spec(
            [Trigger("daily"), Agent("work", "app"), Each("each"), Gather("gather")],
            [Edge("daily", "work"), Edge("work", "each"), Edge("each", "gather")]);

        var result = Validator(blocks: _blocks).Validate(spec, GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, static f => f.Code == "GRAPH-LOOP-003");
    }
}
