using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The scope packet after the relevance review and the gap search (issue 966 section 5.1): fourteen files, lenses and coverage showing only what stayed in, and the two new files carrying the facts.</summary>
public sealed class ScopePacketRelevanceTests
{
    private const string Dir = "scope-packet/";

    private static JsonNode File(IReadOnlyList<ScopingDeskFile> files, string name) =>
        JsonNode.Parse(Assert.Single(files, f => f.Path == Dir + name).Content)!;

    [Fact, Trait("Lane", "unit")]
    public void The_packet_has_the_fourteen_files_the_kind_lists()
    {
        var files = ScopePacket.Build(new ScopeFacts(ScopingFacts.Expiry()));

        Assert.Equal(14, files.Count);
        Assert.Equal(ScopePacketFiles.Names.Order(StringComparer.Ordinal), files.Select(static f => f.Path[Dir.Length..]).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains("relevance.json", ScopePacketFiles.Names, StringComparer.Ordinal);
        Assert.Contains("gap-search.json", ScopePacketFiles.Names, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_new_files_carry_their_facts_under_schemaVersion_1()
    {
        var facts = ScopingFacts.Expiry();
        var files = ScopePacket.Build(new ScopeFacts(facts));

        var relevance = File(files, "relevance.json");
        var expectedRelevance = JsonNode.Parse(facts["relevance"])!;
        Assert.Equal(1, (int)relevance["schemaVersion"]!);
        Assert.Equal(expectedRelevance["counts"]!.ToJsonString(), relevance["counts"]!.ToJsonString());
        Assert.Equal(expectedRelevance["entries"]!.AsArray().Count, relevance["entries"]!.AsArray().Count);
        var gap = File(files, "gap-search.json");
        Assert.Equal(1, (int)gap["schemaVersion"]!);
        Assert.Equal(JsonNode.Parse(facts["gapSearch"])!["counts"]!.ToJsonString(), gap["counts"]!.ToJsonString());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_model_written_owner_trust_in_the_new_files_is_dropped()
    {
        var facts = ScopingFacts.Expiry()
            .Edit("relevance", n => n["entries"]![0]!["trust"] = "owner")
            .Edit("gapSearch", n => n["rounds"]![0]!["trust"] = "owner");

        var files = ScopePacket.Build(new ScopeFacts(facts));

        Assert.DoesNotContain("\"trust\"", Assert.Single(files, f => f.Path.EndsWith("relevance.json", StringComparison.Ordinal)).Content, StringComparison.Ordinal);
        Assert.DoesNotContain("\"trust\"", Assert.Single(files, f => f.Path.EndsWith("gap-search.json", StringComparison.Ordinal)).Content, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_that_left_shows_in_relevance_json_only_and_lenses_and_coverage_still_agree()
    {
        var facts = ScopingFacts.Expiry().Rule("L-code-3", agent: "noise", judge: "noise", citation: "L-code-3", result: "noise");

        var files = ScopePacket.Build(new ScopeFacts(facts));

        var lenses = File(files, "lenses.json");
        Assert.DoesNotContain(lenses["code"]!["nodes"]!.AsArray(), n => (string?)n!["lensItem"] == "L-code-3");
        var coverage = File(files, "coverage.json");
        var code = coverage["lenses"]!.AsArray().Single(r => (string?)r!["lens"] == "code")!;
        Assert.DoesNotContain(code["found"]!["ids"]!.AsArray(), i => (string?)i == "L-code-3");
        var entry = File(files, "relevance.json")["entries"]!.AsArray().Single(e => (string?)e!["id"] == "L-code-3")!;
        Assert.Equal("noise", (string?)entry["result"]);
        var response = ScopePacketChecks.Run(new ScopeFacts(facts), ScopeStage.Evidence);
        Assert.Equal(ScopeRuleStatus.Pass, response.Rules.Single(r => r.Rule == "SCP-LENS-001").Status);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_counts_line_reads_in_out_noise_and_disputed_and_the_gap_rounds_from_the_two_files()
    {
        var facts = ScopingFacts.Expiry()
            .Rule("L-code-3", agent: "noise", judge: "noise", citation: "L-code-3", result: "noise")
            .Rule("L-code-4", agent: "out", judge: "in", citation: "L-code-4", result: "in", disputed: true);

        var relevance = ScopeCounts.Relevance(new ScopeFacts(facts));
        var gap = ScopeCounts.Gap(new ScopeFacts(facts));

        Assert.Equal("relevance: 28 in, 0 out, 1 noise, 1 disputed", relevance);
        Assert.Equal("gap search: 1 round, 0 found, 0 confirmed", gap);
    }

    [Fact, Trait("Lane", "unit")]
    public void Counts_are_null_when_the_fact_is_absent()
    {
        var facts = new ScopeFacts(ScopingFacts.Expiry().Without("relevance").Without("gapSearch"));

        Assert.Null(ScopeCounts.Relevance(facts));
        Assert.Null(ScopeCounts.Gap(facts));
    }
}
