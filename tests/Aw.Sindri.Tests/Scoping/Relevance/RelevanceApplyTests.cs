using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Scoping;
using static Aw.Sindri.Tests.Scoping.Relevance.RelevanceFixtures;

namespace Aw.Sindri.Tests.Scoping.Relevance;

/// <summary>The relevance decision table (spec 4.3): one case per row, the facts it rewrites and the fixes it records.</summary>
[Lane(TestLane.Unit)]
public sealed class RelevanceApplyTests
{
    private const string Export = "L-code-3";

    private static RelevanceApplied Apply(JsonObject verdicts, JsonObject rulings, JsonObject? lenses = null, JsonObject? coverage = null)
    {
        var applied = RelevanceApply.Apply(lenses ?? Lenses(), coverage ?? Coverage(), Brief(), verdicts, rulings);
        Assert.False(applied.IsError, applied.IsError ? applied.Error.Message : string.Empty);
        return applied.Value;
    }

    private static string FailureOf(JsonObject verdicts, JsonObject rulings)
    {
        var applied = RelevanceApply.Apply(Lenses(), Coverage(), Brief(), verdicts, rulings);
        Assert.True(applied.IsError);
        return applied.Error.Message;
    }

    private static JsonNode EntryOf(RelevanceApplied applied, string id) =>
        applied.Relevance["entries"]!.AsArray().Single(e => e!["id"]!.GetValue<string>() == id)!;

    private static string[] Ids(JsonNode lens) =>
        [.. lens["nodes"]!.AsArray().Concat(lens["items"]!.AsArray()).Select(static n => n!["lensItem"]!.GetValue<string>())];

    [Fact]
    public void Both_in_keeps_the_entry_and_it_is_not_disputed()
    {
        var (verdicts, rulings) = AllIn();

        var applied = Apply(verdicts, rulings);

        Assert.Equal(AllIds.Length, applied.Relevance["counts"]!["in"]!.GetValue<int>());
        Assert.Equal(0, applied.Relevance["counts"]!["disputed"]!.GetValue<int>());
        Assert.Empty(applied.MapFixes["fixes"]!.AsArray());
        Assert.Equal(Lenses().ToJsonString(), applied.Lenses.ToJsonString());
    }

    [Fact]
    public void Both_noise_with_a_resolving_citation_removes_the_entry_and_proposes_the_fix()
    {
        var (verdicts, rulings) = AllIn((Verdict(Export, "noise", fix: "ignore"), Ruling(Export, "noise")));

        var applied = Apply(verdicts, rulings);

        Assert.DoesNotContain(Export, Ids(applied.Lenses["code"]!), StringComparer.Ordinal);
        var entry = EntryOf(applied, Export);
        Assert.Equal("noise", entry["result"]!.GetValue<string>());
        Assert.False(entry["disputed"]!.GetValue<bool>());
        Assert.Equal(1, applied.Relevance["counts"]!["noise"]!.GetValue<int>());
        var fix = Assert.Single(applied.MapFixes["fixes"]!.AsArray())!;
        Assert.Equal(Export, fix["id"]!.GetValue<string>());
        Assert.Equal("code:src/billing/export.ts", fix["node"]!.GetValue<string>());
        Assert.Equal("ignore", fix["action"]!.GetValue<string>());
        Assert.Equal("proposed", fix["status"]!.GetValue<string>());
        Assert.Null(fix["relabelTo"]);
    }

    [Fact]
    public void Out_and_noise_that_disagree_remove_the_entry_with_the_judges_class()
    {
        var (verdicts, rulings) = AllIn((Verdict(Export, "out", fix: "demote"), Ruling(Export, "noise")));

        var applied = Apply(verdicts, rulings);

        var entry = EntryOf(applied, Export);
        Assert.Equal("noise", entry["result"]!.GetValue<string>());
        Assert.False(entry["disputed"]!.GetValue<bool>());
        Assert.Equal("demote", Assert.Single(applied.MapFixes["fixes"]!.AsArray())!["action"]!.GetValue<string>());
    }

    [Fact]
    public void Both_out_with_a_relabel_records_where_it_belongs()
    {
        var (verdicts, rulings) = AllIn((Verdict("L-product-2", "out", fix: "relabel", relabelTo: "feature:billing"), Ruling("L-product-2", "out")));

        var applied = Apply(verdicts, rulings);

        var fix = Assert.Single(applied.MapFixes["fixes"]!.AsArray())!;
        Assert.Equal("relabel", fix["action"]!.GetValue<string>());
        Assert.Equal("feature:billing", fix["relabelTo"]!.GetValue<string>());
        Assert.Equal("feature:billing-export", fix["node"]!.GetValue<string>());
        Assert.Equal(1, applied.Relevance["counts"]!["out"]!.GetValue<int>());
    }

    [Fact]
    public void An_uncited_removal_stays_in_and_is_disputed()
    {
        var (verdicts, rulings) = AllIn((Verdict(Export, "noise", citation: "L-code-99"), Ruling(Export, "noise")));

        var applied = Apply(verdicts, rulings);

        Assert.Contains(Export, Ids(applied.Lenses["code"]!), StringComparer.Ordinal);
        Assert.Equal("in", EntryOf(applied, Export)["result"]!.GetValue<string>());
        Assert.True(EntryOf(applied, Export)["disputed"]!.GetValue<bool>());
        Assert.Empty(applied.MapFixes["fixes"]!.AsArray());
    }

    [Theory]
    [InlineData("in", "noise")]
    [InlineData("noise", "in")]
    [InlineData("out", "in")]
    [InlineData("in", "out")]
    [InlineData("noise", "uncertain")]
    [InlineData("in", "uncertain")]
    public void A_disagreement_or_an_uncertain_ruling_keeps_the_entry_in_and_flags_it(string agent, string judge)
    {
        var (verdicts, rulings) = AllIn((Verdict(Export, agent), Ruling(Export, judge)));

        var applied = Apply(verdicts, rulings);

        var entry = EntryOf(applied, Export);
        Assert.Equal("in", entry["result"]!.GetValue<string>());
        Assert.True(entry["disputed"]!.GetValue<bool>());
        Assert.Contains(Export, Ids(applied.Lenses["code"]!), StringComparer.Ordinal);
        Assert.Equal(1, applied.Relevance["counts"]!["disputed"]!.GetValue<int>());
        Assert.Empty(applied.MapFixes["fixes"]!.AsArray());
    }

    [Fact]
    public void An_entry_with_no_verdict_stays_in_and_is_disputed()
    {
        var verdicts = Verdicts([.. AllIds.Where(static id => id != Export).Select(static id => Verdict(id, "in"))]);
        var rulings = Rulings([.. AllIds.Select(static id => Ruling(id, "noise"))]);

        var applied = Apply(verdicts, rulings);

        Assert.True(EntryOf(applied, Export)["disputed"]!.GetValue<bool>());
        Assert.Null(EntryOf(applied, Export)["agent"]);
        Assert.Contains(Export, Ids(applied.Lenses["code"]!), StringComparer.Ordinal);
    }

    [Fact]
    public void A_citation_may_be_a_brief_ref_or_a_node_key()
    {
        var (verdicts, rulings) = AllIn(
            (Verdict(Export, "noise", citation: "R1"), Ruling(Export, "noise")),
            (Verdict("L-product-2", "out", citation: "feature:invitations", fix: "ignore"), Ruling("L-product-2", "out")));

        var applied = Apply(verdicts, rulings);

        Assert.Equal(2, applied.Relevance["counts"]!["noise"]!.GetValue<int>() + applied.Relevance["counts"]!["out"]!.GetValue<int>());
        Assert.Equal(0, applied.Relevance["counts"]!["disputed"]!.GetValue<int>());
    }

    [Fact]
    public void An_id_the_lenses_do_not_hold_fails_with_the_fix_line()
    {
        var (verdicts, rulings) = AllIn();
        verdicts["verdicts"]!.AsArray().Add(Verdict("L-code-77", "in"));

        var message = FailureOf(verdicts, rulings);

        Assert.Contains("L-code-77", message, StringComparison.Ordinal);
        Assert.Contains("fix", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_ruling_for_an_id_the_lenses_do_not_hold_fails()
    {
        var (verdicts, rulings) = AllIn();
        rulings["rulings"]!.AsArray().Add(Ruling("L-history-9", "in"));

        Assert.Contains("L-history-9", FailureOf(verdicts, rulings), StringComparison.Ordinal);
    }

    [Fact]
    public void An_id_twice_in_the_verdicts_or_the_rulings_fails()
    {
        var (verdicts, rulings) = AllIn();
        verdicts["verdicts"]!.AsArray().Add(Verdict(Export, "in"));
        Assert.Contains(Export, FailureOf(verdicts, rulings), StringComparison.Ordinal);

        (verdicts, rulings) = AllIn();
        rulings["rulings"]!.AsArray().Add(Ruling("L-product-1", "in"));
        Assert.Contains("L-product-1", FailureOf(verdicts, rulings), StringComparison.Ordinal);
    }

    [Fact]
    public void Edges_that_touch_a_removed_node_are_dropped_and_the_found_count_falls()
    {
        var (verdicts, rulings) = AllIn((Verdict("L-code-1", "noise"), Ruling("L-code-1", "noise")));

        var applied = Apply(verdicts, rulings);

        var code = applied.Lenses["code"]!;
        var edges = code["edges"]!.AsArray().Select(static e => $"{e!["from"]}>{e["to"]}").ToArray();
        Assert.Equal(["code:src/invites/expiry.ts>surface:screen/invite-dialog", "code:src/billing/export.ts>code:src/invites/expiry.ts"], edges, StringComparer.Ordinal);
        Assert.Equal(2, code["foundCount"]!.GetValue<int>());
        Assert.Equal("v1", code["mapVersion"]!.GetValue<string>());
        Assert.Equal(["surface:screen/invite-dialog"], code["anchors"]!.AsArray().Select(static a => a!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public void Coverage_rows_follow_the_kept_entries()
    {
        var (verdicts, rulings) = AllIn((Verdict("L-code-1", "noise"), Ruling("L-code-1", "noise")));

        var applied = Apply(verdicts, rulings);

        var row = applied.Coverage["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "code")!;
        Assert.Equal("found", row["state"]!.GetValue<string>());
        Assert.Equal(2, row["found"]!["count"]!.GetValue<int>());
        Assert.Equal(["L-code-2", "L-code-3"], row["found"]!["ids"]!.AsArray().Select(static i => i!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(2, row["mapCoverage"]!["mapped"]!.GetValue<int>());
        Assert.Equal(0, row["mapCoverage"]!["new"]!.GetValue<int>());
    }

    [Fact]
    public void A_lens_with_nothing_left_and_no_limits_is_nothing_found()
    {
        var (verdicts, rulings) = AllIn((Verdict("L-product-1", "noise"), Ruling("L-product-1", "noise")), (Verdict("L-product-2", "noise"), Ruling("L-product-2", "noise")));

        var applied = Apply(verdicts, rulings);

        var row = applied.Coverage["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "product")!;
        Assert.Equal("nothing-found", row["state"]!.GetValue<string>());
        Assert.Equal(0, row["found"]!["count"]!.GetValue<int>());
        Assert.Empty(row["found"]!["ids"]!.AsArray());
    }

    [Fact]
    public void A_lens_with_nothing_left_and_a_recorded_limit_is_not_searched()
    {
        var (verdicts, rulings) = AllIn((Verdict("L-history-1", "out"), Ruling("L-history-1", "out")));

        var applied = Apply(verdicts, rulings);

        var row = applied.Coverage["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "history")!;
        Assert.Equal("not-searched", row["state"]!.GetValue<string>());
        _ = Assert.Single(row["limits"]!.AsArray());
        Assert.Empty(applied.Lenses["history"]!["items"]!.AsArray());
    }

    [Fact]
    public void A_history_item_has_no_node_key_so_its_removal_proposes_no_fix()
    {
        var (verdicts, rulings) = AllIn((Verdict("L-history-1", "noise"), Ruling("L-history-1", "noise")));

        var applied = Apply(verdicts, rulings);

        Assert.Equal("noise", EntryOf(applied, "L-history-1")["result"]!.GetValue<string>());
        Assert.Empty(applied.MapFixes["fixes"]!.AsArray());
    }

    [Fact]
    public void A_truncated_bands_unreviewed_remainder_stays_counted()
    {
        var lenses = Lenses();
        lenses["code"]!["foundCount"] = 150;
        lenses["code"]!["truncated"] = new JsonArray("code");
        var coverage = Coverage();
        coverage["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "code")!["found"]!["count"] = 150;
        var (verdicts, rulings) = AllIn((Verdict("L-code-1", "noise"), Ruling("L-code-1", "noise")));

        var applied = Apply(verdicts, rulings, lenses, coverage);

        Assert.Equal(149, applied.Lenses["code"]!["foundCount"]!.GetValue<int>());
        Assert.Equal(["code"], applied.Lenses["code"]!["truncated"]!.AsArray().Select(static t => t!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(["code"], applied.Relevance["truncated"]!.AsArray().Select(static t => t!.GetValue<string>()), StringComparer.Ordinal);
        var row = applied.Coverage["lenses"]!.AsArray().Single(r => r!["lens"]!.GetValue<string>() == "code")!;
        Assert.Equal(149, row["found"]!["count"]!.GetValue<int>());
    }

    [Fact]
    public void The_relevance_fact_holds_both_verdicts_the_models_and_the_counts()
    {
        var (verdicts, rulings) = AllIn((Verdict(Export, "noise"), Ruling(Export, "noise")), (Verdict("L-code-2", "in"), Ruling("L-code-2", "out")));

        var applied = Apply(verdicts, rulings);

        var relevance = applied.Relevance;
        Assert.Equal(ModelMarker, relevance["agent"]!["reportedModel"]!.GetValue<string>());
        Assert.Equal(["jev"], relevance["judge"]!["providers"]!.AsArray().Select(static p => p!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(AllIds.Length, relevance["counts"]!["reviewed"]!.GetValue<int>());
        Assert.Equal(AllIds.Length - 1, relevance["counts"]!["in"]!.GetValue<int>());
        Assert.Equal(1, relevance["counts"]!["noise"]!.GetValue<int>());
        Assert.Equal(1, relevance["counts"]!["disputed"]!.GetValue<int>());
        Assert.Equal(AllIds.Length, relevance["entries"]!.AsArray().Count);
        var entry = EntryOf(applied, Export);
        Assert.Equal("code", entry["lens"]!.GetValue<string>());
        Assert.Equal("code:src/billing/export.ts", entry["key"]!.GetValue<string>());
        Assert.Equal("noise", entry["agent"]!["class"]!.GetValue<string>());
        Assert.Equal("JD-" + Export, entry["judge"]!["decisionId"]!.GetValue<string>());
        Assert.Equal(0.9, entry["judge"]!["confidence"]!.GetValue<double>());
    }
}
