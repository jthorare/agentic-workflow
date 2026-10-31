using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Whole;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>#1724 P2b-1b: the Context head's "Pages in no concept (N)" link, counted by the concepts read, opens those pages in a panel over the concept cards (pages spec §3.4).</summary>
[Lane(TestLane.Unit)]
public sealed class PagesInNoConceptTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FakeSindriApi Serving(int? count) => new()
    {
        ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Three() with { PagesInNoConcept = count }),
        WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.OK, Encoding.UTF8.GetString(AwJson.Serialize(PagesSectionTests.Scheduling(), SindriApiClient.Json))),
    };

    [Fact]
    public async Task The_head_links_the_pages_in_no_concept_with_their_count_and_reads_no_map()
    {
        var api = Serving(12);

        var page = await ConceptPage.Shown(api);

        Expect.Equal((true, Labels.ContextPagesNoConceptLink(12)), (page.ShowNoConceptLink, page.NoConceptLinkText));
        Expect.Equal(0, api.WholeMapAsked.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    public async Task No_count_hides_the_link(int? count) => Expect.True(!(await ConceptPage.Shown(Serving(count))).ShowNoConceptLink);

    [Fact]
    public async Task The_link_reads_the_pages_in_no_concept_and_lists_them_in_a_panel()
    {
        var api = Serving(3);
        var page = await ConceptPage.Shown(api);

        await page.OpenNoConcept(Token);

        var asked = Assert.Single(api.WholeMapAsked);
        Expect.Equal((true, "nodes", null), (asked.PagesInNoConcept, asked.Detail, asked.Concept));
        Expect.Equal((true, Labels.ContextPagesNoConceptHead), (page.ShowNoConcept, page.NoConceptPages.Head));
        Expect.Sequence(["Time-off request", "Schedule week", "Availability"], page.NoConceptPages.Rows.Select(static r => r.Name));
    }

    [Fact]
    public async Task Esc_closes_the_panel_and_the_concept_cards_stay()
    {
        var page = await ConceptPage.Shown(Serving(3));
        await page.OpenNoConcept(Token);

        Expect.True(page.Back());

        Expect.Equal((false, true), (page.ShowNoConcept, page.ShowConcepts));
    }

    [Fact]
    public async Task A_failed_read_says_so_and_Try_again_reads_again()
    {
        var api = Serving(3);
        api.WholeMapAnswer = _ => new RouteAnswer(HttpStatusCode.InternalServerError, "{}");
        var page = await ConceptPage.Shown(api);

        await page.OpenNoConcept(Token);
        Expect.Equal((true, Labels.ContextPagesNoConceptFailed), (page.NoConceptPages.IsFailed, page.NoConceptPages.StatusText));

        api.WholeMapAnswer = Serving(3).WholeMapAnswer;
        await page.OpenNoConcept(Token);
        Expect.Equal((false, 3, 2), (page.NoConceptPages.IsFailed, page.NoConceptPages.Rows.Count, api.WholeMapAsked.Count));
    }
}
