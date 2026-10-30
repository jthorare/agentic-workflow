using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ImportMap;

public sealed class ImportRulesetTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_records_the_rules_its_scope_runs_under()
    {
        var scope = MapWorld.HumanWith("""{"repos":["aw-split-panes"],"docs":{"globs":["docs/**/*.md"]}}""");
        var expected = MapRuleset.Running(scope);
        Assert.NotNull(expected);

        var preview = await _world.Map.PreviewMap(MapWorld.MapPath, scope, MapWorld.None);
        Assert.True(preview.IsSuccess, preview.IsError ? preview.Error.Message : null);
        var imported = await _world.Map.ImportMap(MapWorld.MapPath, preview.Value.Hash, scope, MapWorld.None);
        Assert.True(imported.IsSuccess, imported.IsError ? imported.Error.Message : null);

        var current = (await _world.Store.Versions(MapWorld.None)).Value.Single(v => v.Status == MapVersionStatus.Current);
        Assert.Equal(expected, imported.Value.Version.Ruleset);
        Assert.Equal(expected, current.Ruleset);
    }
}
