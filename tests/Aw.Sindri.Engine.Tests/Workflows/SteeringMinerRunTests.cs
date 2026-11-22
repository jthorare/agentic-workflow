using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The 'steering-miner' starter end to end on the time-skipping server (issue 3787): the shipped spec, the real prompt, code, loop and notify blocks, a model
/// that answers canned labels, and the code bodies run in process. Two batches of turns end in the two findings the labels add up to.
/// </summary>
[Lane(TestLane.Integration)]
public sealed partial class SteeringMinerRunTests
{
    private static readonly TimeSpan _long = TimeSpan.FromMinutes(3);   // why: the first Roslyn compile of the four bodies takes seconds on a cold runner

    private static readonly Dictionary<string, (string Label, string Mistake)> _labels = new(StringComparer.Ordinal)
    {
        ["transcript:1111aaaa#3"] = ("wrong_approach_process", "ran the tests locally instead of in CI"),
        ["transcript:1111aaaa#9"] = ("none", string.Empty),
        ["transcript:1111aaaa#12"] = ("restate", "ignored the instruction to keep the API"),
        ["transcript:2222bbbb#4"] = ("wrong_approach_process", "Ran the tests locally instead of in CI."),
        ["transcript:2222bbbb#8"] = ("restate", "dropped the api it was told to keep"),
        ["transcript:2222bbbb#15"] = ("scope_surface", "missed the settings screen"),
    };

    [Fact]
    public async Task Two_batches_of_turns_end_in_the_findings_their_labels_add_up_to()
    {
        var model = new CannedModel();
        await using var rig = await GraphRig.StartAsync([new PromptBlock(model), new CodeBlock(new InProcessCodePort())]);
        var workflow = await rig.Define(GraphTemplates.Find("steering-miner-payload")!.Spec);
        var trigger = new JsonObject
        {
            ["kind"] = "manual",
            ["payload"] = new JsonObject { ["turns"] = new JsonArray(Batch("transcript:1111aaaa#3", "transcript:1111aaaa#9", "transcript:1111aaaa#12"), Batch("transcript:2222bbbb#4", "transcript:2222bbbb#8", "transcript:2222bbbb#15")) },
        };

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger.ToJsonString());
        var run = await rig.Final(started.RunId, _long);
        var rank = JsonNode.Parse((await rig.ItemRuns(started.RunId, "rank"))[0].Output!)!["result"]!;

        Assert.True(run.Status == WorkflowRunStatus.Succeeded, run.Error);
        var findings = rank["findings"]!.AsArray().Select(static f => (f!["label"]!.GetValue<string>(), f["mistake"]!.GetValue<string>(), f["count"]!.GetValue<int>())).ToList();
        Assert.Equal([("wrong_approach_process", "ran the tests locally instead of in CI", 2), ("restate", "ignored the instruction to keep the API", 2)], findings);
        Assert.Equal(["transcript:2222bbbb#8", "transcript:1111aaaa#12"], rank["findings"]![1]!["refs"]!.AsArray().Select(static r => r!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(5, rank["corrections"]!.GetValue<int>());   // why: the model's row for a turn outside the batch was dropped, and 'none' is no correction
        Assert.Equal(["Steering miner: 2 findings"], rig.Ports.Notified);
        Assert.Equal(3, model.Prompts.Count);   // one label prompt per batch, then the merge prompt
        Assert.All(model.Prompts, static p => Assert.Contains("<untrusted source=\"workflow:node:", p, StringComparison.Ordinal));   // the turns and phrases reach the model fenced
    }

    [Fact]
    public async Task The_transcript_graph_reads_the_same_turns_from_the_transcripts_and_ends_in_the_same_findings()
    {
        var now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var transcripts = new FakeGraphTranscripts([Seed("transcript:1111aaaa", now, 3, 9, 12), Seed("transcript:2222bbbb", now, 4, 8, 15)]);
        var model = new CannedModel();
        await using var rig = await GraphRig.StartAsync([new PromptBlock(model), new CodeBlock(new InProcessCodePort()), .. GraphBlockRegistry.TranscriptBlocks(transcripts, new FakeTimeProvider(now))]);
        var workflow = await rig.Define(GraphTemplates.Find("steering-miner")!.Spec);

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);
        var run = await rig.Final(started.RunId, _long);
        var rank = JsonNode.Parse((await rig.ItemRuns(started.RunId, "rank"))[0].Output!)!["result"]!;

        Assert.True(run.Status == WorkflowRunStatus.Succeeded, run.Error);
        var findings = rank["findings"]!.AsArray().Select(static f => (f!["label"]!.GetValue<string>(), f["mistake"]!.GetValue<string>(), f["count"]!.GetValue<int>())).ToList();
        Assert.Equal([("wrong_approach_process", "ran the tests locally instead of in CI", 2), ("restate", "ignored the instruction to keep the API", 2)], findings);
        Assert.Equal(6, rank["turns"]!.GetValue<int>());
        Assert.Equal(1, transcripts.Reads);   // six turns are one batch, so one 'transcript_batch' read
        Assert.Equal(["Steering miner: 2 findings"], rig.Ports.Notified);
    }

    // why: one session's human turns on the given lines, each after an assistant turn on the line before, newest at the highest line
    private static TranscriptSeed Seed(string session, DateTimeOffset now, params int[] lines)
    {
        var turns = new List<TranscriptSeedTurn>();
        foreach (var line in lines)
        {
            turns.Add(new TranscriptSeedTurn(line - 1, Human: false, now.AddMinutes(line - 100), "the assistant's last words"));
            turns.Add(new TranscriptSeedTurn(line, Human: true, now.AddMinutes(line - 99), $"human text of {session}#{line.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));
        }

        return new TranscriptSeed(session, "claude-code", "/r/a", IsSubagent: false, turns);
    }

    [Fact]
    public async Task An_oversized_batch_fails_at_shape_naming_the_byte_cap_and_never_reaches_label_as_a_cut_output()
    {
        var model = new CannedModel();
        await using var rig = await GraphRig.StartAsync([new PromptBlock(model), new CodeBlock(new InProcessCodePort())]);
        var workflow = await rig.Define(GraphTemplates.Find("steering-miner-payload")!.Spec);
        JsonArray batch = [new JsonObject { ["ref"] = "transcript:1111aaaa#3", ["text"] = new string('a', 70_000) }];   // why: one pasted 70 KB log; its batch's JSON passes the engine's 64 KB step input cap
        var trigger = new JsonObject { ["kind"] = "manual", ["payload"] = new JsonObject { ["turns"] = new JsonArray(batch) } };

        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited, trigger.ToJsonString());
        var run = await rig.Final(started.RunId, _long);
        var shape = (await rig.ItemRuns(started.RunId, "shape"))[1];

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        Assert.Equal(NodeRunStatus.Failed, shape.Status);
        Assert.Contains("split", shape.Error, StringComparison.Ordinal);
        Assert.Contains("60000 bytes", shape.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("SND-GRAPH-108", run.Error, StringComparison.Ordinal);
        Assert.Empty(model.Prompts);   // no model call is spent on a batch that can't go on
    }

    private static JsonArray Batch(params string[] refs)
    {
        var batch = new JsonArray();
        foreach (var reference in refs)
        {
            batch.Add((JsonNode)new JsonObject { ["ref"] = reference, ["text"] = $"human text of {reference}", ["before"] = "the assistant's last words" });
        }

        return batch;
    }

    /// <summary>Answers the label prompt from <see cref="_labels"/> for every ref it shows (plus one row for a ref no batch has) and the merge prompt by joining the two restate phrases.</summary>
    private sealed partial class CannedModel : IGraphModelPort
    {
        private readonly ConcurrentQueue<string> _prompts = new();

        public IReadOnlyList<string> Prompts => [.. _prompts];

        public string Untrusted(string source, string text) => $"<untrusted source=\"{source}\">{text}</untrusted>";

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            _prompts.Enqueue(prompt);
            return ValueTask.FromResult<Result<string>>(prompt.StartsWith("Label each human turn", StringComparison.Ordinal) ? Labels(prompt) : Merges(prompt));
        }

        private static string Labels(string prompt)
        {
            var rows = new JsonArray();
            foreach (var reference in RefPattern.Matches(prompt).Select(static m => m.Value).Distinct(StringComparer.Ordinal))
            {
                var (label, mistake) = _labels[reference];
                rows.Add((JsonNode)new JsonObject { ["ref"] = reference, ["label"] = label, ["mistake"] = mistake });
            }

            rows.Add((JsonNode)new JsonObject { ["ref"] = "transcript:99999999#1", ["label"] = "restate", ["mistake"] = "a turn no batch has" });
            return new JsonObject { ["labels"] = rows }.ToJsonString();
        }

        private static string Merges(string prompt)
        {
            string Id(string mistake) => Regex.Match(prompt, "\"id\":\"(p[0-9]+)\",\"label\":\"restate\",\"mistake\":\"" + Regex.Escape(mistake) + "\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Groups[1].Value;
            var merge = new JsonObject { ["keep"] = Id("ignored the instruction to keep the API"), ["same"] = new JsonArray(Id("dropped the api it was told to keep")) };
            return new JsonObject { ["merges"] = new JsonArray(merge) }.ToJsonString();
        }

        [GeneratedRegex("transcript:[0-9a-f]{8}#[0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
        private static partial Regex RefPattern { get; }
    }
}
