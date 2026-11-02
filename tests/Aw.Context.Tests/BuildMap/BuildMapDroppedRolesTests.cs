using Aw.Context.Core.Engine;
using Aw.Context.Core.Roles;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#1161: the build counts the tracked files each ignored role dropped (they are never indexed, so nothing can count them afterwards), keeps the counts with the map version and 'map status' shows them.</summary>
public sealed class BuildMapDroppedRolesTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    // 'docs' ignores README.md (1 tracked file), 'ui' ignores src/ui/Split.tsx (1), 'nothing' matches no tracked file; 'config' is demoted and so not an ignored role.
    private static ContextScope Ignoring() =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo,
        [
            new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes")
            {
                DenyPaths = [.. PathDenylist.Defaults, "*.md", "src/ui/**", "nothing/**"],
                DemotePaths = ["config/**"],
                Roles =
                [
                    new RepoRole("docs", Demoted: false, ["*.md"]),
                    new RepoRole("ui", Demoted: false, ["src/ui/**"]),
                    new RepoRole("nothing", Demoted: false, ["nothing/**"]),
                    new RepoRole("config", Demoted: true, ["config/**"]),
                ],
            },
        ]);

    [Fact, Trait("Lane", "unit")]
    public async Task The_map_build_records_how_many_tracked_files_each_ignored_role_dropped()
    {
        _ = await _world.Map.BuildMap(progress: null, Ignoring(), MapWorld.None);

        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;

        Assert.Equal(
            [new RoleDropped(FixtureMap.Repo, "docs", 1), new RoleDropped(FixtureMap.Repo, "nothing", 0), new RoleDropped(FixtureMap.Repo, "ui", 1)],
            draft.Version.Dropped);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_with_no_ignored_role_records_an_empty_list()
    {
        _ = await _world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None);

        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;

        Assert.Empty(draft.Version.Dropped!);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_status_shows_an_ignored_roles_dropped_count_and_none_for_a_demoted_one()
    {
        var built = (await _world.Map.BuildMap(progress: null, Ignoring(), MapWorld.None)).Value;
        _ = await _world.Store.Promote(built.Draft.Number, MapWorld.None);

        var status = (await _world.Map.MapStatus(anchor: null, Ignoring(), MapWorld.None)).Value;

        // why: roles are configured, so Roles is set
        var roles = Assert.Single(status.Roles!).Roles.ToDictionary(r => r.Role, StringComparer.Ordinal);
        Assert.Equal(1, roles["docs"].Ignored);
        Assert.Equal(1, roles["ui"].Ignored);
        Assert.Equal(0, roles["nothing"].Ignored);
        Assert.Null(roles["config"].Ignored);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_keeps_the_counts_with_the_version()
    {
        var built = (await _world.Map.BuildMap(progress: null, Ignoring(), MapWorld.None)).Value;
        _ = await _world.Store.Promote(built.Draft.Number, MapWorld.None);

        var current = (await _world.Store.Current(MapWorld.None)).Value!;

        Assert.Equal(3, current.Dropped!.Count);
    }
}
