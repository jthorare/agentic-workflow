using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Pages;

/// <summary>The pages read through the facade: the map is imported with a route surface and an action on it, the profile names the page, and the answer is read as a human.</summary>
public sealed class GetPagesTests : IDisposable
{
    private readonly List<MapWorld> _worlds = [];

    public void Dispose()
    {
        foreach (var world in _worlds)
        {
            world.Dispose();
        }
    }

    private static string Yaml() => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"concept:self-scheduling\", label: \"Self-scheduling\", attributes: {summary: \"People pick shifts.\", source: derived}}",
        "  - {key: \"concept:approval-workflow\", label: \"Approval workflow\", attributes: {summary: \"Managers approve.\", source: derived}}",
        "  - {key: \"surface:route/GET:/schedule/week\", label: \"GET /schedule/week\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/Week.tsx\"}]}",
        "  - {key: \"action:WeekPage.submit\", label: \"submit\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/Week.tsx\"}]}",
        "  - {key: \"code:src/Week.tsx\", label: \"Week.tsx\", provenance: deterministic}",
        "edges:",
        "  - {from: \"code:src/Week.tsx\", to: \"surface:route/GET:/schedule/week\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/Week.tsx\"}]}",
        "  - {from: \"action:WeekPage.submit\", to: \"surface:route/GET:/schedule/week\", type: on, provenance: deterministic}");

    private static ContextScope Scope()
    {
        var page = new PagePin(
            PageId.From("schedule-week"), "Schedule week", ContextRepoName.From("aw-split-panes"), "surface:route/GET:/schedule/week",
            [new PageActionPin(PageActionId.From("submit-schedule"), "Submit a schedule", "action:WeekPage.submit", ["self-scheduling", "approval-workflow"])]);
        return MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes") { Pages = [page] }]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_pages_lists_an_owner_page_with_its_actions_and_concepts()
    {
        var world = new MapWorld();
        _worlds.Add(world);
        _ = await world.ImportYaml(Yaml());

        var query = new PagesQuery(Concept: null, Page: null, Repo: null, PageMarks.All, Limit: null, Offset: null, Draft: false, MapVersion: null);
        var view = (await world.Engine.Pages(query, Scope(), MapWorld.None)).Value;

        var page = Assert.Single(view.Pages);
        Assert.Equal(("schedule-week", "Schedule week", "owner", "aw-split-panes"), (page.Id.Value, page.Name, page.Mark, page.Repo));
        var action = Assert.Single(page.Actions);
        Assert.Equal(("submit-schedule", "owner"), (action.Id.Value, action.Mark));
        Assert.Equal(["approval-workflow", "self-scheduling"], action.Concepts, StringComparer.Ordinal);
        Assert.Equal((1, false), (view.Total, view.Truncated));
    }
}
