using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;
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
}
