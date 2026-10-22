using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Line;

namespace Aw.Sindri.Tests.Line;

/// <summary>The design station's checks (line spec §4.3): one pure function over the facts, run at the draft stage by check and at the evidence stage by evidence.</summary>
[Lane(TestLane.Unit)]
public sealed class DesignChecksTests
{
    private const string Scope = """{"scopeMap":{"surfaces":[{"id":"S1"},{"id":"S2"}]}, "acceptance":{"items":[{"id":"AC-1","text":"WHEN a link is read after expiry THEN the system SHALL answer 410."},{"id":"AC-2","text":"WHEN a link is created THEN the system SHALL store its expiry."}]}, "openQuestions":[{"id":"Q1"}],"answerLog":[{"id":"A1"}]}""";

    private const string Design = """{"sections":[ {"id":"sec-1","covers":["S1"],"cites":["S1","AC-1"],"answers":["Q1"]}, {"id":"sec-2","covers":["S2"],"cites":["AC-2","D1"],"assumes":["D1.a"]}], "coverage":{"excluded":[]}, "decisions":[{"id":"D1","question":"Where does expiry run?","affects":["sec-2"],"cites":["S2"], "options":[{"id":"D1.a","answer":"On read.","tradeoffs":"Exact.","recommended":true,"cites":["S2"]},{"id":"D1.b","answer":"Nightly.","tradeoffs":"Cheap.","cites":["S2"]}]}], "deferred":[], "signals":[{"item":"AC-1","kind":"api-transcript","statement":"A GET after expiry returns 410 within 200 ms.","runs":"head"}, {"item":"AC-2","kind":"test","statement":"The stored expiry equals the requested one.","runs":"head"}], "contracts":[], "acceptance":{"items":[{"id":"AC-1","text":"WHEN a link is read after expiry THEN the system SHALL answer 410."},{"id":"AC-2","text":"WHEN a link is created THEN the system SHALL store its expiry."}]}, "resolutions":[{"finding":"F2","text":"Kept: the check is cheap."}], "escapes":[]}""";

    private const string Rounds = """[{"round":1,"selected":["product"],"writerModel":"opus","lenses":[{"lens":"product","state":"ran","model":"gpt","findings":[{"id":"F2","severity":"MEDIUM"}]}]}]""";

    private static LineReport Check(Action<JsonObject, JsonObject>? edit = null, bool evidence = false, string rounds = Rounds, string accepted = "[]")
    {
        var scope = (JsonObject)JsonNode.Parse(Scope)!;
        var design = (JsonObject)JsonNode.Parse(Design)!;
        edit?.Invoke(scope, design);
        return DesignChecks.Run(scope, design, JsonNode.Parse(rounds), JsonNode.Parse(accepted), evidence);
    }

    private static IEnumerable<string> Failed(LineReport report) => report.Failures.Select(static f => f.Rule).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private static IEnumerable<string> Warned(LineReport report) => report.Findings.Where(static f => f.Warn).Select(static f => f.Rule).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    [Fact]
    public void The_gold_design_passes_the_draft_and_completes_the_evidence_stage()
    {
        var draft = Check();
        var evidence = Check(evidence: true);

        Assert.Equal("pass", draft.Outcome);
        Assert.Equal("complete", evidence.Outcome);
        Assert.Empty(draft.FindingsFact());
    }

    [Fact]
    public void An_uncovered_surface_fails_unless_it_is_excluded_with_a_reason()
    {
        var uncovered = Check((_, d) => d["sections"]![0]!["covers"] = new JsonArray());
        var excluded = Check((_, d) =>
        {
            d["sections"]![0]!["covers"] = new JsonArray();
            d["coverage"]!["excluded"] = JsonNode.Parse("""[{"surface":"S1","reason":"not in this change"}]""");
        });
        var noReason = Check((_, d) =>
        {
            d["sections"]![0]!["covers"] = new JsonArray();
            d["coverage"]!["excluded"] = JsonNode.Parse("""[{"surface":"S1","reason":""}]""");
        });

        Assert.Equal(["DSN-COV-001"], Failed(uncovered), StringComparer.Ordinal);
        Assert.Empty(Failed(excluded));
        Assert.Equal(["DSN-COV-001"], Failed(noReason), StringComparer.Ordinal);
    }

    [Fact]
    public void An_open_question_must_be_answered_carried_or_deferred_with_an_owner_and_a_default()
    {
        var open = Check((_, d) => d["sections"]![0]!["answers"] = new JsonArray());
        var deferred = Check((_, d) =>
        {
            d["sections"]![0]!["answers"] = new JsonArray();
            d["deferred"] = JsonNode.Parse("""[{"question":"Q1","owner":"owner","buildMayAssume":"keep the default"}]""");
        });
        var halfDeferred = Check((_, d) =>
        {
            d["sections"]![0]!["answers"] = new JsonArray();
            d["deferred"] = JsonNode.Parse("""[{"question":"Q1","owner":"owner"}]""");
        });

        Assert.Equal(["DSN-COV-002"], Failed(open), StringComparer.Ordinal);
        Assert.Empty(Failed(deferred));
        Assert.Equal(["DSN-COV-002"], Failed(halfDeferred), StringComparer.Ordinal);
    }

    [Fact]
    public void A_live_acceptance_item_needs_a_complete_signal_and_a_superseded_one_does_not()
    {
        var none = Check((_, d) => d["signals"] = JsonNode.Parse("""[{"item":"AC-1","kind":"test","statement":"returns 410","runs":"head"}]"""));
        var badKind = Check((_, d) => d["signals"]![0]!["kind"] = "Not Kebab");
        var badRuns = Check((_, d) => d["signals"]![0]!["runs"] = "base");
        var superseded = Check((_, d) =>
        {
            d["signals"] = JsonNode.Parse("""[{"item":"AC-1","kind":"test","statement":"returns 410","runs":"head"}]""");
            d["acceptance"]!["items"]![1]!["status"] = "superseded";
        });

        Assert.Equal(["DSN-ACC-001"], Failed(none), StringComparer.Ordinal);
        Assert.Equal(["DSN-ACC-001"], Failed(badKind), StringComparer.Ordinal);
        Assert.Equal(["DSN-ACC-001"], Failed(badRuns), StringComparer.Ordinal);
        Assert.Empty(Failed(superseded));
    }

    [Fact]
    public void A_signal_that_names_a_script_names_one_inside_the_workspace()
    {
        const string Signal = """[{"item":"AC-1","kind":"test","statement":"returns 410","runs":"base+head"%SCRIPT%}]""";
        var none = Check((_, d) => d["signals"] = JsonNode.Parse(Signal.Replace("%SCRIPT%", string.Empty, StringComparison.Ordinal)));
        var relative = Check((_, d) => d["signals"] = JsonNode.Parse(Signal.Replace("%SCRIPT%", ",\"script\":\"proofs/ac-1.sh\"", StringComparison.Ordinal)));
        var outside = Check((_, d) => d["signals"] = JsonNode.Parse(Signal.Replace("%SCRIPT%", ",\"script\":\"../ac-1.sh\"", StringComparison.Ordinal)));
        var rooted = Check((_, d) => d["signals"] = JsonNode.Parse(Signal.Replace("%SCRIPT%", ",\"script\":\"/tmp/ac-1.sh\"", StringComparison.Ordinal)));

        Assert.Empty(Failed(none));   // a missing script is the before/after Step's to report
        Assert.Empty(Failed(relative));
        Assert.Equal(["DSN-ACC-001"], Failed(outside), StringComparer.Ordinal);
        Assert.Equal(["DSN-ACC-001"], Failed(rooted), StringComparer.Ordinal);
    }

    [Fact]
    public void An_unmeasurable_signal_is_refused_unless_it_carries_a_number_or_a_reference()
    {
        var vague = Check((_, d) => d["signals"]![0]!["statement"] = "The page feels fast and clean.");
        var numbered = Check((_, d) => d["signals"]![0]!["statement"] = "The page is fast: under 200 ms.");
        var referenced = Check((_, d) =>
        {
            d["signals"]![0]!["statement"] = "The page looks clean.";
            d["signals"]![0]!["reference"] = "mockups/links.png";
        });

        Assert.Equal(["DSN-ACC-002"], Failed(vague), StringComparer.Ordinal);
        Assert.Empty(Failed(numbered));
        Assert.Empty(Failed(referenced));
    }

    [Fact]
    public void A_decision_needs_two_or_three_options_one_recommended_trade_offs_and_marked_sections()
    {
        var oneOption = Check((_, d) => d["decisions"]![0]!["options"]!.AsArray().RemoveAt(1));
        var twoRecommended = Check((_, d) => d["decisions"]![0]!["options"]![1]!["recommended"] = true);
        var noTradeoffs = Check((_, d) => d["decisions"]![0]!["options"]![1]!["tradeoffs"] = string.Empty);
        var unmarked = Check((_, d) => d["sections"]![1]!["assumes"] = new JsonArray());
        var wrongMarker = Check((_, d) => d["sections"]![1]!["assumes"] = JsonNode.Parse("""["D1.b"]"""));
        var noAffects = Check((_, d) => d["decisions"]![0]!["affects"] = new JsonArray());

        Assert.Equal(["DSN-DEC-001"], Failed(oneOption), StringComparer.Ordinal);
        Assert.Equal(["DSN-DEC-001"], Failed(twoRecommended), StringComparer.Ordinal);
        Assert.Equal(["DSN-DEC-001"], Failed(noTradeoffs), StringComparer.Ordinal);
        Assert.Equal(["DSN-DEC-001"], Failed(unmarked), StringComparer.Ordinal);
        Assert.Equal(["DSN-DEC-001"], Failed(wrongMarker), StringComparer.Ordinal);
        Assert.Equal(["DSN-DEC-001"], Failed(noAffects), StringComparer.Ordinal);
    }

    [Fact]
    public void A_section_must_cite_ids_that_exist()
    {
        var uncited = Check((_, d) => d["sections"]![0]!["cites"] = new JsonArray());
        var unknown = Check((_, d) => d["sections"]![0]!["cites"] = JsonNode.Parse("""["D9"]"""));
        var answer = Check((_, d) => d["sections"]![0]!["cites"] = JsonNode.Parse("""["A1"]"""));

        Assert.Equal(["DSN-CITE-001"], Failed(uncited), StringComparer.Ordinal);
        Assert.Equal(["DSN-CITE-001"], Failed(unknown), StringComparer.Ordinal);
        Assert.Empty(Failed(answer));
    }

    [Fact]
    public void Contract_edits_are_numbered_rows_one_lane_each_and_never_the_same_span_twice()
    {
        const string good = """[{"id":"c1","lane":"engine","target":{"file":"a.md","heading":"H"},"quote":"old text","row":1},{"id":"c2","lane":"ui","target":{"file":"b.md","heading":"H"},"quote":"other","row":2}]""";
        var ok = Check((_, d) => d["contracts"] = JsonNode.Parse(good));
        var noRow = Check((_, d) => d["contracts"] = JsonNode.Parse("""[{"id":"c1","lane":"engine","target":{"file":"a.md"},"quote":"x"}]"""));
        var shared = Check((_, d) => d["contracts"] = JsonNode.Parse("""[{"id":"c1","lane":"engine","target":{"file":"a.md"},"quote":"x","row":1},{"id":"c2","lane":"ui","target":{"file":"b.md"},"quote":"y","row":1}]"""));
        var sameSpan = Check((_, d) => d["contracts"] = JsonNode.Parse("""[{"id":"c1","lane":"engine","target":{"file":"a.md"},"quote":"x","row":1},{"id":"c2","lane":"engine","target":{"file":"a.md"},"quote":"x","row":2}]"""));

        Assert.Empty(Failed(ok));
        Assert.Equal(["DSN-XSPEC-001"], Warned(ok).Where(static r => r.StartsWith("DSN-XSPEC", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.Equal(["DSN-XSPEC-003"], Failed(noRow), StringComparer.Ordinal);
        Assert.Equal(["DSN-XSPEC-003"], Failed(shared), StringComparer.Ordinal);
        Assert.Equal(["DSN-XSPEC-003"], Failed(sameSpan), StringComparer.Ordinal);
    }

    [Fact]
    public void The_rules_that_need_the_repo_or_the_style_pack_warn_that_they_did_not_run()
    {
        var report = Check();

        Assert.Equal(["DSN-STYLE-001"], Warned(report), StringComparer.Ordinal);
    }

    [Fact]
    public void The_evidence_rules_do_not_run_at_the_draft_stage()
    {
        const string high = """[{"round":1,"selected":["product"],"lenses":[{"lens":"product","state":"ran","findings":[{"id":"F1","severity":"HIGH"}]}]}]""";

        Assert.Empty(Failed(Check(rounds: high)));
        Assert.Equal(["DSN-LENS-001"], Failed(Check(evidence: true, rounds: high)), StringComparer.Ordinal);
    }

    [Fact]
    public void A_critical_or_high_finding_left_in_the_last_round_fails_unless_the_owner_accepted_it()
    {
        const string high = """[{"round":1,"selected":["product"],"lenses":[{"lens":"product","state":"ran","findings":[{"id":"F1","severity":"HIGH"}]}]},{"round":2,"selected":["product"],"lenses":[{"lens":"product","state":"ran","findings":[{"id":"F3","severity":"CRITICAL"}]}]}]""";

        Assert.Equal(["DSN-LENS-001"], Failed(Check(evidence: true, rounds: high)), StringComparer.Ordinal);
        Assert.Empty(Failed(Check(evidence: true, rounds: high, accepted: """[{"id":"F3"}]""")));
        Assert.Equal(["DSN-LENS-001"], Failed(Check(evidence: true, rounds: "[]")), StringComparer.Ordinal);
    }

    [Fact]
    public void Every_round_must_run_every_lens_its_pack_selected()
    {
        const string skipped = """[{"round":1,"selected":["product","security"],"lenses":[{"lens":"product","state":"ran","findings":[]},{"lens":"security","state":"not-applicable"}]}]""";

        Assert.Equal(["DSN-LENS-001"], Failed(Check(evidence: true, rounds: skipped)), StringComparer.Ordinal);
    }

    [Fact]
    public void A_lens_on_the_writers_model_warns_and_an_unresolved_medium_finding_fails()
    {
        const string same = """[{"round":1,"selected":["product"],"writerModel":"opus","lenses":[{"lens":"product","state":"ran","model":"opus","findings":[{"id":"F9","severity":"LOW"}]}]}]""";

        var report = Check(evidence: true, rounds: same);

        Assert.Equal(["DSN-LENS-003"], Failed(report), StringComparer.Ordinal);
        Assert.Contains("DSN-LENS-002", Warned(report), StringComparer.Ordinal);
    }

    [Fact]
    public void Acceptance_items_keep_scopes_ids_and_texts_and_added_or_superseded_ones_are_justified()
    {
        var changed = Check((_, d) => d["acceptance"]!["items"]![0]!["text"] = "WHEN a link is read THEN something else.", evidence: true);
        var dropped = Check((_, d) => d["acceptance"]!["items"]!.AsArray().RemoveAt(1), evidence: true);
        var uncited = Check((_, d) => d["acceptance"]!["items"]!.AsArray().Add(JsonNode.Parse("""{"id":"AC-3","text":"WHEN x THEN y."}""")), evidence: true);
        var cited = Check((_, d) =>
        {
            d["acceptance"]!["items"]!.AsArray().Add(JsonNode.Parse("""{"id":"AC-3","text":"WHEN x THEN y.","cites":["sec-2"]}"""));
            d["signals"]!.AsArray().Add(JsonNode.Parse("""{"item":"AC-3","kind":"test","statement":"y holds","runs":"head"}"""));
        }, evidence: true);
        var orphan = Check((_, d) => d["acceptance"]!["items"]![1]!["status"] = "superseded", evidence: true);
        var replaced = Check((_, d) =>
        {
            d["acceptance"]!["items"]![1]!["status"] = "superseded";
            d["acceptance"]!["items"]![1]!["supersededBy"] = "AC-3";
            d["acceptance"]!["items"]!.AsArray().Add(JsonNode.Parse("""{"id":"AC-3","text":"WHEN x THEN y.","cites":["D1"]}"""));
            d["signals"]!.AsArray().Add(JsonNode.Parse("""{"item":"AC-3","kind":"test","statement":"y holds","runs":"head"}"""));
        }, evidence: true);

        Assert.Equal(["DSN-ACC-003"], Failed(changed), StringComparer.Ordinal);
        Assert.Equal(["DSN-ACC-003"], Failed(dropped), StringComparer.Ordinal);
        Assert.Contains("DSN-ACC-003", Failed(uncited), StringComparer.Ordinal);
        Assert.Empty(Failed(cited));
        Assert.Contains("DSN-ACC-003", Failed(orphan), StringComparer.Ordinal);
        Assert.Empty(Failed(replaced));
        Assert.Contains("DSN-ACC-003", Warned(replaced), StringComparer.Ordinal);
    }

    [Fact]
    public void A_surface_or_item_outside_the_scope_packet_must_be_listed_as_an_escape_or_it_warns()
    {
        var unlisted = Check((_, d) => d["sections"]![0]!["covers"]!.AsArray().Add("S9"), evidence: true);
        var listed = Check((_, d) =>
        {
            d["sections"]![0]!["covers"]!.AsArray().Add("S9");
            d["escapes"] = JsonNode.Parse("""[{"kind":"surface","id":"S9"}]""");
        }, evidence: true);

        Assert.Empty(Failed(unlisted));
        Assert.Contains("DSN-ESC-001", Warned(unlisted), StringComparer.Ordinal);
        Assert.DoesNotContain("DSN-ESC-001", Warned(listed), StringComparer.Ordinal);
    }

    [Fact]
    public void The_findings_fact_carries_the_rule_item_message_fix_and_severity()
    {
        var finding = Check((_, d) => d["sections"]![0]!["covers"] = new JsonArray()).FindingsFact()[0]!;

        Assert.Equal("DSN-COV-001", (string)finding["rule"]!);
        Assert.Equal("S1", (string)finding["item"]!);
        Assert.Equal("fail", (string)finding["severity"]!);
        Assert.False(string.IsNullOrEmpty((string)finding["message"]!));
        Assert.False(string.IsNullOrEmpty((string)finding["fix"]!));
    }
}
