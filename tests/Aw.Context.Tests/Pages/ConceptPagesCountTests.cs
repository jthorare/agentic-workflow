using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Pages;

/// <summary>ConceptRow.Pages and SuggestedPages: the pages that express a concept directly or through an action on them, and the map keys of the suggested ones.</summary>
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

    [Fact, Trait("Lane", "unit")]
    public async Task A_concept_row_lists_its_suggested_pages_by_map_key_and_none_when_it_has_none()
    {
        _ = await _world.ImportYaml(PagesWorld.Yaml());

        var view = (await _world.Engine.Concepts(new ConceptsQuery(MapRead.Current, new ConceptPage()), PagesWorld.Human(), MapWorld.None)).Value;

        var suggested = view.Concepts.ToDictionary(c => c.Id.Value, c => c.SuggestedPages, StringComparer.Ordinal);
        Assert.Equal([PagesWorld.Week], suggested["concept:self-scheduling"]);
        Assert.Equal([PagesWorld.Settings], suggested["concept:approval-workflow"]);
        Assert.Null(suggested["concept:billing"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_page_the_owner_named_is_counted_but_not_listed_as_suggested()
    {
        _ = await _world.ImportYaml(PagesWorld.Yaml());
        var named = PagesWorld.Named("schedule-week", "Schedule week", PagesWorld.Week, new PageActionPin(PageActionId.From("submit"), "Submit", "action:WeekPage.submit", ["self-scheduling"]));

        var view = (await _world.Engine.Concepts(new ConceptsQuery(MapRead.Current, new ConceptPage()), PagesWorld.Human(named), MapWorld.None)).Value;

        var row = view.Concepts.Single(c => c.Id.Value == "concept:self-scheduling");
        Assert.Equal((1, null), (row.Pages, row.SuggestedPages));
    }
}
