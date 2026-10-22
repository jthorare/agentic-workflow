using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.Tests.Recipes;

/// <summary>The Recipes page's rows (recipes spec §1, §7): plain names with a readable fallback, the standing and out-of-date words, size, needs and produces, the fingerprint.</summary>
[Lane(TestLane.Unit)]
public sealed class RecipesPageRowsTests
{
    private const string Hash = "sha256:e204c3b4ff1755b975bc92713ab9a7f4ca9ea54a8888a5b32b06bba4fac8c636";

    private static RecipeSummaryView Served(string id = "workflow.phase", string standing = "approved", IReadOnlyList<string>? stale = null, string? displayName = null) =>
        new(id, Version: 2, Hash, standing, Name: null, displayName, stale ?? [],
            [new RecipeInputView("fact:a", "A", FactType.Text, "text", Required: true, Generate: null), new RecipeInputView("fact:b", "B", FactType.Text, "text", Required: false, Generate: null)],
            [new RecipeOutputView("fact:o", "O", FactType.Text, "done")],
            [new RecipeStateView("seed", Order: 1, BranchOf: null), new RecipeStateView("done", Order: 2, BranchOf: null), new RecipeStateView("alt", Order: null, BranchOf: "seed")]);

    [Theory]
    [InlineData("workflow.phase", null, "Build and prove")]
    [InlineData("workflow.line", null, "Idea to merged pull request")]
    [InlineData("workflow.phase", "Ship one phase", "Ship one phase")]
    [InlineData("workflow.bug-fix", null, "Bug Fix")]
    public void The_name_is_served_else_mapped_else_the_id_made_readable(string id, string? displayName, string expected)
    {
        var row = new RecipeRowViewModel(Served(id, displayName: displayName));

        Assert.Equal(expected, row.Name);
        Assert.Equal(id["workflow.".Length..], row.ShortId);
    }

    [Theory]
    [InlineData("approved", RecipeStanding.Approved, "Approved")]
    [InlineData("unapproved", RecipeStanding.NotApproved, "Not approved")]
    [InlineData("revoked", RecipeStanding.Revoked, "Revoked")]
    [InlineData("something-new", RecipeStanding.NotApproved, "Not approved")]
    internal void Standing_reads_in_one_word(string served, RecipeStanding standing, string word)
    {
        var row = new RecipeRowViewModel(Served(standing: served));

        Assert.Equal((standing, word), (row.Standing, row.StandingText));
    }

    [Fact]
    public void A_stale_approved_recipe_is_out_of_date_with_each_reason_in_words()
    {
        var row = new RecipeRowViewModel(Served(stale: ["pin-drift", "child-stale", "legacy-shape", "other"]));

        Assert.True(row.IsOutOfDate);
        Assert.Equal("Out of date", row.OutOfDateText);
        Expect.Sequence(
            ["A step it uses changed since it was approved", "A workflow it starts is out of date", "It is written in an old format", "It needs a look"],
            row.StaleReasons);
        Assert.True(row.NeedsApproval);
    }

    [Fact]
    public void An_approved_current_recipe_needs_nothing()
    {
        var row = new RecipeRowViewModel(Served());

        Assert.False(row.IsOutOfDate);
        Assert.False(row.NeedsApproval);
        Assert.Empty(row.StaleReasons);
    }

    [Theory]
    [InlineData("unapproved")]
    [InlineData("revoked")]
    public void A_recipe_that_is_not_approved_needs_approval(string standing) => Assert.True(new RecipeRowViewModel(Served(standing: standing)).NeedsApproval);

    [Fact]
    public void Size_needs_produces_version_and_fingerprint_come_from_what_is_served()
    {
        var row = new RecipeRowViewModel(Served());

        Assert.Equal(("3 steps", "2 in, 1 out", "v2", "e204c3b4"), (row.StepsText, row.FlowText, row.VersionText, row.Fingerprint));
    }

    [Fact]
    public void A_list_that_serves_no_inputs_outputs_or_states_reads_as_none()
    {
        var row = new RecipeRowViewModel(new RecipeSummaryView("workflow.step", 1, Hash, "approved", Name: null));

        Assert.Equal(("0 steps", "0 in, 0 out", false), (row.StepsText, row.FlowText, row.IsOutOfDate));
    }

    [Fact]
    public void An_approved_row_opens_the_recipe_by_id_and_another_by_its_hash()
    {
        Assert.Equal(new DeepLink(DeepLinkKind.Recipe, "workflow.phase"), new RecipeRowViewModel(Served()).Link);
        Assert.Equal(new DeepLink(DeepLinkKind.Recipe, "workflow.phase@e204c3b4ff17"), new RecipeRowViewModel(Served(standing: "unapproved")).Link);
    }

    [Theory]
    [InlineData("build", true)]
    [InlineData("PHASE", true)]
    [InlineData("zzz", false)]
    [InlineData("", true)]
    public void Search_matches_the_name_or_the_id(string query, bool matches) => Assert.Equal(matches, new RecipeRowViewModel(Served()).Matches(query));

    [Fact]
    public void The_row_has_a_name_a_screen_reader_can_read()
    {
        Assert.Equal("Build and prove, workflow.phase, Approved, version 2", new RecipeRowViewModel(Served()).AccessibleName);
        Assert.Equal("Build and prove, workflow.phase, Not approved, out of date, version 2", new RecipeRowViewModel(Served(standing: "unapproved", stale: ["pin-drift"])).AccessibleName);
    }
}
