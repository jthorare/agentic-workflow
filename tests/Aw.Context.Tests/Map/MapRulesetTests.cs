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
    public void A_changed_depends_on_or_owns_entry_changes_the_hash()
    {
        var plain = Repo("a") with { Concepts = [ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["src/**"])] };
        var depends = Repo("a") with { Concepts = [ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["src/**"]).Declaring(["orders"], owns: null)] };
        var owns = Repo("a") with { Concepts = [ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["src/**"]).Declaring(dependsOn: null, ["invoices"])] };

        Assert.NotEqual(MapRuleset.Of([plain]), MapRuleset.Of([depends]), StringComparer.Ordinal);
        Assert.NotEqual(MapRuleset.Of([plain]), MapRuleset.Of([owns]), StringComparer.Ordinal);
        Assert.NotEqual(MapRuleset.Of([depends]), MapRuleset.Of([owns]), StringComparer.Ordinal);
    }

    private static ContextRepo Pinned(string name, string? summary = null, string[]? aliases = null, string[]? features = null) =>
        Repo("a") with { Concepts = [ConceptPin.Of(ConceptId.From("billing"), name, summary, features ?? ["feature:invoices"], paths: ["src/**"]).WithAliases(aliases)] };

    [Fact, Trait("Lane", "unit")]
    public void A_pin_edit_that_changes_its_hash_changes_the_whole_ruleset_too()
    {
        var before = Pinned("Billing");

        foreach (var edited in new[] { Pinned("Payments"), Pinned("Billing", features: ["feature:invoices", "feature:refunds"]) })
        {
            Assert.NotEqual(MapRuleset.PinHashes([before])["billing"], MapRuleset.PinHashes([edited])["billing"], StringComparer.Ordinal);
            Assert.NotEqual(MapRuleset.Of([before]), MapRuleset.Of([edited]), StringComparer.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_summary_or_alias_edit_changes_neither_the_pins_hash_nor_the_ruleset()
    {
        var before = Pinned("Billing");
        var after = Pinned("Billing", summary: "How we take money", aliases: ["payments"]);

        Assert.Equal(MapRuleset.PinHashes([before]), MapRuleset.PinHashes([after]));
        Assert.Equal(MapRuleset.Of([before]), MapRuleset.Of([after]), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_set_in_two_repos_has_one_hash_and_each_pin_its_own_id()
    {
        var shared = ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["src/**"]);
        var other = ConceptPin.Of(id: null, "Self scheduling", summary: null, features: null, paths: null);
        var hashes = MapRuleset.PinHashes([Repo("a") with { Concepts = [shared] }, Repo("b") with { Concepts = [shared, other] }]);

        Assert.Equal(["billing", "self-scheduling"], hashes.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(64, hashes["billing"].Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_summary_or_alias_edit_leaves_the_hash_unchanged()
    {
        var plain = Repo("a") with { Concepts = [ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: null, paths: ["src/**"])] };
        var described = Repo("a") with { Concepts = [ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: "How we take money", features: null, paths: ["src/**"]).WithAliases(["payments"])] };

        Assert.Equal(MapRuleset.Of([plain]), MapRuleset.Of([described]), StringComparer.Ordinal);
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
