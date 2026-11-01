using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Pages;

/// <summary>ConceptRow.Pages: the pages that express a concept directly or through an action on them.</summary>
public sealed class ConceptPagesCountTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_concept_row_counts_the_pages_that_express_it_directly_or_through_an_action_and_zero_otherwise()
    {
        _ = await _world.ImportYaml(PagesWorld.Yaml());

        var view = (await _world.Engine.Concepts(new ConceptsQuery(MapRead.Current, new ConceptPage()), PagesWorld.Human(), MapWorld.None)).Value;

        var counts = view.Concepts.ToDictionary(c => c.Id.Value, c => c.Pages, StringComparer.Ordinal);
        Assert.Equal((1, 1, 0), (counts["concept:self-scheduling"], counts["concept:approval-workflow"], counts["concept:billing"]));
    }

    // Issue 1870's repro: the owner ticks the concept the map already suggested for the action (what Accept all does).
    [Fact, Trait("Lane", "unit")]
    public async Task An_owner_page_ticking_the_concept_the_map_suggested_keeps_it_on_the_action_and_in_the_count()
    {
        _ = await _world.ImportYaml(PagesWorld.Yaml());
        var owner = PagesWorld.Named("schedule-week", "Schedule week", PagesWorld.Week, new PageActionPin(PageActionId.From("submit"), "Submit", "action:WeekPage.submit", ["self-scheduling"]));
        var scope = PagesWorld.Human(owner);

        var pages = (await _world.Engine.Pages(PagesWorld.Query(PageMarks.Owner), scope, MapWorld.None)).Value;
        var concepts = (await _world.Engine.Concepts(new ConceptsQuery(MapRead.Current, new ConceptPage()), scope, MapWorld.None)).Value;

        Assert.Equal(["self-scheduling"], Assert.Single(pages.Pages).Actions.Single(a => a.Id.Value == "submit").Concepts, StringComparer.Ordinal);
        Assert.Equal(1, concepts.Concepts.Single(c => c.Id.Value == "concept:self-scheduling").Pages);
    }
}
