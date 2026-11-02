using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

/// <summary>Spec 2026-10-27 §10.1 and §10.4: a pin is an input to step 3, so a home feature never spans two pins and each leaf belongs to its pin.</summary>
public sealed class HomeFeaturesPinsTests
{
    private static ConceptPin Pin(string id, params string[] paths) => ConceptPin.Of(ConceptId.From(id), id, summary: null, features: null, paths);

    private static ContextRepo Repo(params ConceptPin[] pins) => HomeTree.Work with { Concepts = pins };

    private static Dictionary<string, string> Homes(HomeAssignment home) => home.Files.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal);

    private static string[] Members(HomeAssignment home, string concept) =>
        [.. home.Map.Edges.Where(e => e is { Type: EdgeType.BelongsTo, Provenance: Provenance.Deterministic } && e.To.Value == concept).Select(e => e.From.Value).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public void A_leaf_wholly_inside_one_pin_keeps_its_plain_key_and_belongs_to_the_pin()
    {
        var home = HomeFeatures.Assign(Repo(Pin("billing", "src/billing/**")), ["src/billing/a.ts", "src/billing/b.ts"]);

        Assert.All(home.Files.Values, key => Assert.Equal("feature:work", key.Value));
        Assert.Equal(["feature:work"], Members(home, "concept:billing"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_leaf_spanning_a_pin_and_the_rest_is_split_and_the_unpinned_remainder_keeps_the_plain_key()
    {
        var home = HomeFeatures.Assign(Repo(Pin("billing", "src/billing/**")), ["src/billing/a.ts", "src/billing/b.ts", "src/util/c.ts"]);

        Assert.Equal("feature:work~billing", HomeTree.HomeOf(home, "src/billing/a.ts"));
        Assert.Equal("feature:work~billing", HomeTree.HomeOf(home, "src/billing/b.ts"));
        Assert.Equal("feature:work", HomeTree.HomeOf(home, "src/util/c.ts"));
        Assert.Equal(["feature:work~billing"], Members(home, "concept:billing"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_leaf_spanning_two_pins_gets_one_leaf_per_pin()
    {
        var home = HomeFeatures.Assign(Repo(Pin("billing", "src/billing/**"), Pin("auth", "src/auth/**")), ["src/billing/a.ts", "src/auth/b.ts", "src/other/c.ts"]);

        Assert.Equal("feature:work~billing", HomeTree.HomeOf(home, "src/billing/a.ts"));
        Assert.Equal("feature:work~auth", HomeTree.HomeOf(home, "src/auth/b.ts"));
        Assert.Equal("feature:work", HomeTree.HomeOf(home, "src/other/c.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_under_two_pins_belongs_to_the_first_pin_in_the_list()
    {
        var home = HomeFeatures.Assign(Repo(Pin("wide", "src/**"), Pin("narrow", "src/billing/**")), ["src/billing/a.ts", "src/x.ts"]);

        Assert.Equal(["feature:work"], Members(home, "concept:wide"));
        Assert.Empty(Members(home, "concept:narrow"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_that_splits_one_directory_of_the_design_notes_package_changes_only_the_files_of_that_pin()
    {
        var paths = HomeTree.ApiPackage();
        var plain = Homes(HomeFeatures.Assign(HomeTree.Work, paths));

        var home = HomeFeatures.Assign(Repo(Pin("mw", "packages/api/src/middleware/f00*")), paths);

        var changed = Homes(home).Where(p => plain[p.Key] != p.Value).Select(p => p.Key).ToList();
        Assert.Equal(10, changed.Count);
        Assert.All(changed, f => Assert.StartsWith("packages/api/src/middleware/f00", f, StringComparison.Ordinal));
        Assert.All(changed, f => Assert.Equal("feature:work.packages.api.src.middleware~mw", HomeTree.HomeOf(home, f)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_that_covers_a_directory_wholly_adds_no_suffix_to_any_key()
    {
        var paths = HomeTree.ApiPackage();
        var plain = Homes(HomeFeatures.Assign(HomeTree.Work, paths));

        var home = HomeFeatures.Assign(Repo(Pin("billing", "packages/api/src/services/billing/**")), paths);

        Assert.Equal(plain, Homes(home));
        Assert.Equal(["feature:work.packages.api.src.services.billing.invoices", "feature:work.packages.api.src.services.billing.payments"], Members(home, "concept:billing"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tilde_in_a_directory_name_is_written_double_and_a_dot_in_a_pin_id_is_written_underscore()
    {
        var home = HomeFeatures.Assign(Repo(Pin("billing.core", "a~b/x.ts")), ["a~b/package.json", "a~b/x.ts", "a~b/y.ts"]);

        Assert.Equal("feature:work.a~~b~billing_core", HomeTree.HomeOf(home, "a~b/x.ts"));
        Assert.Equal("feature:work.a~~b", HomeTree.HomeOf(home, "a~b/y.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_the_profile_repeats_for_two_repos_is_one_pin_so_both_repos_use_its_one_id()
    {
        var shared = Pin("billing", "src/billing/**");
        var one = HomeFeatures.Assign(Repo(shared), ["src/billing/a.ts", "src/b.ts"], extracted: null, pins: [shared, shared]);

        Assert.Equal("feature:work~billing", HomeTree.HomeOf(one, "src/billing/a.ts"));
    }
}
