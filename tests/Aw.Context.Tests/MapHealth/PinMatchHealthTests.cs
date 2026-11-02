using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.MapHealth;

/// <summary>Spec 2026-10-27 §5: after pins and clusters stopped being the same thing, the doctor says which pin matches no feature.</summary>
public sealed class PinMatchHealthTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Scope(params ConceptPin[] pins) =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults], Concepts = pins }]);

    private async Task<(MapHealthState State, string Detail, string? Fix)> Check(ContextScope scope)
    {
        _ = await _world.ImportFixture();
        _world.Store.StampCurrentRuleset(MapRuleset.Of(ScopeRules.Mapped(SettingsReader.Read(scope.Settings).Value, scope.Repos)));
        var check = (await _world.Map.CheckMap(scope, MapWorld.None)).Value;
        return (check.State, check.Detail, check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_that_matches_no_feature_and_no_file_is_a_warning_that_names_it()
    {
        var (state, detail, fix) = await Check(Scope(ConceptPin.Of(ConceptId.From("billing"), "Billing", summary: null, features: ["feature:billing.*"], paths: ["src/billing/**"])));

        Assert.Equal(MapHealthState.Warn, state);
        Assert.Contains("concept pin 'billing' matches no feature", detail, StringComparison.Ordinal);
        Assert.NotNull(fix);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pin_that_matches_a_feature_by_key_or_by_a_file_path_is_no_warning()
    {
        var (state, _, _) = await Check(Scope(
            ConceptPin.Of(ConceptId.From("layout"), "Layout", summary: null, features: ["feature:layout.*"], paths: null),
            ConceptPin.Of(ConceptId.From("tabs"), "Tabs", summary: null, features: null, paths: ["src/tabs/**"])));

        Assert.Equal(MapHealthState.Ok, state);
    }
}
