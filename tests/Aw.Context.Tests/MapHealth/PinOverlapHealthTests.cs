using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Features.CheckMap;
using Aw.Context.Testing.Graph;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.MapHealth;

/// <summary>Spec 2026-10-27 §10.1 and Open question 11: the first pin wins a file, and the doctor warns on a pin pair whose globs both match a real file.</summary>
public sealed class PinOverlapHealthTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ConceptPin Pin(string id, params string[] paths) => ConceptPin.Of(ConceptId.From(id), id, summary: null, features: null, paths);

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
    public async Task Two_pins_whose_globs_match_the_same_file_are_a_warning_that_names_both_and_says_the_first_wins()
    {
        var (state, detail, fix) = await Check(Scope(Pin("wide", "src/**"), Pin("tabs", "src/tabs/**")));

        Assert.Equal(MapHealthState.Warn, state);
        Assert.Contains("concept pins 'wide' and 'tabs' both match src/tabs/", detail, StringComparison.Ordinal);
        Assert.Contains("first", fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_pins_whose_globs_match_different_files_are_no_warning()
    {
        var (state, _, _) = await Check(Scope(Pin("layout", "src/layout/**"), Pin("tabs", "src/tabs/**")));

        Assert.Equal(MapHealthState.Ok, state);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_pins_whose_globs_overlap_on_no_real_file_are_no_warning()
    {
        var (state, _, _) = await Check(Scope(Pin("a", "src/tabs/**"), ConceptPin.Of(ConceptId.From("b"), "b", summary: null, features: ["feature:layout.*"], paths: ["src/tabs/nothing-here/**"])));

        Assert.Equal(MapHealthState.Ok, state);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_pins_whose_globs_both_match_only_an_unread_file_node_are_no_warning()
    {
        var built = new GraphBuilder().Node("code:setup.sh").Build();
        var graph = built with { Nodes = [.. built.Nodes.Select(n => n with { Attributes = UnreadFileNodes.Attributes() })] };

        var health = PinHealth.Of(graph, Scope(Pin("a", "*.sh"), Pin("b", "setup.sh")));

        Assert.DoesNotContain(health.Notes, n => n.Contains("both match", StringComparison.Ordinal));
    }
}
