using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;

namespace Aw.Context.Testing.Map;

/// <summary>Graphs built for the structure checks (the features are a person's: a build drops the deterministic home features of the map it keeps and makes them again from the repo, so seeded deterministic ones would be gone before the checks ran) (map structure spec §4.4): leaf home features with exact file counts and exact numbers of isolated ones, built as graphs, not extractor output.</summary>
public static class StructureGraphs
{
    /// <summary>The repo the graphs map.</summary>
    public const string Repo = FixtureMap.Repo;

    /// <summary>
    /// A graph of <paramref name="files"/>.Count leaf home features, feature i holding files[i] files (each with a deterministic 'implements' edge to it). The first
    /// <paramref name="isolated"/> features link to nothing; every other one has a deterministic 'depends-on' edge to a hub group feature (which holds no file).
    /// </summary>
    /// <param name="files">The file count of each feature.</param>
    /// <param name="isolated">How many features, from the first, have no link.</param>
    /// <param name="fileEdges">Whether one file-level 'depends-on' edge between two files exists (the extractors of imports are installed).</param>
    /// <returns>The graph.</returns>
    public static GraphSnapshot Of(IReadOnlyList<int> files, int isolated, bool fileEdges = true)
    {
        ArgumentNullException.ThrowIfNull(files);
        var hub = NodeKey.From("feature:hub");
        var nodes = new List<GraphNode> { new(hub, "hub", Provenance.Human) };
        var edges = new List<GraphEdge>();
        for (var i = 0; i < files.Count; i++)
        {
            var feature = NodeKey.From(string.Create(CultureInfo.InvariantCulture, $"feature:pkg{i}"));
            nodes.Add(new GraphNode(feature, string.Create(CultureInfo.InvariantCulture, $"pkg{i}"), Provenance.Human));
            for (var j = 0; j < files[i]; j++)
            {
                var code = NodeKey.From(string.Create(CultureInfo.InvariantCulture, $"code:pkg{i}/f{j}.ts"));
                nodes.Add(new GraphNode(code, string.Create(CultureInfo.InvariantCulture, $"f{j}.ts"), Provenance.Deterministic));
                edges.Add(new GraphEdge(code, feature, EdgeType.Implements, Provenance.Deterministic));
            }

            if (i >= isolated)
            {
                edges.Add(new GraphEdge(feature, hub, EdgeType.DependsOn, Provenance.Deterministic));
            }
        }

        if (fileEdges && files.Count > 0 && files[0] > 1)
        {
            edges.Add(new GraphEdge(NodeKey.From("code:pkg0/f0.ts"), NodeKey.From("code:pkg0/f1.ts"), EdgeType.DependsOn, Provenance.Deterministic));
        }

        return new GraphSnapshot(nodes, edges, [], []);
    }

    /// <summary><paramref name="total"/> features of <paramref name="filesEach"/> files, <paramref name="isolated"/> of them isolated.</summary>
    /// <param name="total">How many features.</param>
    /// <param name="isolated">How many are isolated.</param>
    /// <param name="filesEach">Files in each (at least 5 for the isolation measure to count it).</param>
    /// <param name="fileEdges">Whether a file-level edge exists.</param>
    /// <returns>The graph.</returns>
    public static GraphSnapshot Isolated(int total, int isolated, int filesEach = 5, bool fileEdges = true) =>
        Of([.. Enumerable.Repeat(filesEach, total)], isolated, fileEdges);

    /// <summary><paramref name="total"/> features that all link to the hub, <paramref name="oversized"/> of them holding 151 files and the rest 1.</summary>
    /// <param name="total">How many features.</param>
    /// <param name="oversized">How many are over the cap.</param>
    /// <returns>The graph.</returns>
    public static GraphSnapshot Oversized(int total, int oversized) =>
        Of([.. Enumerable.Range(0, total).Select(i => i < oversized ? 151 : 1)], isolated: 0);

    /// <summary>The graph as a current-map import for the fixture repo.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The import.</returns>
    public static MapImport Import(GraphSnapshot graph) => new(ContextRepoName.From(Repo), "a41b07d", graph, []);
}
