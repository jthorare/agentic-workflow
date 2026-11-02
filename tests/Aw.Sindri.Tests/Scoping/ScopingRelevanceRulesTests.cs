using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>SCP-REL-001 (issue 966): the lenses hold exactly the entries the relevance review kept, and nothing leaves them without both reviewers' agreement and a citation that resolves.</summary>
public sealed class ScopingRelevanceRulesTests
{
    private const string Rel = "SCP-REL-001";

    private static ScopingCheckResponse Run(ScopingFacts facts) => ScopePacketChecks.Run(new ScopeFacts(facts), ScopeStage.Evidence);

    private static ScopeRuleStatus Status(ScopingCheckResponse response) => response.Rules.Single(r => r.Rule == Rel).Status;

    [Fact, Trait("Lane", "unit")]
    public void The_gold_facts_pass()
    {
        var response = Run(ScopingFacts.Expiry());

        Assert.Equal(ScopeRuleStatus.Pass, Status(response));
        Assert.DoesNotContain(response.Findings, f => f.Rule == Rel);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_fact_relevance_the_rule_is_not_run_and_the_evidence_is_incomplete()
    {
        var response = Run(ScopingFacts.Expiry().Without("relevance"));

        var rule = response.Rules.Single(r => r.Rule == Rel);
        Assert.Equal(ScopeRuleStatus.NotRun, rule.Status);
        Assert.Contains("relevance", rule.Missing, StringComparer.Ordinal);
        Assert.Equal(ScopeOutcome.Incomplete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_both_reviewers_call_noise_with_a_resolving_citation_may_leave_the_lenses()
    {
        var facts = ScopingFacts.Expiry().Rule("L-code-3", agent: "noise", judge: "noise", citation: "L-code-3", result: "noise");

        var response = Run(facts);

        Assert.Equal(ScopeRuleStatus.Pass, Status(response));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_that_left_the_lenses_although_the_judge_called_it_in_fails_naming_it()
    {
        var facts = ScopingFacts.Expiry().Rule("L-code-3", agent: "noise", judge: "in", citation: "L-code-3", result: "noise");

        var response = Run(facts);

        Assert.Equal(ScopeRuleStatus.Fail, Status(response));
        Assert.Contains(response.Findings, f => f.Rule == Rel && f.Item == "L-code-3" && f.Severity == ScopeSeverity.Fail);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_that_left_the_lenses_with_a_citation_that_resolves_to_nothing_fails()
    {
        var facts = ScopingFacts.Expiry().Rule("L-code-3", agent: "noise", judge: "noise", citation: "L-nowhere-9", result: "noise");

        var response = Run(facts);

        Assert.Contains(response.Findings, f => f.Rule == Rel && f.Item == "L-code-3" && f.Severity == ScopeSeverity.Fail);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_that_left_the_lenses_with_no_judge_ruling_fails()
    {
        var facts = ScopingFacts.Expiry().Rule("L-code-3", agent: "noise", judge: "noise", citation: "L-code-3", result: "noise").WithoutJudge("L-code-3");

        var response = Run(facts);

        Assert.Contains(response.Findings, f => f.Rule == Rel && f.Item == "L-code-3" && f.Severity == ScopeSeverity.Fail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lens_entry_the_relevance_record_never_kept_is_a_bypass_and_fails()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"]!["nodes"]!.AsArray().Add(new JsonObject
        {
            ["key"] = "surface:route/links-export",
            ["kind"] = "surface",
            ["label"] = "Export a link",
            ["band"] = "product",
            ["lensItem"] = "L-code-99",
        }));

        var response = Run(facts);

        Assert.Equal(ScopeRuleStatus.Fail, Status(response));
        Assert.Contains(response.Findings, f => f.Rule == Rel && f.Message.Contains("L-code-99", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_the_relevance_record_kept_in_that_the_lenses_lack_is_a_stale_pass_and_fails()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"]!["nodes"]!.AsArray().RemoveAt(2));

        var response = Run(facts);

        Assert.Equal(ScopeRuleStatus.Fail, Status(response));
        Assert.Contains(response.Findings, f => f.Rule == Rel && f.Message.Contains("L-code-3", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Counts_that_disagree_with_the_entries_fail()
    {
        var facts = ScopingFacts.Expiry().Edit("relevance", n => n["counts"]!["in"] = 3);

        var response = Run(facts);

        Assert.Equal(ScopeRuleStatus.Fail, Status(response));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_disputed_entry_warns_with_both_verdicts_and_never_fails_or_holds_the_evidence_back()
    {
        var facts = ScopingFacts.Expiry().Rule("L-code-3", agent: "noise", judge: "in", citation: "L-code-3", result: "in", disputed: true);

        var response = Run(facts);

        Assert.Equal(ScopeRuleStatus.Warn, Status(response));
        Assert.Equal(ScopeOutcome.Complete, response.Outcome);
        var warning = Assert.Single(response.Warnings, w => w.Rule == Rel);
        Assert.Equal("L-code-3", warning.Item);
        Assert.Contains("AI said noise", warning.Message, StringComparison.Ordinal);
        Assert.Contains("judge said in", warning.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_draft_stage_does_not_run_the_rule()
    {
        var response = ScopePacketChecks.Run(new ScopeFacts(ScopingFacts.Expiry()), ScopeStage.Draft);

        Assert.Equal(ScopeRuleStatus.NotRun, Status(response));
    }
}
