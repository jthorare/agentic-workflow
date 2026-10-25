using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>
/// The desk blocks that read the context engine's JSON and the brief (SC-K2): read-brief, anchor, reanchor, gather and propose. Each turns what
/// the human hands the desk (the brief's text, the views of `context lenses|code|history --json`, the text shape's answer, the propose answer)
/// into the Step's facts, in the shapes the recipe and schemas/scoping declare.
/// </summary>
public sealed class ScopingDeskBlocksTests
{
    private const string BriefText = "# Shared links expire after a chosen time\n\n## 1. Problem\n\nA shared link stays open for ever.\n\n## 2. What we want\n\n- Pick a time after which the link stops working.\n\n## 3. Not now\n\n- Changing the time on an existing link.\n";

    private static async Task<ScopingDeskResponse> Run(string block, ScopingFacts facts) =>
        (await new ScopingDeskService().Handle(new ScopingDeskRequest(block, facts), CancellationToken.None)).Value;

    private static JsonNode Out(ScopingDeskResponse response, string key) => JsonNode.Parse(response.Outputs[key])!;

    private static string[] Strings(JsonNode node) => [.. node.AsArray().Select(static x => x!.GetValue<string>())];

    private static ScopingFacts ReadBriefFacts(string text = BriefText, string? section = null) => new()
    {
        ["scopeInput"] = section is null ? """{"kind":"brief","repo":"fixture-notes","path":"expiry.md"}""" : $$"""{"kind":"brief","repo":"fixture-notes","path":"expiry.md","section":"{{section}}"}""",
        ["briefSource"] = new JsonObject { ["path"] = "expiry.md", ["text"] = text, ["version"] = "sha256:0123456789ab", ["fetchedAt"] = "2026-10-16T09:00:00Z" }.ToJsonString(),
        ["contextQuery"] = ScopingFacts.View("contextQuery"),
    };

    [Fact, Trait("Lane", "unit")]
    public async Task Read_brief_writes_the_brief_with_its_one_ref_and_the_text_shapes_anchors()
    {
        var response = await Run("read-brief", ReadBriefFacts());

        Assert.Equal("step.scope.read-brief", response.Block);
        Assert.Equal("read", response.Outcome);
        var brief = Out(response, "brief");
        Assert.Equal(BriefText, brief["text"]!.GetValue<string>());
        Assert.Equal("Shared links expire after a chosen time", brief["subject"]!.GetValue<string>());
        var reference = Assert.Single(brief["refs"]!.AsArray())!;
        Assert.Equal("R1", reference["id"]!.GetValue<string>());
        Assert.Equal("brief", reference["source"]!.GetValue<string>());
        Assert.Equal("expiry.md", reference["key"]!.GetValue<string>());
        Assert.Equal("untrusted", reference["trust"]!.GetValue<string>());
        Assert.Equal("2026-10-16T09:00:00Z", reference["fetchedAt"]!.GetValue<string>());
        Assert.Equal(Strings(JsonNode.Parse(ScopingFacts.View("contextQuery"))!["suggestedAnchors"]!), Strings(Out(response, "anchors")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Read_brief_with_a_section_keeps_that_heading_to_the_next_one()
    {
        var response = await Run("read-brief", ReadBriefFacts(section: "2"));

        var brief = Out(response, "brief");
        Assert.StartsWith("## 2. What we want", brief["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.DoesNotContain("## 3.", brief["text"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("expiry.md#2", brief["refs"]![0]!["key"]!.GetValue<string>());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("   \n  ")]
    public async Task Read_brief_of_an_empty_file_reports_empty_and_writes_no_facts(string text)
    {
        var response = await Run("read-brief", ReadBriefFacts(text));

        Assert.Equal("empty", response.Outcome);
        Assert.Empty(response.Outputs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Read_brief_with_a_section_the_file_lacks_reports_empty()
    {
        var response = await Run("read-brief", ReadBriefFacts(section: "9"));

        Assert.Equal("empty", response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Read_brief_without_the_text_shapes_answer_is_refused_naming_the_file_to_write()
    {
        var result = await new ScopingDeskService().Handle(new ScopingDeskRequest("read-brief", ReadBriefFacts().Without("contextQuery")), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("contextQuery", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Anchor_takes_the_brief_text_s_suggested_anchors()
    {
        var facts = new ScopingFacts { ["brief"] = ScopingFacts.Expiry()["brief"], ["contextQuery"] = ScopingFacts.View("contextQuery") };

        var response = await Run("anchor", facts);

        Assert.Equal("step.scope.anchor", response.Block);
        Assert.Equal("anchored", response.Outcome);
        Assert.Equal(Strings(JsonNode.Parse(ScopingFacts.View("contextQuery"))!["suggestedAnchors"]!), Strings(Out(response, "anchors")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Anchor_keeps_every_suggested_anchor_past_twenty_without_a_warning()
    {
        var many = new JsonObject { ["suggestedAnchors"] = new JsonArray([.. Enumerable.Range(1, 25).Select(static i => (JsonNode?)JsonValue.Create(string.Create(CultureInfo.InvariantCulture, $"feature:f{i}")))]) };
        var facts = new ScopingFacts { ["brief"] = ScopingFacts.Expiry()["brief"], ["contextQuery"] = many.ToJsonString() };

        var response = await Run("anchor", facts);

        Assert.Equal(25, Out(response, "anchors").AsArray().Count);
        Assert.Empty(response.Warnings);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Reanchor_derives_anchors_from_the_map_s_node_keys_and_keeps_the_changed_files()
    {
        var facts = new ScopingFacts
        {
            ["scopeMap"] = ScopingFacts.Expiry()["scopeMap"],
            ["anchors"] = ScopingFacts.Expiry()["anchors"],
            ["lenses"] = ScopingFacts.Expiry()["lenses"],
            ["contextQuery"] = ScopingFacts.View("contextQuery"),
        };

        var response = await Run("reanchor", facts);

        Assert.Equal("anchored", response.Outcome);
        var anchors = Strings(Out(response, "anchors"));
        Assert.Contains("surface:route/links-create", anchors, StringComparer.Ordinal);
        Assert.Contains("code:migrations/006_link_expiry.sql", anchors, StringComparer.Ordinal);   // a changed file the brief anchored stays
        Assert.Equal(anchors.Length, anchors.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_maps_the_three_views_into_all_four_lenses_and_their_coverage_rows()
    {
        var response = await Run("gather", GatherFacts());

        Assert.Equal("step.scope.gather", response.Block);
        Assert.Equal("gathered", response.Outcome);
        var lenses = Out(response, "lenses");
        var rows = Out(response, "coverage")["lenses"]!.AsArray();
        Assert.Equal(["code", "product", "related", "history"], rows.Select(static r => r!["lens"]!.GetValue<string>()), StringComparer.Ordinal);
        foreach (var lens in new[] { "code", "product", "related", "history" })
        {
            Assert.Equal(Strings(JsonNode.Parse(ScopingFacts.Expiry()["anchors"])!), Strings(lenses[lens]!["anchors"]!));
            Assert.Equal("v1", lenses[lens]!["mapVersion"]!.GetValue<string>());
        }

        Assert.Equal(2, lenses["related"]!["depth"]!.GetValue<int>());
        Assert.All(rows, static r => Assert.Equal("found", r!["state"]!.GetValue<string>()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_numbers_every_found_item_once_per_lens_and_the_row_lists_the_same_ids()
    {
        var response = await Run("gather", GatherFacts());

        var lenses = Out(response, "lenses");
        var rows = Out(response, "coverage")["lenses"]!.AsArray();
        foreach (var lens in new[] { "code", "product", "related", "history" })
        {
            var held = lenses[lens]!["nodes"]!.AsArray().Select(static n => n!["lensItem"]!.GetValue<string>())
                .Concat(lenses[lens]!["items"]!.AsArray().Select(static i => i!["lensItem"]!.GetValue<string>())).ToList();
            Assert.Equal(held.Count, held.Distinct(StringComparer.Ordinal).Count());
            Assert.All(held, id => Assert.StartsWith($"L-{lens}-", id, StringComparison.Ordinal));
            var row = rows.Single(r => r!["lens"]!.GetValue<string>() == lens)!;
            Assert.Equal(held, [.. Strings(row["found"]!["ids"]!)], StringComparer.Ordinal);
            Assert.Equal(held.Count, row["found"]!["count"]!.GetValue<int>());
            Assert.Equal(held.Count, lenses[lens]!["foundCount"]!.GetValue<int>());
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_output_passes_the_lens_and_coverage_rules_beside_the_gold_facts()
    {
        var response = await Run("gather", GatherFacts());
        var facts = ScopingFacts.Expiry();
        facts["lenses"] = response.Outputs["lenses"];
        facts["coverage"] = response.Outputs["coverage"];

        var report = ScopePacketChecks.Run(new ScopeFacts(facts), ScopeStage.Evidence);   // SCP-LENS-001 is an evidence-stage rule

        foreach (var rule in new[] { "SCP-COV-001", "SCP-COV-002", "SCP-LENS-001", "SCP-SCHEMA-001" })
        {
            Assert.Equal(ScopeRuleStatus.Pass, report.Rules.Single(r => string.Equals(r.Rule, rule, StringComparison.Ordinal)).Status);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_records_the_index_not_built_limit_on_the_code_lens_from_the_views_own_words()
    {
        var response = await Run("gather", GatherFacts());

        var code = Out(response, "coverage")["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "code")!;
        Assert.Contains(code["searched"]!["sources"]!.AsArray(), s => s!["state"]!.GetValue<string>() == "unavailable" && s["reason"]!.GetValue<string>() == "index-not-built");
        Assert.Equal("index-not-built", Assert.Single(code["limits"]!.AsArray())!["why"]!.GetValue<string>());
    }

    // The live lenses view lists the code index among its searched sources (it feeds the code band's symbols); copied into the related lens,
    // index-not-built there was a limit on the wrong lens and failed SCP-COV-002 on every run until the index was built (FRN-4965 on web-app).
    [Fact, Trait("Lane", "unit")]
    public async Task Gather_keeps_the_lenses_views_code_index_source_out_of_the_related_lens_so_an_unbuilt_index_passes_the_check()
    {
        var facts = GatherFacts();
        var lenses = JsonNode.Parse(facts["lensesView"])!.AsObject();
        lenses["searched"]!["sources"]!.AsArray().Add(new JsonObject { ["id"] = "code-index", ["state"] = "unavailable", ["reason"] = "index-not-built" });
        facts["lensesView"] = lenses.ToJsonString();
        var response = await Run("gather", facts);
        var check = ScopingFacts.Expiry();
        check["lenses"] = response.Outputs["lenses"];
        check["coverage"] = response.Outputs["coverage"];

        var related = Out(response, "coverage")["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "related")!;
        var report = ScopePacketChecks.Run(new ScopeFacts(check), ScopeStage.Draft);

        Assert.DoesNotContain(related["searched"]!["sources"]!.AsArray(), s => s!["id"]!.GetValue<string>() == "code-index");
        Assert.DoesNotContain(report.Findings, f => f.Rule == "SCP-COV-002");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_records_a_catching_up_code_index_as_a_limit_that_passes_the_check_and_keeps_both_commits_and_the_pending_files()
    {
        var facts = GatherFacts();
        var code = JsonNode.Parse(facts["codeView"])!.AsObject();
        var pending = new JsonArray("src/Worker.cs", "src/New.cs");
        code["searched"]!["sources"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "code-index",
            ["state"] = "ok",
            ["reason"] = "catching-up",
            ["catchingUp"] = new JsonObject { ["indexCommit"] = "0123456789abcdef0123456789abcdef01234567", ["mapCommit"] = "1234567890abcdef1234567890abcdef12345678", ["pending"] = pending },
        });
        facts["codeView"] = code.ToJsonString();
        var response = await Run("gather", facts);
        var check = ScopingFacts.Expiry();
        check["lenses"] = response.Outputs["lenses"];
        check["coverage"] = response.Outputs["coverage"];

        var row = Out(response, "coverage")["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "code")!;
        var source = row["searched"]!["sources"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == "code-index")!;
        Assert.Contains(row["limits"]!.AsArray(), l => l!["why"]!.GetValue<string>() == "catching-up");
        Assert.Equal(["src/Worker.cs", "src/New.cs"], source["catchingUp"]!["pending"]!.AsArray().Select(static p => p!.GetValue<string>()), StringComparer.Ordinal);
        var report = ScopePacketChecks.Run(new ScopeFacts(check), ScopeStage.Evidence);
        Assert.Equal(ScopeRuleStatus.Pass, report.Rules.Single(static r => string.Equals(r.Rule, "SCP-COV-002", StringComparison.Ordinal)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_over_the_code_lens_golden_whose_commit_is_not_held_records_both_limits_and_passes_the_coverage_rules()
    {
        var facts = GatherFacts();
        facts["codeView"] = ScopingFacts.View("codeView.commit-not-held");   // context's own golden: context-code/commit-not-held.json
        var response = await Run("gather", facts);
        var check = ScopingFacts.Expiry();
        check["lenses"] = response.Outputs["lenses"];
        check["coverage"] = response.Outputs["coverage"];

        var code = Out(response, "coverage")["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "code")!;
        Assert.Equal(["commit-not-held", "index-not-built"], code["limits"]!.AsArray().Select(static l => l!["why"]!.GetValue<string>()).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var report = ScopePacketChecks.Run(new ScopeFacts(check), ScopeStage.Evidence);
        Assert.Equal(ScopeRuleStatus.Pass, report.Rules.Single(static r => string.Equals(r.Rule, "SCP-COV-002", StringComparison.Ordinal)).Status);
        Assert.Equal(ScopeRuleStatus.Pass, report.Rules.Single(static r => string.Equals(r.Rule, "SCP-LENS-001", StringComparison.Ordinal)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_coverage_with_a_never_synced_source_keeps_its_sync_pair_and_validates_against_the_coverage_schema()
    {
        var facts = GatherFacts();
        var history = JsonNode.Parse(ScopingFacts.View("historyView"))!;
        history["searched"]!["sources"]!.AsArray().Add(JsonNode.Parse("""{"id":"linear","state":"unavailable","reason":"not-synced","code":null,"sync":{"state":"failed","code":"SND-CONTEXT-101","failures":3}}"""));
        facts["historyView"] = history.ToJsonString();

        var response = await Run("gather", facts);

        var coverage = Out(response, "coverage");
        var row = coverage["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "history")!;
        Assert.Equal("failed", row["searched"]!["sources"]!.AsArray().Single(s => s!["id"]!.GetValue<string>() == "linear")!["sync"]!["state"]!.GetValue<string>());
        Assert.Empty(ScopeSchemas.Errors("coverage", coverage));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_over_the_history_lens_golden_finds_its_changes_and_records_the_unconfigured_sources_as_a_limit()
    {
        var facts = GatherFacts();
        facts["historyView"] = ScopingFacts.View("historyView.golden");   // context's own golden: context-history/two-anchors.json
        var response = await Run("gather", facts);
        var check = ScopingFacts.Expiry();
        check["lenses"] = response.Outputs["lenses"];
        check["coverage"] = response.Outputs["coverage"];

        var history = Out(response, "coverage")["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "history")!;
        Assert.Equal(("found", 4), (history["state"]!.GetValue<string>(), history["found"]!["count"]!.GetValue<int>()));
        Assert.Equal("not-configured", Assert.Single(history["limits"]!.AsArray())!["why"]!.GetValue<string>());
        Assert.All(Out(response, "lenses")["history"]!["items"]!.AsArray(), static i => Assert.StartsWith("commit:", i!["citations"]![0]!.GetValue<string>(), StringComparison.Ordinal));
        var report = ScopePacketChecks.Run(new ScopeFacts(check), ScopeStage.Evidence);
        Assert.Equal(ScopeRuleStatus.Pass, report.Rules.Single(static r => string.Equals(r.Rule, "SCP-COV-002", StringComparison.Ordinal)).Status);
        Assert.Equal(ScopeRuleStatus.Pass, report.Rules.Single(static r => string.Equals(r.Rule, "SCP-LENS-001", StringComparison.Ordinal)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_with_a_degraded_source_makes_the_check_report_regather()
    {
        var facts = GatherFacts();
        facts["historyView"] = WithDegraded(ScopingFacts.View("historyView"));
        var response = await Run("gather", facts);
        var check = ScopingFacts.Expiry();
        check["lenses"] = response.Outputs["lenses"];
        check["coverage"] = response.Outputs["coverage"];

        var report = ScopePacketChecks.Run(new ScopeFacts(check), ScopeStage.Draft);

        Assert.Contains(report.Findings, f => f.Rule == "SCP-COV-002");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_without_the_history_view_records_the_source_as_not_configured_and_the_row_as_not_searched()
    {
        var response = await Run("gather", GatherFacts().Without("historyView"));

        var history = Out(response, "coverage")["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "history")!;
        Assert.Equal("not-searched", history["state"]!.GetValue<string>());
        Assert.Equal("not-configured", Assert.Single(history["limits"]!.AsArray())!["why"]!.GetValue<string>());
        Assert.Equal(0, history["found"]!["count"]!.GetValue<int>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Gather_without_the_lenses_view_is_refused_naming_the_file_to_pass()
    {
        var result = await new ScopingDeskService().Handle(new ScopingDeskRequest("gather", GatherFacts().Without("lensesView")), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("lensesView", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_gather_mapping_is_one_function_the_desk_and_the_block_runtime_both_call()
    {
        var facts = GatherFacts();
        var assembled = LensGather.Assemble(JsonNode.Parse(facts["lensesView"]), JsonNode.Parse(facts["codeView"]), JsonNode.Parse(facts["historyView"]), Strings(JsonNode.Parse(facts["anchors"])!));

        Assert.Equal(["code", "history", "product", "related"], assembled.Lenses.Select(static p => p.Key).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(4, assembled.Coverage["lenses"]!.AsArray().Count);
    }

    // A view that couldn't find an anchor says so (missingAnchors); the gather listed every asked anchor as searched and passed. Each missing anchor
    // is now out of the row's searched anchors and a recorded limit (anchor-missing), so the check and the stop see it.
    [Fact, Trait("Lane", "unit")]
    public void An_anchor_a_view_could_not_find_is_a_recorded_limit_not_a_searched_anchor()
    {
        var facts = GatherFacts();
        var anchors = Strings(JsonNode.Parse(facts["anchors"])!);
        var lenses = JsonNode.Parse(facts["lensesView"])!.AsObject();
        lenses["missingAnchors"] = new JsonArray("feature:nowhere");
        var code = JsonNode.Parse(facts["codeView"])!.AsObject();
        code["missingAnchors"] = new JsonArray("surface:screen/gone");

        var assembled = LensGather.Assemble(lenses, code, JsonNode.Parse(facts["historyView"]), [.. anchors, "feature:nowhere", "surface:screen/gone"]);
        var rows = assembled.Coverage["lenses"]!.AsArray().ToDictionary(static r => r!["lens"]!.GetValue<string>(), StringComparer.Ordinal);

        Assert.DoesNotContain("feature:nowhere", Strings(rows["product"]!["searched"]!["anchors"]!), StringComparer.Ordinal);
        Assert.Contains(rows["product"]!["limits"]!.AsArray(), l => l!["why"]!.GetValue<string>() == "anchor-missing" && l["not"]!.GetValue<string>() == "feature:nowhere");
        Assert.Contains(rows["code"]!["limits"]!.AsArray(), l => l!["why"]!.GetValue<string>() == "anchor-missing" && l["not"]!.GetValue<string>() == "surface:screen/gone");
        Assert.Contains(rows["code"]!["limits"]!.AsArray(), l => l!["not"]!.GetValue<string>() == "feature:nowhere");
        Assert.DoesNotContain(rows["history"]!["limits"]!.AsArray(), l => l!["why"]!.GetValue<string>() == "anchor-missing");
        Assert.NotNull(ScopeFixLines.ByName("anchor-missing"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Propose_writes_each_surfaces_graph_status_from_the_context_engines_answer()
    {
        var facts = new ScopingFacts { ["scopeMap"] = Unproposed(), ["proposeResult"] = ScopingFacts.View("proposeResult") };

        var response = await Run("propose", facts);

        Assert.Equal("step.scope.propose", response.Block);
        Assert.Equal("proposed", response.Outcome);
        var surfaces = Out(response, "scopeMap")["surfaces"]!.AsArray();
        Assert.Equal("new", surfaces.Single(s => s!["id"]!.GetValue<string>() == "S22")!["graphStatus"]!.GetValue<string>());
        Assert.Equal("new", surfaces.Single(s => s!["id"]!.GetValue<string>() == "S23")!["graphStatus"]!.GetValue<string>());
        Assert.Equal("surface:dialog/share-expiry", surfaces.Single(s => s!["id"]!.GetValue<string>() == "S22")!["key"]!.GetValue<string>());   // the node the engine proposed
        Assert.Equal("mapped", surfaces.Single(s => s!["id"]!.GetValue<string>() == "S2")!["graphStatus"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Propose_with_no_answer_reports_skipped_and_marks_the_surfaces_with_no_node_unknown()
    {
        var response = await Run("propose", new ScopingFacts { ["scopeMap"] = Unproposed() });

        Assert.Equal("skipped", response.Outcome);
        var surfaces = Out(response, "scopeMap")["surfaces"]!.AsArray();
        Assert.Equal("unknown", surfaces.Single(s => s!["id"]!.GetValue<string>() == "S22")!["graphStatus"]!.GetValue<string>());
        Assert.Equal("mapped", surfaces.Single(s => s!["id"]!.GetValue<string>() == "S2")!["graphStatus"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Propose_leaves_every_other_field_of_the_map_as_it_was()
    {
        var map = Unproposed();
        var response = await Run("propose", new ScopingFacts { ["scopeMap"] = map, ["proposeResult"] = ScopingFacts.View("proposeResult") });

        var before = JsonNode.Parse(map)!.AsObject();
        var after = Out(response, "scopeMap").AsObject();
        Assert.Equal(before["workstreams"]!.ToJsonString(), after["workstreams"]!.ToJsonString());
        Assert.Equal(before["excluded"]!.ToJsonString(), after["excluded"]!.ToJsonString());
        Assert.Equal(before["surfaces"]!.AsArray().Count, after["surfaces"]!.AsArray().Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_desk_names_all_the_scoping_block_steps_it_covers()
    {
        var result = await new ScopingDeskService().Handle(new ScopingDeskRequest("size", new ScopingFacts()), CancellationToken.None);

        Assert.True(result.IsError);
        foreach (var block in new[] { "step.scope.read-brief", "step.scope.gather", "step.scope.anchor", "step.scope.reanchor", "step.scope.propose" })
        {
            Assert.Contains(block, result.Error.Message, StringComparison.Ordinal);
        }
    }

    private static ScopingFacts GatherFacts() => new()
    {
        ["anchors"] = ScopingFacts.Expiry()["anchors"],
        ["lensesView"] = ScopingFacts.View("lensesView"),
        ["codeView"] = ScopingFacts.View("codeView"),
        ["historyView"] = ScopingFacts.View("historyView"),
    };

    /// <summary>The gold map as it stood before propose: S22 and S23 have no node key yet.</summary>
    private static string Unproposed() => ScopingFacts.Expiry().Edit("scopeMap", map =>
    {
        foreach (var surface in map["surfaces"]!.AsArray().OfType<JsonObject>().Where(static s => string.Equals(s["id"]!.GetValue<string>(), "S22", StringComparison.Ordinal) || string.Equals(s["id"]!.GetValue<string>(), "S23", StringComparison.Ordinal)))
        {
            _ = surface.Remove("key");
            surface["graphStatus"] = "unknown";
        }
    })["scopeMap"];

    private static string WithDegraded(string view)
    {
        var node = JsonNode.Parse(view)!.AsObject();
        node["degraded"] = new JsonArray("tracker");
        return node.ToJsonString();
    }
}
