using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.WholeMap;

/// <summary>The whole map names the owner page that named a node, so the app knows which page a row is without a second read (#1816).</summary>
public sealed class WholeMapPageIdTests : IDisposable
{
    private const string Route = "surface:route/GET:/schedule/week";
    private const string Suggested = "surface:route/GET:/other";
    private const string OwnerScreen = "surface:screen/owner-x";

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Whole_map_serves_the_page_id_of_a_named_surface()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            $"  - {{key: \"{Route}\", label: \"GET /schedule/week\"}}",
            $"  - {{key: \"{Suggested}\", label: \"GET /other\"}}"));
        var repo = ContextRepoName.From(FixtureMap.Repo);
        var scope = MapWorld.Human with
        {
            Repos = [new ContextRepo(repo, "/work/aw-split-panes")
            {
                Pages = [new PagePin(PageId.From("schedule-week"), "Week", repo, Route, []), new PagePin(PageId.From("x"), "X", repo, SurfaceKey: null, [])],
            }],
        };

        var view = (await _world.Engine.WholeMap(new WholeMapQuery("nodes", [], MapRead.Current), scope, MapWorld.None)).Value;

        var byKey = view.Nodes!.ToDictionary(n => n.Key.Value, n => n.PageId?.Value, StringComparer.Ordinal);
        Assert.Equal("schedule-week", byKey[Route]);
        Assert.Equal("x", byKey[OwnerScreen]);
        Assert.Null(byKey[Suggested]);
    }
}
