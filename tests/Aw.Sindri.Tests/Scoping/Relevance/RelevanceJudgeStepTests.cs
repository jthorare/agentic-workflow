using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;
using static Aw.Sindri.Tests.Scoping.Relevance.RelevanceFixtures;

namespace Aw.Sindri.Tests.Scoping.Relevance;

/// <summary>The relevance judge (spec 4.2): one call per entry over a projection that never holds the agent's labels, escalations and failures as 'uncertain'.</summary>
[Lane(TestLane.Unit)]
public sealed class RelevanceJudgeStepTests
{
    private static ScopingFacts Facts()
    {
        var (verdicts, _) = AllIn((Verdict("L-code-3", "noise", citation: CitationMarker, fix: "relabel", relabelTo: RelabelMarker), Ruling("L-code-3", "noise")));
        return new ScopingFacts
        {
            ["brief"] = Brief().ToJsonString(),
            ["lenses"] = Lenses().ToJsonString(),
            ["relevanceVerdicts"] = verdicts.ToJsonString(),
        };
    }

    private static StepContext Context(ScopingFacts facts, Dictionary<string, JsonElement>? arguments = null) =>
        ScopingStepContexts.Of("step.scope.relevance-judge", facts) with { Arguments = arguments ?? [] };

    private static Dictionary<string, JsonElement> Args(int? parallel = null, double? threshold = null, string[]? providers = null)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (parallel is { } p)
        {
            arguments["parallel"] = JsonSerializer.SerializeToElement(p);
        }

        if (threshold is { } t)
        {
            arguments["threshold"] = JsonSerializer.SerializeToElement(t);
        }

        if (providers is not null)
        {
            arguments["providers"] = JsonSerializer.SerializeToElement(providers);
        }

        return arguments;
    }

    private static RelevanceJudgeStep Step(ScriptedJudge judge) => new(judge, new PassThroughScrubber());

    private static JsonNode Rulings(BlockStepResult result) =>
        JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:relevanceRulings")].GetRawText())!;

    private static JsonNode RulingOf(BlockStepResult result, string id) =>
        Rulings(result)["rulings"]!.AsArray().Single(r => r!["id"]!.GetValue<string>() == id)!;

    [Fact]
    public async Task It_asks_the_judge_once_per_entry_in_the_lenses()
    {
        var judge = new ScriptedJudge(static _ => Decided("in"));

        var result = await Step(judge).Run(Context(Facts()), TestContext.Current.CancellationToken);

        Assert.Equal("ruled", result.Result!.Outcome.Value);
        Assert.Equal(AllIds.Length, judge.Requests.Count);
        Assert.Equal(AllIds.Length, Rulings(result)["rulings"]!.AsArray().Count);
    }

    [Fact]
    public async Task No_request_holds_a_verdict_reason_citation_fix_or_model()
    {
        var judge = new ScriptedJudge(static _ => Decided("noise"));

        _ = await Step(judge).Run(Context(Facts()), TestContext.Current.CancellationToken);

        Assert.NotEmpty(judge.Requests);
        foreach (var request in judge.Requests)
        {
            var text = AllText(request);
            Assert.DoesNotContain(ReasonMarker, text, StringComparison.Ordinal);
            Assert.DoesNotContain(CitationMarker, text, StringComparison.Ordinal);
            Assert.DoesNotContain(RelabelMarker, text, StringComparison.Ordinal);
            Assert.DoesNotContain(ModelMarker, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_request_holds_the_brief_text_and_the_entry_but_not_its_neighbours()
    {
        var judge = new ScriptedJudge(static _ => Decided("in"));

        _ = await Step(judge).Run(Context(Facts()), TestContext.Current.CancellationToken);

        var request = judge.Requests.Single(r => AllText(r).Contains("L-code-1", StringComparison.Ordinal));
        var text = AllText(request);
        Assert.Contains(BriefText, text, StringComparison.Ordinal);
        Assert.Contains("code:src/admin/Shell.ts", text, StringComparison.Ordinal);
        Assert.Contains("Shell.ts", text, StringComparison.Ordinal);
        Assert.Contains("surface:screen/invite-dialog", text, StringComparison.Ordinal);
        Assert.DoesNotContain("code:src/invites/expiry.ts", text, StringComparison.Ordinal);   // an edge's other end is a neighbour
        Assert.DoesNotContain("billing", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trust", text, StringComparison.Ordinal);                          // the brief's refs are left out
        Assert.Equal(["in", "out", "noise"], request.Choices.Select(static c => c.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_providers_and_the_threshold_come_from_the_arguments_or_the_defaults()
    {
        var given = new ScriptedJudge(static _ => Decided("in"));
        _ = await Step(given).Run(Context(Facts(), Args(threshold: 0.8, providers: ["jev"])), TestContext.Current.CancellationToken);
        var first = given.Requests[0];
        Assert.Equal(0.8, first.Threshold);
        Assert.Equal(["jev"], first.Providers, StringComparer.Ordinal);

        var defaults = new ScriptedJudge(static _ => Decided("in"));
        _ = await Step(defaults).Run(Context(Facts()), TestContext.Current.CancellationToken);
        Assert.Equal(0.6, defaults.Requests[0].Threshold);
        Assert.Equal(JudgeProviders.Default, defaults.Requests[0].Providers);
    }

    [Fact]
    public async Task Parallel_bounds_the_calls_in_flight()
    {
        var judge = new ScriptedJudge(static _ => Decided("in"), delay: TimeSpan.FromMilliseconds(30));

        var result = await Step(judge).Run(Context(Facts(), Args(parallel: 2)), TestContext.Current.CancellationToken);

        Assert.Equal("ruled", result.Result!.Outcome.Value);
        Assert.InRange(judge.MostInFlight, 2, 2);
    }

    [Fact]
    public async Task A_decided_answer_becomes_a_ruling_with_its_confidence_provider_and_decision()
    {
        var judge = new ScriptedJudge(static _ => new ChooseAnswer(ChooseStatus.Decided, "noise", "model", 0.91, "jev", "JD-0001"));

        var result = await Step(judge).Run(Context(Facts(), Args(providers: ["jev"])), TestContext.Current.CancellationToken);

        var ruling = RulingOf(result, "L-code-3");
        Assert.Equal("noise", ruling["class"]!.GetValue<string>());
        Assert.Equal(0.91, ruling["confidence"]!.GetValue<double>());
        Assert.Equal("jev", ruling["provider"]!.GetValue<string>());
        Assert.Equal("JD-0001", ruling["decisionId"]!.GetValue<string>());
        Assert.Equal("model", ruling["reasonCode"]!.GetValue<string>());
        Assert.Equal(["jev"], Rulings(result)["judge"]!["providers"]!.AsArray().Select(static p => p!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_escalated_answer_is_uncertain()
    {
        var judge = new ScriptedJudge(static _ => new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "below-threshold", DecisionRef: "JD-0003"));

        var result = await Step(judge).Run(Context(Facts()), TestContext.Current.CancellationToken);

        var ruling = RulingOf(result, "L-code-1");
        Assert.Equal("uncertain", ruling["class"]!.GetValue<string>());
        Assert.Null(ruling["confidence"]);
        Assert.Null(ruling["provider"]);
        Assert.Equal("JD-0003", ruling["decisionId"]!.GetValue<string>());
        Assert.Equal("below-threshold", ruling["reasonCode"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_choice_that_is_not_one_of_the_options_is_uncertain()
    {
        var judge = new ScriptedJudge(static _ => new ChooseAnswer(ChooseStatus.Decided, "maybe", "model", 0.9, "jev", "JD-9"));

        var result = await Step(judge).Run(Context(Facts()), TestContext.Current.CancellationToken);

        Assert.Equal("uncertain", RulingOf(result, "L-code-1")["class"]!.GetValue<string>());
        Assert.Equal("invalid-choice", RulingOf(result, "L-code-1")["reasonCode"]!.GetValue<string>());
    }

    [Fact]
    public async Task One_failed_call_is_uncertain_with_the_failure_word_and_the_others_stand()
    {
        var judge = new ScriptedJudge(request => AllText(request).Contains("L-code-2", StringComparison.Ordinal)
            ? new ChooseAnswer(ChooseStatus.Failed, Choice: null, "timeout")
            : Decided("in"));

        var result = await Step(judge).Run(Context(Facts()), TestContext.Current.CancellationToken);

        Assert.Equal("ruled", result.Result!.Outcome.Value);
        Assert.Equal("uncertain", RulingOf(result, "L-code-2")["class"]!.GetValue<string>());
        Assert.Equal("timeout", RulingOf(result, "L-code-2")["reasonCode"]!.GetValue<string>());
        Assert.Equal("in", RulingOf(result, "L-code-1")["class"]!.GetValue<string>());
    }

    [Fact]
    public async Task Every_call_failing_fails_the_Step()
    {
        var judge = new ScriptedJudge(static _ => new ChooseAnswer(ChooseStatus.Failed, Choice: null, "boom"));

        var result = await Step(judge).Run(Context(Facts()), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("boom", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_judge_that_did_not_finish_on_the_first_call_fails_the_Step_without_asking_the_rest()
    {
        var judge = new ScriptedJudge(static _ => new ChooseAnswer(ChooseStatus.Failed, Choice: null, "the judge did not finish"));

        var result = await Step(judge).Run(Context(Facts()), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        _ = Assert.Single(judge.Requests);
    }

    [Fact]
    public async Task A_missing_lenses_fact_fails_the_Step()
    {
        var result = await Step(new ScriptedJudge(static _ => Decided("in"))).Run(Context(Facts().Without("lenses")), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("lenses", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_token_ends_the_Step_before_any_call()
    {
        var judge = new ScriptedJudge(static _ => Decided("in"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Step(judge).Run(Context(Facts()), cancelled.Token));

        Assert.Empty(judge.Requests);
    }

    private static ChooseAnswer Decided(string choice) => new(ChooseStatus.Decided, choice, "model", 0.9, "jev", "JD-1");

    private static string AllText(ChooseRequest request) =>
        string.Join('\n', [request.Prompt, .. request.Choices.Select(static c => c.Name + " " + c.Criterion), .. request.Facts.Select(static f => f.Key + " " + f.Value)]);

    private sealed class ScriptedJudge(Func<ChooseRequest, ChooseAnswer> answer, TimeSpan? delay = null) : IJudgeChoice
    {
        private readonly Lock _gate = new();
        private readonly List<ChooseRequest> _requests = [];
        private int _inFlight;

        public IReadOnlyList<ChooseRequest> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public int MostInFlight { get; private set; }

        public async ValueTask<ChooseAnswer> Choose(ChooseRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _requests.Add(request);
                _inFlight++;
                MostInFlight = Math.Max(MostInFlight, _inFlight);
            }

            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken);
            }

            lock (_gate)
            {
                _inFlight--;
            }

            return answer(request);
        }
    }
}
