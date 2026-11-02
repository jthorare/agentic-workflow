using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Scoping.GapSearch;

/// <summary>'step.scope.gap-log' (issue 966, spec 4.6): the candidates and the judge's rulings become one round of the cumulative record, the anchors to look at again and the findings.</summary>
[Lane(TestLane.Unit)]
public sealed class GapLogTests
{
    private static readonly string[] _countNames = ["found", "confirmed", "alreadyCovered", "notInScope", "uncertain"];

    private const string NoRounds = """{"rounds":[]}""";

    private static string Candidate(string id, string anchor, string title = "A nightly job") =>
        $$"""{"id":"{{id}}","title":"{{title}}","kind":"job","citation":"code:{{anchor}}","excerpt":"purge()","query":"expiresAt","anchor":"code:{{anchor}}","why":"It deletes rows."}""";

    private static JsonNode Candidates(params string[] items) => JsonNode.Parse($$"""{"model":"claude-opus-5-5","searches":[{"tool":"context_query","query":"expiry"}],"candidates":[{{string.Join(',', items)}}]}""")!;

    private static string Ruling(string id, string ruling) =>
        $$"""{"id":"{{id}}","ruling":"{{ruling}}","confidence":0.8,"provider":"jev","decisionId":"JD-{{id}}","reasonCode":"model"}""";

    private static JsonNode Rulings(params string[] items) => JsonNode.Parse($$"""{"judge":{"providers":["jev"],"threshold":0.6},"rulings":[{{string.Join(',', items)}}]}""")!;

    private static JsonNode Empty => JsonNode.Parse(NoRounds)!;

    private static JsonNode None => JsonNode.Parse("[]")!;

    [Fact]
    public void A_round_with_no_real_gap_is_clear_and_sets_the_latch()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts"), Candidate("C2", "src/b.ts")), Rulings(Ruling("C1", "already-covered"), Ruling("C2", "not-in-scope")), None);

        var round = result.Value;
        Assert.Equal("clear", round.Outcome);
        Assert.Empty(round.GapAnchors);
        Assert.Null(round.Findings);
        Assert.Equal(1, round.Closed!["round"]!.GetValue<int>());
    }

    [Fact]
    public void A_real_gap_confirms_the_round_with_its_anchor_and_one_finding()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts", "Purge job")), Rulings(Ruling("C1", "real-gap")), None);

        var round = result.Value;
        Assert.Equal("confirmed", round.Outcome);
        Assert.Null(round.Closed);
        Assert.Equal(["code:src/a.ts"], round.GapAnchors.Select(static a => a!.GetValue<string>()), StringComparer.Ordinal);
        var finding = Assert.Single(round.Findings!)!;
        Assert.Equal("SCP-GAP-001", finding["rule"]!.GetValue<string>());
        Assert.Equal("Purge job", finding["item"]!.GetValue<string>());
        Assert.Equal("fail", finding["severity"]!.GetValue<string>());
        Assert.Contains("Purge job", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("code:src/a.ts", finding["message"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(finding["fix"]!.GetValue<string>()));
    }

    [Fact]
    public void Candidates_get_global_ids_by_round_and_position()
    {
        var first = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts"), Candidate("C2", "src/b.ts")), Rulings(Ruling("C1", "real-gap"), Ruling("C2", "real-gap")), None).Value;

        var second = GapLog.Apply(first.GapSearch, Candidates(Candidate("C1", "src/c.ts")), Rulings(Ruling("C1", "already-covered")), None).Value;

        Assert.Equal(["G1.1", "G1.2"], Ids(first.GapSearch, 0), StringComparer.Ordinal);
        Assert.Equal(["G2.1"], Ids(second.GapSearch, 1), StringComparer.Ordinal);
    }

    [Fact]
    public void An_earlier_round_is_never_rewritten()
    {
        var first = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts")), Rulings(Ruling("C1", "real-gap")), None).Value;
        var before = first.GapSearch["rounds"]![0]!.DeepClone();

        var second = GapLog.Apply(first.GapSearch, Candidates(), Rulings(), None).Value;

        Assert.True(JsonNode.DeepEquals(before, second.GapSearch["rounds"]![0]));
        Assert.Equal(2, second.GapSearch["rounds"]!.AsArray().Count);
        Assert.Equal(2, second.GapSearch["counts"]!["rounds"]!.GetValue<int>());
        Assert.Equal(1, second.GapSearch["counts"]!["found"]!.GetValue<int>());
        Assert.Equal(1, second.GapSearch["counts"]!["confirmed"]!.GetValue<int>());
    }

    [Fact]
    public void The_round_records_what_ran_the_searches_and_the_counts()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts"), Candidate("C2", "src/b.ts"), Candidate("C3", "src/c.ts"), Candidate("C4", "src/d.ts")),
            Rulings(Ruling("C1", "real-gap"), Ruling("C2", "already-covered"), Ruling("C3", "not-in-scope"), Ruling("C4", "uncertain")), None).Value;

        var round = result.GapSearch["rounds"]![0]!;
        Assert.Equal(1, round["round"]!.GetValue<int>());
        Assert.Equal("claude-opus-5-5", round["agent"]!["reportedModel"]!.GetValue<string>());
        Assert.Equal("jev", round["judge"]!["providers"]![0]!.GetValue<string>());
        Assert.Equal("expiry", round["searches"]![0]!["query"]!.GetValue<string>());
        var counts = round["counts"]!;
        Assert.Equal([4, 1, 1, 1, 1], _countNames.Select(n => counts[n]!.GetValue<int>()));
        var first = round["candidates"]![0]!;
        Assert.Equal("real-gap", first["ruling"]!.GetValue<string>());
        Assert.Equal("JD-C1", first["decisionId"]!.GetValue<string>());
        Assert.Equal("expiresAt", first["query"]!.GetValue<string>());
    }

    [Fact]
    public void Anchors_are_distinct_and_only_from_real_gaps()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts"), Candidate("C2", "src/a.ts"), Candidate("C3", "src/b.ts"), Candidate("C4", "src/c.ts")),
            Rulings(Ruling("C1", "real-gap"), Ruling("C2", "real-gap"), Ruling("C3", "not-in-scope"), Ruling("C4", "uncertain")), None).Value;

        Assert.Equal(["code:src/a.ts"], result.GapAnchors.Select(static a => a!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(2, result.Findings!.Count);
    }

    [Fact]
    public void An_uncertain_ruling_counts_but_is_not_a_confirmed_gap()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts")), Rulings(Ruling("C1", "uncertain")), None).Value;

        Assert.Equal("clear", result.Outcome);
        Assert.Equal(1, result.GapSearch["rounds"]![0]!["counts"]!["uncertain"]!.GetValue<int>());
        Assert.Equal(0, result.GapSearch["rounds"]![0]!["counts"]!["confirmed"]!.GetValue<int>());
    }

    [Fact]
    public void An_empty_search_is_a_clear_round()
    {
        var result = GapLog.Apply(Empty, Candidates(), Rulings(), None).Value;

        Assert.Equal("clear", result.Outcome);
        Assert.Equal(0, result.GapSearch["rounds"]![0]!["counts"]!["found"]!.GetValue<int>());
    }

    [Fact]
    public void Earlier_gap_findings_are_replaced_and_other_findings_kept()
    {
        var held = JsonNode.Parse("""[{"rule":"SCP-GAP-001","item":"Old","message":"old","fix":"x","severity":"fail"},{"rule":"SCP-COV-001","item":"code","message":"kept","fix":"y","severity":"fail"}]""")!;

        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts", "New")), Rulings(Ruling("C1", "real-gap")), held).Value;

        Assert.Equal(["SCP-COV-001", "SCP-GAP-001"], result.Findings!.Select(static f => f!["rule"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("New", result.Findings![1]!["item"]!.GetValue<string>());
    }

    [Fact]
    public void A_ruling_for_an_unknown_candidate_fails()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts")), Rulings(Ruling("C1", "real-gap"), Ruling("C9", "real-gap")), None);

        Assert.True(result.IsError);
        Assert.Contains("C9", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_candidate_with_no_ruling_fails()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts"), Candidate("C2", "src/b.ts")), Rulings(Ruling("C1", "real-gap")), None);

        Assert.True(result.IsError);
        Assert.Contains("C2", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_candidate_ids_fail()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts"), Candidate("C1", "src/b.ts")), Rulings(Ruling("C1", "real-gap")), None);

        Assert.True(result.IsError);
        Assert.Contains("C1", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_rulings_for_one_candidate_fail()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts")), Rulings(Ruling("C1", "real-gap"), Ruling("C1", "not-in-scope")), None);

        Assert.True(result.IsError);
    }

    [Fact]
    public void A_ruling_word_the_judge_does_not_give_fails()
    {
        var result = GapLog.Apply(Empty, Candidates(Candidate("C1", "src/a.ts")), Rulings(Ruling("C1", "maybe")), None);

        Assert.True(result.IsError);
        Assert.Contains("maybe", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Step_on_a_confirmed_round_writes_the_record_the_anchors_and_the_findings()
    {
        var result = await Run(Candidates(Candidate("C1", "src/a.ts")), Rulings(Ruling("C1", "real-gap")));

        Assert.Equal("confirmed", result.Result!.Outcome.Value);
        Assert.Equal(["findings", "gapAnchors", "gapSearch"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_Step_on_a_clear_round_writes_the_record_empty_anchors_and_the_latch()
    {
        var result = await Run(Candidates(Candidate("C1", "src/a.ts")), Rulings(Ruling("C1", "not-in-scope")));

        Assert.Equal("clear", result.Result!.Outcome.Value);
        Assert.Equal(["gapAnchors", "gapSearch", "gapSearchClosed"], ScopingStepContexts.Keys(result), StringComparer.Ordinal);
        Assert.Empty(JsonNode.Parse(result.Result.Outputs[Core.Workflow.Ids.FactKey.From("fact:gapAnchors")].GetRawText())!.AsArray());
    }

    [Fact]
    public async Task The_Step_fails_with_the_reason_when_a_ruling_is_missing()
    {
        var result = await Run(Candidates(Candidate("C1", "src/a.ts")), Rulings());

        Assert.Null(result.Result);
        Assert.Contains("C1", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Step_fails_when_a_fact_it_reads_is_missing()
    {
        var facts = new ScopingFacts { ["gapSearch"] = NoRounds, ["findings"] = "[]" };

        var result = await new GapLogStep().Run(ScopingStepContexts.Of("step.scope.gap-log", facts), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("gapCandidates", result.Failure, StringComparison.Ordinal);
    }

    private static async Task<BlockStepResult> Run(JsonNode candidates, JsonNode rulings)
    {
        var facts = new ScopingFacts { ["gapSearch"] = NoRounds, ["gapCandidates"] = candidates.ToJsonString(), ["gapRulings"] = rulings.ToJsonString(), ["findings"] = "[]" };
        return await new GapLogStep().Run(ScopingStepContexts.Of("step.scope.gap-log", facts), TestContext.Current.CancellationToken);
    }

    private static string[] Ids(JsonNode record, int round) => [.. record["rounds"]![round]!["candidates"]!.AsArray().Select(static c => c!["id"]!.GetValue<string>())];
}
