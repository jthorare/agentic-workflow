using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Ops;
using Aw.Sindri.Core.Graph.Validation;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

/// <summary>The spec's top-level 'result' names the node whose output is the run's result (#3807).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphResultFieldTests
{
    // daily -> rank -> tell
    private static GraphSpec Base(string? result) => Spec([Trigger("daily"), Search("rank"), Notify("tell", "{{rank.facts}}")], [Edge("daily", "rank"), Edge("rank", "tell")])
        with { Result = result is null ? null : NodeId.From(result) };

    [Fact]
    public void A_result_that_names_no_node_is_refused()
    {
        var result = Validator().Validate(Base("ranked"), GraphMode.Activate);

        var finding = Assert.Single(result.Findings, f => f.Code == "GRAPH-RESULT-001");
        Assert.Equal("result", finding.Field);
        Assert.Contains("'ranked'", finding.ToString(), StringComparison.Ordinal);
        Assert.False(result.Passed);
    }

    [Fact]
    public void A_result_that_names_a_node_passes()
    {
        var result = Validator().Validate(Base("rank"), GraphMode.Activate);

        Assert.DoesNotContain(result.Findings, f => f.Code == "GRAPH-RESULT-001");
        Assert.True(result.Passed);
    }

    [Fact]
    public void The_result_round_trips_and_a_spec_without_one_writes_no_result_key()
    {
        var parsed = GraphJson.Parse(GraphJson.Write(Base("rank"))).Value;

        Assert.Equal(NodeId.From("rank"), parsed.Result);
        Assert.DoesNotContain("\"result\"", GraphJson.Write(Base(result: null)), StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_follows_the_node_into_the_result()
    {
        var result = GraphOps.Apply(Base("rank"), baseRevision: 1, currentRevision: 1, [new RenameOp(NodeId.From("rank"), NodeId.From("ranked"))], Validator());

        Assert.Equal(NodeId.From("ranked"), result.Value.Result);
    }
}
