using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The built-in 'default' review settings, the 'extends' resolver and the repo pick (PR review page spec §7.1, §7.2).</summary>
public sealed class PrReviewDefaultsTests
{
    private static PrReviewSettings Review(params (string Name, PrReviewSettingsEntry Entry)[] entries) =>
        new(Settings: entries.ToDictionary(static e => e.Name, static e => e.Entry, StringComparer.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void Default_has_the_documented_panel_order_no_risky_and_no_checks()
    {
        var resolved = PrReviewDefaults.Resolve(review: null, "default")!;

        Assert.Equal(["requiredChecks", "docMap", "testPlan", "ci", "evidence", "risk", "conversation"], resolved.Panels);
        Assert.Empty(resolved.Risky);
        Assert.Empty(resolved.Checks);
        Assert.Contains("**/packages.lock.json", resolved.Collapse, StringComparer.Ordinal);
        Assert.All(PrReviewDefaults.Keys, k => Assert.Equal("default", resolved.Sources[k]));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_inherits_what_it_leaves_out_and_replaces_what_it_sets()
    {
        var review = Review(("sindri", new(Extends: "default", Collapse: ["**/*.lock"])));

        var resolved = PrReviewDefaults.Resolve(review, "sindri")!;

        Assert.Equal(["**/*.lock"], resolved.Collapse);
        Assert.Equal(PrReviewDefaults.Panels, resolved.Panels);
        Assert.Equal("sindri", resolved.Sources["collapse"]);
        Assert.Equal("default", resolved.Sources["panels"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_chain_takes_each_key_from_the_nearest_entry_that_sets_it()
    {
        var review = Review(("base", new(Panels: ["ci"], Collapse: ["a"])), ("child", new(Extends: "base", Collapse: ["b"])));

        var resolved = PrReviewDefaults.Resolve(review, "child")!;

        Assert.Equal(["ci"], resolved.Panels);
        Assert.Equal(["b"], resolved.Collapse);
        Assert.Equal("base", resolved.Sources["panels"]);
        Assert.Equal("child", resolved.Sources["collapse"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_or_an_unknown_name_does_not_resolve()
    {
        var review = Review(("a", new(Extends: "b")), ("b", new(Extends: "a")));

        Assert.Null(PrReviewDefaults.Resolve(review, "a"));
        Assert.Null(PrReviewDefaults.Resolve(review, "ghost"));
    }

    [Fact, Trait("Lane", "unit")]
    public void For_repo_picks_the_one_entry_naming_it()
    {
        var review = Review(("sindri", new(Panels: ["ci"]))) with { Projects = [new("prj_1", ["o/a"], "sindri"), new("prj_2", ["o/b"])] };

        var pick = PrReviewDefaults.For(review, "o/a");

        Assert.False(pick.Ambiguous);
        Assert.Equal("sindri", pick.Settings!.Name);
        Assert.Equal(["ci"], pick.Settings.Panels);
    }

    [Fact, Trait("Lane", "unit")]
    public void For_repo_with_several_entries_is_ambiguous_and_carries_the_choices()
    {
        var review = new PrReviewSettings(Projects: [new("prj_1", ["o/a"]), new("prj_2", ["o/a"])]);

        var pick = PrReviewDefaults.For(review, "o/a");

        Assert.True(pick.Ambiguous);
        Assert.Null(pick.Settings);
        Assert.True(pick.Choices.Select(static c => c.Project).SequenceEqual(["prj_1", "prj_2"], StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void For_repo_with_no_entry_gets_default()
    {
        var pick = PrReviewDefaults.For(new PrReviewSettings(Projects: [new("prj_1", ["o/a"])]), "o/other");

        Assert.Equal("default", pick.Settings!.Name);
        Assert.Empty(pick.Choices);
        Assert.Equal("default", PrReviewDefaults.For(review: null, "o/a").Settings!.Name);
    }
}
