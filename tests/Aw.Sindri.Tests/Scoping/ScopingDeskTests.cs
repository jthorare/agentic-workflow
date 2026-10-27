using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>'scoping desk' (scoping spec §8.2): a block Step's code over the facts a Step directory holds.</summary>
public sealed class ScopingDeskTests
{
    private static async Task<ScopingDeskResponse> Run(string block, ScopingFacts facts) =>
        (await new ScopingDeskService().Handle(new ScopingDeskRequest(block, facts), CancellationToken.None)).Value;

    private static JsonNode Out(ScopingDeskResponse response, string key) => JsonNode.Parse(response.Outputs[key])!;

    [Fact, Trait("Lane", "unit")]
    public async Task Check_on_the_gold_facts_reports_pass_and_appends_a_round()
    {
        var response = await Run("check", ScopingFacts.Expiry().Without("rounds"));

        Assert.Equal("step.scope.check", response.Block);
        Assert.Equal("pass", response.Outcome);
        Assert.DoesNotContain("findings", response.Outputs.Keys, StringComparer.Ordinal);   // the descriptor writes fact:findings on gaps and regather only
        var rounds = Assert.Single(response.Files, f => f.Path == "rounds.jsonl");
        Assert.True(rounds.Append);
        var line = JsonNode.Parse(rounds.Content.TrimEnd('\n'))!;
        Assert.Equal(1, line["round"]!.GetValue<int>());
        Assert.Equal("first", line["trigger"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Check_numbers_the_next_round_and_diffs_it_against_the_last_by_id()
    {
        var first = await Run("check", ScopingFacts.Expiry().Without("rounds"));
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"]!.AsArray().RemoveAt(0));
        facts["rounds"] = "[" + first.Files.Single(f => f.Path == "rounds.jsonl").Content.TrimEnd('\n') + "]";

        var second = await Run("check", facts);

        var line = JsonNode.Parse(second.Files.Single(f => f.Path == "rounds.jsonl").Content.TrimEnd('\n'))!;
        Assert.Equal(2, line["round"]!.GetValue<int>());
        Assert.Equal(["AC-1"], line["removed"]!.AsArray().Select(x => x!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Check_on_a_gap_reports_gaps_with_the_findings_as_fact_findings()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"] = new JsonArray());

        var response = await Run("step.scope.check", facts);

        Assert.Equal("gaps", response.Outcome);
        Assert.Contains(response.Findings, f => f.Rule == "SCP-ACC-001");
        Assert.Contains(Out(response, "findings").AsArray(), f => f!["rule"]!.GetValue<string>() == "SCP-ACC-001");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Check_on_a_degraded_source_reports_regather()
    {
        var response = await Run("check", ScopingCheckTests.Limited("history", "degraded", "timeout", code: null, "not-searched", 0));

        Assert.Equal("regather", response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_on_the_gold_facts_is_complete_and_assembles_the_fifteen_packet_files()
    {
        var response = await Run("evidence", ScopingFacts.Expiry());

        Assert.Equal("complete", response.Outcome);
        Assert.Equal(
            ["acceptance.json", "answers.jsonl", "brief.md", "challenge.json", "coverage.json", "gap-search.json", "graph.json", "lenses.json", "questions.json", "relevance.json", "rounds.jsonl", "runs.json", "scope-map.json", "size.json", "sources.json"],
            response.Files.Where(f => f.Path.StartsWith("scope-packet/", StringComparison.Ordinal)).Select(f => f.Path["scope-packet/".Length..]).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["acceptance", "coverage", "openQuestions", "recommendation", "runs", "scopeMap"], response.Outputs.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);   // fact:scopePacket is the hash the CLI adds
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_stamps_trust_on_every_node_edge_and_item_of_lenses_json_as_untrusted()
    {
        var response = await Run("evidence", ScopingFacts.Expiry());

        var lenses = JsonNode.Parse(response.Files.Single(f => f.Path == "scope-packet/lenses.json").Content)!;
        Assert.Equal(1, lenses["schemaVersion"]!.GetValue<int>());
        foreach (var lens in new[] { "code", "product", "related", "history" })
        {
            var entry = lenses[lens]!;
            Assert.All(entry["nodes"]!.AsArray(), n => Assert.Equal("untrusted", n!["trust"]!.GetValue<string>()));
            Assert.All(entry["edges"]!.AsArray(), e => Assert.Equal("untrusted", e!["trust"]!.GetValue<string>()));
            Assert.All(entry["items"]!.AsArray(), i => Assert.Equal("untrusted", i!["trust"]!.GetValue<string>()));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_stamps_owner_only_on_a_verifiable_match_and_untrusted_everywhere_else()
    {
        // AC-6 yields from the owner's chosen option text verbatim; AC-1 cites only lens items
        var facts = ScopingFacts.Expiry().Edit("acceptance", n =>
        {
            n["items"]![1]!["text"] = "WHILE a link has no expiry time THE system SHALL keep it valid until it is revoked or its note is deleted";
            n["items"]![1]!["citations"] = new JsonArray("A1");
        }).Edit("questions", n => n[0]!["options"]![0]!["yields"] = "WHILE a link has no expiry time THE system SHALL keep it valid until it is revoked or its note is deleted");

        var response = await Run("evidence", facts);

        var items = JsonNode.Parse(response.Files.Single(f => f.Path == "scope-packet/acceptance.json").Content)!["items"]!.AsArray();
        Assert.Equal("owner", items.Single(i => i!["id"]!.GetValue<string>() == "AC-2")!["trust"]!.GetValue<string>());
        Assert.Equal("untrusted", items.Single(i => i!["id"]!.GetValue<string>() == "AC-1")!["trust"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_item_citing_a_lens_item_beside_an_answer_is_untrusted_a_mixed_citation_is_never_owner()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n =>
        {
            n["items"]![1]!["text"] = "WHILE a link has no expiry time THE system SHALL keep it valid until it is revoked or its note is deleted";
            n["items"]![1]!["citations"] = new JsonArray("A1", "L-code-3");
        }).Edit("questions", n => n[0]!["options"]![0]!["yields"] = "WHILE a link has no expiry time THE system SHALL keep it valid until it is revoked or its note is deleted");

        var response = await Run("evidence", facts);

        var items = JsonNode.Parse(response.Files.Single(f => f.Path == "scope-packet/acceptance.json").Content)!["items"]!.AsArray();
        Assert.Equal("untrusted", items.Single(i => i!["id"]!.GetValue<string>() == "AC-2")!["trust"]!.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_moves_an_uncited_item_to_flagged_and_it_never_reaches_the_acceptance_list()
    {
        var facts = ScopingFacts.Expiry().Edit("acceptance", n => n["items"]![3]!["citations"] = new JsonArray("L-code-99"));

        var response = await Run("evidence", facts);

        var map = JsonNode.Parse(response.Files.Single(f => f.Path == "scope-packet/scope-map.json").Content)!;
        Assert.Contains(map["flagged"]!.AsArray(), f => f!["item"]!.GetValue<string>() == "AC-4");
        var items = JsonNode.Parse(response.Files.Single(f => f.Path == "scope-packet/acceptance.json").Content)!["items"]!.AsArray();
        Assert.DoesNotContain(items, i => i!["id"]!.GetValue<string>() == "AC-4");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_with_a_failing_rule_reports_incomplete_with_the_findings_and_writes_no_packet()
    {
        var facts = ScopingFacts.Expiry().Edit("challenge", n => { var rounds = n["rounds"]!.AsArray(); rounds.RemoveAt(0); rounds[0]!["verdict"] = "found"; });

        var response = await Run("evidence", facts);

        Assert.Equal("incomplete", response.Outcome);
        Assert.Contains(response.Findings, f => f.Rule == "SCP-CHAL-001");
        Assert.Empty(response.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_with_only_coverage_findings_reports_regather()
    {
        var response = await Run("evidence", ScopingCheckTests.Limited("history", "degraded", "timeout", code: null, "found", 3));

        Assert.Equal("regather", response.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Challenge_log_appends_the_round_and_a_found_round_becomes_findings()
    {
        var facts = new ScopingFacts
        {
            ["challenge"] = """{"rounds":[]}""",
            ["challengeRound"] = """{"walked":[{"kind":"lens","name":"code"}],"verdict":"found","surfaces":[{"title":"Export of a link","citations":["L-related-1"]}]}""",
        };

        var response = await Run("challenge-log", facts);

        Assert.Equal("found", response.Outcome);
        _ = Assert.Single(Out(response, "challenge")["rounds"]!.AsArray());
        Assert.Equal(1, Out(response, "challenge")["rounds"]![0]!["round"]!.GetValue<int>());
        _ = Assert.Single(Out(response, "findings").AsArray());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Challenge_log_refuses_a_verdict_that_disagrees_with_its_items()
    {
        var facts = new ScopingFacts
        {
            ["challenge"] = """{"rounds":[]}""",
            ["challengeRound"] = """{"walked":[{"kind":"lens","name":"code"}],"verdict":"nothingNew","surfaces":[{"title":"x","citations":["L-code-1"]}]}""",
        };

        var result = await new ScopingDeskService().Handle(new ScopingDeskRequest("challenge-log", facts), CancellationToken.None);

        Assert.True(result.IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_appends_the_latest_batch_to_the_answer_log_once_by_its_digest()
    {
        var facts = new ScopingFacts
        {
            ["answers"] = """[{"round":1,"actor":"owner","at":"2026-10-16T09:10:00Z","questionIds":["Q1"],"answers":[{"question":"Q1","option":"Q1.a","text":"x"}]}]""",
            ["answerLog"] = "[]",
        };

        var once = await Run("interview.record", facts);
        facts["answerLog"] = once.Outputs["answerLog"];
        var twice = await Run("step.interview.record", facts);

        Assert.Equal("recorded", once.Outcome);
        Assert.Equal("A1", Out(once, "answerLog")[0]!["id"]!.GetValue<string>());
        _ = Assert.Single(Out(twice, "answerLog").AsArray());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Intake_sorts_the_input_by_kind_and_carries_the_eleven_seeds()
    {
        var response = await Run("intake", new ScopingFacts { ["scopeInput"] = """{"kind":"tracker","repo":"fixture-notes","key":"NTS-135"}""" });

        Assert.Equal("tracker", response.Outcome);
        Assert.Equal(
            ["acceptance", "answerLog", "answers", "challenge", "findings", "gapAnchors", "gapSearch", "openQuestions", "questions", "roundLog", "scopeMap"],
            response.Outputs.Keys.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_block_the_desk_helper_does_not_cover_is_refused_with_the_ones_it_does()
    {
        var result = await new ScopingDeskService().Handle(new ScopingDeskRequest("step.scope.draft", new ScopingFacts()), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("check", result.Error.Message, StringComparison.Ordinal);
    }
}
