using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Map;

public sealed class MapRulesetTests
{
    private static ContextRepo Repo(string name, params string[] deny) => new(ContextRepoName.From(name), "/work/" + name) { DenyPaths = deny };

    [Fact, Trait("Lane", "unit")]
    public void The_same_rules_hash_the_same_in_any_order()
    {
        var one = MapRuleset.Of([Repo("a", "x/**", "y/**"), Repo("b", "z/**")]);
        var other = MapRuleset.Of([Repo("b", "z/**"), Repo("a", "y/**", "x/**")]);

        Assert.Equal(one, other, StringComparer.Ordinal);
        Assert.Equal(64, one.Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_changed_deny_glob_changes_the_hash() =>
        Assert.NotEqual(MapRuleset.Of([Repo("a", "x/**")]), MapRuleset.Of([Repo("a", "x/**", "y/**")]), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_same_glob_in_another_repo_changes_the_hash() =>
        Assert.NotEqual(MapRuleset.Of([Repo("a", "x/**"), Repo("b")]), MapRuleset.Of([Repo("a"), Repo("b", "x/**")]), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_changed_concept_pin_changes_the_hash()
    {
        var plain = Repo("a");
        var pinned = plain with { Concepts = [ConceptPin.Of(id: null, "Scheduling", summary: null, ["feature:shifts"], paths: null)] };
        var renamed = plain with { Concepts = [ConceptPin.Of(id: null, "Rota", summary: null, ["feature:shifts"], paths: null)] };

        Assert.NotEqual(MapRuleset.Of([plain]), MapRuleset.Of([pinned]), StringComparer.Ordinal);
        Assert.NotEqual(MapRuleset.Of([pinned]), MapRuleset.Of([renamed]), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Demote_globs_change_the_hash_and_the_same_ones_hash_the_same()
    {
        var plain = Repo("a", "x/**");
        var demoting = plain with { DemotePaths = ["docs/**"] };

        Assert.NotEqual(MapRuleset.Of([plain]), MapRuleset.Of([demoting]), StringComparer.Ordinal);
        Assert.Equal(MapRuleset.Of([demoting]), MapRuleset.Of([plain with { DemotePaths = ["docs/**"] }]), StringComparer.Ordinal);
    }
}
