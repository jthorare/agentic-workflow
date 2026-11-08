using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

[Lane(TestLane.Unit)]
public sealed class GraphValidatorTests
{
    [Fact]
    public void A_spec_with_two_triggers_is_refused_naming_both_nodes()
    {
        var spec = Spec([Schedule("daily"), Schedule("hourly"), Notify("tell", "hi")], [Edge("daily", "tell"), Edge("hourly", "tell")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-TRIGGER-001");
        Assert.Equal(["daily", "hourly"], finding.Nodes);
        Assert.Contains("'daily'", finding.ToString(), StringComparison.Ordinal);
        Assert.Contains("'hourly'", finding.ToString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(finding.Fix));
        Assert.False(result.Passed);
    }

    [Fact]
    public void A_reference_to_a_node_that_is_not_upstream_is_refused()
    {
        // daily -> a -> c, and daily -> b: b is a sibling branch of c, and c is downstream of a
        var spec = Spec(
            [Schedule("daily"), Search("a", "after {{c.facts}}"), Search("b"), Search("c", "before {{b.facts}}")],
            [Edge("daily", "a"), Edge("daily", "b"), Edge("a", "c")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var refused = result.Findings.Where(f => f.Code == "GRAPH-REF-001").ToList();
        Assert.Equal(2, refused.Count);
        var downstream = Assert.Single(refused, f => f.Nodes.SequenceEqual(["a"], StringComparer.Ordinal));
        Assert.Equal("config.query", downstream.Field);
        Assert.Contains("'c'", downstream.ToString(), StringComparison.Ordinal);
        var sibling = Assert.Single(refused, f => f.Nodes.SequenceEqual(["c"], StringComparer.Ordinal));
        Assert.Contains("'b'", sibling.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(result.Findings, f => f.Code == "GRAPH-REF-002");
    }

    [Fact]
    public void A_cycle_is_refused_with_the_path()
    {
        var spec = Spec(
            [Schedule("daily"), Search("a"), Search("b"), Search("c")],
            [Edge("daily", "a"), Edge("a", "b"), Edge("b", "c"), Edge("c", "a")]);

        var result = Validator().Validate(spec, GraphMode.Activate);

        var cycle = Assert.Single(result.Findings, f => f.Code == "GRAPH-CYCLE-001");
        Assert.Equal(["a", "b", "c"], cycle.Nodes);
        Assert.Contains("a -> b -> c -> a", cycle.ToString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(cycle.Fix));
    }

    [Fact]
    public void A_draft_may_have_wiring_gaps_but_activation_refuses_every_finding()
    {
        var unconnected = Spec([Schedule("daily"), Search("a"), Search("lonely")], [Edge("daily", "a")]);
        var noTrigger = Spec([Search("a"), Search("b")], [Edge("a", "b")]);

        foreach (var (spec, code) in new[] { (unconnected, "GRAPH-WIRING-001"), (noTrigger, "GRAPH-TRIGGER-002") })
        {
            var draft = Validator().Validate(spec, GraphMode.Draft);
            var activate = Validator().Validate(spec, GraphMode.Activate);

            var gap = Assert.Single(draft.Findings);
            Assert.Equal(code, gap.Code);
            Assert.True(gap.WiringGap);
            Assert.True(draft.Passed);
            Assert.False(activate.Passed);
            Assert.Equal(activate.Findings.Count, activate.Blocking.Count);
        }
    }

    [Fact]
    public void A_draft_still_refuses_what_is_not_a_wiring_gap()
    {
        var odd = new GraphNode(NodeId.From("odd"), "teleport", Title: null, Config: JsonNode.Parse("{}")!.AsObject());
        var spec = Spec([Schedule("daily"), odd], [Edge("daily", "odd")]);

        var draft = Validator().Validate(spec, GraphMode.Draft);

        var unknown = Assert.Single(draft.Findings, f => f.Code == "GRAPH-BLOCK-001");
        Assert.Equal(["odd"], unknown.Nodes);
        Assert.False(draft.Passed);
    }
}
