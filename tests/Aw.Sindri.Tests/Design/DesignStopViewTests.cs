using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Design;

/// <summary>The stop-2 view (line spec §6.4, LN-API2): the gold design and plan read into the seven sections, live before evidence with the sections that lack data named in notYet.</summary>
public sealed class DesignStopViewTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly JsonSerializerOptions _indented = new(_json) { WriteIndented = true };

    internal static async Task<DesignStopView> Show(LineFixture facts)
    {
        var result = await new DesignShowService().Handle(new DesignShowRequest(facts), CancellationToken.None);
        return result.Value;
    }

    internal static string Serialized(DesignStopView view) => JsonSerializer.Serialize(view, _indented) + "\n";

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_design_and_plan_match_the_stop_golden()
    {
        var view = await Show(LineFixture.Plan());

        Assert.Empty(view.NotYet);
        Golden.Match(Serialized(view), typeof(DesignStopViewTests).Assembly, "Golden/Api/workflow-instance-design-stop/gold-stop.json", Normalization.None);   // LN-API2 serves this view as is
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_three_decisions_come_with_their_options_and_the_recommendation_named()
    {
        var view = await Show(LineFixture.Plan());

        Assert.Equal(["D1", "D2", "D3"], view.Decisions.Select(d => d.Decision), StringComparer.Ordinal);
        var d3 = view.Decisions[2];
        Assert.Equal("D3.a", d3.Recommended);
        Assert.Equal("answered", d3.Status);
        Assert.Equal("D3.a", d3.Chosen);
        Assert.Equal(["sec-2.2", "sec-3"], d3.Affects, StringComparer.Ordinal);
        Assert.Equal(["D3.a", "D3.b"], d3.Options.Select(o => o.Option), StringComparer.Ordinal);
        Assert.Equal([true, false], d3.Options.Select(o => o.Recommended));
        Assert.Contains("Swept by the nightly job", d3.Options[1].Answer, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_open_decision_has_no_chosen_option()
    {
        var view = await Show(LineFixture.Plan().Edit("design/decisions.json", n => { var d = n["decisions"]![2]!; d["status"] = "open"; _ = d.AsObject().Remove("chosen"); }));

        Assert.Equal("open", view.Decisions[2].Status);
        Assert.Null(view.Decisions[2].Chosen);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_item_the_design_added_is_listed_untrusted_and_a_superseded_item_names_its_successor()
    {
        var view = await Show(LineFixture.Plan().Edit("design/acceptance.json", n => { var item = n["items"]![9]!; item["status"] = "superseded"; item["supersededBy"] = "AC-4"; }));

        Assert.Equal(["AC-11"], view.Acceptance.Added.Select(i => i.Item), StringComparer.Ordinal);
        Assert.Equal("untrusted", view.Acceptance.Added[0].Trust);
        Assert.Equal([("AC-10", "AC-4")], view.Acceptance.Superseded.Select(s => (s.Item, s.SupersededBy)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_items_the_scope_had_are_not_added_when_the_scope_packet_is_there()
    {
        var facts = LineFixture.Plan();
        facts["scope/acceptance.json"] = LineFixture.Design()["scope/acceptance.json"];

        var view = await Show(facts);

        Assert.Equal(["AC-11"], view.Acceptance.Added.Select(i => i.Item), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_escapes_are_the_designs_own_list()
    {
        var view = await Show(LineFixture.Plan());

        Assert.Equal([("acceptance", "AC-11"), ("surface", "surface:route/export-download")], view.Escapes.Select(e => (e.Kind, e.Target)));
        Assert.All(view.Escapes, e => Assert.Equal("scoping", e.From));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_graph_has_the_phases_with_their_stacks_waves_bases_files_and_a_hidden_count_only()
    {
        var view = await Show(LineFixture.Plan());

        Assert.Equal(4, view.Graph.PrCount);
        Assert.Equal(["P1", "P2", "P4"], view.Graph.CriticalPath, StringComparer.Ordinal);
        Assert.Equal([["P1"], ["P2", "P3"], ["P4"]], view.Graph.Waves.Select(w => (string[])[.. w]));
        var p2 = view.Graph.Phases[1];
        Assert.Equal(("P2", "S", "feature", 1, 2, "dependency-head P1", false), (p2.Phase, p2.Size, p2.Kind, p2.Stack, p2.Wave, p2.Base, p2.WaitsForMerge));
        Assert.Equal(["AC-2", "AC-3", "AC-5", "AC-8"], p2.Items, StringComparer.Ordinal);
        Assert.Equal(2, p2.HiddenScenarios);
        Assert.Equal(["tests/revoke.test.ts"], p2.TestChanges.Select(t => t.Path), StringComparer.Ordinal);
        Assert.Contains("a revoked link and an expired link both end the link", p2.TestChanges[0].Reason, StringComparison.Ordinal);
        Assert.Equal(["src/sharing/links.ts", "src/sharing/routes.ts", "new: src/sharing/expired-page.ts"], p2.AllowedFiles, StringComparer.Ordinal);
        Assert.True(view.Graph.Phases[3].WaitsForMerge);
        Assert.Equal(2, view.Graph.Phases[2].Stack);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_graph_follows_the_profiles_parallel_when_given_in_the_options()
    {
        var facts = LineFixture.Plan();
        facts["options.json"] = "{\"parallel\":1}";

        var view = await Show(facts);

        Assert.Equal(4, view.Graph.Waves.Count);
        Assert.Equal(1, view.Graph.Parallel);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_trace_runs_phase_by_phase_with_each_items_signals()
    {
        var view = await Show(LineFixture.Plan());

        Assert.Equal(11, view.Trace.Count);
        Assert.Equal(("AC-4", "P1"), (view.Trace[0].Item, view.Trace[0].Phase));
        Assert.Equal(["SG-4a", "SG-4b"], view.Trace[0].Signals.Select(s => s.Signal), StringComparer.Ordinal);
        Assert.Equal(("api-transcript", "head"), (view.Trace[0].Signals[0].Kind, view.Trace[0].Signals[0].RunsAt));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_coverage_table_lists_each_surface_with_its_sections_or_its_exclusion()
    {
        var view = await Show(LineFixture.Plan());

        Assert.Equal(23, view.Coverage.Count);
        Assert.Equal(("S1", "feature:sharing", "sec-1"), (view.Coverage[0].Scope, view.Coverage[0].Key, view.Coverage[0].Sections[0]));
        var excluded = Assert.Single(view.Coverage, c => c.Excluded is not null);
        Assert.Equal("S14", excluded.Scope);
        Assert.Contains("linkRevocation", excluded.Excluded, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_spec_links_each_assumes_marker_to_its_decision_and_option()
    {
        var view = await Show(LineFixture.Plan());

        Assert.StartsWith("# Shared links expire after a chosen time", view.Spec.Text, StringComparison.Ordinal);
        Assert.Contains(new StopAssumes("sec-2.2", "D3", "D3.a"), view.Spec.Assumes);
        Assert.Equal(["D3", "D2", "D1", "D3"], view.Spec.Assumes.Select(a => a.Decision), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_contract_edit_shows_its_three_checks_and_the_ones_that_could_not_run_say_so()
    {
        var plain = await Show(LineFixture.Plan());
        var facts = LineFixture.Plan();
        facts["repo/docs/decisions/0001-links-never-expire.md"] = LineFixture.Design()["repo/docs/decisions/0001-links-never-expire.md"];
        var withRepo = await Show(facts);

        var edit = plain.Contracts[0];
        Assert.Equal(("X1", "docs", "docs/decisions/0001-links-never-expire.md", 1), (edit.Edit, edit.Lane, edit.File, edit.Row));
        Assert.Equal([("DSN-XSPEC-001", "not-run"), ("DSN-XSPEC-002", "not-run"), ("DSN-XSPEC-003", "pass")], edit.Checks.Select(c => (c.Rule, c.Status)));
        Assert.All(withRepo.Contracts[0].Checks, c => Assert.Equal("pass", c.Status));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_contract_edit_with_a_failing_check_says_fail()
    {
        var facts = LineFixture.Plan().Edit("design/contracts.json", n => n["edits"]![0]!["quote"] = "A shared link expires after thirty days.");
        facts["repo/docs/decisions/0001-links-never-expire.md"] = LineFixture.Design()["repo/docs/decisions/0001-links-never-expire.md"];

        var view = await Show(facts);

        Assert.Equal("fail", view.Contracts[0].Checks.Single(c => c.Rule == "DSN-XSPEC-002").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_models_come_from_the_run_records_and_a_shared_model_is_a_warning_shown_with_them()
    {
        var gold = await Show(LineFixture.Plan());
        var shared = await Show(LineFixture.Plan().Edit("design/runs.json", n => n["runs"]![1]!["model"] = "fixture-writer-model"));

        Assert.Equal(("fixture-writer-model", "fixture-a"), (gold.Models.Writer!.Model, gold.Models.Writer.Vendor));
        Assert.Equal(("fixture-lens-model", "fixture-b"), (gold.Models.Lens!.Model, gold.Models.Lens.Vendor));
        Assert.Empty(gold.Models.Warnings);
        Assert.Contains("fixture-writer-model", Assert.Single(shared.Models.Warnings), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_lens_rounds_are_a_timeline_with_each_lens_and_its_findings()
    {
        var view = await Show(LineFixture.Plan());

        Assert.Equal([1, 2], view.LensRounds.Select(r => r.Round));
        Assert.Equal(["product", "architecture", "design", "devex", "security"], view.LensRounds[0].Lenses.Select(l => l.Lens), StringComparer.Ordinal);
        var finding = view.LensRounds[0].Lenses[1].Findings[0];
        Assert.Equal(("F-1", "HIGH", "sec-2.2"), (finding.Finding, finding.Severity, finding.Cite));
        Assert.Contains("Fixed", finding.Resolution, StringComparison.Ordinal);
        Assert.Empty(view.LensRounds[1].Lenses.SelectMany(l => l.Findings));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_finding_the_owner_accepted_is_listed_first_as_a_known_risk()
    {
        var accepted = LineFixture.Plan().EditText("design/lens-rounds.jsonl", t => t.TrimEnd() + "\n" +
            "{\"round\":2,\"lens\":\"security\",\"packHash\":\"sha256:c8b638addb288a6a4ed90dc729673fc379d36cc4e871c661fc744f20a74cad4d\",\"model\":\"fixture-lens-model\",\"notApplicable\":null,\"changedSections\":[],\"findings\":[{\"id\":\"F-9\",\"severity\":\"HIGH\",\"cite\":\"sec-2.2\",\"text\":\"The token is guessable.\",\"resolution\":\"Accepted by the owner at the lens desk.\",\"acceptedByOwner\":true}]}\n");

        var view = await Show(accepted);

        var risk = Assert.Single(view.Accepted);
        Assert.Equal(("F-9", "security", "HIGH"), (risk.Finding, risk.Lens, risk.Severity));
        Assert.Equal("The token is guessable.", risk.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_freeze_refusal_is_the_banner_naming_the_rule()
    {
        var facts = LineFixture.Plan();
        facts["freezeRefused.json"] = "{\"rule\":\"SND-LINE-001\",\"message\":\"the design packet changed after the approve\"}";

        var view = await Show(facts);

        Assert.Equal(new StopFreezeRefused("SND-LINE-001", "the design packet changed after the approve"), view.FreezeRefused);
        Assert.Null((await Show(LineFixture.Plan())).FreezeRefused);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Before_evidence_each_section_without_data_is_empty_and_named_in_notYet()
    {
        var facts = LineFixture.Plan().Without("design/lens-rounds.jsonl").Without("design/runs.json").Without("tasks.json").Without("holdout.json");

        var view = await Show(facts);

        Assert.Equal(["accepted", "models", "graph", "trace", "lensRounds"], view.NotYet, StringComparer.Ordinal);
        Assert.Empty(view.LensRounds);
        Assert.Empty(view.Graph.Phases);
        Assert.Empty(view.Trace);
        Assert.Null(view.Models.Writer);
        Assert.NotEmpty(view.Decisions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_nothing_at_all_every_section_is_not_yet()
    {
        var view = await Show([]);

        Assert.Equal(["accepted", "models", "decisions", "acceptance", "escapes", "graph", "trace", "coverage", "spec", "contracts", "lensRounds"], view.NotYet, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Json_names_the_sections_as_the_api_lists_them()
    {
        var node = JsonNode.Parse(Serialized(await Show(LineFixture.Plan())))!;

        Assert.Equal(["notYet", "accepted", "models", "decisions", "acceptance", "escapes", "graph", "trace", "coverage", "spec", "contracts", "lensRounds", "freezeRefused"], node.AsObject().Select(p => p.Key), StringComparer.Ordinal);
    }
}
