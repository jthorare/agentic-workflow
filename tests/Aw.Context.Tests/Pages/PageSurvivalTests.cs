using System.Diagnostics;
using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Pages;

/// <summary>The pages overlay through the readers: it shows on the next cached read, an export never carries it and an import drops it (pages spec §3.3).</summary>
public sealed class PageSurvivalTests : IDisposable
{
    private const string OwnerScreen = "surface:screen/owner-schedule-week";

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope WithPage(string name) =>
        MapWorld.Human with
        {
            Repos =
            [
                new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes")
                {
                    Pages = [new PagePin(PageId.From("schedule-week"), name, ContextRepoName.From(FixtureMap.Repo), SurfaceKey: null, [])],
                },
                MapWorld.Repos[1],
            ],
        };

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_page_edit_shows_on_the_next_cached_read()
    {
        _ = await _world.ImportFixture();
        var reader = new MapReader(_world.Store, _world.Intents, new MapViewCache());

        var before = await reader.Read(MapWorld.Human, draft: false, MapWorld.None);
        var first = await reader.Read(WithPage("Schedule week"), draft: false, MapWorld.None);
        var renamed = await reader.Read(WithPage("Week schedule"), draft: false, MapWorld.None);

        Assert.DoesNotContain(before.Value.Graph.Nodes, n => n.Key.Value == OwnerScreen);
        Assert.Equal("Schedule week", Assert.Single(first.Value.Graph.Nodes, n => n.Key.Value == OwnerScreen).Label);
        Assert.Equal("Week schedule", Assert.Single(renamed.Value.Graph.Nodes, n => n.Key.Value == OwnerScreen).Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_export_holds_the_map_and_never_the_pages()
    {
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path: null, WithPage("Schedule week"), MapWorld.None);

        Assert.True(exported.IsSuccess, exported.IsError ? exported.Error.Message : null);
        var document = _world.Exports.Documents[exported.Value.Path];
        Assert.DoesNotContain(document.Nodes, n => n.Key.Value == OwnerScreen);
        Assert.DoesNotContain(document.Edges ?? [], e => e.EvidenceOrEmpty.Any(r => r.Key.Value.StartsWith("page:", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_import_that_carries_page_nodes_and_edges_drops_them()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"surface:screen/owner-schedule-week\", label: \"Schedule week\", evidence: [{key: \"page:aw-split-panes/schedule-week\"}]}",
            "edges:",
            "  - {from: \"surface:screen/owner-schedule-week\", to: \"feature:kept\", type: part-of, evidence: [{key: \"page:aw-split-panes/schedule-week\"}]}"));

        var read = await new MapReader(_world.Store, _world.Intents).Read(MapWorld.Human, draft: false, MapWorld.None);

        Assert.Contains(read.Value.Graph.Nodes, n => n.Key.Value == "feature:kept");
        Assert.DoesNotContain(read.Value.Graph.Nodes, n => n.Key.Value == OwnerScreen);
        Assert.Empty(read.Value.Graph.Edges);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_most_pages_with_the_most_actions_lay_over_a_map_inside_the_unit_budget()
    {
        var pages = Enumerable.Range(0, PageRules.MaxPages).Select(p => new PagePin(
            PageId.From(string.Create(CultureInfo.InvariantCulture, $"page-{p}")), string.Create(CultureInfo.InvariantCulture, $"Page {p}"), ContextRepoName.From("web"), SurfaceKey: null,
            [.. Enumerable.Range(0, PageRules.MaxActions).Select(a => new PageActionPin(PageActionId.From(string.Create(CultureInfo.InvariantCulture, $"a{a}")), string.Create(CultureInfo.InvariantCulture, $"Action {a}"), ActionKey: null, ["self-scheduling"]))])).ToList();
        var repos = new[] { new ContextRepo(ContextRepoName.From("web"), "/work/web") { Pages = pages } };

        var watch = Stopwatch.StartNew();
        var applied = PageOverlay.Apply(PageOverlayTests.MapOf(), repos);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), string.Create(CultureInfo.InvariantCulture, $"200 pages x 40 actions took {watch.ElapsedMilliseconds} ms"));
        Assert.Equal(PageRules.MaxPages * PageRules.MaxActions, applied.Edges.Count(e => e.Type == EdgeType.Expresses && e.From.Value.StartsWith("action:owner/", StringComparison.Ordinal)));
    }
}
