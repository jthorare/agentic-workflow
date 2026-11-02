using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>Issue #1373, spec 2026-10-27 §11.2: with pins, a directory doc and a tracker record reach the pin holding their files, and a tracker project reaches every pin its issues' files fall under.</summary>
public sealed class RecordPinLinksTests : IDisposable
{
    private const string PrKey = "github:pr/acme/app/9";
    private const string ProjectKey = "linear:project/p1";
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private static readonly string[] _dirs = ["alpha", "beta", "gamma"];
    private readonly List<MapWorld> _worlds = [];

    public void Dispose() => _worlds.ForEach(w => w.Dispose());

    private static StoredRecord Touching(string key, SourceId source, RecordKind kind, params string[] paths) =>
        RecordSamples.Record(key, "v1", source) with { Kind = kind, Repo = _repo, Links = [.. paths.Select(p => new RecordLink("path:aw-split-panes/" + p, RecordLinkKind.Touches, Title: null))] };

    private static StoredRecord InProject(StoredRecord issue) => issue with { Links = [.. issue.Links, new RecordLink(ProjectKey, RecordLinkKind.InProject, "Project one")] };

    private async Task<GraphSnapshot> Build()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["alpha/AGENTS.md"] = "Files: `alpha/F00.cs`.", ["alpha/Alpha.csproj"] = "<Project/>", ["beta/Beta.csproj"] = "<Project/>", ["gamma/Gamma.csproj"] = "<Project/>" };
        foreach (var dir in _dirs)
        {
            for (var i = 0; i < 2; i++)
            {
                files[string.Create(CultureInfo.InvariantCulture, $"{dir}/F0{i}.cs")] = "class C {}";
            }
        }

        var world = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files));
        _worlds.Add(world);
        var doc = RecordSamples.Write(
            RecordSamples.Record(RepoDocs.Key(_repo, "alpha/AGENTS.md").Value, FakeRepoReader.BlobId(files["alpha/AGENTS.md"]), RepoDocs.Id) with { Kind = RecordKind.Doc, Repo = _repo, Title = "alpha/AGENTS.md" },
            files["alpha/AGENTS.md"]);
        var pr = RecordSamples.Write(Touching(PrKey, RecordSamples.Github, RecordKind.Pr, "alpha/F00.cs", "alpha/F01.cs"), "Touch two alpha files");
        var project = RecordSamples.Write(RecordSamples.Record(ProjectKey) with { Kind = RecordKind.Project, Title = "Project one" }, "A project");
        var issues = new[]
        {
            RecordSamples.Write(InProject(Touching("linear:issue/1", RecordSamples.Linear, RecordKind.Issue, "alpha/F00.cs")), "An alpha issue"),
            RecordSamples.Write(InProject(Touching("linear:issue/2", RecordSamples.Linear, RecordKind.Issue, "beta/F00.cs")), "A beta issue"),
            RecordSamples.Write(Touching("linear:issue/3", RecordSamples.Linear, RecordKind.Issue, "gamma/F00.cs"), "A gamma issue in no project"),
        };
        foreach (var source in new[] { doc, pr, project }.Concat(issues).GroupBy(w => w.Record.Source))
        {
            _ = await world.Records.Commit(RecordSamples.Batch(source.Key, 1, [.. source]), MapWorld.None);
        }

        var pins = _dirs.Select(d => new ConceptPin(ConceptId.From(d), d, Summary: null, [], [d + "/**"], [], [])).ToList();
        var repos = new[] { new ContextRepo(_repo, "/work/aw-split-panes") { Concepts = pins } };
        _ = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, repos), MapWorld.None)).Value;
        return (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;
    }

    private static List<string> PinsOf(GraphSnapshot graph, string record) =>
        [.. graph.Edges.Where(e => e.From.Value == "record:" + record && e.To.Kind == NodeKind.Concept).Select(e => e.To.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_directory_doc_reaches_the_pin_holding_its_directory()
    {
        var graph = await Build();

        Assert.Equal(["concept:alpha"], PinsOf(graph, RepoDocs.Key(_repo, "alpha/AGENTS.md").Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pull_request_reaches_the_pin_its_files_are_homed_in()
    {
        var graph = await Build();

        Assert.Equal(["concept:alpha"], PinsOf(graph, PrKey), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_project_reaches_every_pin_its_issues_files_fall_under_and_no_other()
    {
        var graph = await Build();

        Assert.Equal(["concept:alpha", "concept:beta"], PinsOf(graph, ProjectKey), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_issue_links_to_its_project()
    {
        var graph = await Build();

        Assert.Contains(graph.Edges, e => e is { Type: EdgeType.Mentions } && e.From.Value == "record:linear:issue/1" && e.To.Value == "record:" + ProjectKey);
    }
}
