using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ShowMapStatus;

/// <summary>Issue 1041: 'map status' says when the current map was built under older map rules than the ones the scope runs under now.</summary>
public sealed class MapStatusRulesTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Scope() =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults] }]);

    [Fact, Trait("Lane", "unit")]
    public async Task Status_says_older_rules_when_the_recorded_rules_differ_from_the_running_ones()
    {
        _ = await _world.ImportFixture();
        _world.Store.StampCurrentRuleset("rules-from-an-older-build");

        var status = (await _world.Map.MapStatus(anchor: null, Scope(), MapWorld.None)).Value;

        Assert.True(status.RulesOlder);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_says_nothing_when_the_recorded_rules_are_the_running_ones()
    {
        _ = await _world.ImportFixture();
        var scope = Scope();
        _world.Store.StampCurrentRuleset(MapRuleset.Of(ScopeRules.Mapped(SettingsReader.Read(scope.Settings).Value, scope.Repos)));

        var status = (await _world.Map.MapStatus(anchor: null, scope, MapWorld.None)).Value;

        Assert.False(status.RulesOlder);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_says_nothing_for_a_map_that_recorded_no_rules()
    {
        _ = await _world.ImportFixture();

        var status = (await _world.Map.MapStatus(anchor: null, Scope(), MapWorld.None)).Value;

        Assert.False(status.RulesOlder);
    }
}
