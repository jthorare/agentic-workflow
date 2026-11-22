using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Interpolation;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Infrastructure.Workflow.Code;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Graph.Blocks;
using Aw.Sindri.Tests.Workflow.Code;

namespace Aw.Sindri.Tests.Graph.Ops;

/// <summary>
/// The 'steering-miner' starter (issue 3787): its graph, and its four code bodies run on the inputs the engine renders from their config. Each body is the exact
/// text the template ships, compiled by the real compiler and run in process (<see cref="InProcessCodePort"/>).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SteeringMinerTests : IClassFixture<RoslynWarmup>
{
    /// <summary>Takes the warm-up so xUnit builds it before the first fact; the fixture's work is done in its constructor.</summary>
    /// <param name="warmup">The warm-up, already run.</param>
    public SteeringMinerTests(RoslynWarmup warmup)
    {
        ArgumentNullException.ThrowIfNull(warmup);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GraphSpec Spec => GraphTemplates.Find("steering-miner")!.Spec;

    [Fact]
    public void The_graph_is_a_manual_start_a_loop_over_batches_and_a_ranked_notify_and_it_validates_with_its_code_compiled()
    {
        var spec = Spec;

        Assert.Equal(["manual", "for_each", "code", "prompt", "code", "collect", "code", "prompt", "code", "notify"], spec.Nodes.Select(static n => n.Type), StringComparer.Ordinal);
        var each = spec.Nodes.Single(static n => n.Type == "for_each");
        Assert.Equal("{{trigger.payload.turns}}", each.Config["items"]!.GetValue<string>());
        Assert.Equal(20, each.Config["maxItems"]!.GetValue<int>());
        var validator = GraphFixtures.Validator(
            blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: [new CodeBlock(runner: null!)]).Types,
            cron: (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
            condition: text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []),
            codeCheck: body => [.. new CodeCompiler().Compile(body, Token).Diagnostics.Select(static d => new CodeProblem(d.Line, d.Column, d.Id, d.Message))]);

        var result = validator.Validate(spec, GraphMode.Activate);

        Assert.True(result.Passed, string.Join("; ", result.Findings));
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Shape_keeps_the_first_twenty_well_formed_turns_and_cuts_text_to_its_head_and_before_to_its_tail()
    {
        JsonArray batch =
        [
            new JsonObject { ["text"] = "no ref" },
            new JsonObject { ["ref"] = "transcript:0000000a#1 x", ["text"] = "a ref with a space" },
            new JsonObject { ["ref"] = "transcript:0000000a#2", ["text"] = "   " },
            "not a turn",
            new JsonObject { ["ref"] = Ref(10), ["text"] = new string('a', 1600), ["before"] = new string('x', 100) + new string('y', 400) },
        ];
        for (var line = 11; line < 32; line++)
        {
            batch.Add((JsonNode)new JsonObject { ["ref"] = Ref(line), ["text"] = "turn " + line.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        batch.Add((JsonNode)new JsonObject { ["ref"] = Ref(10), ["text"] = "the same ref again" });

        var shaped = Run("shape", new JsonObject { ["item"] = batch })!;

        Assert.Equal(Enumerable.Range(10, 20).Select(Ref), shaped["refs"]!.AsArray().Select(static r => r!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(27, shaped["given"]!.GetValue<int>());
        Assert.Equal(7, shaped["dropped"]!.GetValue<int>());
        var first = shaped["turns"]![0]!;
        Assert.Equal(new string('a', 1500), first["text"]!.GetValue<string>());
        Assert.Equal(new string('y', 400), first["before"]!.GetValue<string>());
        Assert.Null(shaped["turns"]![1]!["before"]);
    }

    [Fact]
    public void Shape_keeps_a_full_batch_of_long_non_ascii_turns_under_the_node_output_cap()
    {
        // why: 20 turns of 350 + 80 '€' are about 54 KB as JSON (6 bytes a character), a full batch just under shape's 60,000-byte input cap
        var batch = new JsonArray();
        for (var line = 1; line <= 20; line++)
        {
            batch.Add((JsonNode)new JsonObject { ["ref"] = "transcript:" + new string('b', 60) + "#" + line.ToString(System.Globalization.CultureInfo.InvariantCulture), ["text"] = new string('€', 350), ["before"] = new string('€', 80) });
        }

        var shaped = Run("shape", new JsonObject { ["item"] = batch })!;
        var stored = new JsonObject { ["result"] = shaped.DeepClone() }.ToJsonString();

        Assert.Equal(20, shaped["turns"]!.AsArray().Count);
        Assert.True(Encoding.UTF8.GetByteCount(stored) < NodeRunLimits.PayloadBytes, "a full batch is " + Encoding.UTF8.GetByteCount(stored).ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes");
        Assert.All(shaped["turns"]!.AsArray(), static t => Assert.NotEmpty(t!["text"]!.GetValue<string>()));
    }

    [Fact]
    public void Keep_drops_rows_it_cannot_trust_and_keeps_only_corrections_with_a_phrase()
    {
        JsonNode?[] refs = [Ref(1), Ref(2), Ref(3), Ref(4), Ref(5)];
        var labels = new JsonArray(
            Row(Ref(1), "wrong_approach_design", "Skipped the tests"),
            Row(Ref(2), "none", string.Empty),
            Row(Ref(3), "RESTATE", "  repeated\nthe   ask "),
            Row(Ref(1), "restate", "a second row for the same turn"),
            Row("transcript:ffffffff#9", "restate", "a turn that is not in the batch"),
            Row(Ref(4), "made_up", "a label outside the set"),
            "not a row",
            Row(Ref(5), "scope_surface", "   "));

        var kept = Run("keep", Scope(("shape", new JsonObject { ["result"] = new JsonObject { ["refs"] = new JsonArray(refs) } }), ("label", new JsonObject { ["labels"] = labels })))!;

        Assert.Equal([(Ref(1), "wrong_approach_design", "Skipped the tests"), (Ref(3), "restate", "repeated the ask")], Corrections(kept));
        Assert.Equal(5, kept["turns"]!.GetValue<int>());
        Assert.Equal(4, kept["labeled"]!.GetValue<int>());   // why: 1, 2, 3 and 5 got a label from the set; 5 has no phrase, so it counts as dropped too
        Assert.Equal(5, kept["dropped"]!.GetValue<int>());
    }

    [Fact]
    public void Keep_cuts_a_long_mistake_phrase()
    {
        var kept = Run("keep", Scope(
            ("shape", new JsonObject { ["result"] = new JsonObject { ["refs"] = new JsonArray(Ref(1)) } }),
            ("label", new JsonObject { ["labels"] = new JsonArray(Row(Ref(1), "restate", string.Join(' ', Enumerable.Repeat("word", 50)))) })))!;

        Assert.True(Corrections(kept).Single().Mistake.Length <= 60);
    }

    [Fact]
    public void Two_corrections_of_the_same_mistake_make_one_finding_and_one_alone_makes_none()
    {
        var ranked = Rank([Batch(4, ("transcript:aaaaaaaa#1", "restate", "Repeated the ask!"), ("transcript:aaaaaaaa#2", "wrong_approach_design", "skipped tests")), Batch(3, ("transcript:bbbbbbbb#7", "restate", "repeated the ask"))]);

        var finding = Assert.Single(ranked["findings"]!.AsArray())!;
        Assert.Equal("restate", finding["label"]!.GetValue<string>());
        Assert.Equal("repeated the ask", finding["mistake"]!.GetValue<string>());   // why: the two phrases tie on turns, so the shorter one names the finding
        Assert.Equal(2, finding["count"]!.GetValue<int>());
        Assert.Equal(2, finding["sessions"]!.GetValue<int>());
        Assert.Equal(["transcript:aaaaaaaa#1", "transcript:bbbbbbbb#7"], finding["refs"]!.AsArray().Select(static r => r!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("2x restate: repeated the ask\nFrom 7 labeled turns of 7: 3 corrections.", ranked["summary"]!.GetValue<string>());
    }

    [Fact]
    public void A_turn_seen_twice_counts_once_and_rows_without_a_correction_label_or_phrase_are_dropped()
    {
        var malformed = Batch(3, ("transcript:aaaaaaaa#1", "restate", "skipped the tests"));
        var corrections = malformed["corrections"]!.AsArray();
        corrections.Add((JsonNode)"not a row");
        corrections.Add((JsonNode)new JsonObject { ["ref"] = 5, ["label"] = "restate", ["mistake"] = "skipped the tests" });
        corrections.Add((JsonNode)Row("transcript:aaaaaaaa#3", "none", "skipped the tests"));
        corrections.Add((JsonNode)Row("transcript:aaaaaaaa#4", "restate", "?!"));

        var once = Rank([malformed, Batch(1, ("transcript:aaaaaaaa#1", "restate", "skipped the tests"))]);
        var twice = Rank([malformed, Batch(1, ("transcript:aaaaaaaa#2", "restate", "skipped the tests"))]);

        Assert.Empty(once["findings"]!.AsArray());
        Assert.Equal(1, once["corrections"]!.GetValue<int>());
        Assert.StartsWith("No mistake was corrected twice.", once["summary"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(2, Assert.Single(twice["findings"]!.AsArray())!["count"]!.GetValue<int>());
    }

    [Fact]
    public void Findings_rank_by_turns_then_by_sessions()
    {
        var ranked = Rank([Batch(20,
            ("transcript:00000001#1", "scope_surface", "missed the other screens"), ("transcript:00000001#2", "scope_surface", "missed the other screens"),
            ("transcript:00000002#1", "restate", "ignored the earlier answer"), ("transcript:00000003#1", "restate", "ignored the earlier answer"),
            ("transcript:00000004#1", "wrong_approach_process", "ran tests locally not on ci"), ("transcript:00000005#1", "wrong_approach_process", "ran tests locally not on ci"),
            ("transcript:00000006#1", "wrong_approach_process", "ran tests locally not on ci"))]);

        Assert.Equal(["ran tests locally not on ci", "ignored the earlier answer", "missed the other screens"], ranked["findings"]!.AsArray().Select(static f => f!["mistake"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(3, ranked["count"]!.GetValue<int>());
    }

    [Fact]
    public void A_merge_joins_phrases_of_one_label_and_ignores_another_label_and_unknown_ids()
    {
        JsonObject[] batches = [Batch(5,
            ("transcript:aaaaaaaa#1", "restate", "repeated the ask"), ("transcript:bbbbbbbb#1", "restate", "asked again for the same thing"),
            ("transcript:cccccccc#1", "wrong_approach_design", "repeated the question"))];
        var phrases = Run("group", Scope(("gather", new JsonObject { ["items"] = new JsonArray([.. batches.Select(static b => (JsonNode?)b.DeepClone())]) })))!["phrases"]!.AsArray();
        string Id(string mistake) => phrases.Single(p => string.Equals(p!["mistake"]!.GetValue<string>(), mistake, StringComparison.Ordinal))!["id"]!.GetValue<string>();
        var merges = new JsonArray(new JsonObject { ["keep"] = Id("repeated the ask"), ["same"] = new JsonArray(Id("asked again for the same thing"), Id("repeated the question"), "p999") });

        var ranked = Rank(batches, merges);

        var finding = Assert.Single(ranked["findings"]!.AsArray())!;
        Assert.Equal(("restate", "repeated the ask", 2), (finding["label"]!.GetValue<string>(), finding["mistake"]!.GetValue<string>(), finding["count"]!.GetValue<int>()));   // why: the merge's 'keep' names the phrase when the turns tie
        Assert.Equal(["transcript:aaaaaaaa#1", "transcript:bbbbbbbb#1"], finding["refs"]!.AsArray().Select(static r => r!.GetValue<string>()).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void Group_lists_each_label_and_phrase_once_with_most_turns_first()
    {
        var batch = Batch(4,
            ("transcript:aaaaaaaa#1", "restate", "skipped the tests"), ("transcript:aaaaaaaa#2", "rigor", "not a correction label"),
            ("transcript:aaaaaaaa#3", "restate", "Repeated the ask"), ("transcript:aaaaaaaa#4", "restate", "repeated, the ask"));
        var grouped = Run("group", Scope(("gather", new JsonObject { ["items"] = new JsonArray(batch) })))!;

        var phrases = grouped["phrases"]!.AsArray().Select(static p => (p!["id"]!.GetValue<string>(), p["label"]!.GetValue<string>(), p["count"]!.GetValue<int>())).ToList();
        Assert.Equal([("p1", "restate", 2), ("p2", "restate", 1)], phrases);
        Assert.Equal(3, grouped["corrections"]!.GetValue<int>());
    }

    private static string Ref(int line) => "transcript:0000000a#" + line.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static JsonObject Row(string reference, string label, string mistake) => new() { ["ref"] = reference, ["label"] = label, ["mistake"] = mistake };

    /// <summary>One 'keep' result, as the 'gather' collect holds it per batch.</summary>
    private static JsonObject Batch(int turns, params (string Ref, string Label, string Mistake)[] rows) => new()
    {
        ["turns"] = turns,
        ["labeled"] = turns,
        ["dropped"] = 0,
        ["corrections"] = new JsonArray([.. rows.Select(static r => (JsonNode?)Row(r.Ref, r.Label, r.Mistake))]),
    };

    private static JsonObject Rank(IReadOnlyList<JsonObject> batches, JsonArray? merges = null) =>
        Run("rank", Scope(("gather", new JsonObject { ["items"] = new JsonArray([.. batches.Select(static b => (JsonNode?)b.DeepClone())]) }), ("merge", new JsonObject { ["merges"] = merges ?? [] })))!.AsObject();

    private static List<(string Ref, string Label, string Mistake)> Corrections(JsonNode kept) =>
        [.. kept["corrections"]!.AsArray().Select(static c => (c!["ref"]!.GetValue<string>(), c["label"]!.GetValue<string>(), c["mistake"]!.GetValue<string>()))];

    private static JsonObject Scope(params (string Node, JsonObject Outputs)[] nodes)
    {
        var scope = new JsonObject();
        foreach (var (node, outputs) in nodes)
        {
            scope[node] = outputs;
        }

        return scope;
    }

    // why: the engine interpolates the node's 'input' with the run's values, then hands the code its JSON text; the same Template.Render runs here
    private static JsonNode? Run(string nodeId, JsonObject scope)
    {
        var config = Spec.Nodes.Single(n => string.Equals(n.Id.Value, nodeId, StringComparison.Ordinal)).Config;
        var input = Template.Render(config["input"]!.GetValue<string>(), reference => Lookup(scope, reference));
        var result = InProcessCodePort.Execute(config["code"]!.GetValue<string>(), input?.ToJsonString() ?? "null", Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return JsonNode.Parse(result.Value);
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
