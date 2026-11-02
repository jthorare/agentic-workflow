using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.MapHealth;

/// <summary>Issue 1041: doctor 'context:map' doesn't call a map built under other rules "current".</summary>
public sealed class MapRulesHealthTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Context_map_warns_when_the_current_map_was_built_under_other_rules()
    {
        _ = await _world.ImportFixture();
        _world.Store.StampCurrentRuleset("rules-from-an-older-build");
        var scope = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults] }]);

        var check = (await _world.Map.CheckMap(scope, MapWorld.None)).Value;

        Assert.Equal(MapHealthState.Warn, check.State);
    }
}
