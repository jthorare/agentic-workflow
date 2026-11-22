using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>'transcript_turns' and 'transcript_batch' (issue 3787) over the fake transcripts.</summary>
[Lane(TestLane.Unit)]
public sealed class TranscriptBlocksTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Turns_come_out_newest_first_in_batches_of_refs_and_the_oldest_are_cut_at_maxTurns()
    {
        var port = new FakeGraphTranscripts([Session("transcript:0000000a", Humans(1, 5)), Session("transcript:0000000b", Humans(10, 3))]);

        var result = await Turns(port, new JsonObject { ["batchSize"] = 3, ["maxTurns"] = 7 });

        var batches = result["batches"]!.AsArray().Select(static b => b!.AsArray().Select(static r => r!.GetValue<string>()).ToList()).ToList();
        Assert.Equal([3, 3, 1], batches.Select(static b => b.Count));
        Assert.Equal("transcript:0000000a#1", batches[0][0]);   // why: line 1 of session a is the newest turn of the seed
        Assert.Equal((7, 2, true), (result["turns"]!.GetValue<int>(), result["sessions"]!.GetValue<int>(), result["truncated"]!.GetValue<bool>()));
        Assert.Equal(0, port.Reads);   // the list reads no text
    }

    [Fact]
    public async Task Since_counts_back_from_the_clock()
    {
        var port = new FakeGraphTranscripts([Session("transcript:0000000a", [new TranscriptSeedTurn(1, Human: true, _now.AddHours(-1), "new"), new TranscriptSeedTurn(2, Human: true, _now.AddHours(-30), "old")])]);

        var result = await Turns(port, new JsonObject { ["since"] = "1d" });

        Assert.Equal(["transcript:0000000a#1"], result["batches"]![0]!.AsArray().Select(static r => r!.GetValue<string>()), StringComparer.Ordinal);
        Assert.False(result["truncated"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("since", "soon")]
    [InlineData("batchSize", 21)]
    [InlineData("maxTurns", 401)]
    [InlineData("providers", "copilot")]
    public void A_config_out_of_range_is_a_finding_on_its_field(string field, object value)
    {
        var config = new JsonObject { [field] = value switch { int n => n, string s when string.Equals(field, "providers", StringComparison.Ordinal) => new JsonArray(s), string s => s, _ => null } };

        var problems = new TranscriptTurnsBlock(new FakeGraphTranscripts([]), new FakeTimeProvider(_now)).CheckConfig(config);

        Assert.Equal(field, Assert.Single(problems).Field);
    }

    [Fact]
    public async Task A_batch_reads_its_turns_with_the_tail_of_the_assistant_text_before_them()
    {
        var port = new FakeGraphTranscripts([Session("transcript:0000000a", [
            new TranscriptSeedTurn(1, Human: true, _now, new string('a', 1700)),
            new TranscriptSeedTurn(2, Human: false, _now, new string('x', 100) + new string('y', 400)),
            new TranscriptSeedTurn(3, Human: true, _now, "no, the other way")])]);

        var result = await Batch(port, new JsonArray("transcript:0000000a#3", "transcript:0000000a#1", "transcript:ffffffff#4", "not a ref"));

        var turns = result["turns"]!.AsArray();
        Assert.Equal(["transcript:0000000a#3", "transcript:0000000a#1"], turns.Select(static t => t!["ref"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(new string('y', 400), turns[0]!["before"]!.GetValue<string>());
        Assert.Equal(1500, turns[1]!["text"]!.GetValue<string>().Length);
        Assert.Null(turns[1]!["before"]);
        Assert.Equal((1, 1), (result["missing"]!.GetValue<int>(), result["dropped"]!.GetValue<int>()));
    }

    [Fact]
    public async Task A_full_batch_of_long_non_ascii_turns_fits_the_node_output_cap()
    {
        var seed = new List<TranscriptSeedTurn>();
        var refs = new JsonArray();
        for (var line = 1; line <= 40; line += 2)
        {
            seed.Add(new TranscriptSeedTurn(line, Human: false, _now, new string('€', 1000)));
            seed.Add(new TranscriptSeedTurn(line + 1, Human: true, _now, new string('€', 3000)));
            refs.Add((JsonNode)("transcript:0000000a#" + (line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var result = await Batch(new FakeGraphTranscripts([Session("transcript:0000000a", seed)]), refs);
        var stored = new JsonObject { ["turns"] = result["turns"]!.DeepClone(), ["missing"] = 0, ["dropped"] = 0 }.ToJsonString();

        Assert.Equal(20, result["turns"]!.AsArray().Count);
        Assert.True(Encoding.UTF8.GetByteCount(stored) < NodeRunLimits.PayloadBytes, "a full batch is " + Encoding.UTF8.GetByteCount(stored).ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes");
    }

    [Fact]
    public async Task Refs_that_are_not_a_list_fail_the_node()
    {
        var result = await new TranscriptBatchBlock(new FakeGraphTranscripts([])).Execute(BlockFakes.Context(new JsonObject { ["refs"] = "transcript:0000000a#1" }), Token);

        Assert.True(result.IsError);
    }

    private static TranscriptSeed Session(string session, IReadOnlyList<TranscriptSeedTurn> turns) => new(session, "claude-code", "/r/a", IsSubagent: false, turns);

    private static List<TranscriptSeedTurn> Humans(int firstLine, int count) =>
        [.. Enumerable.Range(0, count).Select(i => new TranscriptSeedTurn(firstLine + i, Human: true, _now.AddMinutes(-(firstLine + i)), "turn"))];

    private static async Task<JsonObject> Turns(FakeGraphTranscripts port, JsonObject config)
    {
        var result = await new TranscriptTurnsBlock(port, new FakeTimeProvider(_now)).Execute(BlockFakes.Context(config), Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return Outputs(result.Value);
    }

    private static async Task<JsonObject> Batch(FakeGraphTranscripts port, JsonArray refs)
    {
        var result = await new TranscriptBatchBlock(port).Execute(BlockFakes.Context(new JsonObject { ["refs"] = refs }), Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return Outputs(result.Value);
    }

    private static JsonObject Outputs(BlockResult result)
    {
        var outputs = new JsonObject();
        foreach (var (name, value) in result.Outputs)
        {
            outputs[name] = value?.DeepClone();
        }

        return outputs;
    }
}
