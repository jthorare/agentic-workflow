using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#1670 guards: a file the repo's deny globs hide gets no file node and no edge from a record, and the extractor version stays put.</summary>
public sealed class BuildMapUnreadFileGuardsTests : IDisposable
{
    private const string PrKey = "github:pr/acme/app/71";
    private const string Secret = "secrets/key.sh";
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
            [Secret] = "#!/bin/sh\nKEY=1",
        };
        foreach (var (path, text) in more)
        {
            files[path] = text;
        }

        return files;
    }

    private static ContextScope DenyingSecrets() =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo,
        [
            new ContextRepo(_repo, "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "secrets/**"] },
        ]);

    private async Task<GraphSnapshot> Build(IReadOnlyDictionary<string, string> files, RecordWrite write)
    {
        var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files));
        _worlds.Add(world);
        _ = await world.Records.Commit(RecordSamples.Batch(write.Record.Source, 1, [write]), MapWorld.None);
        var report = await world.Map.BuildMap(progress: null, DenyingSecrets(), MapWorld.None);
        Assert.False(report.IsError, report.IsError ? report.Error.ToString() : "");
        return (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;
    }

    private static void AssertSecretAbsentAndSetupPresent(GraphSnapshot graph)
    {
        Assert.Null(graph.Resolve(NodeKey.From("code:" + Secret)));
        Assert.DoesNotContain(graph.Edges, e => e.To.Value == "code:" + Secret || e.From.Value == "code:" + Secret);
        Assert.NotNull(graph.Resolve(NodeKey.From("code:setup.sh")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pr_touching_a_denied_file_gets_no_node_or_edge_for_it()
    {
        var pr = RecordSamples.Write(
            RecordSamples.Record(PrKey, "v1", RecordSamples.Github) with { Kind = RecordKind.Pr, Repo = _repo, Links = [.. new[] { Secret, "setup.sh" }.Select(p => new RecordLink("path:aw-split-panes/" + p, RecordLinkKind.Touches, Title: null))] },
            "Touch some files");

        AssertSecretAbsentAndSetupPresent(await Build(Files(), pr));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_citing_a_denied_file_gets_no_node_or_edge_for_it()
    {
        const string text = "Run `secrets/key.sh` then `setup.sh`.";
        var doc = RecordSamples.Write(
            RecordSamples.Record(RepoDocs.Key(_repo, "alpha/AGENTS.md").Value, FakeRepoReader.BlobId(text), RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _repo, Title = "alpha/AGENTS.md" }, text);

        AssertSecretAbsentAndSetupPresent(await Build(Files(("alpha/AGENTS.md", text)), doc));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_extractor_version_is_unchanged_by_file_nodes() =>
        // why: #1670's file nodes are made in the record half, so the backtest's as-of cache key doesn't change.
        Assert.Equal(7, MapExtractor.Version);
}
