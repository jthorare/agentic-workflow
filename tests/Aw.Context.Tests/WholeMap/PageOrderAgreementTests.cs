using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Context.Core.WholeMap;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.WholeMap;

/// <summary>The concept drill and the page read list owner pages in the profile's order, as the pages in no concept do (pages spec §3.4, #1791).</summary>
public sealed class PageOrderAgreementTests : IDisposable
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
        "  - {key: \"concept:scheduling\", label: \"Scheduling\", attributes: {summary: \"People pick shifts.\", source: derived}}",
        "edges:",
        $"  - {{from: \"code:src/Week.tsx\", to: \"{Route}\", type: implements}}");

    private static ContextScope Profile(params PagePin[] pages) =>
        MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { Pages = pages }]);

    private static PagePin[] FirstThenSecond(params string[] concepts)
    {
        var repo = ContextRepoName.From(FixtureMap.Repo);
        PageActionPin[] Actions(string id) => concepts.Length == 0 ? [] : [new PageActionPin(PageActionId.From(id), "Do it", ActionKey: null, concepts)];
        return
        [
            new PagePin(PageId.From("first-page"), "First page", repo, SurfaceKey: null, Actions("act-first")),
            new PagePin(PageId.From("second-page"), "Second page", repo, Route, Actions("act-second")),
        ];
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_concept_drill_lists_the_pages_of_actions_that_express_it_in_profile_order()
    {
        _ = await _world.ImportYaml(Yaml());
        var scope = Profile(FirstThenSecond("scheduling"));

        var view = (await _world.Engine.WholeMap(new WholeMapQuery(Detail: null, Locate: [], MapRead.Current, Repo: null, Area: null) { Concept = "concept:scheduling" }, scope, MapWorld.None)).Value;

        Assert.Equal([OwnerScreen, Route], view.Pages);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_page_read_and_the_pages_in_no_concept_list_owner_pages_in_the_same_order()
    {
        _ = await _world.ImportYaml(Yaml());
        var scope = Profile(FirstThenSecond());

        var read = (await _world.Engine.Pages(new PagesQuery(Concept: null, Page: null, Repo: null, PageMarks.Owner, Limit: null, Offset: null, Draft: false, MapVersion: null), scope, MapWorld.None)).Value;
        var opened = (await _world.Engine.WholeMap(new WholeMapQuery(Detail: null, Locate: [], MapRead.Current, Repo: null, Area: null) { PagesInNoConcept = true }, scope, MapWorld.None)).Value;

        Assert.Equal([OwnerScreen, Route], read.Pages.Select(p => p.Key.Value), StringComparer.Ordinal);
        Assert.Equal(read.Pages.Select(p => p.Key.Value), opened.Pages, StringComparer.Ordinal);
    }
}
