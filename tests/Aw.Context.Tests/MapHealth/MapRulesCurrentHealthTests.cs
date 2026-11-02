using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.MapHealth;

/// <summary>Issue 1041: the doctor's 'context:map' stays green for a map built under the rules the scope runs under now, and says what is wrong when none were recorded.</summary>
public sealed class MapRulesCurrentHealthTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Scope() =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults] }]);

    [Fact, Trait("Lane", "unit")]
    public async Task Context_map_is_ok_when_the_map_was_built_under_the_rules_the_scope_runs_under()
    {
        _ = await _world.ImportFixture();
        var scope = Scope();
        _world.Store.StampCurrentRuleset(MapRuleset.Of(ScopeRules.Mapped(SettingsReader.Read(scope.Settings).Value, scope.Repos)));

        var check = (await _world.Map.CheckMap(scope, MapWorld.None)).Value;

        Assert.Equal(MapHealthState.Ok, check.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_map_warns_and_names_the_fix_when_no_rules_were_recorded()
    {
        _ = await _world.ImportFixture();

        var check = (await _world.Map.CheckMap(Scope(), MapWorld.None)).Value;

        Assert.Equal(MapHealthState.Warn, check.State);
        Assert.Contains("older map rules", check.Detail, StringComparison.Ordinal);
        Assert.NotNull(check.Fix);
    }
}
