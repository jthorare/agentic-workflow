using System.Text.Json.Nodes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The stop after the relevance review and the gap search (issue 966 section 5.2): the counts in the header and the rows the owner must read.</summary>
public sealed class ScopingRelevanceShowTests
{
    private static async Task<ScopingView> Show(ScopingFacts facts) =>
        (await new ScopingShowService().Handle(new ScopingShowRequest(facts), CancellationToken.None)).Value;

    private static IReadOnlyList<ScopingRow> Flagged(ScopingView view) => view.Sections.Single(s => s.Name == "flagged").Rows;

    private static JsonObject Candidate(string id, string title, string ruling) => new()
    {
        ["id"] = id,
        ["title"] = title,
        ["kind"] = "job",
        ["citation"] = "code:src/jobs/purge.ts",
        ["excerpt"] = "x",
        ["query"] = "expiresAt",
        ["anchor"] = "code:src/jobs/purge.ts",
        ["why"] = "w",
        ["ruling"] = ruling,
    };

    [Fact, Trait("Lane", "unit")]
    public async Task The_header_adds_the_relevance_and_gap_search_counts_to_the_lens_count()
    {
        var view = await Show(ScopingFacts.Expiry());

        Assert.Equal("1 of 4 lenses limited; relevance: 29 in, 0 out, 0 noise, 0 disputed; gap search: 1 round, 0 found, 0 confirmed", view.CoverageHeader);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_disputed_entry_is_a_flagged_row_naming_both_verdicts()
    {
        var facts = ScopingFacts.Expiry().Rule("L-code-3", agent: "noise", judge: "in", citation: "L-code-3", result: "in", disputed: true);

        var row = Assert.Single(Flagged(await Show(facts)), r => r.Key == "L-code-3");

        Assert.Equal("disputed", row.Status);
        Assert.StartsWith("disputed: ", row.Text, StringComparison.Ordinal);
        Assert.Contains("AI said noise, judge said in", row.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_candidate_ruled_uncertain_is_a_flagged_row()
    {
        var facts = ScopingFacts.Expiry().Edit("gapSearch", n => n["rounds"]![0]!["candidates"] = new JsonArray(Candidate("G1.1", "Nightly purge", "uncertain")));

        var row = Assert.Single(Flagged(await Show(facts)), r => r.Key == "G1.1");

        Assert.Equal("uncertain", row.Status);
        Assert.Contains("Nightly purge", row.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_confirmed_gap_in_the_last_round_is_an_open_gap_row()
    {
        var facts = ScopingFacts.Expiry().Edit("gapSearch", n => n["rounds"]![0]!["candidates"] = new JsonArray(Candidate("G1.1", "Nightly purge", "real-gap")));

        var row = Assert.Single(Flagged(await Show(facts)), r => r.Key == "G1.1");

        Assert.Equal("open-gap", row.Status);
        Assert.Equal("open gap: Nightly purge (code:src/jobs/purge.ts)", row.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Before_the_relevance_step_has_run_the_flagged_section_says_what_writes_the_fact()
    {
        var view = await Show(ScopingFacts.Expiry().Without("relevance").Without("gapSearch"));

        var rows = Flagged(view);
        Assert.Contains(rows, r => r.Text == "not yet: written by step.scope.relevance-apply");
        Assert.Contains(rows, r => r.Text == "not yet: written by step.scope.gap-log");
        Assert.Equal("1 of 4 lenses limited", view.CoverageHeader);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_checklist_lists_the_four_new_rules()
    {
        var view = await Show(ScopingFacts.Expiry());

        var keys = view.Sections.Single(s => s.Name == "checklist").Rows.Select(r => r.Key).ToList();
        Assert.Contains("SCP-REL-001", keys, StringComparer.Ordinal);
        Assert.Contains("SCP-REL-002", keys, StringComparer.Ordinal);
        Assert.Contains("SCP-GAP-001", keys, StringComparer.Ordinal);
        Assert.Contains("SCP-GAP-002", keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_model_warning_for_the_gap_searcher_or_the_relevance_agent_is_listed_before_the_rules()
    {
        var facts = ScopingFacts.Expiry();
        facts["runs"] = "[]";

        var rows = (await Show(facts)).Sections.Single(s => s.Name == "checklist").Rows;

        Assert.Equal(["SCP-CHAL-002", "SCP-REL-002", "SCP-GAP-002"], rows.Take(3).Select(r => r.Key), StringComparer.Ordinal);
    }
}
