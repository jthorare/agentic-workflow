using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

/// <summary>Spec 2026-10-27 §2.1: the module root rule, and which files get no home at all.</summary>
public sealed class HomeFeaturesModuleTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_nested_package_json_is_its_own_module_and_not_part_of_the_one_around_it()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, ["package.json", "a.ts", "packages/web/package.json", "packages/web/b.ts"]);

        Assert.Equal("feature:work", HomeTree.HomeOf(home, "a.ts"));
        Assert.Equal("feature:work.packages.web", HomeTree.HomeOf(home, "packages/web/b.ts"));
        Assert.DoesNotContain(home.Map.Edges, e => e.Type == EdgeType.PartOf);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_csproj_beside_cs_files_is_a_module_though_the_csproj_is_not_a_readable_source_file()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, ["src/Api/Api.csproj", "src/Api/Orders.cs", "src/Core/Core.csproj", "src/Core/Order.cs", "src/Core/Money.cs"]);

        Assert.Equal("feature:work.src.api", HomeTree.HomeOf(home, "src/Api/Orders.cs"));
        Assert.Equal("feature:work.src.core", HomeTree.HomeOf(home, "src/Core/Money.cs"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_package_file_is_one_module_keyed_by_the_repo()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, ["src/a.ts", "scripts/b.js", "root.mjs"]);

        Assert.Equal(["feature:work"], HomeTree.FeatureKeys(home.Map));
        Assert.Equal(3, home.Files.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_package_json_under_node_modules_marks_no_module()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, ["node_modules/left-pad/package.json", "a.ts"]);

        Assert.Equal(["feature:work"], HomeTree.FeatureKeys(home.Map));
    }

    [Fact, Trait("Lane", "unit")]
    public void Src_scripts_and_root_files_are_all_homed_never_left_out()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, [.. HomeTree.Files("src/a", 100), .. HomeTree.Files("src/b", 100), .. HomeTree.Files("scripts", 10), "tool.js"]);

        Assert.Equal(211, home.Files.Count);
        Assert.Equal("feature:work._", HomeTree.HomeOf(home, "scripts/f000.ts"));
        Assert.Equal("feature:work._", HomeTree.HomeOf(home, "tool.js"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/__tests__/a.ts")]
    [InlineData("tests/a.ts")]
    [InlineData("src/a.test.ts")]
    [InlineData("src/a.spec.tsx")]
    [InlineData("src/tsconfig.json")]
    [InlineData("package-lock.json")]
    [InlineData("src/config.json")]
    [InlineData("src/App/bin/Debug/out.cs")]
    [InlineData("src/App/obj/Debug/out.cs")]
    [InlineData("src/readme.md")]
    [InlineData("node_modules/x/index.js")]
    [InlineData("dist/index.js")]
    public void A_test_manifest_json_build_output_or_non_source_file_gets_no_home(string path)
    {
        var home = HomeFeatures.Assign(HomeTree.Work, ["src/real.ts", path]);

        Assert.False(home.Files.ContainsKey(path), path);
        Assert.Equal(["code:src/real.ts"], HomeTree.Homes(home.Map).Select(e => e.From.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_demoted_or_denied_file_gets_no_home_but_its_neighbors_do()
    {
        var repo = HomeTree.Repo("work", demote: ["legacy/**"], deny: ["secret/**"]);

        var home = HomeFeatures.Assign(repo, ["src/a.ts", "legacy/old.ts", "secret/key.ts"]);

        Assert.Equal(["src/a.ts"], home.Files.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_is_part_of_the_home_feature_of_the_file_that_defines_it()
    {
        var extracted = MapExtractor.Extract(HomeTree.Work.Name,
        [
            new RepoFile("src/tabs/ids.ts", "export type CommandId = \"close-tab\";"),
            new RepoFile("src/layout/ids.ts", "export type CommandId = \"split\";"),
        ]);
        var paths = new[] { "a/package.json", "src/tabs/ids.ts", "src/layout/ids.ts" };

        var home = HomeFeatures.Assign(HomeTree.Work, paths, extracted);

        var claim = Assert.Single(home.Map.Edges, e => e.Type == EdgeType.PartOf && e.From.Value == "surface:command/close-tab");
        Assert.Equal(("feature:work", Provenance.Deterministic), (claim.To.Value, claim.Provenance));
        Assert.Equal(2, home.Map.Edges.Count(e => e.Type == EdgeType.PartOf));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_paths_give_the_same_map_in_any_order()
    {
        var paths = HomeTree.ApiPackage();

        var first = HomeFeatures.Assign(HomeTree.Work, paths);
        var second = HomeFeatures.Assign(HomeTree.Work, [.. paths.AsEnumerable().Reverse()]);

        Assert.Equal(first.Map.Edges.Select(e => (e.From.Value, e.To.Value, e.Type)), second.Map.Edges.Select(e => (e.From.Value, e.To.Value, e.Type)));
        Assert.Equal(first.Map.Nodes.Select(n => n.Key.Value), second.Map.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_labeler_is_asked_about_every_feature_with_paths_only_and_a_label_replaces_the_path_name()
    {
        var home = HomeFeatures.Assign(HomeTree.Work, ["a/package.json", .. HomeTree.Files("a", 3)]);

        var request = Assert.Single(home.LabelRequests);
        Assert.Equal(["a/f000.ts", "a/f001.ts", "a/f002.ts"], request.Files, StringComparer.Ordinal);
        var named = home.WithLabels([new ClusterLabel(request.Id, "Alpha tools")]);

        Assert.Equal("Alpha tools", named.Map.Nodes.Single(n => n.Key.Kind == NodeKind.Feature).Label);
        Assert.Equal("A", home.Map.Nodes.Single(n => n.Key.Kind == NodeKind.Feature).Label);
    }
}
