using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>SCP-GAP-001 (issue 966): the gap search ran, and its last round confirmed nothing (a warning once the loop is spent).</summary>
public sealed class ScopingGapRulesTests
{
    private const string Gap = "SCP-GAP-001";

    private static ScopingCheckResponse Run(ScopingFacts facts, int gapCap = ScopeRules.DefaultGapRounds) =>
        ScopePacketChecks.Run(new ScopeFacts(facts), ScopeStage.Evidence, gapCap: gapCap);

    private static ScopeRuleStatus Status(ScopingCheckResponse response) => response.Rules.Single(r => r.Rule == Gap).Status;

    private static JsonObject Candidate(string id, string ruling) => new()
    {
        ["id"] = id,
        ["title"] = "Nightly job that purges expired invitations",
        ["kind"] = "job",
        ["citation"] = "code:src/jobs/purgeInvitations.ts",
        ["excerpt"] = "export async function purgeInvitations() {}",
        ["query"] = "expiresAt",
        ["anchor"] = "code:src/jobs/purgeInvitations.ts",
        ["why"] = "A changed expiry window changes which rows this job deletes.",
        ["ruling"] = ruling,
        ["confidence"] = 0.8,
        ["provider"] = "fixture-judge",
        ["decisionId"] = "JD-0100",
    };

    private static ScopingFacts WithRound(string ruling) => ScopingFacts.Expiry().Edit("gapSearch", n => n["rounds"]![0]!["candidates"] = new JsonArray(Candidate("G1.1", ruling)));

    [Fact, Trait("Lane", "unit")]
    public void The_gold_facts_pass()
    {
        var response = Run(ScopingFacts.Expiry());

        Assert.Equal(ScopeRuleStatus.Pass, Status(response));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_fact_gapSearch_the_rule_is_not_run()
    {
        var response = Run(ScopingFacts.Expiry().Without("gapSearch"));

        Assert.Equal(ScopeRuleStatus.NotRun, response.Rules.Single(r => r.Rule == Gap).Status);
        Assert.Equal(ScopeOutcome.Incomplete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_record_with_no_round_fails_because_the_search_has_not_run_and_the_evidence_is_incomplete()
    {
        var response = Run(ScopingFacts.Expiry().Edit("gapSearch", n => n["rounds"]!.AsArray().Clear()));

        Assert.Equal(ScopeRuleStatus.Fail, Status(response));
        Assert.Contains(response.Findings, f => f.Rule == Gap && f.Message.Contains("has not run", StringComparison.Ordinal));
        Assert.Equal(ScopeOutcome.Incomplete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_confirmed_last_round_fails_while_the_gap_loop_has_rounds_left()
    {
        var response = Run(WithRound("real-gap"), gapCap: 3);

        Assert.Equal(ScopeRuleStatus.Fail, Status(response));
        Assert.Contains(response.Findings, f => f.Rule == Gap && f.Message.Contains("Nightly job that purges expired invitations", StringComparison.Ordinal));
        Assert.Equal(ScopeOutcome.Incomplete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_confirmed_last_round_only_warns_once_the_gap_loop_is_spent()
    {
        var response = Run(WithRound("real-gap"), gapCap: 1);

        Assert.Equal(ScopeRuleStatus.Warn, Status(response));
        Assert.Equal(ScopeOutcome.Complete, response.Outcome);
        Assert.Contains(response.Warnings, w => w.Rule == Gap && w.Message.Contains("Nightly job that purges expired invitations", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_candidate_ruled_uncertain_is_a_warning_that_never_fails()
    {
        var response = Run(WithRound("uncertain"));

        Assert.Equal(ScopeRuleStatus.Warn, Status(response));
        Assert.Equal(ScopeOutcome.Complete, response.Outcome);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("already-covered")]
    [InlineData("not-in-scope")]
    public void A_candidate_the_judge_threw_out_leaves_the_rule_passing(string ruling)
    {
        var response = Run(WithRound(ruling));

        Assert.Equal(ScopeRuleStatus.Pass, Status(response));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_earlier_confirmed_round_does_not_count_once_a_later_round_is_clear()
    {
        var facts = WithRound("real-gap").Edit("gapSearch", n =>
        {
            var rounds = n["rounds"]!.AsArray();
            var clear = rounds[0]!.DeepClone();
            clear["round"] = 2;
            clear["candidates"] = new JsonArray();
            rounds.Add(clear);
        });

        var response = Run(facts);

        Assert.Equal(ScopeRuleStatus.Pass, Status(response));
    }
}
