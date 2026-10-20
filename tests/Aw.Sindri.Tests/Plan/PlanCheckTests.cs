using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Features.Plan;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Plan;

/// <summary>PlanChecks through 'plan check' (line spec §6.3): the gold plan passes, and each PLN rule has a planted fixture that names it.</summary>
public sealed class PlanCheckTests
{
    private static readonly string[] _fixItems = ["AC-6", "AC-9", "AC-11"];

    private static JsonNode Task(JsonNode n, string id) => n["tasks"]!.AsArray().Single(t => (string?)t!["id"] == id)!;

    private static JsonNode Phase(JsonNode n, string id) => n["phases"]!.AsArray().Single(t => (string?)t!["id"] == id)!;

    internal static async Task<LineCheckResponse> Run(LineFixture facts, LineStage stage = LineStage.Draft)
    {
        var result = await new PlanCheckService().Handle(new PlanCheckRequest(facts, stage), CancellationToken.None);
        return result.Value;
    }

    private static void Fails(LineCheckResponse response, string rule, string? item = null)
    {
        Assert.Contains(response.Findings, f => f.Rule == rule && (item is null || f.Item == item));
        Assert.False(response.Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_plan_passes_the_draft_stage_with_every_draft_rule_run()
    {
        var response = await Run(LineFixture.Plan());

        Assert.Empty(response.Findings);
        Assert.Equal(LineOutcome.Pass, response.Outcome);
        Assert.Equal("plan", response.Station);
        Assert.Equal(LineRuleStatus.Pass, response.Rules.Single(r => r.Rule == "PLN-ACC-001").Status);
        Assert.Equal(LineRuleStatus.NotRun, response.Rules.Single(r => r.Rule == "PLN-EXEC-001").Status);   // an evidence rule
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_plan_completes_the_evidence_stage_with_no_rule_left_unrun()
    {
        var response = await Run(LineFixture.Plan(), LineStage.Evidence);

        Assert.Empty(response.Findings);
        Assert.Equal(LineOutcome.Complete, response.Outcome);
        Assert.DoesNotContain(response.Rules, r => r.Status is LineRuleStatus.NotRun or LineRuleStatus.Fail);
        Assert.Equal(7, response.Rules.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_plan_whose_aw_packets_validation_failed_is_PLN_PKT_001_naming_each_failed_check()
    {
        var facts = LineFixture.Plan().Edit("validation.json", n =>
        {
            n["passed"] = false;
            n["failures"] = new JsonArray(new JsonObject { ["rule"] = "PKT-SCOPE-002", ["subject"] = "P2,P3", ["message"] = "two phases write src/a.ts" });
        });

        var response = await Run(facts);

        Fails(response, "PLN-PKT-001", "PKT-SCOPE-002");
        Assert.Contains("src/a.ts", response.Findings.Single(f => f.Rule == "PLN-PKT-001").Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_plan_with_no_validation_answer_leaves_PLN_PKT_001_not_run()
    {
        var response = await Run(LineFixture.Plan().Without("validation.json"));

        var rule = response.Rules.Single(r => r.Rule == "PLN-PKT-001");
        Assert.Equal(LineRuleStatus.NotRun, rule.Status);
        Assert.Contains(rule.Missing, m => m == "validation.json");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_design_item_in_no_phase_is_PLN_ACC_001()
    {
        var facts = LineFixture.Plan().Edit("tasks.json", n => Task(n, "P4")["acceptance"]!.AsArray().RemoveAt(1));

        Fails(await Run(facts), "PLN-ACC-001", "AC-9");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_design_item_in_two_phases_is_PLN_ACC_001()
    {
        var facts = LineFixture.Plan().Edit("tasks.json", n => Task(n, "P3")["acceptance"]!.AsArray().Insert(0, Task(n, "P1")["acceptance"]![0]!.DeepClone()));

        Fails(await Run(facts), "PLN-ACC-001", "AC-4");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_item_whose_text_differs_from_the_design_is_PLN_ACC_001()
    {
        var facts = LineFixture.Plan().Edit("tasks.json", n => Task(n, "P1")["acceptance"]![0]!["text"] = "If the time is bad, refuse.");

        Fails(await Run(facts), "PLN-ACC-001", "AC-4");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_superseded_design_item_needs_no_phase()
    {
        var facts = LineFixture.Plan()
            .Edit("design/acceptance.json", n => { var item = n["items"]!.AsArray().Single(i => (string?)i!["id"] == "AC-9")!; item["status"] = "superseded"; item["supersededBy"] = "AC-6"; })
            .Edit("tasks.json", n => Task(n, "P4")["acceptance"]!.AsArray().RemoveAt(1));

        Assert.DoesNotContain((await Run(facts)).Findings, f => f.Rule == "PLN-ACC-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_of_a_size_over_the_cap_is_PLN_SIZE_001()
    {
        Fails(await Run(LineFixture.Plan().Edit("tasks.json", n => Task(n, "P2")["size"] = "L")), "PLN-SIZE-001", "P2");
        Fails(await Run(LineFixture.Plan().Edit("tasks.json", n => Task(n, "P2")["size"] = "XXL")), "PLN-SIZE-001", "P2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_profile_cap_is_read_from_the_options()
    {
        var facts = LineFixture.Plan();
        facts["options.json"] = "{\"maxPhaseSize\":\"XS\"}";

        var response = await Run(facts);

        Assert.Equal(["P1", "P2", "P4"], [.. response.Findings.Where(f => f.Rule == "PLN-SIZE-001").Select(f => f.Item)]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fix_phase_whose_item_has_only_a_head_signal_is_PLN_SIG_001()
    {
        var facts = LineFixture.Plan().Edit("tasks.json", n => Task(n, "P2")["rules"]![0] = "kind:fix");

        Fails(await Run(facts), "PLN-SIG-001", "P2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fix_phase_whose_every_item_has_a_base_plus_head_signal_passes()
    {
        var facts = LineFixture.Plan()
            .Edit("tasks.json", n => Task(n, "P4")["rules"]![0] = "kind:fix")
            .Edit("design/signals.json", n => { foreach (var id in _fixItems) { foreach (var s in n["signals"]![id]!.AsArray()) { s!["runsAt"] = "base+head"; } } });

        Assert.DoesNotContain((await Run(facts)).Findings, f => f.Rule == "PLN-SIG-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_with_no_real_app_signal_is_PLN_SIG_001_unless_it_is_docs_or_config()
    {
        var edited = LineFixture.Plan().Edit("design/signals.json", n => { foreach (var s in n["signals"]!["AC-1"]!.AsArray()) { s!["kind"] = "test"; } });

        Fails(await Run(edited), "PLN-SIG-001", "P3");
        Assert.DoesNotContain((await Run(edited.Edit("tasks.json", n => Task(n, "P3")["rules"]![0] = "kind:docs"))).Findings, f => f.Rule == "PLN-SIG-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_with_two_dependencies_not_marked_waitsForMerge_is_PLN_STACK_001()
    {
        var facts = LineFixture.Plan().Edit("graph.json", n => Phase(n, "P4")["waitsForMerge"] = false);

        Fails(await Run(facts), "PLN-STACK-001", "P4");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stack_deeper_than_the_cap_is_PLN_STACK_001()
    {
        var facts = LineFixture.Plan();
        facts["options.json"] = "{\"maxStack\":2}";

        Fails(await Run(facts), "PLN-STACK-001", "P4");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dependency_that_does_not_exist_is_PLN_STACK_001_and_does_not_loop()
    {
        var facts = LineFixture.Plan().Edit("tasks.json", n => Task(n, "P2")["deps"] = new JsonArray("P9", "P2"));

        Fails(await Run(facts), "PLN-STACK-001", "P2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_the_executability_review_calls_not_executable_is_PLN_EXEC_001()
    {
        var facts = LineFixture.Plan().Edit("exec-review.json", n => { var p = Phase(n, "P2"); p["executable"] = false; p["contextMissing"] = new JsonArray("the route's tests"); });

        var response = await Run(facts, LineStage.Evidence);

        Fails(response, "PLN-EXEC-001", "P2");
        Assert.Contains("the route's tests", response.Findings.Single(f => f.Rule == "PLN-EXEC-001").Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_the_review_never_covered_is_PLN_EXEC_001()
    {
        var facts = LineFixture.Plan().Edit("exec-review.json", n => n["phases"]!.AsArray().RemoveAt(3));

        Fails(await Run(facts, LineStage.Evidence), "PLN-EXEC-001", "P4");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_with_fewer_hidden_scenarios_than_the_minimum_is_PLN_HOLD_003()
    {
        var facts = LineFixture.Plan().Edit("holdout.json", n => { var p = Phase(n, "P2"); p["count"] = 2; p["scenarioIds"] = new JsonArray("P2-H1", "P2-C"); });

        Fails(await Run(facts, LineStage.Evidence), "PLN-HOLD-003", "P2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_phase_with_no_canary_or_no_set_hash_is_PLN_HOLD_003()
    {
        Fails(await Run(LineFixture.Plan().Edit("holdout.json", n => Phase(n, "P1")["canaries"] = 0), LineStage.Evidence), "PLN-HOLD-003", "P1");
        Fails(await Run(LineFixture.Plan().Edit("holdout.json", n => Phase(n, "P3")["setHash"] = "abc"), LineStage.Evidence), "PLN-HOLD-003", "P3");
        Fails(await Run(LineFixture.Plan().Edit("holdout.json", n => n["phases"]!.AsArray().RemoveAt(3)), LineStage.Evidence), "PLN-HOLD-003", "P4");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_holdout_minimum_is_read_from_the_options()
    {
        var facts = LineFixture.Plan();
        facts["options.json"] = "{\"holdoutMin\":3}";

        var response = await Run(facts, LineStage.Evidence);

        Assert.Equal(4, response.Findings.Count(f => f.Rule == "PLN-HOLD-003"));
    }
}
