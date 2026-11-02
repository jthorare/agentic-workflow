using Aw.Context.Core.Graph;
using Aw.Context.Testing.Extract;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>The map build reads 'context.docs.directoryNames' (U18b): a file with a listed name is a doc record node in any directory, and one the list omits is not.</summary>
public sealed class BuildMapDocNamesTests
{
    private static async Task<GraphSnapshot> Build(string section)
    {
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal) { ["pkg/NOTES.md"] = "Notes.", ["README.md"] = "Readme." };
        using var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files));
        _ = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith(section), MapWorld.None)).Value;
        return (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;
    }

    private static bool HasRecord(GraphSnapshot graph, string path) =>
        graph.Nodes.Any(n => n.Key.Kind == NodeKind.Record && n.Label == path);

    [Fact, Trait("Lane", "unit")]
    public async Task A_listed_name_becomes_a_record_node_and_a_name_the_list_omits_does_not()
    {
        var graph = await Build("""{"repos":["aw-split-panes"],"docs":{"directoryNames":["NOTES.md"]}}""");

        Assert.True(HasRecord(graph, "pkg/NOTES.md"));
        Assert.False(HasRecord(graph, "README.md"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_key_README_is_a_record_node()
    {
        var graph = await Build(MapWorld.OnlyFixtureRepo);

        Assert.True(HasRecord(graph, "README.md"));
        Assert.False(HasRecord(graph, "pkg/NOTES.md"));
    }
}
