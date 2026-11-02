using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Extract;

namespace Aw.Context.Tests.Map;

/// <summary>Spec 2026-10-27 §3: a deny glob drops a roll-up edge when any file it cites is denied; a rebuild drops the roll-up edges and makes them again.</summary>
public sealed class RollupDenyAndPurgeTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Denying(params string[] globs) =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, .. globs] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    private static string Map(string importedFile) => MapWorld.Yaml(
        "schemaVersion: 2", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"feature:a\", label: \"A\"}", "  - {key: \"feature:b\", label: \"B\"}",
        "edges:",
        $"  - {{from: \"feature:a\", to: \"feature:b\", type: depends-on, provenance: deterministic, count: 2, evidence: [{{key: \"repo:aw-split-panes/src/a1.ts\", span: \"repo:aw-split-panes/src/b1.ts\"}}, {{key: \"repo:aw-split-panes/src/a2.ts\", span: \"repo:aw-split-panes/{importedFile}\"}}]}}");

    private async Task<bool> Shows(ContextScope scope) =>
        (await _world.Engine.Lenses("feature:a", read: MapRead.Current, depth: null, scope, MapWorld.None)).Value.Bands.Product.Edges.Any(e => e.Type == EdgeType.DependsOn);

    [Fact, Trait("Lane", "unit")]
    public async Task A_deny_glob_on_a_file_in_the_evidence_drops_the_roll_up_edge()
    {
        _ = await _world.ImportYaml(Map("src/b2.ts"));

        Assert.True(await Shows(MapWorld.Human));
        Assert.False(await Shows(Denying("src/a2.ts")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deny_glob_on_the_imported_file_of_a_cited_pair_drops_it_too_and_an_unrelated_glob_keeps_it()
    {
        _ = await _world.ImportYaml(Map("src/b2.ts"));

        Assert.False(await Shows(Denying("src/b2.ts")));
        Assert.True(await Shows(Denying("src/other.ts")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rebuild_drops_the_stored_roll_up_edges_so_the_new_ones_replace_them()
    {
        var old = FeatureRollup.Apply(RollupFixture.Files());
        var stored = new GraphSnapshot([.. old.Nodes], [.. old.Edges], [], []);
        var found = FeatureRollup.Apply(RollupFixture.Files([("b1", "billing"), ("o1", "orders")], [("b1", "o1")], []));

        var (graph, _, _) = MapAccess.WithoutBuiltStructure(stored, [], found, DateTimeOffset.Parse("2026-10-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        Assert.DoesNotContain(graph.Edges, e => e.Count is not null);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_roll_up_edge_between_features_a_person_wrote_is_dropped_on_a_rebuild_too_because_only_the_build_writes_a_count()
    {
        var stored = new GraphSnapshot(
            [new GraphNode(NodeKey.From("feature:mine"), "Mine"), new GraphNode(NodeKey.From("feature:yours"), "Yours")],
            [new GraphEdge(NodeKey.From("feature:mine"), NodeKey.From("feature:yours"), EdgeType.DependsOn, Provenance.Deterministic, Count: 2)], [], []);

        var (graph, _, _) = MapAccess.WithoutBuiltStructure(stored, [], ExtractedMap.Empty, DateTimeOffset.Parse("2026-10-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

        Assert.Empty(graph.Edges);
    }
}
