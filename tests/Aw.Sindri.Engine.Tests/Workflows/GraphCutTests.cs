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
    public void An_output_over_two_megabytes_is_stored_as_an_envelope_under_the_cap_and_reported_as_cut()
    {
        var huge = new string('x', 3 * 1024 * 1024);

        var fitted = StoredJson.Fit(huge);

        Assert.True(Encoding.UTF8.GetByteCount(fitted.Text!) <= NodeRunLimits.PayloadBytes);
        Assert.True(fitted.Cut);   // why: the caller sets Truncated from it, so a later node that reads it fails with SND-GRAPH-108
        Assert.True(JsonNode.Parse(fitted.Text!)!["truncated"]!.GetValue<bool>());
    }

    private static string Build(string kind)
    {
        var cap = NodeRunLimits.PayloadBytes;
        var beforeCut = new string('x', cap - 128 - 1);   // why: Fit keeps cap - 128 characters, so the next character sits on the cut
        return kind switch
        {
            "two-byte" => new string('\u00e9', cap),
            "three-byte" => new string('\u4e2d', cap),
            "surrogate-at-cut" => beforeCut + "\U0001F600" + new string('x', cap),
            "surrogates-throughout" => string.Concat(Enumerable.Repeat("\U0001F600", cap)),
            "quotes-at-cut" => beforeCut + new string('"', 200) + new string('x', cap),
            "backslashes-at-cut" => beforeCut + new string('\\', 200) + new string('x', cap),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
        };
    }

    [Theory]
    [InlineData("two-byte")]
    [InlineData("three-byte")]
    [InlineData("surrogate-at-cut")]
    [InlineData("surrogates-throughout")]
    [InlineData("quotes-at-cut")]
    [InlineData("backslashes-at-cut")]
    public void A_text_over_the_cap_is_cut_to_json_that_parses_and_fits(string kind)
    {
        var fitted = StoredJson.Fit(Build(kind));

        Assert.True(fitted.Cut);
        Assert.True(Encoding.UTF8.GetByteCount(fitted.Text!) <= NodeRunLimits.PayloadBytes);
        Assert.NotNull(JsonNode.Parse(fitted.Text!));
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
