using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Interpolation;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Code;

namespace Aw.Sindri.Tests.Graph.Ops;

/// <summary>
/// The 64 KB step caps of the 'steering-miner' starter (review of PR 3895): the engine marks a step whose input it cut as unreadable, so 'shape' and 'group' fail
/// on an input that would pass the cap, each naming its cap, and the steps after them stay under it. The bodies are the shipped text, run in process.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SteeringMinerCapsTests : IClassFixture<RoslynWarmup>
{
    /// <summary>Takes the warm-up so xUnit builds it before the first fact; the fixture's work is done in its constructor.</summary>
    /// <param name="warmup">The warm-up, already run.</param>
    public SteeringMinerCapsTests(RoslynWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GraphSpec Spec => GraphTemplates.Find("steering-miner")!.Spec;

    [Fact]
    public void Each_cap_leaves_room_for_the_step_s_own_code_under_the_engine_s_input_cap()
    {
        // why: the stored input is the step's whole config as JSON: the input, the code as an escaped string and the timeout
        Assert.True(StoredSize("shape", new string('x', 60_000)) <= NodeRunLimits.PayloadBytes, "shape's input at its 60,000-byte batch cap passes 64 KB");
        Assert.True(StoredSize("rank", new string('x', 45_000 + 6_000)) <= NodeRunLimits.PayloadBytes, "rank's input at group's 45,000-byte cap plus 6,000 bytes of merges passes 64 KB");
    }

    [Fact]
    public void Shape_fails_a_batch_whose_json_passes_60000_bytes_and_names_the_cap()
    {
        JsonArray batch = [new JsonObject { ["ref"] = "transcript:0000000a#1", ["text"] = new string('a', 61_000) }];

        var result = Run("shape", new JsonObject { ["item"] = batch });

        Assert.True(result.IsError);
        Assert.Contains("60000 bytes", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("split", result.Error.Message, StringComparison.Ordinal);
    }

    // why: '"' costs the most in label's stored input (8 bytes: '\"' in the rendered prompt, then '\\' and '\u0022'), '\' the most chars per turn; the worst
    // batch shape accepts is 20 turns each holding exactly the most of one of them that shape's twice-written budget keeps (2,000 bytes of text, 500 before),
    // and the same turns padded until the batch's own JSON sits just under shape's 60,000-byte cap
    [Theory]
    [InlineData('"', false)]
    [InlineData('"', true)]
    [InlineData('\\', false)]
    [InlineData('\\', true)]
    public void The_worst_batch_shape_accepts_keeps_label_s_input_under_the_cap(char heavy, bool padToCap)
    {
        var (textKept, beforeKept) = (MostThatFit(heavy, 2_000), MostThatFit(heavy, 500));
        var batch = Batch(heavy, textKept, beforeKept);
        if (padToCap)
        {
            var perChar = JsonValue.Create(new string(heavy, 1))!.ToJsonString().Length - 2;
            batch = Batch(heavy, textKept + ((59_900 - batch.ToJsonString().Length) / (20 * perChar)), beforeKept);
        }

        var shaped = Run("shape", new JsonObject { ["item"] = batch });

        Assert.True(shaped.IsSuccess, shaped.IsError ? shaped.Error.Message : null);
        Assert.InRange(batch.ToJsonString().Length, padToCap ? 55_000 : 0, 60_000);
        var turns = JsonNode.Parse(shaped.Value)!["turns"]!.AsArray();
        Assert.Equal(20, turns.Count);
        if (!padToCap)
        {
            Assert.All(turns, t => Assert.Equal(textKept, t!["text"]!.GetValue<string>().Length));   // the budget is reached, not cut below
        }

        var label = (JsonObject)Spec.Nodes.Single(static n => string.Equals(n.Id.Value, "label", StringComparison.Ordinal)).Config.DeepClone();
        label["prompt"] = Template.Render(label["prompt"]!.GetValue<string>(), reference => Lookup(new JsonObject { ["shape"] = new JsonObject { ["result"] = JsonNode.Parse(shaped.Value) } }, reference));
        var stored = Encoding.UTF8.GetByteCount(label.ToJsonString());
        Assert.True(stored < NodeRunLimits.PayloadBytes, "label's input is " + stored.ToString(CultureInfo.InvariantCulture) + " bytes");
    }

    [Fact]
    public void Group_fails_a_run_whose_corrections_pass_45000_bytes_before_the_merge_question_and_names_the_cap()
    {
        var corrections = new JsonArray();
        for (var line = 1; line <= 400; line++)
        {
            corrections.Add((JsonNode)new JsonObject { ["ref"] = "transcript:0000000a#" + line.ToString(CultureInfo.InvariantCulture), ["label"] = "restate", ["mistake"] = "a phrase of the mistake that is close to the sixty char cap" });
        }

        JsonArray gathered = [new JsonObject { ["turns"] = 400, ["labeled"] = 400, ["dropped"] = 0, ["corrections"] = corrections }];
        var result = Run("group", new JsonObject { ["gather"] = new JsonObject { ["items"] = gathered } });

        Assert.True(result.IsError);
        Assert.Contains("45000", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rank_ignores_a_merge_past_p200_and_a_merge_of_a_phrase_with_itself()
    {
        var corrections = new JsonArray();
        for (var n = 0; n <= 200; n++)
        {
            var number = n.ToString("000", CultureInfo.InvariantCulture);
            corrections.Add((JsonNode)new JsonObject { ["ref"] = "transcript:0000000a#" + (n + 1).ToString(CultureInfo.InvariantCulture), ["label"] = "restate", ["mistake"] = "mistake " + number });
        }

        JsonArray gathered = [new JsonObject { ["turns"] = 201, ["labeled"] = 201, ["dropped"] = 0, ["corrections"] = corrections }];
        JsonArray merges = [new JsonObject { ["keep"] = "p1", ["same"] = new JsonArray("p201", "p1") }];   // why: p201 is 'mistake 200', a phrase the merge question never showed

        var ranked = JsonNode.Parse(Run("rank", new JsonObject { ["gather"] = new JsonObject { ["items"] = gathered }, ["merge"] = new JsonObject { ["merges"] = merges } }).Value)!;

        Assert.Empty(ranked["findings"]!.AsArray());
    }

    private static JsonArray Batch(char heavy, int text, int before)
    {
        var batch = new JsonArray();
        for (var line = 1; line <= 20; line++)
        {
            batch.Add((JsonNode)new JsonObject { ["ref"] = "transcript:0000000a#" + line.ToString(CultureInfo.InvariantCulture), ["text"] = new string(heavy, text), ["before"] = new string(heavy, before) });
        }

        return batch;
    }

    // why: shape's own measure: a text's size as JSON written twice
    private static int MostThatFit(char heavy, int bytes)
    {
        var count = 0;
        while (JsonValue.Create(JsonValue.Create(new string(heavy, count + 1))!.ToJsonString())!.ToJsonString().Length <= bytes)
        {
            count++;
        }

        return count;
    }

    // why: the engine stores a step's interpolated config through StoredJson.Fit; this is that config's size with an input of 'filler' text
    private static int StoredSize(string nodeId, string filler)
    {
        var config = (JsonObject)Spec.Nodes.Single(n => string.Equals(n.Id.Value, nodeId, StringComparison.Ordinal)).Config.DeepClone();
        config["input"] = filler;
        return Encoding.UTF8.GetByteCount(config.ToJsonString());
    }

    private static Result<string> Run(string nodeId, JsonObject scope)
    {
        var config = Spec.Nodes.Single(n => string.Equals(n.Id.Value, nodeId, StringComparison.Ordinal)).Config;
        var input = Template.Render(config["input"]!.GetValue<string>(), reference => Lookup(scope, reference));
        return InProcessCodePort.Execute(config["code"]!.GetValue<string>(), input?.ToJsonString() ?? "null", Token);
    }

    private static JsonNode? Lookup(JsonObject scope, string reference)
    {
        JsonNode? node = scope;
        foreach (var part in reference.Split('.'))
        {
            node = node is JsonObject map ? map[part] : null;
        }

        return node;
    }
}
