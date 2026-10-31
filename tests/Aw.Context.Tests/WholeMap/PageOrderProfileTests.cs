using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.WholeMap;

/// <summary>The pages in no concept list owner-named pages in the profile's order, not the map's node order (pages spec §3.4, #1791).</summary>
public sealed class PageOrderProfileTests : IDisposable
{
    private const string Route = "surface:route/GET:/schedule/week";
    private const string OwnerScreen = "surface:screen/owner-first-page";

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    // The route surface sits first in node order; the page that names no surface gets an owner node appended after it.
    private static string Yaml() => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        $"  - {{key: \"{Route}\", label: \"GET /schedule/week\"}}",
        "  - {key: \"code:src/Week.tsx\", label: \"Week.tsx\"}",
        "edges:",
        $"  - {{from: \"code:src/Week.tsx\", to: \"{Route}\", type: implements}}");

    private static ContextScope Profile(params PagePin[] pages) =>
        MapWorld.Human with
        {
            Repos = [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { Pages = pages }],
        };

    [Fact, Trait("Lane", "unit")]
    public async Task Owner_pages_with_no_concept_list_in_profile_order_not_map_order()
    {
        _ = await _world.ImportYaml(Yaml());
        var repo = ContextRepoName.From(FixtureMap.Repo);
        var scope = Profile(
            new PagePin(PageId.From("first-page"), "First page", repo, SurfaceKey: null, []),
            new PagePin(PageId.From("second-page"), "Second page", repo, Route, []));

        var view = (await _world.Engine.WholeMap(new WholeMapQuery(Detail: null, Locate: [], MapRead.Current, Repo: null, Area: null) { PagesInNoConcept = true }, scope, MapWorld.None)).Value;

        Assert.Equal([OwnerScreen, Route], view.Pages);
    }
}
