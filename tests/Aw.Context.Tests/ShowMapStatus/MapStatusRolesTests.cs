using Aw.Context.Core.Engine;
using Aw.Context.Core.Roles;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ShowMapStatus;

/// <summary>Issue 1105: 'map status' carries the role counts per repo, and leaves them out when the profile configures no role.</summary>
public sealed class MapStatusRolesTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Scope(params RepoRole[] roles) =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults], DemotePaths = [.. roles.SelectMany(r => r.Globs)], Roles = roles }]);

    [Fact, Trait("Lane", "unit")]
    public async Task Status_counts_the_indexed_files_of_a_demoted_role()
    {
        _ = await _world.ImportFixture();

        var status = (await _world.Map.MapStatus(anchor: null, Scope(new RepoRole("layout", Demoted: true, ["src/layout/**"])), MapWorld.None)).Value;

        // why: the demoted role is configured, so Roles is set
        var repo = Assert.Single(status.Roles!);
        Assert.Equal(FixtureMap.Repo, repo.Repo);
        Assert.Equal([new RoleCount("layout", Demoted: true, 2)], repo.Roles);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_leaves_the_roles_out_when_nothing_is_configured_and_nothing_looks_like_a_role()
    {
        _ = await _world.ImportFixture();

        var status = (await _world.Map.MapStatus(anchor: null, Scope(), MapWorld.None)).Value;

        Assert.Null(status.Roles);
    }
}
