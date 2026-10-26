using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Scoping.GapSearch;

/// <summary>'step.scope.gap-judge' (issue 966, spec 4.5): one judge call per candidate over a projection that holds the brief, the acceptance items, the map, the gathered entries and the candidate's four fields, and nothing of the searcher's argument.</summary>
[Lane(TestLane.Unit)]
public sealed class GapJudgeStepTests
{
    private static readonly string[] _providerList = ["jev", "rules"];

    private const string Why = "WHY-the-job-matters-9f3";
    private const string Query = "QUERY-expiresAt-7c1";
    private const string Anchor = "ANCHOR-code:src/jobs/purge-4d2";
    private const string Model = "MODEL-claude-opus-5-5";
    private const string Search = "SEARCH-neighbors-depth-8e5";
    private const string Trust = "TRUST-untrusted-1b7";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Candidate(string id, string title) =>
        $$"""{"id":"{{id}}","title":"{{title}}","kind":"job","citation":"code:src/jobs/{{id}}.ts","excerpt":"export async function {{id}}() {}","query":"{{Query}}","anchor":"{{Anchor}}","why":"{{Why}}"}""";

    private static string Candidates(params string[] items) =>
        $$"""{"model":"{{Model}}","searches":[{"tool":"context_neighbors","query":"{{Search}}","depth":3}],"candidates":[{{string.Join(',', items)}}]}""";

    private static StepContext Context(string candidates, Dictionary<string, JsonElement>? arguments = null)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fact:brief"] = $$"""{"text":"Shared links expire after a chosen time.","refs":[{"id":"R1","source":"file","key":"brief.md","trust":"{{Trust}}"}],"subject":"links"}""",
            ["fact:acceptance"] = """{"items":[{"id":"AC-1","text":"When a link is opened after its expiry the system shall refuse it.","form":"ears"}]}""",
            ["fact:scopeMap"] = """{"surfaces":[{"id":"S1","key":"surface:route/link-resolve","kind":"surface","title":"Open shared link","detail":"DETAIL-not-shown"}]}""",
            ["fact:lenses"] = """{"code":{"anchors":["surface:route/link-resolve"],"nodes":[{"key":"code:src/links/resolve.ts","kind":"code","label":"resolve.ts","band":"code","lensItem":"L-code-1"}],"items":[{"lensItem":"L-code-2","kind":"symbol","label":"resolveLink","citations":["code:src/links/resolve.ts"]}],"edges":[]}}""",
            ["fact:gapCandidates"] = candidates,
        };
        return new StepContext(
            BlockId.From("step.scope.gap-judge"),
            arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            facts.ToDictionary(static f => FactKey.From(f.Key), static f => JsonSerializer.SerializeToElement(f.Value)),
            InjectionSections.Empty);
    }

    private static Dictionary<string, JsonElement> Args(params (string Name, object Value)[] arguments) =>
        arguments.ToDictionary(static a => a.Name, static a => JsonSerializer.SerializeToElement(a.Value), StringComparer.Ordinal);

    private static JsonNode Out(BlockStepResult result) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:gapRulings")].GetRawText())!;

    private static ChooseAnswer Decided(string choice, double confidence = 0.9) => new(ChooseStatus.Decided, choice, "model", confidence, "jev", "JD-1");

    private static string Everything(ChooseRequest request) =>
        string.Join('\n', [request.Prompt, .. request.Choices.Select(static c => c.Name + c.Criterion), .. request.Facts.Select(static f => f.Key + f.Value)]);

    [Fact]
    public async Task No_request_holds_anything_of_the_searchers_argument()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"));

        _ = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"), Candidate("C2", "Reminder email"))), Token);

        Assert.Equal(2, judge.Requests.Count);
        foreach (var request in judge.Requests)
        {
            var text = Everything(request);
            foreach (var hidden in new[] { Why, Query, Anchor, Model, Search, Trust, "DETAIL-not-shown" })
            {
                Assert.DoesNotContain(hidden, text, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task A_request_holds_the_brief_the_acceptance_items_the_map_the_gathered_entries_and_the_candidates_four_fields()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"));

        _ = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"))), Token);

        var text = Everything(Assert.Single(judge.Requests));
        foreach (var shown in new[] { "Shared links expire after a chosen time.", "AC-1", "shall refuse it", "S1", "surface:route/link-resolve", "Open shared link", "code:src/links/resolve.ts", "resolve.ts", "resolveLink", "Purge job", "job", "code:src/jobs/C1.ts", "export async function C1() {}" })
        {
            Assert.Contains(shown, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_request_offers_the_three_rulings()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"));

        _ = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"))), Token);

        Assert.Equal(["real-gap", "already-covered", "not-in-scope"], Assert.Single(judge.Requests).Choices.Select(static c => c.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Each_candidate_gets_its_own_call_and_the_rulings_keep_their_ids_in_order()
    {
        var judge = new ScriptedJudge(static r => Decided(r.Facts["candidate"].Contains("Reminder", StringComparison.Ordinal) ? "not-in-scope" : "real-gap"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"), Candidate("C2", "Reminder email"), Candidate("C3", "Export"))), Token);

        Assert.Equal("ruled", result.Result!.Outcome.Value);
        var rulings = Out(result)["rulings"]!.AsArray();
        Assert.Equal(["C1", "C2", "C3"], rulings.Select(static r => r!["id"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(["real-gap", "not-in-scope", "real-gap"], rulings.Select(static r => r!["ruling"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("jev", rulings[0]!["provider"]!.GetValue<string>());
        Assert.Equal("JD-1", rulings[0]!["decisionId"]!.GetValue<string>());
        Assert.Equal("model", rulings[0]!["reasonCode"]!.GetValue<string>());
        Assert.Equal(0.9, rulings[0]!["confidence"]!.GetValue<double>());
    }

    [Fact]
    public async Task The_calls_run_at_most_parallel_at_a_time()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"), delay: TimeSpan.FromMilliseconds(30));
        var items = Enumerable.Range(1, 8).Select(static i => Candidate("C" + i.ToString(CultureInfo.InvariantCulture), "Job " + i.ToString(CultureInfo.InvariantCulture))).ToArray();

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(items), Args(("parallel", 2))), Token);

        Assert.Equal("ruled", result.Result!.Outcome.Value);
        Assert.Equal(8, judge.Requests.Count);
        Assert.InRange(judge.MostAtOnce, 1, 2);
    }

    [Fact]
    public async Task An_escalated_answer_is_an_uncertain_ruling_with_its_reason()
    {
        var judge = new ScriptedJudge(static _ => new ChooseAnswer(ChooseStatus.Escalated, Choice: null, "below-threshold", DecisionRef: "JD-3"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"))), Token);

        var ruling = Out(result)["rulings"]![0]!;
        Assert.Equal("uncertain", ruling["ruling"]!.GetValue<string>());
        Assert.Equal("below-threshold", ruling["reasonCode"]!.GetValue<string>());
        Assert.Null(ruling["confidence"]);
        Assert.Null(ruling["provider"]);
        Assert.Equal("JD-3", ruling["decisionId"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_choice_that_is_not_one_of_the_three_is_an_uncertain_ruling()
    {
        var judge = new ScriptedJudge(static _ => Decided("maybe"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"))), Token);

        Assert.Equal("uncertain", Out(result)["rulings"]![0]!["ruling"]!.GetValue<string>());
    }

    [Fact]
    public async Task One_failed_call_is_an_uncertain_ruling_and_the_Step_still_completes()
    {
        var judge = new ScriptedJudge(static r => r.Facts["candidate"].Contains("Export", StringComparison.Ordinal) ? new ChooseAnswer(ChooseStatus.Failed, Choice: null, "judge choose exited 1: boom") : Decided("real-gap"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"), Candidate("C2", "Export"))), Token);

        Assert.Equal("ruled", result.Result!.Outcome.Value);
        var rulings = Out(result)["rulings"]!.AsArray();
        Assert.Equal("real-gap", rulings[0]!["ruling"]!.GetValue<string>());
        Assert.Equal("uncertain", rulings[1]!["ruling"]!.GetValue<string>());
        Assert.Equal("failed", rulings[1]!["reasonCode"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_Step_fails_when_every_call_failed()
    {
        var judge = new ScriptedJudge(static _ => new ChooseAnswer(ChooseStatus.Failed, Choice: null, "judge choose exited 1: boom"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"), Candidate("C2", "Export"))), Token);

        Assert.Null(result.Result);
        Assert.Contains("boom", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Step_fails_at_once_when_the_judge_is_not_there()
    {
        var judge = new ScriptedJudge(static _ => new ChooseAnswer(ChooseStatus.Failed, Choice: null, "judge choose did not finish (judge: NotFound); fix: build judge and put it on the PATH, or set AW_SINDRI_JUDGE"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"), Candidate("C2", "Export"), Candidate("C3", "Reminder"))), Token);

        Assert.Null(result.Result);
        Assert.Contains("did not finish", result.Failure, StringComparison.Ordinal);
        _ = Assert.Single(judge.Requests);
    }

    [Fact]
    public async Task No_candidates_is_an_empty_ruled_round()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates()), Token);

        Assert.Equal("ruled", result.Result!.Outcome.Value);
        Assert.Empty(Out(result)["rulings"]!.AsArray());
        Assert.Empty(judge.Requests);
    }

    [Fact]
    public async Task The_text_is_scrubbed_before_it_leaves()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"));
        var secret = Candidate("C1", "Purge job").Replace("export async function C1() {}", "token hunter2", StringComparison.Ordinal);

        _ = await new GapJudgeStep(judge, new Masking()).Run(Context(Candidates(secret)), Token);

        var text = Everything(Assert.Single(judge.Requests));
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_providers_and_threshold_reach_the_judge_and_the_record()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job")), Args(("providers", _providerList), ("threshold", 0.75))), Token);

        var request = Assert.Single(judge.Requests);
        Assert.Equal(["jev", "rules"], request.Providers, StringComparer.Ordinal);
        Assert.Equal(0.75, request.Threshold);
        Assert.Equal(["jev", "rules"], Out(result)["judge"]!["providers"]!.AsArray().Select(static p => p!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(0.75, Out(result)["judge"]!["threshold"]!.GetValue<double>());
    }

    [Fact]
    public async Task A_threshold_outside_zero_to_one_fails_the_Step()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"));

        var result = await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job")), Args(("threshold", 1.5))), Token);

        Assert.Null(result.Result);
        Assert.Contains("threshold", result.Failure, StringComparison.Ordinal);
        Assert.Empty(judge.Requests);
    }

    [Fact]
    public async Task A_cancelled_token_ends_the_Step_before_any_call()
    {
        var judge = new ScriptedJudge(static _ => Decided("real-gap"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new GapJudgeStep(judge, new PassThroughScrubber()).Run(Context(Candidates(Candidate("C1", "Purge job"))), cancelled.Token));

        Assert.Empty(judge.Requests);
    }

    private sealed class Masking : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("hunter2", "[redacted]", StringComparison.Ordinal);
    }

    /// <summary>A judge that answers each request by a function of it, and keeps the requests and the most calls in flight at once.</summary>
    private sealed class ScriptedJudge(Func<ChooseRequest, ChooseAnswer> answer, TimeSpan delay = default) : IJudgeChoice
    {
        private readonly List<ChooseRequest> _requests = [];
        private readonly Lock _gate = new();
        private int _active;

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

        public int MostAtOnce { get; private set; }

        public async ValueTask<ChooseAnswer> Choose(ChooseRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _requests.Add(request);
                _active++;
                MostAtOnce = Math.Max(MostAtOnce, _active);
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            lock (_gate)
            {
                _active--;
            }

            return answer(request);
        }
    }
}
