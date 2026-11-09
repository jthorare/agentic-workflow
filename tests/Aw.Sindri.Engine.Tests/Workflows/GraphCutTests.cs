using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What the graph activities cut before a value enters workflow history or the store (PR 9a review: S2, S3).</summary>
[Lane(TestLane.Unit)]
public sealed class GraphCutTests
{
    private static NodeRun Row(int item, string value, bool truncated = false) =>
        new(WorkflowRunId.From("cut@1:1"), NodeId.From("gather"), item, NodeRunStatus.Succeeded, Input: null, new JsonObject { ["value"] = value }.ToJsonString(), truncated, Error: null, Usage: null);

    [Fact]
    public void Text_that_fits_the_store_cap_passes_through_unchanged()
    {
        var text = new string('a', 1000);

        Assert.Equal(text, GraphBlockActivities.Fit(text));
        Assert.Null(GraphBlockActivities.Fit(text: null));
    }

    [Fact]
    public void An_output_over_two_megabytes_is_cut_to_the_store_cap_and_still_reads_as_truncated_to_the_store()
    {
        var huge = new string('x', 3 * 1024 * 1024);

        var fitted = GraphBlockActivities.Fit(huge)!;
        _ = NodeRunLimits.Cap(fitted, out var truncated);

        Assert.True(Encoding.UTF8.GetByteCount(fitted) < NodeRunLimits.PayloadBytes + 16);
        Assert.True(truncated);   // why: the store cuts and flags it, so a later node that reads it fails with SND-GRAPH-108
    }

    [Fact]
    public void Two_hundred_items_of_400_bytes_collect_into_a_failure_that_names_the_size_and_the_cap()
    {
        IReadOnlyList<NodeRun> rows = [.. Enumerable.Range(1, 200).Select(i => Row(i, new string('v', 400)))];

        var (output, failure) = GraphRunActivities.Gather(rows);

        Assert.Null(output);
        Assert.Contains("200 items", failure, StringComparison.Ordinal);
        Assert.Contains("65536", failure, StringComparison.Ordinal);
        Assert.Contains("bytes", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Values_inside_the_cap_collect_in_item_order()
    {
        IReadOnlyList<NodeRun> rows = [Row(1, "a"), Row(2, "b")];

        var (output, failure) = GraphRunActivities.Gather(rows);

        Assert.Null(failure);
        Assert.Equal("""{"items":["a","b"],"count":2}""", output);
    }

    [Fact]
    public void A_value_the_store_cut_still_names_its_item()
    {
        IReadOnlyList<NodeRun> rows = [Row(1, "a"), Row(2, "b", truncated: true)];

        var (_, failure) = GraphRunActivities.Gather(rows);

        Assert.Contains("item 2", failure, StringComparison.Ordinal);
    }
}
