using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Issue #1373, spec 2026-10-27 §11.2: the map build links a record to the features its files are homed in (and a directory doc to the home feature at its directory), so a feature lens finds it.</summary>
public sealed class RecordFeatureLinksTests : IDisposable
{
    private const string PrKey = "github:pr/acme/app/9";
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private readonly List<MapWorld> _worlds = [];

    public void Dispose() => _worlds.ForEach(w => w.Dispose());

    private static string Doc(string path) => "record:" + RepoDocs.Key(_repo, path).Value;

    private static string Cites(string dir) => $"Files: `{dir}/F00.cs` and `{dir}/F01.cs`.";

    private static async Task<(GraphSnapshot Graph, MapWorld World)> Build(List<MapWorld> worlds)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alpha/AGENTS.md"] = Cites("alpha"),
            ["beta/AGENTS.md"] = Cites("beta"),
            ["README.md"] = "# Root, cites nothing.",
        };
        var files = new Dictionary<string, string>(texts, StringComparer.Ordinal)
        {
            ["alpha/Alpha.csproj"] = "<Project/>",
            ["beta/Beta.csproj"] = "<Project/>",
        };
        foreach (var dir in new[] { "alpha", "beta" })
        {
            for (var i = 0; i < 3; i++)
            {
                files[string.Create(CultureInfo.InvariantCulture, $"{dir}/F0{i}.cs")] = "class C {}";
            }
        }

        var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files));
        worlds.Add(world);
        var rows = texts.Select(t => RecordSamples.Write(
            RecordSamples.Record(RepoDocs.Key(_repo, t.Key).Value, FakeRepoReader.BlobId(t.Value), RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _repo, Title = t.Key }, t.Value)).ToArray();
        var pr = RecordSamples.Write(
            RecordSamples.Record(PrKey, "v1", RecordSamples.Github) with
            {
                Kind = RecordKind.Pr,
                Repo = _repo,
                Links = [new RecordLink("path:aw-split-panes/alpha/F00.cs", RecordLinkKind.Touches, Title: null), new RecordLink("path:aw-split-panes/alpha/F01.cs", RecordLinkKind.Touches, Title: null)],
            },
            "Touch two alpha files");
        foreach (var source in rows.Append(pr).GroupBy(w => w.Record.Source))
        {
            _ = await world.Records.Commit(RecordSamples.Batch(source.Key, 1, [.. source]), MapWorld.None);
        }

        _ = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None)).Value;
        return ((await world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph, world);
    }

    private static string HomeOf(GraphSnapshot graph, string path) =>
        graph.Edges.Single(e => e is { Type: EdgeType.Implements, Provenance: Provenance.Deterministic } && e.From.Value == "code:" + path && e.To.Kind == NodeKind.Feature).To.Value;

    private static List<GraphEdge> FeatureEdges(GraphSnapshot graph, string record) =>
        [.. graph.Edges.Where(e => e is { Type: EdgeType.Mentions } && e.From.Value == record && e.To.Kind == NodeKind.Feature)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_directory_doc_links_to_the_home_feature_at_its_directory_and_still_to_its_cited_files()
    {
        var (graph, _) = await Build(_worlds);

        foreach (var dir in new[] { "alpha", "beta" })
        {
            var doc = Doc(dir + "/AGENTS.md");
            var features = FeatureEdges(graph, doc);
            var feature = Assert.Single(features);
            Assert.Equal(HomeOf(graph, dir + "/F00.cs"), feature.To.Value, StringComparer.Ordinal);
            Assert.Equal(Provenance.Deterministic, feature.Provenance);
            var files = graph.Edges.Where(e => e is { Type: EdgeType.Mentions } && e.From.Value == doc && e.To.Kind == NodeKind.Code).Select(e => e.To.Value).Order(StringComparer.Ordinal);
            Assert.Equal([$"code:{dir}/F00.cs", $"code:{dir}/F01.cs"], files, StringComparer.Ordinal);
        }

        Assert.NotEqual(HomeOf(graph, "alpha/F00.cs"), HomeOf(graph, "beta/F00.cs"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_root_doc_does_not_link_to_every_feature_below_it()
    {
        var (graph, _) = await Build(_worlds);

        Assert.Empty(FeatureEdges(graph, Doc("README.md")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_under_the_file_cap_links_to_the_feature_its_files_are_homed_in_and_keeps_its_per_file_edges()
    {
        var (graph, _) = await Build(_worlds);

        var key = "record:" + PrKey;
        var feature = Assert.Single(FeatureEdges(graph, key));
        Assert.Equal(HomeOf(graph, "alpha/F00.cs"), feature.To.Value, StringComparer.Ordinal);
        Assert.Equal(Provenance.Deterministic, feature.Provenance);
        Assert.Equal(2, feature.Count);
        var files = graph.Edges.Where(e => e is { Type: EdgeType.Mentions } && e.From.Value == key && e.To.Kind == NodeKind.Code).Select(e => e.To.Value).Order(StringComparer.Ordinal);
        Assert.Equal(["code:alpha/F00.cs", "code:alpha/F01.cs"], files, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_lens_on_a_feature_lists_its_directory_doc_and_the_pull_request_that_touched_its_files()
    {
        var (graph, world) = await Build(_worlds);

        var lenses = await world.Engine.Lenses(HomeOf(graph, "alpha/F00.cs"), MapRead.NewestDraft, depth: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None);

        var records = lenses.Value.Bands.Intent.Records.Select(r => r.Key.Value).ToList();
        Assert.Contains(Doc("alpha/AGENTS.md"), records, StringComparer.Ordinal);
        Assert.Contains("record:" + PrKey, records, StringComparer.Ordinal);
        Assert.DoesNotContain(Doc("beta/AGENTS.md"), records, StringComparer.Ordinal);
    }
}
