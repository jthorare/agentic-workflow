using System.Globalization;
using Aw.Context.Core;
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

/// <summary>Spec 2026-10-27 §7.1 and §7.5, end to end: the map build reads the record store, puts its lines in the report, and rolls a record over the file cap up to the features its files belong to.</summary>
public sealed class BuildMapRecordsTests : IDisposable
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private readonly List<MapWorld> _worlds = [];

    public void Dispose() => _worlds.ForEach(w => w.Dispose());

    private static ContextScope Scope => MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    private MapWorld World(IReadOnlyDictionary<string, string>? files = null, IRecordStore? store = null)
    {
        var reader = files is null ? null : new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files);
        var world = new MapWorld(reader, records: store);
        _worlds.Add(world);
        return world;
    }

    private static RecordWrite Issue(string key, string text) =>
        RecordSamples.Write(RecordSamples.Record(key, "v1", RecordSamples.Github) with { Kind = RecordKind.Issue, Repo = _repo, Title = "Tax is wrong" }, text);

    private static RecordWrite DocRow(string path, string version, string text) =>
        RecordSamples.Write(RecordSamples.Record(RepoDocs.Key(_repo, path).Value, version, RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _repo, Title = path }, text);

    private static async Task Put(FakeRecordStore store, params RecordWrite[] writes)
    {
        foreach (var source in writes.GroupBy(w => w.Record.Source))
        {
            _ = await store.Commit(RecordSamples.Batch(source.Key, 1, [.. source]), MapWorld.None);
        }
    }

    private static async Task<(MapBuildReport Report, GraphSnapshot Graph)> Build(MapWorld world)
    {
        var report = (await world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;
        return (report, (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph);
    }

    private static bool Has(MapBuildReport report, string line) => report.Degraded.Contains(line, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task A_tracker_record_citing_a_file_is_a_node_with_a_mentions_edge_in_the_draft_and_the_report_carries_the_record_lines()
    {
        var world = World();
        await Put(world.Records, Issue("github:issue/acme/app/5", "The bug is in `src/keys.ts`."));

        var (report, graph) = await Build(world);

        Assert.NotNull(graph.Resolve(NodeKey.From("record:github:issue/acme/app/5")));
        Assert.Contains(graph.Edges, e => e is { Type: EdgeType.Mentions } && e.From.Value == "record:github:issue/acme/app/5" && e.To.Value == "code:src/keys.ts");
        Assert.Contains(report.Notes, d => d.StartsWith("records: as of stamp ", StringComparison.Ordinal));
        Assert.Contains(report.Notes, n => string.Equals(n, "citations: 1 resolved, 0 unresolved (0 missing, 0 ambiguous)", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Degraded, d => d.StartsWith("records: as of", StringComparison.Ordinal) || d.StartsWith("citations:", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Proposed, p => p.Key.Kind == NodeKind.Record);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_doc_whose_stored_version_is_its_blob_at_head_gets_its_citations_and_a_stale_one_is_counted()
    {
        const string text = "Keys live in `src/keys.ts`.";
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal) { ["docs/fresh.md"] = text, ["docs/stale.md"] = text };
        var world = World(files);
        await Put(world.Records, DocRow("docs/fresh.md", FakeRepoReader.BlobId(text), text), DocRow("docs/stale.md", "0000000000000000000000000000000000000000", text));

        var (report, graph) = await Build(world);

        Assert.Contains(graph.Edges, e => e.From.Value == "record:" + RepoDocs.Key(_repo, "docs/fresh.md").Value && e.To.Value == "code:src/keys.ts");
        Assert.DoesNotContain(graph.Edges, e => e.From.Value == "record:" + RepoDocs.Key(_repo, "docs/stale.md").Value && e.Type != EdgeType.BelongsTo);
        Assert.True(Has(report, "records: 1 docs older than HEAD"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_over_the_file_cap_gets_one_edge_per_feature_with_its_file_count_and_none_per_file()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["alpha/Alpha.csproj"] = "<Project/>", ["beta/Beta.csproj"] = "<Project/>" };
        var paths = Enumerable.Range(0, 30).SelectMany(i => new[] { string.Create(CultureInfo.InvariantCulture, $"alpha/F{i:D2}.cs"), string.Create(CultureInfo.InvariantCulture, $"beta/F{i:D2}.cs") }).ToList();
        foreach (var path in paths)
        {
            files[path] = "class C {}";
        }

        var world = World(files);
        var pr = RecordSamples.Write(
            RecordSamples.Record("github:pr/acme/app/9", "v1", RecordSamples.Github) with { Kind = RecordKind.Pr, Repo = _repo, Links = [.. paths.Select(p => new RecordLink("path:aw-split-panes/" + p, RecordLinkKind.Touches, Title: null))] },
            "Rename everything");
        await Put(world.Records, pr);

        var (report, graph) = await Build(world);

        var key = "record:github:pr/acme/app/9";
        var homes = graph.Edges.Where(e => e is { Type: EdgeType.Implements, Provenance: Provenance.Deterministic } && e.To.Kind == NodeKind.Feature && paths.Contains(e.From.Value["code:".Length..], StringComparer.Ordinal))
            .GroupBy(e => e.To.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => e.From.Value == key && e.Type != EdgeType.BelongsTo).ToList();
        Assert.True(homes.Count >= 2, "the fixture should give two features");
        Assert.NotNull(graph.Resolve(NodeKey.From(key)));
        Assert.DoesNotContain(edges, e => e.To.Kind == NodeKind.Code);
        Assert.All(edges, e => Assert.Equal(EdgeType.Mentions, e.Type));
        Assert.Equal(homes.Keys.Order(StringComparer.Ordinal), edges.Select(e => e.To.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(edges, e => Assert.Equal(homes[e.To.Value], e.Count));
        Assert.True(Has(report, "records over the file cap: 1 (60 paths)"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_store_that_cannot_be_read_is_a_degraded_line_and_the_build_still_writes_its_draft()
    {
        var world = World(store: new UnreadableRecordStore(new FakeRecordStore()));

        var (report, graph) = await Build(world);

        Assert.True(Has(report, $"records: unavailable ({ContextErrors.StoreUnavailable.Code})"));
        Assert.NotEmpty(graph.Nodes);
        Assert.DoesNotContain(graph.Nodes, n => n.Key.Kind == NodeKind.Record);
    }
}
