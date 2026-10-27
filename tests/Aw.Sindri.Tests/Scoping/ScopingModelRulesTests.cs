using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// The model checks over the run records (issue 966 section 6): SCP-CHAL-002 (challenger against drafter), SCP-GAP-002 (gap searcher against drafter)
/// and SCP-REL-002 (relevance agent against the judge). A default install, which names no models, only warns; a fail needs both runs to have asked for a model.
/// </summary>
public sealed class ScopingModelRulesTests
{
    private static ScopingCheckResponse Run(ScopingFacts facts) => ScopePacketChecks.Run(new ScopeFacts(facts), ScopeStage.Evidence);

    private static ScopeRuleStatus Status(ScopingCheckResponse response, string rule) => response.Rules.Single(r => r.Rule == rule).Status;

    private static JsonObject Record(string step, string? model, string? requested = null, string via = "agent", string vendor = "fixture") => new()
    {
        ["step"] = step,
        ["state"] = step["step.scope.".Length..],
        ["round"] = 1,
        ["via"] = via,
        ["model"] = model,
        ["requested"] = requested,
        ["vendor"] = vendor,
        ["tokens"] = 1000,
        ["launch"] = via == "agent" ? "LCH-1" : null,
    };

    private static ScopingFacts WithRuns(params JsonObject[] records)
    {
        var facts = ScopingFacts.Expiry();
        facts["runs"] = new JsonArray([.. records.Cast<JsonNode>()]).ToJsonString();
        return facts;
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_record_list_warns_for_all_three_and_never_holds_the_evidence_back()
    {
        var response = Run(WithRuns());

        Assert.Equal(ScopeOutcome.Complete, response.Outcome);
        Assert.Equal(ScopeRuleStatus.Warn, Status(response, "SCP-CHAL-002"));
        Assert.Equal(ScopeRuleStatus.Warn, Status(response, "SCP-GAP-002"));
        Assert.Equal(ScopeRuleStatus.Warn, Status(response, "SCP-REL-002"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_model_with_both_runs_requesting_it_fails_the_challenger_check()
    {
        var response = Run(WithRuns(Record("step.scope.draft", "m-1", "m-1"), Record("step.scope.challenge", "m-1", "m-1")));

        Assert.Equal(ScopeRuleStatus.Fail, Status(response, "SCP-CHAL-002"));
        Assert.Equal(ScopeOutcome.Incomplete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_model_when_either_run_asked_for_none_only_warns_and_names_the_profile_key()
    {
        var response = Run(WithRuns(Record("step.scope.draft", "m-1", "m-1"), Record("step.scope.challenge", "m-1")));

        Assert.Equal(ScopeRuleStatus.Warn, Status(response, "SCP-CHAL-002"));
        Assert.Contains(response.Warnings, w => w.Rule == "SCP-CHAL-002" && w.Message.Contains("workflow.scoping.models", StringComparison.Ordinal));
        Assert.Equal(ScopeOutcome.Complete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Different_models_pass_the_challenger_check()
    {
        var response = Run(WithRuns(Record("step.scope.draft", "m-1", "m-1"), Record("step.scope.challenge", "m-2", "m-2")));

        Assert.Equal(ScopeRuleStatus.Pass, Status(response, "SCP-CHAL-002"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_run_at_the_desk_warns_that_the_model_is_self_reported()
    {
        var response = Run(WithRuns(Record("step.scope.draft", "m-1"), Record("step.scope.challenge", model: null, via: "desk")));

        Assert.Equal(ScopeRuleStatus.Warn, Status(response, "SCP-CHAL-002"));
        Assert.Contains(response.Warnings, w => w.Rule == "SCP-CHAL-002" && w.Message.Contains("desk", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_gap_searcher_on_the_drafters_model_fails_when_both_requested_it()
    {
        var response = Run(WithRuns(Record("step.scope.draft", "m-1", "m-1"), Record("step.scope.gap-search", "m-1", "m-1")));

        Assert.Equal(ScopeRuleStatus.Fail, Status(response, "SCP-GAP-002"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_gap_searcher_on_the_drafters_model_warns_when_nothing_was_requested()
    {
        var response = Run(WithRuns(Record("step.scope.draft", "m-1"), Record("step.scope.gap-search", "m-1")));

        Assert.Equal(ScopeRuleStatus.Warn, Status(response, "SCP-GAP-002"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_gap_searcher_on_another_model_passes()
    {
        var response = Run(WithRuns(Record("step.scope.draft", "m-1", "m-1"), Record("step.scope.gap-search", "fixture-searcher-model", "fixture-searcher-model")));

        Assert.Equal(ScopeRuleStatus.Pass, Status(response, "SCP-GAP-002"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_gap_searcher_with_no_record_warns()
    {
        var response = Run(WithRuns(Record("step.scope.draft", "m-1")));

        Assert.Equal(ScopeRuleStatus.Warn, Status(response, "SCP-GAP-002"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_self_reported_model_that_differs_from_the_recorded_one_warns_and_the_record_wins()
    {
        var facts = WithRuns(Record("step.scope.draft", "m-1", "m-1"), Record("step.scope.challenge", "m-3", "m-3"), Record("step.scope.gap-search", "m-2", "m-2"))
            .Edit("gapSearch", n => n["rounds"]![0]!["agent"]!["reportedModel"] = "m-9");

        var response = Run(facts);

        Assert.Equal(ScopeRuleStatus.Warn, Status(response, "SCP-GAP-002"));
        Assert.Contains(response.Warnings, w => w.Rule == "SCP-GAP-002" && w.Message.Contains("m-9", StringComparison.Ordinal) && w.Message.Contains("m-2", StringComparison.Ordinal));
        Assert.Equal(ScopeOutcome.Complete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_judge_from_the_relevance_agents_own_vendor_warns_and_another_provider_passes()
    {
        var same = WithRuns(Record("step.scope.relevance-review", "fixture-reviewer-model", vendor: "anthropic"))
            .Edit("relevance", n => n["judge"]!["providers"] = new JsonArray("claude-cli"));
        var other = WithRuns(Record("step.scope.relevance-review", "fixture-reviewer-model", vendor: "anthropic"))
            .Edit("relevance", n => n["judge"]!["providers"] = new JsonArray("jev"));

        Assert.Equal(ScopeRuleStatus.Warn, Status(Run(same), "SCP-REL-002"));
        Assert.Equal(ScopeRuleStatus.Pass, Status(Run(other), "SCP-REL-002"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_gold_facts_pass_all_three_checks()
    {
        var response = Run(ScopingFacts.Expiry());

        Assert.Equal(ScopeRuleStatus.Pass, Status(response, "SCP-CHAL-002"));
        Assert.Equal(ScopeRuleStatus.Pass, Status(response, "SCP-GAP-002"));
        Assert.Equal(ScopeRuleStatus.Pass, Status(response, "SCP-REL-002"));
    }
}
