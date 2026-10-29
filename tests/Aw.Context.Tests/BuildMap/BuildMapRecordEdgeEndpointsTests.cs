using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#1642 and #1643: a record's file edges only point at files that are nodes, and a file with no home feature does not break the roll-up to features.</summary>
public sealed class BuildMapRecordEdgeEndpointsTests : IDisposable
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private readonly List<MapWorld> _worlds = [];

    public void Dispose() => _worlds.ForEach(w => w.Dispose());

    private static ContextScope Scope => MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    private MapWorld World(IReadOnlyDictionary<string, string> files)
    {
        var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files));
        _worlds.Add(world);
        return world;
    }

    private static RecordWrite Pr(string key, params string[] paths) =>
        RecordSamples.Write(
            RecordSamples.Record(key, "v1", RecordSamples.Github) with { Kind = RecordKind.Pr, Repo = _repo, Links = [.. paths.Select(p => new RecordLink("path:aw-split-panes/" + p, RecordLinkKind.Touches, Title: null))] },
            "Touch some files");

    private static async Task Put(MapWorld world, RecordWrite write) =>
        _ = await world.Records.Commit(RecordSamples.Batch(write.Record.Source, 1, [write]), MapWorld.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_whose_file_has_no_home_feature_is_built_without_a_rollup_edge_for_it()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["alpha/Alpha.csproj"] = "<Project/>", ["alpha/F00.cs"] = "class C {}", ["loose/Loose.cs"] = "class L {}" };
        var world = World(files);
        await Put(world, Pr("github:pr/acme/app/7", "alpha/F00.cs", "loose/Loose.cs"));

        var report = await world.Map.BuildMap(progress: null, Scope, MapWorld.None);

        Assert.False(report.IsError, report.IsError ? report.Error.ToString() : "");
        var graph = (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;
        Assert.NotNull(graph.Resolve(NodeKey.From("record:github:pr/acme/app/7")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_citing_a_file_with_no_code_node_saves_with_no_edge_to_a_missing_node()
    {
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal) { ["setup.sh"] = "#!/bin/sh\necho hi" };
        var world = World(files);
        await Put(world, Pr("github:pr/acme/app/8", "src/keys.ts", "setup.sh"));

        var report = await world.Map.BuildMap(progress: null, Scope, MapWorld.None);

        Assert.False(report.IsError, report.IsError ? report.Error.ToString() : "");
        var graph = (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;
        var nodes = graph.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(graph.Edges, e => !nodes.Contains(e.From.Value) || !nodes.Contains(e.To.Value));
    }
}
