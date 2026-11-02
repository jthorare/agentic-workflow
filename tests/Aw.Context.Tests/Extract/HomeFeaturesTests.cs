using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

/// <summary>Spec 2026-10-27 §2.1 and §6 step 3: one home feature per file, chosen by directory, split at the cap.</summary>
public sealed class HomeFeaturesTests
{
    private const string Api = "feature:work.packages.api";

    [Fact, Trait("Lane", "unit")]
    public void The_design_notes_package_makes_18_leaves_the_biggest_150_and_one_home_per_file()
    {
        var paths = HomeTree.ApiPackage();

        var home = HomeFeatures.Assign(HomeTree.Work, paths);

        var homes = HomeTree.Homes(home.Map);
        Assert.Equal(1_704, homes.Count);
        Assert.Equal(1_704, homes.Select(e => e.From.Value).Distinct(StringComparer.Ordinal).Count());
        var holders = homes.GroupBy(e => e.To.Value, StringComparer.Ordinal).ToList();
        Assert.Equal(18, holders.Count);
        Assert.Equal(150, holders.Max(g => g.Count()));
        Assert.Equal(Api + ".src.routes.v1.public", holders.Single(g => g.Skip(149).Any()).Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_design_notes_package_also_makes_the_group_features_that_hold_no_file_each_part_of_its_parent()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, HomeTree.ApiPackage());

        var holders = HomeTree.Homes(home.Map).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        string[] groups = [Api, Api + ".src", Api + ".src.routes", Api + ".src.routes.v1", Api + ".src.services", Api + ".src.services.billing", Api + ".src.services.auth", Api + ".src.repositories"];
        Assert.All(groups, g => Assert.DoesNotContain(g, holders));
        Assert.All(groups, g => Assert.Contains(home.Map.Nodes, n => n.Key.Value == g));
        Assert.Equal(26, HomeTree.FeatureKeys(home.Map).Length);
        Assert.Contains(home.Map.Edges, e => e is { Type: EdgeType.PartOf, Provenance: Provenance.Deterministic } && e.From.Value == Api + ".src.routes.v1" && e.To.Value == Api + ".src.routes");
        Assert.Contains(home.Map.Edges, e => e.Type == EdgeType.PartOf && e.From.Value == Api + ".src.routes.v1.admin" && e.To.Value == Api + ".src.routes.v1");
        Assert.Contains(home.Map.Edges, e => e.Type == EdgeType.PartOf && e.From.Value == Api + ".src" && e.To.Value == Api);
        Assert.Equal(Api + ".src.routes.v1._", HomeTree.HomeOf(home, "packages/api/src/routes/v1/f000.ts"));
        Assert.Equal(Api + ".src._", HomeTree.HomeOf(home, "packages/api/src/f000.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_home_edge_and_feature_is_deterministic_and_the_edge_cites_its_file()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, ["a/package.json", .. HomeTree.Files("a", 3)]);

        var edge = HomeTree.Homes(home.Map).OrderBy(e => e.From.Value, StringComparer.Ordinal).First();
        Assert.Equal(("code:a/f000.ts", "feature:work.a", Provenance.Deterministic, 1.0), (edge.From.Value, edge.To.Value, edge.Provenance, edge.Confidence));
        Assert.Equal(["repo:work/a/f000.ts"], edge.EvidenceOrEmpty.Select(r => r.Key.Value), StringComparer.Ordinal);
        Assert.All(home.Map.Nodes.Where(n => n.Key.Kind == NodeKind.Feature), n => Assert.Equal(Provenance.Deterministic, n.Provenance));
        Assert.Contains(home.Map.Nodes, n => n.Key.Value == "code:a/f000.ts");
        Assert.All(home.Map.Edges, e => Assert.True(EdgeRules.Allows(e.Type, e.From.Kind, e.To.Kind), $"{e.From.Value} {e.Type} {e.To.Value}"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_directory_at_exactly_the_cap_is_one_feature_and_one_file_more_splits_it()
    {
        var atCap = HomeFeatures.Assign(HomeTree.Work, [.. HomeTree.Files("d/a", 75), .. HomeTree.Files("d/b", 75)]);
        var over = HomeFeatures.Assign(HomeTree.Work, [.. HomeTree.Files("d/a", 76), .. HomeTree.Files("d/b", 75)]);

        Assert.Equal(["feature:work"], HomeTree.Homes(atCap.Map).Select(e => e.To.Value).Distinct(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["feature:work.d.a", "feature:work.d.b"], HomeTree.Homes(over.Map).Select(e => e.To.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains(over.Map.Nodes, n => n.Key.Value == "feature:work.d");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_flat_directory_over_the_cap_stays_one_feature_under_its_own_key()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, [.. HomeTree.Files("flat", 400), .. HomeTree.Files("small", 2)]);

        var holders = HomeTree.Homes(home.Map).GroupBy(e => e.To.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        Assert.Equal(400, holders["feature:work.flat"]);
        Assert.Equal(2, holders["feature:work.small"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Loose_files_over_the_cap_beside_a_subdirectory_stay_one_over_cap_underscore_leaf()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, [.. HomeTree.Files("x", 200), .. HomeTree.Files("x/sub", 3)]);

        var holders = HomeTree.Homes(home.Map).GroupBy(e => e.To.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        Assert.Equal(200, holders["feature:work.x._"]);
        Assert.Equal(3, holders["feature:work.x.sub"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_module_over_the_cap_keeps_src_apart_and_its_root_files_and_other_folders_in_one_leaf_while_that_remainder_fits()
    {
        var home = HomeFeatures.Assign(HomeTree.Work,
        [
            "pkg/package.json", .. HomeTree.Files("pkg/src/a", 100), .. HomeTree.Files("pkg/src/b", 100),
            .. HomeTree.Files("pkg", 5, "root"), .. HomeTree.Files("pkg/scripts", 10), .. HomeTree.Files("pkg/prisma", 3),
        ]);

        Assert.Equal("feature:work.pkg.src.a", HomeTree.HomeOf(home, "pkg/src/a/f000.ts"));
        Assert.Equal("feature:work.pkg._", HomeTree.HomeOf(home, "pkg/root000.ts"));
        Assert.Equal("feature:work.pkg._", HomeTree.HomeOf(home, "pkg/scripts/f000.ts"));
        Assert.Equal("feature:work.pkg._", HomeTree.HomeOf(home, "pkg/prisma/f002.ts"));
        Assert.Equal(218, home.Files.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_remainder_over_the_cap_gives_each_other_folder_its_own_group()
    {
        var home = HomeFeatures.Assign(HomeTree.Work,
        [
            "pkg/package.json", .. HomeTree.Files("pkg/src", 60), .. HomeTree.Files("pkg/scripts", 100), .. HomeTree.Files("pkg/prisma", 100), .. HomeTree.Files("pkg", 4, "root"),
        ]);

        Assert.Equal("feature:work.pkg.src", HomeTree.HomeOf(home, "pkg/src/f000.ts"));
        Assert.Equal("feature:work.pkg.scripts", HomeTree.HomeOf(home, "pkg/scripts/f000.ts"));
        Assert.Equal("feature:work.pkg.prisma", HomeTree.HomeOf(home, "pkg/prisma/f000.ts"));
        Assert.Equal("feature:work.pkg._", HomeTree.HomeOf(home, "pkg/root000.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_directory_lookup_names_the_feature_at_each_feature_directory_and_never_an_underscore_leaf()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, HomeTree.ApiPackage());

        Assert.Equal(Api, home.Directories["packages/api"].Value);
        Assert.Equal(Api + ".src.routes.v1", home.Directories["packages/api/src/routes/v1"].Value);
        Assert.Equal(Api + ".src.routes.v1.public", home.Directories["packages/api/src/routes/v1/public"].Value);
        Assert.Equal(Api + ".src.config", home.Directories["packages/api/src/config"].Value);
        Assert.DoesNotContain(home.Directories.Values, k => k.Value.EndsWith("._", StringComparison.Ordinal));
        Assert.Equal(1_704, home.Files.Count);
    }
}
