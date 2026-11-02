using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>A concept's scoped map loads from the real answer (its concept node sits in an area the answer does not list), and a read that throws ends in the failure row.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptScopedMapTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_concept_opens_its_scoped_map_from_the_real_answer_with_the_concept_node_homed()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        api.WholeMapAnswer = query => query.Concept is null ? ContextAnswers.WholeMap(query) : RouteAnswer.Api("context-whole", "concept-open");
        var page = await ConceptPage.Shown(api);

        await page.OpenConcept("concept:billing", Token);

        Expect.Equal((true, false, false), (page.Whole.IsReady, page.Whole.IsLoading, page.Whole.IsFailed));
        Expect.True(page.Whole.Areas.Any(a => string.Equals(a.Key, "area:unassigned", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_scoped_read_that_throws_ends_in_the_failure_row_and_not_in_loading()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        api.WholeMapAnswer = query => query.Concept is null ? ContextAnswers.WholeMap(query) : throw new InvalidOperationException("the answer could not be drawn");
        var page = await ConceptPage.Shown(api);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => page.OpenConcept("concept:billing", Token));

        Expect.Equal((true, false, true), (page.Whole.IsFailed, page.Whole.IsLoading, page.ShowWholeStatus));
    }
}
