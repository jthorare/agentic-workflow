using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping.Steps;

/// <summary>The scoping station's pure Steps on the block runtime (scoping spec §8.2, §9): the desk helper's code behind IBlockStep, so a scoping instance passes them without a desk task.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopingPureStepTests
{
    private static readonly string[] _seeds = ["acceptance", "answerLog", "answers", "challenge", "findings", "openQuestions", "questions", "scopeMap"];

    private static async Task<BlockStepResult> Run(IBlockStep step, string id, ScopingFacts facts, IReadOnlyDictionary<string, JsonElement>? inputs = null) =>
        await step.Run(ScopingStepContexts.Of(id, facts, inputs), TestContext.Current.CancellationToken);

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[Core.Workflow.Ids.FactKey.From("fact:" + key)].GetRawText())!;

    [Fact]
    public async Task Intake_sorts_the_input_and_carries_the_eight_seeds()
    {
        var result = await Run(new ScopeIntakeStep(), "step.scope.intake", new ScopingFacts { ["scopeInput"] = """{"kind":"tracker","repo":"fixture-notes","key":"NTS-135"}""" });

        Assert.Equal("tracker", result.Result!.Outcome.Value);
        Assert.Equal(_seeds, ScopingStepContexts.Keys(result), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Intake_with_an_unknown_kind_fails_the_Step_with_the_reason()
    {
        var result = await Run(new ScopeIntakeStep(), "step.scope.intake", new ScopingFacts { ["scopeInput"] = """{"kind":"essay"}""" });

        Assert.Null(result.Result);
        Assert.Contains("essay", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Brief_seed_writes_the_empty_answers_and_log()
    {
        var result = await Run(new BriefSeedStep(), "step.brief.seed", []);

        Assert.Equal("seeded", result.Result!.Outcome.Value);
        Assert.Empty(Out(result, "answers").AsArray());
        Assert.Empty(Out(result, "answerLog").AsArray());
    }

    [Fact]
    public async Task Check_on_the_gold_facts_passes_and_writes_no_findings()
    {
        var result = await Run(new ScopeCheckStep(new InMemoryRunArea()), "step.scope.check", ScopingFacts.Expiry());

        Assert.Equal("pass", result.Result!.Outcome.Value);
        Assert.Empty(ScopingStepContexts.Keys(result));
    }

    [Fact]
    public async Task Check_on_a_gap_reports_gaps_with_the_findings()
    {
        var result = await Run(new ScopeCheckStep(new InMemoryRunArea()), "step.scope.check", ScopingFacts.Expiry().Edit("acceptance", n => n["items"] = new JsonArray()));

        Assert.Equal("gaps", result.Result!.Outcome.Value);
        Assert.Contains(Out(result, "findings").AsArray(), f => f!["rule"]!.GetValue<string>() == "SCP-ACC-001");
    }

    [Fact]
    public async Task Check_on_a_degraded_source_reports_regather()
    {
        var result = await Run(new ScopeCheckStep(new InMemoryRunArea()), "step.scope.check", ScopingCheckTests.Limited("history", "degraded", "timeout", code: null, "not-searched", 0));

        Assert.Equal("regather", result.Result!.Outcome.Value);
        Assert.Equal(["findings"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Challenge_log_appends_the_round_and_a_found_round_becomes_findings()
    {
        var result = await Run(new ChallengeLogStep(), "step.scope.challenge-log", Round("found", "Export of a link"));

        Assert.Equal("found", result.Result!.Outcome.Value);
        Assert.Equal(1, Out(result, "challenge")["rounds"]![0]!["round"]!.GetValue<int>());
        _ = Assert.Single(Out(result, "findings").AsArray());
    }

    [Fact]
    public async Task Challenge_log_with_nothing_new_writes_the_record_and_no_findings()
    {
        var result = await Run(new ChallengeLogStep(), "step.scope.challenge-log", Round("nothingNew"));

        Assert.Equal("nothingNew", result.Result!.Outcome.Value);
        Assert.Equal(["challenge"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Challenge_log_fails_the_Step_when_the_verdict_disagrees_with_its_items()
    {
        var result = await Run(new ChallengeLogStep(), "step.scope.challenge-log", Round("nothingNew", "x"));

        Assert.Null(result.Result);
        Assert.Contains("disagrees", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Record_appends_the_latest_batch_to_the_log_once_by_its_digest()
    {
        var facts = new ScopingFacts
        {
            ["answers"] = """[{"round":1,"actor":"owner","at":"2026-10-16T09:10:00Z","questionIds":["Q1"],"answers":[{"question":"Q1","option":"Q1.a","text":"x"}]}]""",
            ["answerLog"] = "[]",
        };

        var once = await Run(new InterviewRecordStep(), "step.interview.record", facts);
        facts["answerLog"] = Out(once, "answerLog").ToJsonString();
        var twice = await Run(new InterviewRecordStep(), "step.interview.record", facts);

        Assert.Equal("recorded", once.Result!.Outcome.Value);
        Assert.Equal("A1", Out(once, "answerLog")[0]!["id"]!.GetValue<string>());
        _ = Assert.Single(Out(twice, "answerLog").AsArray());
    }

    [Fact]
    public async Task Gather_assembles_the_lenses_from_its_Step_inputs_results()
    {
        var result = await Run(new ScopeGatherStep(), "step.scope.gather", new ScopingFacts { ["anchors"] = ScopingFacts.Expiry()["anchors"] }, Views());

        Assert.Equal("gathered", result.Result!.Outcome.Value);
        Assert.Equal(["coverage", "lenses"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
        Assert.Equal(["code", "product", "related", "history"], Out(result, "coverage")["lenses"]!.AsArray().Select(static r => r!["lens"]!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Gather_without_the_lenses_view_fails_the_Step()
    {
        var result = await Run(new ScopeGatherStep(), "step.scope.gather", new ScopingFacts { ["anchors"] = ScopingFacts.Expiry()["anchors"] });

        Assert.Null(result.Result);
        Assert.Contains("input.context.lenses", result.Failure, StringComparison.Ordinal);   // as a block it names its input, not the desk helper's --lenses
        Assert.DoesNotContain("--lenses", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gather_without_the_lenses_view_names_why_the_lenses_input_gave_none()
    {
        var context = ScopingStepContexts.Of("step.scope.gather", new ScopingFacts { ["anchors"] = ScopingFacts.Expiry()["anchors"] }) with
        {
            Injection = new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["lenses"] = "note: anchors is not a list of text, so it was ignored\nThe context lenses are unavailable for this Step (this Step named no anchors); it runs without them.\n",
            }),
        };

        var result = await new ScopeGatherStep().Run(context, TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("this Step named no anchors", result.Failure, StringComparison.Ordinal);
        Assert.Contains("input.context.lenses", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_token_stops_a_Step()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new BriefSeedStep().Run(ScopingStepContexts.Of("step.brief.seed", []), cancelled.Token));
    }

    private static ScopingFacts Round(string verdict, params string[] surfaces) => new()
    {
        ["challenge"] = """{"rounds":[]}""",
        ["challengeRound"] = new JsonObject
        {
            ["walked"] = new JsonArray(new JsonObject { ["kind"] = "lens", ["name"] = "code" }),
            ["verdict"] = verdict,
            ["surfaces"] = new JsonArray([.. surfaces.Select(static t => (JsonNode?)new JsonObject { ["title"] = t, ["citations"] = new JsonArray("L-code-1") })]),
        }.ToJsonString(),
    };

    private static Dictionary<string, JsonElement> Views() => new(StringComparer.Ordinal)
    {
        ["input.context.lenses"] = JsonDocument.Parse(ScopingFacts.View("lensesView")).RootElement.Clone(),
        ["input.context.code"] = JsonDocument.Parse(ScopingFacts.View("codeView")).RootElement.Clone(),
        ["input.context.history"] = JsonDocument.Parse(ScopingFacts.View("historyView")).RootElement.Clone(),
    };
}
