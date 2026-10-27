using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Subgraph;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetSubgraph;

/// <summary>A subgraph read leaves out a demoted file's code node that depth expansion alone would reach, and counts it; a key asked for by name is always answered (issue 1152).</summary>
public sealed class SubgraphDemoteTests : IDisposable
{
    private const string Feature = "feature:kept";
    private const string Demoted = "code:src/a.test.ts";
    private const string Product = "code:src/b.ts";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Demoting(params string[] globs) =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DemotePaths = [.. globs] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    private async Task Import() =>
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"code:src/a.test.ts\", label: \"a.test.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:src/a.test.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}"));

    private async Task<SubgraphView> Ask(string[] keys, int depth, ContextScope scope) =>
        (await _world.Engine.Subgraph(new SubgraphQuery(keys, depth, MapRead.Current), scope, MapWorld.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_demoted_neighbour_and_its_edges_are_left_out_and_counted()
    {
        await Import();

        var view = await Ask([Feature], depth: 1, Demoting("**/*.test.ts"));

        Assert.Equal([Product], view.Bands.Code.Files.Select(f => "code:" + f.Path), StringComparer.Ordinal);
        Assert.DoesNotContain(view.Edges, e => e.From.Value == Demoted || e.To.Value == Demoted);
        Assert.Equal(1, view.Demoted);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_demoted_key_asked_for_by_name_is_answered()
    {
        await Import();

        var view = await Ask([Demoted], depth: 0, Demoting("**/*.test.ts"));

        Assert.Equal([Demoted], view.Nodes.Select(n => n.Value), StringComparer.Ordinal);
        Assert.Equal(["src/a.test.ts"], view.Bands.Code.Files.Select(f => f.Path), StringComparer.Ordinal);
        Assert.Empty(view.Missing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_with_no_demote_globs_changes_nothing()
    {
        await Import();

        var view = await Ask([Feature], depth: 1, MapWorld.Human);

        Assert.Equal(["src/a.test.ts", "src/b.ts"], view.Bands.Code.Files.Select(f => f.Path).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains(view.Edges, e => e.From.Value == Demoted);
        Assert.Null(view.Demoted);
    }
}
