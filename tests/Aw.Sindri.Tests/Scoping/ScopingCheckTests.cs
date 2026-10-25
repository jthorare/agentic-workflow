using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>ScopePacketChecks through 'scoping check' (scoping spec §8.2): the gold facts pass, and each failing rule has one broken fixture that names it.</summary>
public sealed class ScopingCheckTests
{
    internal static async Task<ScopingCheckResponse> Run(ScopingFacts facts, ScopeStage stage = ScopeStage.Draft)
    {
        var result = await new ScopingCheckService().Handle(new ScopingCheckRequest(facts, stage), CancellationToken.None);
        return result.Value;
    }

    private static void Names(ScopingCheckResponse response, string rule, ScopeOutcome outcome)
    {
        Assert.Contains(response.Findings, f => f.Rule == rule);
        Assert.Equal(outcome, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_facts_pass_the_draft_stage_with_every_draft_rule_run()
    {
        var response = await Run(ScopingFacts.Expiry());

        Assert.Empty(response.Findings);
        Assert.Equal(ScopeOutcome.Pass, response.Outcome);
        Assert.True(response.Passed);
        Assert.DoesNotContain(response.Rules, r => r.Status == ScopeRuleStatus.Fail);
        Assert.Equal(ScopeRuleStatus.Pass, response.Rules.Single(r => r.Rule == "SCP-ANS-001").Status);
        Assert.Equal(ScopeRuleStatus.NotRun, response.Rules.Single(r => r.Rule == "SCP-LENS-001").Status);   // an evidence rule
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_facts_complete_the_evidence_stage_with_no_rule_left_unrun()
    {
        var response = await Run(ScopingFacts.Expiry(), ScopeStage.Evidence);

        Assert.Empty(response.Findings);
        Assert.Equal(ScopeOutcome.Complete, response.Outcome);
        Assert.DoesNotContain(response.Rules, r => r.Status is ScopeRuleStatus.NotRun or ScopeRuleStatus.Fail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_surface_with_no_citation_is_SCP_CITE_001()
    {
        var facts = ScopingFacts.Expiry().Edit("scopeMap", n => n["surfaces"]![1]!["citations"] = new JsonArray());

        Names(await Run(facts), "SCP-CITE-001", ScopeOutcome.Gaps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_citation_that_resolves_to_nothing_is_SCP_CITE_002()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"]![0]!["citations"] = new JsonArray("L-code-99", "A9"));

        var response = await Run(facts);

        Names(response, "SCP-CITE-002", ScopeOutcome.Gaps);
        Assert.Equal("AC-1", response.Findings.First(f => f.Rule == "SCP-CITE-002").Item);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_citation_that_resolves_through_a_brief_ref_or_an_answer_is_enough()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"]![0]!["citations"] = new JsonArray("R1"))
            .Edit("scopeMap", n => n["surfaces"]![1]!["citations"] = new JsonArray("L-code-99", "A2"));

        Assert.Empty((await Run(facts)).Findings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_coverage_with_three_rows_is_SCP_COV_001()
    {
        var facts = ScopingFacts.Expiry().Edit("coverage", n => n["lenses"]!.AsArray().RemoveAt(3));

        Names(await Run(facts), "SCP-COV-001", ScopeOutcome.Gaps);
    }

    /// <summary>The five recorded limits (§8.2's table): the row is a recorded state, passes, and is never "nothing found".</summary>
    [Theory, Trait("Lane", "unit")]
    [InlineData("code", "unavailable", "index-not-built", null)]
    [InlineData("history", "unavailable", "history-unlinked", null)]
    [InlineData("code", "unavailable", "commit-not-held", "SND-CONTEXT-110")]
    [InlineData("history", "unavailable", "not-synced", "SND-CONTEXT-161")]
    [InlineData("product", "not-configured", "not-configured", null)]
    [InlineData("history", "not-configured", "not-configured", null)]
    public async Task A_row_for_each_recorded_limit_passes_as_not_searched(string lens, string state, string reason, string? code)
    {
        var facts = Limited(lens, state, reason, code, rowState: "not-searched", count: 0);

        var response = await Run(facts);

        Assert.Empty(response.Findings);
        Assert.Equal(ScopeOutcome.Pass, response.Outcome);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("code", "unavailable", "index-not-built", null)]
    [InlineData("history", "unavailable", "not-synced", "SND-CONTEXT-161")]
    [InlineData("code", "ok", "catching-up", null)]
    public async Task A_found_row_may_carry_a_recorded_limit(string lens, string state, string reason, string? code)
    {
        var facts = Limited(lens, state, reason, code, rowState: "found", count: 3);

        Assert.Empty((await Run(facts)).Findings);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("history", "degraded", "timeout", null)]
    [InlineData("history", "degraded", "not-synced", "SND-CONTEXT-161")]
    [InlineData("code", "degraded", "index-not-built", null)]
    [InlineData("code", "degraded", "stale", null)]
    [InlineData("code", "degraded", "catching-up", null)]
    [InlineData("code", "unavailable", "something-else", null)]
    public async Task A_degraded_or_unmatched_source_is_SCP_COV_002_and_a_regather(string lens, string state, string reason, string? code)
    {
        var facts = Limited(lens, state, reason, code, rowState: "not-searched", count: 0);

        var response = await Run(facts);

        Names(response, "SCP-COV-002", ScopeOutcome.Regather);
        Assert.All(response.Findings, f => Assert.Equal("SCP-COV-002", f.Rule));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("history", "unavailable", "index-not-built")]
    [InlineData("product", "unavailable", "index-not-built")]
    [InlineData("code", "unavailable", "history-unlinked")]
    [InlineData("code", "unavailable", "not-synced")]
    [InlineData("related", "unavailable", "commit-not-held")]
    [InlineData("history", "ok", "catching-up")]
    public async Task A_limits_reason_on_the_wrong_lens_is_SCP_COV_002(string lens, string state, string reason)
    {
        var facts = Limited(lens, state, reason, code: null, rowState: "found", count: 3);

        Names(await Run(facts), "SCP-COV-002", ScopeOutcome.Regather);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_limited_row_with_nothing_found_must_be_not_searched_never_nothing_found()
    {
        var facts = Limited("code", "unavailable", "index-not-built", code: null, rowState: "nothing-found", count: 0);

        var response = await Run(facts);

        Names(response, "SCP-COV-002", ScopeOutcome.Regather);
        Assert.Contains(response.Findings, f => f.Message.Contains("nothing-found", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_found_row_with_a_count_of_zero_and_a_not_searched_row_with_no_limit_are_SCP_COV_002()
    {
        var found = ScopingFacts.Expiry().Edit("coverage", n => n["lenses"]![1]!["found"]!["count"] = 0);
        var bare = ScopingFacts.Expiry().Edit("coverage", n => n["lenses"]![1]!["state"] = "not-searched");

        Names(await Run(found), "SCP-COV-002", ScopeOutcome.Regather);
        Names(await Run(bare), "SCP-COV-002", ScopeOutcome.Regather);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_matching_source_whose_limit_is_missing_from_limits_is_SCP_COV_002()
    {
        var facts = ScopingFacts.Expiry().Edit("coverage", n => n["lenses"]![0]!["limits"] = new JsonArray());

        Names(await Run(facts), "SCP-COV-002", ScopeOutcome.Regather);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_regather_needs_every_failing_rule_to_be_SCP_COV_002()
    {
        var facts = Limited("history", "degraded", "timeout", code: null, rowState: "not-searched", count: 0)
            .Edit("acceptance", n => n["items"]![0]!["citations"] = new JsonArray());

        var response = await Run(facts);

        Assert.Equal(ScopeOutcome.Gaps, response.Outcome);
        Assert.Contains(response.Findings, f => f.Rule == "SCP-COV-002");
        Assert.Contains(response.Findings, f => f.Rule == "SCP-CITE-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lens_the_profile_marks_none_is_not_applicable_with_its_reason()
    {
        var facts = ScopingFacts.Expiry().Edit("coverage", n =>
        {
            var row = n["lenses"]![3]!;
            row["state"] = "not-applicable";
            row["reason"] = "profile marks history none for this repo";
            row["found"]!["count"] = 0;
        });

        Assert.Empty((await Run(facts)).Findings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_acceptance_list_is_SCP_ACC_001()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"] = new JsonArray());

        Names(await Run(facts), "SCP-ACC-001", ScopeOutcome.Gaps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_in_scope_surface_with_no_item_and_no_marker_is_SCP_ACC_002()
    {
        var facts = ScopingFacts.Expiry().Edit("scopeMap", n => n["surfaces"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == "S1")!.AsObject().Remove("noBehaviourChange"));

        var response = await Run(facts);

        Names(response, "SCP-ACC-002", ScopeOutcome.Gaps);
        Assert.Equal("S1", response.Findings.First(f => f.Rule == "SCP-ACC-002").Item);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_related_surface_with_no_regression_item_is_SCP_ACC_005_and_a_regression_item_clears_it()
    {
        var facts = ScopingFacts.Expiry().Edit("scopeMap", n =>
        {
            var s = n["surfaces"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == "S1")!;
            s["lens"] = "related";
            _ = s.AsObject().Remove("noBehaviourChange");
        }).Edit("acceptance", n => n["items"]![0]!["surfaces"] = new JsonArray("S2", "S4"));
        Names(await Run(facts), "SCP-ACC-005", ScopeOutcome.Gaps);

        var cleared = facts.Edit("acceptance", n => n["items"]!.AsArray().Add(JsonNode.Parse("""
            {"id":"AC-11","text":"WHILE a link is shared THE sharing feature SHALL continue to open the note as it does today.","form":"ears","surfaces":["S1"],"citations":["L-code-1"],"status":"failing","evidence":[]}
            """)));
        Assert.DoesNotContain((await Run(cleared)).Findings, f => f.Rule == "SCP-ACC-005");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_batch_in_answers_missing_from_the_log_is_SCP_ANS_001()
    {
        var facts = ScopingFacts.Expiry().Edit("answers", n => n.AsArray().Add(JsonNode.Parse("""{"id":"A3","batch":3,"round":3,"actor":"owner","questionIds":["Q8"],"answers":[{"question":"Q8","option":"Q8.a","text":"x"}]}""")));

        Names(await Run(facts), "SCP-ANS-001", ScopeOutcome.Gaps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_log_with_a_gap_in_its_ids_is_SCP_ANS_001()
    {
        var facts = ScopingFacts.Expiry().Edit("answerLog", n => n[1]!["id"] = "A3");

        Names(await Run(facts), "SCP-ANS-001", ScopeOutcome.Gaps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_workstream_gap_or_a_cycle_is_SCP_MAP_001()
    {
        var gap = ScopingFacts.Expiry().Edit("scopeMap", n => n["workstreams"]!.AsArray().RemoveAt(1));
        var twice = ScopingFacts.Expiry().Edit("scopeMap", n => n["workstreams"]![1]!["surfaces"]!.AsArray().Add("S1"));
        var cycle = ScopingFacts.Expiry().Edit("scopeMap", n => n["workstreams"]![0]!["deps"] = new JsonArray("ws-2"));
        var unknown = ScopingFacts.Expiry().Edit("scopeMap", n => n["workstreams"]![1]!["deps"] = new JsonArray("ws-9"));

        Names(await Run(gap), "SCP-MAP-001", ScopeOutcome.Gaps);
        Names(await Run(twice), "SCP-MAP-001", ScopeOutcome.Gaps);
        Names(await Run(cycle), "SCP-MAP-001", ScopeOutcome.Gaps);
        Names(await Run(unknown), "SCP-MAP-001", ScopeOutcome.Gaps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_model_written_trust_is_a_schema_finding_at_the_draft_stage()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"]![0]!["trust"] = "owner");

        var response = await Run(facts);

        Names(response, "SCP-TRUST-001", ScopeOutcome.Gaps);
        Assert.Equal("AC-1", response.Findings.First(f => f.Rule == "SCP-TRUST-001").Item);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_that_breaks_the_fact_schema_is_named_whatever_schemaVersion_says()
    {
        // a switch on schemaVersion alone is defeated by a model that adds it: the draft is checked against $defs/fact directly
        var facts = ScopingFacts.Expiry().Edit("scopeMap", n =>
        {
            n["schemaVersion"] = 1;
            n["surfaces"]![0]!["graphStatus"] = "bogus";
        });

        var response = await Run(facts);

        Names(response, "SCP-SCHEMA-001", ScopeOutcome.Gaps);
        Assert.Contains(response.Findings, f => f.Message.Contains("graphStatus", StringComparison.Ordinal) || f.Message.Contains("schemaVersion", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rule_whose_facts_are_absent_is_not_run_with_the_missing_keys_and_does_not_fail()
    {
        var facts = ScopingFacts.Expiry().Without("answers");

        var response = await Run(facts);

        Assert.Empty(response.Findings);
        var rule = response.Rules.Single(r => r.Rule == "SCP-ANS-001");
        Assert.Equal(ScopeRuleStatus.NotRun, rule.Status);
        Assert.Equal(["answers"], rule.Missing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_not_run_rule_is_never_a_pass_at_the_evidence_stage()
    {
        var facts = ScopingFacts.Expiry().Without("answers");

        var response = await Run(facts, ScopeStage.Evidence);

        Assert.Equal(ScopeOutcome.Incomplete, response.Outcome);
        Assert.Equal(ScopeRuleStatus.NotRun, response.Rules.Single(r => r.Rule == "SCP-ANS-001").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Missing_run_records_warn_and_never_hold_the_evidence_back()
    {
        var response = await Run(ScopingFacts.Expiry().Without("runs"), ScopeStage.Evidence);

        Assert.Equal(ScopeOutcome.Complete, response.Outcome);
        Assert.Equal(ScopeRuleStatus.Warn, response.Rules.Single(r => r.Rule == "SCP-CHAL-002").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_found_last_round_after_the_second_opinion_is_spent_warns_and_before_it_fails()
    {
        var spent = await Run(ScopingFacts.Expiry().Edit("challenge", n => { var rounds = n["rounds"]!.AsArray(); rounds.Add(rounds[1]!.DeepClone()); rounds[2]!["verdict"] = "found"; }), ScopeStage.Evidence);
        var notYet = await Run(ScopingFacts.Expiry().Edit("challenge", n => n["rounds"]![1]!["verdict"] = "found"), ScopeStage.Evidence);
        var first = await Run(ScopingFacts.Expiry().Edit("challenge", n => { var rounds = n["rounds"]!.AsArray(); rounds.RemoveAt(0); rounds[0]!["verdict"] = "found"; }), ScopeStage.Evidence);

        Assert.Equal(ScopeOutcome.Complete, spent.Outcome);
        Assert.Equal(ScopeRuleStatus.Warn, spent.Rules.Single(r => r.Rule == "SCP-CHAL-001").Status);
        Assert.Equal(ScopeOutcome.Incomplete, first.Outcome);
        Assert.Equal(ScopeOutcome.Incomplete, notYet.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lens_or_dimension_no_round_walked_warns_once_the_second_opinion_is_spent_and_before_it_fails()
    {
        static void Spend(JsonNode n)
        {
            var rounds = n["rounds"]!.AsArray();
            rounds.Add(rounds[1]!.DeepClone());
            foreach (var round in rounds)
            {
                round!["walked"] = new JsonArray();
            }
        }

        var spent = await Run(ScopingFacts.Expiry().Edit("challenge", Spend), ScopeStage.Evidence);
        var notYet = await Run(ScopingFacts.Expiry().Edit("challenge", n => { foreach (var round in n["rounds"]!.AsArray()) { round!["walked"] = new JsonArray(); } }), ScopeStage.Evidence);

        Assert.Equal(ScopeOutcome.Complete, spent.Outcome);
        Assert.Equal(ScopeRuleStatus.Warn, spent.Rules.Single(r => r.Rule == "SCP-CHAL-001").Status);
        Assert.Equal(ScopeOutcome.Incomplete, notYet.Outcome);
        Assert.Equal(ScopeRuleStatus.Fail, notYet.Rules.Single(r => r.Rule == "SCP-CHAL-001").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dimension_any_round_walked_counts_as_walked()
    {
        var facts = ScopingFacts.Expiry().Edit("challenge", n =>
        {
            var last = n["rounds"]![1]!.AsObject();
            last["walked"] = new JsonArray();
        });

        var response = await Run(facts, ScopeStage.Evidence);

        Assert.DoesNotContain(response.Findings, f => f.Rule == "SCP-CHAL-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_evidence_stage_with_only_coverage_findings_is_a_regather()
    {
        var facts = Limited("history", "degraded", "timeout", code: null, rowState: "found", count: 3);

        Assert.Equal(ScopeOutcome.Regather, (await Run(facts, ScopeStage.Evidence)).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_evidence_stage_with_only_lens_findings_is_incomplete_never_a_regather()
    {
        var facts = ScopingFacts.Expiry().Edit("lenses", n => n["code"]!["anchors"] = new JsonArray("feature:other"));

        var response = await Run(facts, ScopeStage.Evidence);

        Names(response, "SCP-LENS-001", ScopeOutcome.Incomplete);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_challenger_on_the_drafters_model_warns_once_the_second_opinion_is_spent_and_before_it_fails()
    {
        static void SameModel(JsonNode n) => n["runs"]![1]!["model"] = n["runs"]![0]!["model"]!.DeepClone();
        static void Spend(JsonNode n)
        {
            var rounds = n["rounds"]!.AsArray();
            rounds.Add(rounds[1]!.DeepClone());
        }

        var spent = await Run(ScopingFacts.Expiry().Edit("runs", SameModel).Edit("challenge", Spend), ScopeStage.Evidence);
        var notYet = await Run(ScopingFacts.Expiry().Edit("runs", SameModel), ScopeStage.Evidence);

        Assert.Equal(ScopeOutcome.Complete, spent.Outcome);
        Assert.Equal(ScopeRuleStatus.Warn, spent.Rules.Single(r => r.Rule == "SCP-CHAL-002").Status);
        Assert.Equal(ScopeOutcome.Incomplete, notYet.Outcome);
        Assert.Equal(ScopeRuleStatus.Fail, notYet.Rules.Single(r => r.Rule == "SCP-CHAL-002").Status);
    }

    /// <summary>The gold facts with one lens's coverage row turned into a limited one.</summary>
    internal static ScopingFacts Limited(string lens, string state, string reason, string? code, string rowState, int count) =>
        ScopingFacts.Expiry().Edit("coverage", n =>
        {
            var row = n["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == lens)!;
            var source = new JsonObject { ["id"] = "limited", ["state"] = state, ["reason"] = reason };
            if (code is not null)
            {
                source["code"] = code;
            }

            row["searched"]!["sources"] = new JsonArray(source);
            row["state"] = rowState;
            row["found"] = new JsonObject { ["count"] = count, ["ids"] = new JsonArray([.. Enumerable.Range(1, count).Select(i => JsonValue.Create(string.Create(CultureInfo.InvariantCulture, $"L-{lens}-{i}")))]) };
            row["limits"] = state == "degraded" ? [] : new JsonArray(new JsonObject { ["not"] = "what the source covers", ["why"] = reason });
        });
}
