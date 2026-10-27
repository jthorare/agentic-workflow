using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Roles;

namespace Aw.Context.Tests.Roles;

/// <summary>Issue 1105: files per role per repo, and the role-like files no glob covers.</summary>
public sealed class RoleCounterTests
{
    private static ContextRepo Repo(string name, params RepoRole[] roles) =>
        new(ContextRepoName.From(name), "/work/" + name) { Roles = roles, DemotePaths = [.. roles.Where(r => r.Demoted).SelectMany(r => r.Globs)], DenyPaths = [.. PathDenylist.Defaults, .. roles.Where(r => !r.Demoted).SelectMany(r => r.Globs)] };

    private static Dictionary<string, IReadOnlyList<string>> Paths(string repo, params string[] paths) =>
        new(StringComparer.Ordinal) { [repo] = paths };

    [Fact, Trait("Lane", "unit")]
    public void Files_are_counted_per_role_in_name_order()
    {
        var web = Repo("web-app", new RepoRole("mocks", Demoted: true, ["**/__mocks__/**"]), new RepoRole("demo", Demoted: true, ["demo/**"]));

        var counted = RoleCounter.Of([web], Paths("web-app", "src/a.ts", "demo/a.ts", "demo/b.ts", "src/__mocks__/x.ts")).Single();

        Assert.Equal("web-app", counted.Repo);
        Assert.Equal([new RoleCount("demo", Demoted: true, 2), new RoleCount("mocks", Demoted: true, 1)], counted.Roles);
        Assert.Null(counted.Unassigned);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_role_that_matches_no_indexed_file_counts_zero()
    {
        var web = Repo("web-app", new RepoRole("generated", Demoted: false, ["**/*.generated.ts"]));

        var counted = RoleCounter.Of([web], Paths("web-app", "src/a.ts")).Single();

        Assert.Equal([new RoleCount("generated", Demoted: false, 0)], counted.Roles);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ignored_role_carries_the_count_the_build_recorded_and_a_demoted_one_does_not()
    {
        var web = Repo("web-app", new RepoRole("generated", Demoted: false, ["**/*.generated.ts"]), new RepoRole("demo", Demoted: true, ["demo/**"]));

        var counted = RoleCounter.Of([web], Paths("web-app", "src/a.ts", "demo/a.ts"), [new RoleDropped("web-app", "generated", 312)]).Single();

        Assert.Equal([new RoleCount("demo", Demoted: true, 1), new RoleCount("generated", Demoted: false, 0) { Ignored = 312 }], counted.Roles);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ignored_role_has_no_count_when_the_map_recorded_none()
    {
        var web = Repo("web-app", new RepoRole("generated", Demoted: false, ["**/*.generated.ts"]));

        var counted = RoleCounter.Of([web], Paths("web-app", "src/a.ts"), dropped: null).Single();

        Assert.Null(Assert.Single(counted.Roles).Ignored);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dropped_count_of_another_repo_or_role_is_not_borrowed()
    {
        var web = Repo("web-app", new RepoRole("generated", Demoted: false, ["**/*.generated.ts"]));

        var counted = RoleCounter.Of([web], Paths("web-app", "src/a.ts"), [new RoleDropped("api", "generated", 9), new RoleDropped("web-app", "mocks", 4), new RoleDropped("web-app", "generated", 0)]).Single();

        Assert.Equal(0, Assert.Single(counted.Roles).Ignored);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_role_like_file_outside_every_glob_is_unassigned_with_examples_in_path_order()
    {
        var web = Repo("web-app", new RepoRole("demo", Demoted: true, ["demo/**"]));

        var counted = RoleCounter.Of([web], Paths("web-app", "src/__tests__/b.ts", "src/__tests__/a.ts", "src/a.spec.ts", "demo/a.test.ts", "src/main.ts")).Single();

        // why: the role-like files above are outside every glob, so Unassigned is set
        Assert.Equal(3, counted.Unassigned!.Count);
        Assert.Equal(["src/__tests__/a.ts", "src/__tests__/b.ts", "src/a.spec.ts"], counted.Unassigned.Examples);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unassigned_examples_stop_at_five_but_the_count_does_not()
    {
        var web = Repo("web-app");
        var paths = Enumerable.Range(0, 8).Select(i => string.Create(CultureInfo.InvariantCulture, $"src/__tests__/t{i}.ts")).ToArray();

        var counted = RoleCounter.Of([web], Paths("web-app", paths)).Single();

        // why: the role-like files above are outside every glob, so Unassigned is set
        Assert.Equal(8, counted.Unassigned!.Count);
        Assert.Equal(5, counted.Unassigned.Examples.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_role_and_no_role_like_file_is_left_out()
        => Assert.Empty(RoleCounter.Of([Repo("web-app")], Paths("web-app", "src/a.ts")));

    [Fact, Trait("Lane", "unit")]
    public void A_file_denied_by_an_ignore_glob_is_not_unassigned()
    {
        var web = Repo("web-app", new RepoRole("tests", Demoted: false, ["**/*.test.*"]));

        var counted = RoleCounter.Of([web], Paths("web-app", "src/a.test.ts")).Single();

        Assert.Null(counted.Unassigned);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/__tests__/a.ts", true)]
    [InlineData("a.test.ts", true)]
    [InlineData("src/a.spec.tsx", true)]
    [InlineData("tests/fixtures/x.json", true)]
    [InlineData("src/__mocks__/m.ts", true)]
    [InlineData("pkg/testdata/in.txt", true)]
    [InlineData("src/latest.ts", false)]
    [InlineData("src/contest/main.ts", false)]
    public void Role_like_paths_are_the_fixed_set(string path, bool like) => Assert.Equal(like, RoleLikePaths.Matches(path));
}
