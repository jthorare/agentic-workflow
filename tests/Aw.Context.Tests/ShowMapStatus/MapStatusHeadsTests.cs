using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ShowMapStatus;

/// <summary>Issue 3308: 'map status' names every mapped repo with the commit the map was built at, not only the version's first repo; a session sees its own.</summary>
public sealed class MapStatusHeadsTests : IDisposable
{
    private static readonly MapHead[] _twoRepos =
        [new(ContextRepoName.From(FixtureMap.Repo), "1111111", OnDefault: true), new(ContextRepoName.From("other"), "2222222222222222222222222222222222222222", OnDefault: true)];

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_sees_every_mapped_repo_with_its_commit_in_the_builds_order()
    {
        _ = await _world.Store.SaveCurrent(Maps.Checked(Maps.Fixture()) with { Heads = _twoRepos }, _world.Clock.GetUtcNow(), MapWorld.None);

        var status = (await _world.Map.MapStatus(anchor: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(_twoRepos, status.Heads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_sees_only_its_own_repos_head()
    {
        _ = await _world.Store.SaveCurrent(Maps.Checked(Maps.Fixture()) with { Heads = _twoRepos }, _world.Clock.GetUtcNow(), MapWorld.None);

        var status = (await _world.Map.MapStatus(anchor: null, MapWorld.Session("other"), MapWorld.None)).Value;

        MapHead[] own = [_twoRepos[1]];
        Assert.Equal(own, status.Heads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_that_recorded_no_heads_leaves_them_out()
    {
        _ = await _world.Store.SaveCurrent(Maps.Checked(Maps.Fixture()) with { Commit = null }, _world.Clock.GetUtcNow(), MapWorld.None);

        var status = (await _world.Map.MapStatus(anchor: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.Null(status.Heads);
    }
}
