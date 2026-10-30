using System.Text.Json;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#1670: a record's mentions edge to a tracked file the extractor doesn't read (setup.sh, a script) survives the build, onto a code: node marked read: false that implements nothing.</summary>
public sealed class BuildMapUnreadFileMentionsTests : IDisposable
{
    private const string PrKey = "github:pr/acme/app/70";
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private readonly List<MapWorld> _worlds = [];

    public void Dispose() => _worlds.ForEach(w => w.Dispose());

    private static Dictionary<string, string> Files(params (string Path, string Text)[] more)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alpha/Alpha.csproj"] = "<Project/>",
            ["alpha/F00.cs"] = "class C {}",
            ["setup.sh"] = "#!/bin/sh\necho hi",
        };
        foreach (var (path, text) in more)
        {
            files[path] = text;
        }

        return files;
    }

    private static RecordWrite Pr(params string[] paths) =>
        RecordSamples.Write(
            RecordSamples.Record(PrKey, "v1", RecordSamples.Github) with { Kind = RecordKind.Pr, Repo = _repo, Links = [.. paths.Select(p => new RecordLink("path:aw-split-panes/" + p, RecordLinkKind.Touches, Title: null))] },
            "Touch some files");

    private static RecordWrite Doc(string path, string text) =>
        RecordSamples.Write(
            RecordSamples.Record(RepoDocs.Key(_repo, path).Value, FakeRepoReader.BlobId(text), RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _repo, Title = path }, text);

    private static string DocNode(string path) => "record:" + RepoDocs.Key(_repo, path).Value;

    private async Task<GraphSnapshot> Build(IReadOnlyDictionary<string, string> files, params RecordWrite[] writes)
    {
        var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files));
        _worlds.Add(world);
        foreach (var source in writes.GroupBy(w => w.Record.Source))
        {
            _ = await world.Records.Commit(RecordSamples.Batch(source.Key, 1, [.. source]), MapWorld.None);
        }

        var report = await world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None);
        Assert.False(report.IsError, report.IsError ? report.Error.ToString() : "");
        return (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;
    }

    private static bool HasMention(GraphSnapshot graph, string from, string to) =>
        graph.Edges.Any(e => e.Type == EdgeType.Mentions && e.From.Value == from && e.To.Value == to);

    [Fact, Trait("Lane", "unit")]
    public async Task A_pr_touching_a_file_the_extractor_does_not_read_keeps_its_mentions_edge_to_that_file()
    {
        var graph = await Build(Files(), Pr("alpha/F00.cs", "setup.sh"));

        Assert.True(HasMention(graph, "record:" + PrKey, "code:setup.sh"));
        Assert.NotNull(graph.Resolve(NodeKey.From("code:setup.sh")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_node_made_for_a_record_is_marked_unread_and_implements_nothing()
    {
        var graph = await Build(Files(), Pr("alpha/F00.cs", "setup.sh"));

        var node = graph.Resolve(NodeKey.From("code:setup.sh"));
        Assert.NotNull(node);
        Assert.Equal("setup.sh", node.Label);
        Assert.Equal(Provenance.Deterministic, node.Provenance);
        Assert.True(node.Attributes?.TryGetProperty("read", out var read) == true && read.ValueKind == JsonValueKind.False);
        Assert.DoesNotContain(graph.Edges, e => e.Type == EdgeType.Implements && e.From.Value == "code:setup.sh");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_citing_a_script_mentions_it()
    {
        var graph = await Build(Files(("scripts/a.sh", "#!/bin/sh"), ("alpha/AGENTS.md", "Run `scripts/a.sh`.")), Doc("alpha/AGENTS.md", "Run `scripts/a.sh`."));

        Assert.True(HasMention(graph, DocNode("alpha/AGENTS.md"), "code:scripts/a.sh"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_citing_another_doc_mentions_that_doc_record_not_a_file_node()
    {
        const string readme = "See `alpha/AGENTS.md`.";
        const string agents = "Alpha rules.";
        var graph = await Build(Files(("README.md", readme), ("alpha/AGENTS.md", agents)), Doc("README.md", readme), Doc("alpha/AGENTS.md", agents));

        Assert.True(HasMention(graph, DocNode("README.md"), DocNode("alpha/AGENTS.md")));
        Assert.Null(graph.Resolve(NodeKey.From("code:alpha/AGENTS.md")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task File_nodes_for_records_leave_the_home_features_unchanged()
    {
        var files = Files(("alpha/F01.cs", "class D {}"), ("alpha/F02.cs", "class E {}"));
        var withRecord = await Build(files, Pr("alpha/F00.cs", "setup.sh"));
        var without = await Build(files);

        Assert.NotNull(withRecord.Resolve(NodeKey.From("code:setup.sh")));
        Assert.Equal(FeatureKeys(without), FeatureKeys(withRecord));
        Assert.Equal(Implements(without), Implements(withRecord));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_touched_path_not_in_the_tree_gets_no_node()
    {
        var graph = await Build(Files(), Pr("alpha/F00.cs", "gone.sh"));

        Assert.Null(graph.Resolve(NodeKey.From("code:gone.sh")));
        Assert.DoesNotContain(graph.Edges, e => e.To.Value == "code:gone.sh");
    }

    private static List<string> FeatureKeys(GraphSnapshot graph) =>
        [.. graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature).Select(n => n.Key.Value).Order(StringComparer.Ordinal)];

    private static List<(string From, string To)> Implements(GraphSnapshot graph) =>
        [.. graph.Edges.Where(e => e.Type == EdgeType.Implements).Select(e => (e.From.Value, e.To.Value)).Order()];
}
