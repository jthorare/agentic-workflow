using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Testing.Line;

namespace Aw.Sindri.Tests.Design;

/// <summary>DesignPacketChecks through 'design check' (line spec §4.3): the gold design passes, and each DSN rule has a planted fixture that names it.</summary>
public sealed class DesignCheckTests
{
    internal static async Task<LineCheckResponse> Run(LineFixture facts, LineStage stage = LineStage.Draft)
    {
        var result = await new DesignCheckService().Handle(new DesignCheckRequest(facts, stage), CancellationToken.None);
        return result.Value;
    }

    private static void Fails(LineCheckResponse response, string rule, string? item = null)
    {
        Assert.Contains(response.Findings, f => f.Rule == rule && (item is null || f.Item == item));
        Assert.False(response.Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_design_passes_the_draft_stage_with_every_draft_rule_run()
    {
        var response = await Run(LineFixture.Design());

        Assert.Empty(response.Findings);
        Assert.Equal(LineOutcome.Pass, response.Outcome);
        Assert.Equal("design", response.Station);
        Assert.Equal(LineRuleStatus.Pass, response.Rules.Single(r => r.Rule == "DSN-COV-001").Status);
        Assert.Equal(LineRuleStatus.NotRun, response.Rules.Single(r => r.Rule == "DSN-LENS-001").Status);   // an evidence rule
        Assert.DoesNotContain(response.Rules, r => r.Status == LineRuleStatus.Fail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_gold_design_completes_the_evidence_stage_with_no_rule_left_unrun()
    {
        var response = await Run(LineFixture.Design(), LineStage.Evidence);

        Assert.Empty(response.Findings);
        Assert.Empty(response.Warnings);
        Assert.Equal(LineOutcome.Complete, response.Outcome);
        Assert.DoesNotContain(response.Rules, r => r.Status is LineRuleStatus.NotRun or LineRuleStatus.Fail);
        Assert.Equal(15, response.Rules.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rule_whose_files_are_absent_is_not_run_and_names_them_and_the_evidence_stage_is_then_incomplete()
    {
        var facts = LineFixture.Design().Without("signals.json");

        var draft = await Run(facts);
        var evidence = await Run(facts, LineStage.Evidence);

        var rule = draft.Rules.Single(r => r.Rule == "DSN-ACC-001");
        Assert.Equal(LineRuleStatus.NotRun, rule.Status);
        Assert.Contains(rule.Missing, m => m == "signals.json");
        Assert.Equal(LineOutcome.Pass, draft.Outcome);
        Assert.Equal(LineOutcome.Incomplete, evidence.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_surface_no_section_covers_is_DSN_COV_001()
    {
        var facts = LineFixture.Design().Edit("sections.json", n => n["sections"]!.AsArray().Single(s => (string?)s!["id"] == "sec-2.3")!["covers"] = new JsonArray());

        Fails(await Run(facts), "DSN-COV-001", "S23");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_excluded_surface_with_no_reason_is_DSN_COV_001()
    {
        var facts = LineFixture.Design().Edit("coverage.json", n => n["excluded"]![0]!["reason"] = " ");

        Fails(await Run(facts), "DSN-COV-001", "S14");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_coverage_row_that_disagrees_with_the_sections_is_DSN_COV_001()
    {
        var facts = LineFixture.Design().Edit("coverage.json", n => n["surfaces"]![0]!["sections"] = new JsonArray("sec-4"));

        Fails(await Run(facts), "DSN-COV-001", "S1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_question_nobody_answered_carried_or_deferred_is_DSN_COV_002()
    {
        var facts = LineFixture.Design().Edit("scope/questions.json", n => n["questions"]!.AsArray().Add(new JsonObject { ["id"] = "Q9", ["question"] = "Who owns it?" }));

        Fails(await Run(facts), "DSN-COV-002", "Q9");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_scope_question_deferred_with_an_owner_and_a_default_passes_and_without_a_default_fails()
    {
        var question = new Action<JsonNode>(n => n["questions"]!.AsArray().Add(new JsonObject { ["id"] = "Q9", ["question"] = "Who owns it?" }));
        var deferred = new Action<JsonNode, string?>((n, assume) => n["deferred"] = new JsonArray(new JsonObject { ["question"] = "Q9", ["owner"] = "owner", ["buildMayAssume"] = assume }));

        Assert.Empty((await Run(LineFixture.Design().Edit("scope/questions.json", question).Edit("coverage.json", n => deferred(n, "the sharer"))))
            .Findings);
        Fails(await Run(LineFixture.Design().Edit("scope/questions.json", question).Edit("coverage.json", n => deferred(n, null))), "DSN-COV-002", "Q9");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_item_with_no_signal_is_DSN_ACC_001()
    {
        var facts = LineFixture.Design().Edit("signals.json", n => n["signals"]!.AsObject().Remove("AC-2"));

        Fails(await Run(facts), "DSN-ACC-001", "AC-2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_signal_of_an_unknown_kind_or_place_is_DSN_ACC_001()
    {
        Fails(await Run(LineFixture.Design().Edit("signals.json", n => n["signals"]!["AC-2"]![0]!["kind"] = "vibes")), "DSN-ACC-001", "SG-2a");
        Fails(await Run(LineFixture.Design().Edit("signals.json", n => n["signals"]!["AC-2"]![0]!["runsAt"] = "tail")), "DSN-ACC-001", "SG-2a");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_superseded_item_needs_no_signal()
    {
        var facts = LineFixture.Design()
            .Edit("acceptance.json", n => { var item = n["items"]!.AsArray().Single(i => (string?)i!["id"] == "AC-10")!; item["status"] = "superseded"; item["supersededBy"] = "AC-4"; })
            .Edit("signals.json", n => n["signals"]!.AsObject().Remove("AC-10"));

        Assert.DoesNotContain((await Run(facts)).Findings, f => f.Rule == "DSN-ACC-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unmeasurable_term_in_a_signal_is_DSN_ACC_002()
    {
        var facts = LineFixture.Design().Edit("signals.json", n => n["signals"]!["AC-2"]![1]!["statement"] = "Opening the link in a browser feels fast and intuitive, with 0 notes shown.");

        Fails(await Run(facts), "DSN-ACC-002", "SG-2b");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_signal_with_no_number_and_no_reference_is_DSN_ACC_002()
    {
        var facts = LineFixture.Design().Edit("signals.json", n => n["signals"]!["AC-2"]![1]!["statement"] = "Opening the link in a browser shows the expired page.");

        Fails(await Run(facts), "DSN-ACC-002", "SG-2b");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edited_scope_item_text_is_DSN_ACC_003()
    {
        var facts = LineFixture.Design().Edit("acceptance.json", n => n["items"]![0]!["text"] = "When a member shares a note, the system shall maybe store the time.");

        var response = await Run(facts, LineStage.Evidence);

        Fails(response, "DSN-ACC-003", "AC-1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dropped_scope_item_is_DSN_ACC_003()
    {
        var facts = LineFixture.Design().Edit("acceptance.json", n => n["items"]!.AsArray().RemoveAt(1));

        Fails(await Run(facts, LineStage.Evidence), "DSN-ACC-003", "AC-2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_superseded_item_with_no_successor_is_DSN_ACC_003()
    {
        var facts = LineFixture.Design().Edit("acceptance.json", n => n["items"]![9]!["status"] = "superseded");

        Fails(await Run(facts, LineStage.Evidence), "DSN-ACC-003", "AC-10");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_superseded_item_flags_stop_2_and_an_added_item_that_cites_nothing_fails()
    {
        var superseded = LineFixture.Design().Edit("acceptance.json", n => { var item = n["items"]![9]!; item["status"] = "superseded"; item["supersededBy"] = "AC-4"; });
        var uncited = LineFixture.Design().Edit("acceptance.json", n => n["items"]![10]!["citations"] = new JsonArray("ZZ-1"));

        var flagged = await Run(superseded, LineStage.Evidence);

        Assert.Contains(flagged.Warnings, w => w.Rule == "DSN-ACC-003" && w.Item == "AC-10");
        Assert.DoesNotContain(flagged.Findings, f => f.Rule == "DSN-ACC-003");
        Fails(await Run(uncited, LineStage.Evidence), "DSN-ACC-003", "AC-11");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_with_no_recommended_option_is_DSN_DEC_001()
    {
        var facts = LineFixture.Design().Edit("decisions.json", n => n["decisions"]![2]!["options"]![0]!["recommended"] = false);

        Fails(await Run(facts), "DSN-DEC-001", "D3");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_with_one_option_is_DSN_DEC_001()
    {
        var facts = LineFixture.Design().Edit("decisions.json", n => n["decisions"]![1]!["options"]!.AsArray().RemoveAt(1));

        Fails(await Run(facts), "DSN-DEC-001", "D2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_affected_section_with_no_assumes_marker_is_DSN_DEC_001()
    {
        var sections = LineFixture.Design().Edit("sections.json", n => n["sections"]!.AsArray().Single(s => (string?)s!["id"] == "sec-3")!["assumes"] = new JsonArray());
        var spec = LineFixture.Design().EditText("spec.md", t => t.Replace("assumes: D3.a", "assumes: nothing", StringComparison.Ordinal));

        Fails(await Run(sections), "DSN-DEC-001", "D3");
        Fails(await Run(spec), "DSN-DEC-001", "D3");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_with_nothing_affected_is_DSN_DEC_001()
    {
        var facts = LineFixture.Design().Edit("decisions.json", n => n["decisions"]![0]!["affects"] = new JsonArray());

        Fails(await Run(facts), "DSN-DEC-001", "D1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_section_that_cites_nothing_is_DSN_CITE_001()
    {
        var facts = LineFixture.Design().Edit("sections.json", n => n["sections"]!.AsArray().Single(s => (string?)s!["id"] == "sec-2.1")!["cites"] = new JsonArray());

        Fails(await Run(facts), "DSN-CITE-001", "sec-2.1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_section_that_cites_an_id_that_resolves_to_nothing_is_DSN_CITE_001()
    {
        var facts = LineFixture.Design().Edit("sections.json", n => n["sections"]!.AsArray().Single(s => (string?)s!["id"] == "sec-2.1")!["cites"] = new JsonArray("AC-4", "AC-99"));

        Fails(await Run(facts), "DSN-CITE-001", "sec-2.1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edit_against_a_file_or_heading_the_base_lacks_is_DSN_XSPEC_001()
    {
        Fails(await Run(LineFixture.Design().Edit("contracts.json", n => n["edits"]![0]!["target"]!["file"] = "docs/decisions/0002-nothing.md")), "DSN-XSPEC-001", "X1");
        Fails(await Run(LineFixture.Design().Edit("contracts.json", n => n["edits"]![0]!["target"]!["heading"] = "0002: Nothing here")), "DSN-XSPEC-001", "X1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edit_that_quotes_text_the_target_does_not_hold_is_DSN_XSPEC_002()
    {
        var facts = LineFixture.Design().Edit("contracts.json", n => n["edits"]![0]!["quote"] = "A shared link expires after thirty days.");

        Fails(await Run(facts), "DSN-XSPEC-002", "X1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edit_with_no_numbered_row_in_the_spec_is_DSN_XSPEC_003()
    {
        var facts = LineFixture.Design().EditText("spec.md", t => t.Replace("1. Docs: edit decision 0001", "- Docs: edit decision 0001", StringComparison.Ordinal));

        Fails(await Run(facts), "DSN-XSPEC-003", "X1");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_edits_that_change_one_quoted_span_are_DSN_XSPEC_003()
    {
        var facts = LineFixture.Design()
            .Edit("contracts.json", n => n["edits"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "X2",
                ["lane"] = "product",
                ["row"] = 2,
                ["target"] = new JsonObject { ["file"] = "docs/decisions/0001-links-never-expire.md", ["heading"] = "0001: Shared links never expire" },
                ["quote"] = "A shared link stays valid until its note is deleted.",
                ["change"] = "x",
            }))
            .EditText("spec.md", t => t.TrimEnd() + "\n2. Product: restate decision 0001 (X2).\n");

        Fails(await Run(facts), "DSN-XSPEC-003", "X2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_forbidden_phrase_in_the_spec_is_DSN_STYLE_001()
    {
        var facts = LineFixture.Design().EditText("spec.md", t => t.Replace("works as it does today", "works as it does today, in order to stay safe", StringComparison.Ordinal));

        Fails(await Run(facts), "DSN-STYLE-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_required_section_and_an_over_long_section_are_DSN_STYLE_001()
    {
        var missing = LineFixture.Design().EditText("spec.md", t => t.Replace("## Data", "## Storage", StringComparison.Ordinal));
        var longer = LineFixture.Design().EditText("spec.md", t => t.Replace("## Data\n", "## Data\n" + string.Concat(Enumerable.Repeat("word ", 200)) + "\n", StringComparison.Ordinal));

        Assert.Contains((await Run(missing)).Findings, f => f.Rule == "DSN-STYLE-001" && f.Message.Contains("Data", StringComparison.Ordinal));
        Assert.Contains((await Run(longer)).Findings, f => f.Rule == "DSN-STYLE-001" && f.Message.Contains("160", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_spec_is_judged_by_the_style_pack_it_names_so_the_formal_rendering_passes_the_formal_pack_only()
    {
        var formal = LineFixture.Design().EditText("spec.md", _ => LineFixture.FormalSpec()).EditText("pack/style.json", _ => LineFixture.FormalPack());
        var wrongPack = LineFixture.Design().EditText("spec.md", _ => LineFixture.FormalSpec());

        Assert.DoesNotContain((await Run(formal)).Findings, f => f.Rule == "DSN-STYLE-001");
        Fails(await Run(wrongPack), "DSN-STYLE-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_last_round_with_a_HIGH_finding_that_nobody_accepted_is_DSN_LENS_001()
    {
        var facts = LineFixture.Design().EditText("lens-rounds.jsonl", t => t.TrimEnd() + "\n" +
            "{\"round\":2,\"lens\":\"security\",\"packHash\":\"sha256:c8b638addb288a6a4ed90dc729673fc379d36cc4e871c661fc744f20a74cad4d\",\"model\":\"fixture-lens-model\",\"notApplicable\":null,\"changedSections\":[],\"findings\":[{\"id\":\"F-9\",\"severity\":\"HIGH\",\"cite\":\"sec-2.2\",\"text\":\"x\",\"resolution\":\"y\"}]}\n");

        Fails(await Run(facts, LineStage.Evidence), "DSN-LENS-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_HIGH_finding_the_owner_accepted_at_the_lens_desk_does_not_fail_DSN_LENS_001()
    {
        var facts = LineFixture.Design().EditText("lens-rounds.jsonl", t => t.TrimEnd() + "\n" +
            "{\"round\":2,\"lens\":\"security\",\"packHash\":\"sha256:c8b638addb288a6a4ed90dc729673fc379d36cc4e871c661fc744f20a74cad4d\",\"model\":\"fixture-lens-model\",\"notApplicable\":null,\"changedSections\":[],\"findings\":[{\"id\":\"F-9\",\"severity\":\"HIGH\",\"cite\":\"sec-2.2\",\"text\":\"x\",\"resolution\":\"Accepted by the owner at the lens desk.\",\"acceptedByOwner\":true}]}\n");

        Assert.DoesNotContain((await Run(facts, LineStage.Evidence)).Findings, f => f.Rule == "DSN-LENS-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_round_that_skipped_a_lens_of_its_pack_is_DSN_LENS_001()
    {
        var facts = LineFixture.Design().EditText("lens-rounds.jsonl", t => string.Join('\n', t.Split('\n').Where(l => !l.Contains("\"round\":1,\"lens\":\"devex\"", StringComparison.Ordinal))));

        Fails(await Run(facts, LineStage.Evidence), "DSN-LENS-001", "devex");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lens_model_equal_to_the_writers_is_a_DSN_LENS_002_warning_that_never_fails()
    {
        var facts = LineFixture.Design().Edit("runs.json", n => n["runs"]![1]!["model"] = "fixture-writer-model");

        var response = await Run(facts, LineStage.Evidence);

        Assert.Contains(response.Warnings, w => w.Rule == "DSN-LENS-002");
        Assert.Equal(LineOutcome.Complete, response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lens_vendor_equal_to_the_writers_warns_only_when_the_profile_allows_more_than_one()
    {
        var same = LineFixture.Design().Edit("runs.json", n => n["runs"]![1]!["vendor"] = "fixture-a");

        Assert.DoesNotContain((await Run(same, LineStage.Evidence)).Warnings, w => w.Rule == "DSN-LENS-002");
        same["options.json"] = "{\"multiVendor\":true}";
        Assert.Contains((await Run(same, LineStage.Evidence)).Warnings, w => w.Rule == "DSN-LENS-002");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_medium_finding_with_no_written_resolution_is_DSN_LENS_003()
    {
        var facts = LineFixture.Design().EditText("lens-rounds.jsonl", t => t.Replace("Not changed: what the page shows beyond the fact is decision D2; the owner picks at stop 2.", string.Empty, StringComparison.Ordinal));

        Fails(await Run(facts, LineStage.Evidence), "DSN-LENS-003", "F-2");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_added_item_missing_from_the_escapes_is_a_DSN_ESC_001_warning_that_never_fails()
    {
        var facts = LineFixture.Design().Edit("escapes.json", n => n["escapes"]!.AsArray().RemoveAt(0));

        var response = await Run(facts, LineStage.Evidence);

        Assert.Contains(response.Warnings, w => w.Rule == "DSN-ESC-001" && w.Item == "AC-11");
        Assert.Equal(LineOutcome.Complete, response.Outcome);
    }
}
