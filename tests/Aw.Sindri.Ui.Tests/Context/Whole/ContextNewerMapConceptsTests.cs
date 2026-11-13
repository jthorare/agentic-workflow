using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Concepts;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3261: a page opened on a map with no concepts reads the concepts again when a newer map version is published, so the concept layer appears.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextNewerMapConceptsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer StatusAt(int number) =>
        new(HttpStatusCode.OK, GoldenFiles.Api("context-map", "map-tabs-anchor").Replace("\"number\": 1", $"\"number\": {number.ToString(CultureInfo.InvariantCulture)}", StringComparison.Ordinal));

    [Fact]
    public async Task A_newer_map_with_concepts_brings_the_concept_layer_on_the_next_open()
    {
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Without("empty")) };
        var page = await ConceptPage.Shown(api);
        Expect.Equal((false, 1), (page.ShowConcepts, api.ContextConceptsAsked.Count));

        api.ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Ok([AConcepts.Concept("concept:billing", "Billing")]));
        api.MapStatusAnswer = StatusAt(2);
        await page.Opened(Token);

        Expect.Equal((true, true, 2), (page.ShowConcepts, page.Concepts.HasConcepts, api.ContextConceptsAsked.Count));
        Expect.Equal(string.Empty, page.Concepts.Note);
    }

    [Fact]
    public async Task The_same_map_version_does_not_read_the_concepts_again()
    {
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Without("empty")) };
        var page = await ConceptPage.Shown(api);

        await page.Opened(Token);

        Expect.Equal(1, api.ContextConceptsAsked.Count);
    }
}
