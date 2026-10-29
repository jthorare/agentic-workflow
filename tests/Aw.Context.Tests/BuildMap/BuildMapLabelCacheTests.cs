using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Models;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure;
using Aw.Context.Testing.Code;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Extract;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.DependencyInjection;
using TempHome = Aw.Context.Tests.Infrastructure.TempHome;

namespace Aw.Context.Tests.BuildMap;

/// <summary>
/// Issue 1723, q149: model calls happen at setup and on new data only. The real labeler (AddContextLabeling) over a counting fake
/// model port and a temp context.db, driven through BuildMap. Each module (a directory with a package.json) is one home feature, and
/// cluster ids are positional ('h00000' ...), so a module that sorts first shifts every later id: a label cache must key on the files.
/// </summary>
public sealed class BuildMapLabelCacheTests : IDisposable
{
    private const string Labeling = """{"repos":["aw-split-panes"],"labeling":{"enabled":true}}""";
    private static readonly string[] _modules = ["mod-b", "mod-c", "mod-d"];
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly FakeModelPort _model = new();
    private readonly TempHome _home = new();
    private readonly ServiceProvider _labeling;
    private readonly MapWorld _world;
    private int _answers;

    public BuildMapLabelCacheTests()
    {
        foreach (var module in _modules)
        {
            AddModule(module);
        }

        // The model names whatever it is asked about, and numbers each answer, so a label that came from a second ask differs from the first.
        _model.Responder = request => request.Purpose == ModelPurpose.Labeling ? Answer(request.Prompt) : "[]";
        _labeling = new ServiceCollection()
            .AddSingleton<IModelPort>(_model)
            .AddSingleton<IScrubberFactory>(_home.Factory)
            .AddSingleton(_home.Database)
            .AddSingleton(_home.Settings)
            .AddSingleton<TimeProvider>(_home.Clock)
            .AddContextLabeling()
            .BuildServiceProvider();
        var labeler = _labeling.GetRequiredService<ILabeler>();
        _world = new MapWorld(
            new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, _files),
            codeSearch: new FakeCodeSearch(MapWorld.Repos[0], MapWorld.Head, _files),
            configure: services => services.AddSingleton(labeler));
    }

    public void Dispose()
    {
        _world.Dispose();
        _labeling.Dispose();
        _home.Dispose();
    }

    // A cluster line of the labeling prompt: 'h00000: first/file, second/file'.
    private static IEnumerable<(string Id, string Files, string Line)> ClusterLines(string prompt) =>
        prompt.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 7 && l[0] == 'h' && l[6] == ':' && l[1..6].All(char.IsAsciiDigit))
            .Select(l => (l[..6], l[8..], l));

    private void AddModule(string name)
    {
        _files[name + "/package.json"] = "{}";
        _files[name + "/src/index.ts"] = "export const " + name.Replace('-', '_') + " = 1;";
    }

    private string Answer(string prompt)
    {
        var labels = ClusterLines(prompt).Select(c =>
        {
            _answers++;
            var first = c.Files.Split(", ")[0];
            return new { id = c.Id, label = string.Create(CultureInfo.InvariantCulture, $"Feature of {first} n{_answers}") };
        });
        return JsonSerializer.Serialize(labels);
    }

    private List<ModelRequest> LabelingAsks => [.. _model.Requests.Where(r => r.Purpose == ModelPurpose.Labeling)];

    private async Task<Dictionary<string, string>> BuildAndReadLabels()
    {
        var built = await _world.Map.BuildMap(progress: null, MapWorld.HumanWith(Labeling), MapWorld.None);
        Assert.True(built.IsSuccess, built.IsError ? built.Error.Message : null);
        var graph = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!.Graph;
        var labelOf = graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature).ToDictionary(n => n.Key.Value, n => n.Label, StringComparer.Ordinal);
        return graph.Edges
            .Where(e => e.Type == EdgeType.Implements && e.To.Kind == NodeKind.Feature && e.From.Value.EndsWith("/src/index.ts", StringComparison.Ordinal))
            .ToDictionary(e => e.From.Value["code:".Length..], e => labelOf[e.To.Value], StringComparer.Ordinal);
    }

    private static List<string> ClusterLinesOf(IEnumerable<ModelRequest> asks) =>
        [.. asks.SelectMany(r => ClusterLines(r.Prompt)).Select(c => c.Line)];

    [Fact, Trait("Lane", "integration")]
    public async Task Rebuild_of_an_unchanged_repo_asks_the_labeler_model_nothing()
    {
        var first = await BuildAndReadLabels();
        Assert.Equal(_modules.Length, ClusterLinesOf(LabelingAsks).Count);
        var askedByFirst = LabelingAsks.Count;

        var second = await BuildAndReadLabels();

        Assert.Equal(0, LabelingAsks.Count - askedByFirst);
        Assert.Equal(first, second);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Adding_one_feature_asks_the_model_about_that_one_cluster_only()
    {
        _ = await BuildAndReadLabels();
        var askedByFirst = LabelingAsks.Count;
        AddModule("mod-a");

        _ = await BuildAndReadLabels();

        var line = Assert.Single(ClusterLinesOf(LabelingAsks.Skip(askedByFirst)));
        Assert.Contains("mod-a/src/index.ts", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_labels_stays_with_its_files_when_ids_shift()
    {
        var before = await BuildAndReadLabels();
        AddModule("mod-a");

        var after = await BuildAndReadLabels();

        Assert.Equal(_modules.Length + 1, after.Count);
        foreach (var (file, label) in before)
        {
            Assert.Equal(label, after[file]);
        }
    }
}
