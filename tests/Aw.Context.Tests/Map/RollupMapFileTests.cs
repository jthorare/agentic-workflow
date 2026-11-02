using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Map;

/// <summary>Spec 2026-10-27 §3 "Map-file policy for Tags and Count": version 2 only when a map holds a 'tags' edge or a Count; a plain map still exports as 1.</summary>
public sealed class RollupMapFileTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static string Rolled(int version, string edge) => MapWorld.Yaml(
        string.Create(CultureInfo.InvariantCulture, $"schemaVersion: {version}"), "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"feature:a\", label: \"A\"}", "  - {key: \"feature:b\", label: \"B\"}", "  - {key: \"thing:t\", label: \"T\"}", "  - {key: \"code:src/a.ts\", label: \"a.ts\"}",
        "edges:", edge);

    private const string DependsOnCount = "  - {from: \"feature:a\", to: \"feature:b\", type: depends-on, provenance: deterministic, count: 3, evidence: [{key: \"repo:aw-split-panes/src/a.ts\", span: \"repo:aw-split-panes/src/b.ts\"}]}";

    [Fact, Trait("Lane", "unit")]
    public async Task A_plain_map_exports_as_version_1()
    {
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);

        Assert.Equal(1, _world.Exports.Documents[exported.Value.Path].SchemaVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_holding_a_count_exports_as_version_2_and_the_count_round_trips()
    {
        _ = await _world.ImportYaml(Rolled(2, DependsOnCount));

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);

        var document = _world.Exports.Documents[exported.Value.Path];
        Assert.Equal(2, document.SchemaVersion);
        Assert.Equal(3, Assert.Single(document.Edges!, e => e.Type == EdgeType.DependsOn).Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_holding_a_tags_edge_exports_as_version_2()
    {
        _ = await _world.ImportYaml(Rolled(2, "  - {from: \"code:src/a.ts\", to: \"feature:a\", type: tags, provenance: model, confidence: 0.7}"));

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);

        Assert.Equal(2, _world.Exports.Documents[exported.Value.Path].SchemaVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reader_of_version_1_alone_refuses_a_version_4_file_with_the_file_invalid_code()
    {
        var check = MapDocumentValidator.Check(Maps.Parse(Rolled(4, DependsOnCount)), KeyMigrations.Shipped);

        Assert.True(check.IsError);
        Assert.Equal(ContextErrors.MapFileInvalid.Code, check.Error.Code);
        Assert.Contains("schemaVersion: 4 isn't supported (this build reads 1, 2, 3)", check.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("  - {from: \"code:src/a.ts\", to: \"feature:a\", type: implements, count: 2}")]
    [InlineData("  - {from: \"feature:a\", to: \"feature:b\", type: part-of, count: 2}")]
    [InlineData("  - {from: \"feature:a\", to: \"feature:b\", type: depends-on, provenance: human, count: 2}")]
    [InlineData("  - {from: \"feature:a\", to: \"feature:b\", type: depends-on, provenance: deterministic, count: 0}")]
    public void The_importer_refuses_a_count_on_an_edge_that_is_not_a_roll_up(string edge)
    {
        var check = MapDocumentValidator.Check(Maps.Parse(Rolled(2, edge)), KeyMigrations.Shipped);

        Assert.True(check.IsError);
        Assert.Equal(ContextErrors.MapFileInvalid.Code, check.Error.Code);
        Assert.Contains("edges[0].count", check.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_importer_takes_a_count_on_a_deterministic_feature_edge()
    {
        var map = Maps.Checked(Maps.Parse(Rolled(2, DependsOnCount)));

        Assert.Equal(3, Assert.Single(map.Graph.Edges, e => e.Type == EdgeType.DependsOn).Count);
    }
}
