using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Extract;

/// <summary>Spec 2026-10-27 §2.1 "Keys": the key comes from the repo name and the path and nothing else.</summary>
public sealed partial class HomeFeaturesKeyTests
{
    private static string[] Modules(params string[] dirs) =>
        [.. dirs.SelectMany(d => new[] { d + "/package.json", d + "/x.ts" })];

    [Fact, Trait("Lane", "unit")]
    public void Two_repos_with_the_same_folders_get_disjoint_keys_and_a_session_scoped_to_one_sees_none_of_the_other()
    {
        var paths = Modules("packages/api", "packages/web");

        var one = HomeFeatures.Assign(HomeTree.Repo("one"), paths);
        var two = HomeFeatures.Assign(HomeTree.Repo("two"), paths);

        Assert.Empty(HomeTree.FeatureKeys(one.Map).Intersect(HomeTree.FeatureKeys(two.Map), StringComparer.Ordinal));
        Assert.Equal(["feature:one.packages.api", "feature:one.packages.web"], HomeTree.FeatureKeys(one.Map), StringComparer.Ordinal);
        var joined = one.Map.Plus(two.Map);
        var graph = new GraphSnapshot([.. joined.Nodes], [.. joined.Edges], [], []);
        var scoped = RepoScope.NodesFor(graph, ContextRepoName.From("one"));
        Assert.Contains("feature:one.packages.api", scoped);
        Assert.DoesNotContain("feature:two.packages.api", scoped);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dot_an_underscore_and_a_slash_in_a_name_give_three_different_keys()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, Modules("a/b.c", "a/b_c", "a.b/c"));

        Assert.Equal(["feature:work.a.b__c", "feature:work.a.b_c", "feature:work.a_b.c"], HomeTree.FeatureKeys(home.Map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Folders_that_differ_only_in_case_all_get_a_hash_suffix_and_a_new_unrelated_sibling_renames_nobody()
    {
        var both = HomeFeatures.Assign(HomeTree.Work, Modules("Models", "models", "other"));
        var more = HomeFeatures.Assign(HomeTree.Work, Modules("Models", "models", "other", "another"));

        var models = HomeTree.FeatureKeys(both.Map).Where(k => k.StartsWith("feature:work.models", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, models.Count);
        Assert.All(models, k => Assert.Matches(Suffixed, k));
        Assert.Equal(2, models.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("feature:work.other", HomeTree.FeatureKeys(both.Map), StringComparer.Ordinal);
        Assert.Subset(HomeTree.FeatureKeys(more.Map).ToHashSet(StringComparer.Ordinal), HomeTree.FeatureKeys(both.Map).ToHashSet(StringComparer.Ordinal));
    }

    [GeneratedRegex("^feature:work\\.models-[0-9a-f]{6}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Suffixed { get; }

    [Fact, Trait("Lane", "unit")]
    public void A_folder_named_underscore_does_not_collide_with_the_loose_file_leaf()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, [.. HomeTree.Files("src", 160), "src/_/x.ts"]);

        Assert.Equal("feature:work.src._", HomeTree.HomeOf(home, "src/f000.ts"));
        Assert.Equal("feature:work.src.__", HomeTree.HomeOf(home, "src/_/x.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Repos_named_a_dot_b_and_a_underscore_b_get_different_keys()
    {
        var dotted = HomeFeatures.Assign(HomeTree.Repo("a.b"), ["x.ts"]);
        var scored = HomeFeatures.Assign(HomeTree.Repo("a_b"), ["x.ts"]);

        Assert.Equal(["feature:a_b"], HomeTree.FeatureKeys(dotted.Map), StringComparer.Ordinal);
        Assert.Equal(["feature:a__b"], HomeTree.FeatureKeys(scored.Map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_non_ascii_name_is_kept_and_lowercased_by_the_key_function()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, Modules("src/Café"));

        Assert.Equal(["feature:work.src.café"], HomeTree.FeatureKeys(home.Map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_key_over_200_characters_keeps_its_head_and_ends_in_8_hex_characters_of_the_path()
    {
        var one = string.Concat(Enumerable.Repeat("a", 250));
        var dirs = new[] { $"{one}/{one}/{one}/x", $"{one}/{one}/{one}/y" };

        var home = HomeFeatures.Assign(HomeTree.Work, Modules(dirs));

        var keys = HomeTree.FeatureKeys(home.Map);
        Assert.Equal(2, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, k =>
        {
            Assert.True(k.Length <= 200, k.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.StartsWith("feature:work.aaaa", k, StringComparison.Ordinal);
            Assert.Matches("-[0-9a-f]{8}$", k);
            Assert.True(NodeKey.TryFrom(k, out _));
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_the_key_function_rejects_gets_no_home_and_breaks_nothing_for_its_neighbors()
    {
        var tooLong = string.Concat(Enumerable.Repeat("d/", 600)) + "x.ts";

        var home = HomeFeatures.Assign(HomeTree.Work, ["ok/a.ts", "line\nbreak/b.ts", tooLong]);

        Assert.Equal(["ok/a.ts"], home.Files.Keys, StringComparer.Ordinal);
    }
}
