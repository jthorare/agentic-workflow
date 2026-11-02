using System.Globalization;
using Aw.Context.Core.Extract;

namespace Aw.Context.Tests.Extract;

/// <summary>Spec 2026-10-27 §2.1 "Stability" and §2.4: a key follows the path, so a rebuild or a change elsewhere keeps it.</summary>
public sealed class HomeFeaturesStabilityTests
{
    private static Dictionary<string, string> Homes(IEnumerable<string> paths)
    {
        var home = HomeFeatures.Assign(HomeTree.Work, [.. paths]);
        return home.Files.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal);
    }

    private static List<string> Tree() =>
    [
        "a/package.json", .. HomeTree.Files("a/one", 90), .. HomeTree.Files("a/two", 80),
        "b/package.json", .. HomeTree.Files("b", 10),
    ];

    [Fact, Trait("Lane", "unit")]
    public void Building_twice_gives_the_same_homes() =>
        Assert.Equal(Homes(Tree()), Homes(Tree()));

    [Fact, Trait("Lane", "unit")]
    public void Moving_one_file_between_two_directories_changes_only_that_file()
    {
        var before = Homes(Tree());
        var moved = Tree();
        _ = moved.Remove("a/one/f000.ts");
        moved.Add("a/two/moved.ts");

        var after = Homes(moved);

        var changed = after.Where(p => !before.TryGetValue(p.Key, out var was) || was != p.Value).Select(p => p.Key).ToList();
        Assert.Equal(["a/two/moved.ts"], changed, StringComparer.Ordinal);
        Assert.All(before.Keys.Where(k => k != "a/one/f000.ts"), k => Assert.Equal(before[k], after[k]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Renaming_a_directory_changes_only_the_files_below_it()
    {
        var before = Homes(Tree());
        var renamed = Tree().Select(p => p.StartsWith("b/", StringComparison.Ordinal) ? "c/" + p[2..] : p).ToList();

        var after = Homes(renamed);

        Assert.All(before.Keys.Where(k => k.StartsWith("a/", StringComparison.Ordinal)), k => Assert.Equal(before[k], after[k]));
        Assert.All(after.Where(p => p.Key.StartsWith("c/", StringComparison.Ordinal)), p => Assert.Equal("feature:work.c", p.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Crossing_the_cap_with_one_file_changes_exactly_the_children_of_that_directory_and_the_old_key_stays_as_the_group()
    {
        List<string> atCap = ["a/package.json", .. HomeTree.Files("a/one", 90), .. HomeTree.Files("a/two", 60), "b/package.json", .. HomeTree.Files("b", 10)];
        var before = Homes(atCap);

        var home = HomeFeatures.Assign(HomeTree.Work, [.. atCap, "a/one/one-more.ts"]);
        var after = home.Files.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal);

        Assert.All(before.Where(p => p.Key.StartsWith("a/", StringComparison.Ordinal)), p => Assert.Equal("feature:work.a", p.Value));
        Assert.Equal("feature:work.a.one", after["a/one/f000.ts"]);
        Assert.Equal("feature:work.a.two", after["a/two/f000.ts"]);
        Assert.Contains(home.Map.Nodes, n => n.Key.Value == "feature:work.a");
        Assert.DoesNotContain(HomeTree.Homes(home.Map), e => e.To.Value == "feature:work.a");
        Assert.All(before.Where(p => p.Key.StartsWith("b/", StringComparison.Ordinal)), p => Assert.Equal(p.Value, after[p.Key]));
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [Fact, Trait("Lane", "unit")]
    public void The_property_every_file_has_exactly_one_home_and_none_is_over_the_cap_over_a_generated_tree()
    {
        var paths = new List<string>();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 2_000; i++)
        {
            var path = Inv($"a/d{i / 40 % 10}/e{i / 400}/file{i}.ts");
            paths.Add(path);
            _ = expected.Add(path);
        }

        for (var i = 0; i < 50; i++)
        {
            paths.Add(Inv($"a/d{i % 10}/tests/t{i}.ts"));
            paths.Add(Inv($"a/d{i % 10}/tsconfig.json"));
            paths.Add(Inv($"a/d{i % 10}/gen/legacy{i}.ts"));
        }

        var repo = HomeTree.Repo("work", demote: ["a/*/gen/**"]);
        var home = HomeFeatures.Assign(repo, paths);

        var homes = HomeTree.Homes(home.Map);
        Assert.Equal(2_000, homes.Count);
        Assert.Equal(expected, homes.Select(e => e.From.Value["code:".Length..]).ToHashSet(StringComparer.Ordinal));
        Assert.Equal(2_000, homes.Select(e => e.From.Value).Distinct(StringComparer.Ordinal).Count());
        Assert.All(homes.GroupBy(e => e.To.Value, StringComparer.Ordinal), g => Assert.False(g.Skip(HomeFeatures.SizeCap).Any(), g.Key));
        Assert.Equal(2_000, home.Files.Count);
    }
}
