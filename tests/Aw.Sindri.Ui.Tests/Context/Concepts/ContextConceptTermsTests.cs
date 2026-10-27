using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The concept card's top terms, read from GET /sindri/context/concepts/glossary: terms show on the card by concept id, count in the concept search, and a glossary with none, a refusal or a server without the route leaves the cards bare.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptTermsTests
{
    // Mirrors tests/Aw.Sindri.Tests/Golden/Api/context-concepts-glossary/terms.json; GoldenContractTests reads all three goldens.
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string TermsBody = """
        {"apiVersion":1,"repo":"aw-split-panes","mapVersion":"v1","state":"ok",
         "concepts":[{"id":"concept:billing","name":"Billing","terms":["invoice","shift"]},{"id":"concept:scheduling","name":"Self scheduling","terms":["shift","swap","roster"]}],
         "terms":[{"term":"invoice","concepts":["concept:billing"]},{"term":"roster","concepts":["concept:scheduling"]},{"term":"shift","concepts":["concept:billing","concept:scheduling"]},{"term":"swap","concepts":["concept:scheduling"]}]}
        """;

    // Mirrors .../context-concepts-glossary/no-terms-empty.json.
    private const string EmptyBody = """{"apiVersion":1,"repo":"aw-split-panes","mapVersion":"v1","state":"empty","concepts":[],"terms":[]}""";

    // Mirrors .../context-concepts-glossary/no-map-030.json.
    private const string NoMapBody = """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"SND-CONTEXT-030","status":404,"detail":"there is no map yet (fix: sindri context map import <file>)","traceId":"<trace>"}""";
#pragma warning restore MA0136

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextConceptsView Concepts() =>
        AConcepts.Ok([AConcepts.Concept("concept:scheduling", "Self scheduling", "People book slots."), AConcepts.Concept("concept:billing", "Billing", "Invoices."), AConcepts.Concept("concept:sync", "Sync")], links: []);

    private static FakeSindriApi Serving(RouteAnswer glossary) => new()
    {
        ContextConceptsAnswer = _ => AConcepts.Served(Concepts()),
        ContextConceptGlossaryAnswer = _ => glossary,
    };

    [Fact]
    public async Task Each_card_shows_the_terms_of_its_own_concept_and_a_concept_without_any_shows_none()
    {
        var page = await ConceptPage.Shown(Serving(new RouteAnswer(HttpStatusCode.OK, TermsBody)));

        Expect.Sequence(
            [("Terms: shift · swap · roster", true), ("Terms: invoice · shift", true), (string.Empty, false)],
            page.Concepts.Tiles.Take(3).Select(t => (t.TermsText, t.HasTerms)));
    }

    [Fact]
    public async Task The_card_shows_the_first_three_terms_and_how_many_more_there_are()
    {
        var glossary = AConcepts.Glossary("ok", new ConceptTerms("concept:scheduling", "Self scheduling", ["shift", "swap", "roster", "rota", "leave"]));
        var page = await ConceptPage.Shown(Serving(AConcepts.Served(glossary)));

        Expect.Equal("Terms: shift · swap · roster +2", page.Concepts.Tiles[0].TermsText);
    }

    [Fact]
    public async Task The_glossary_is_read_for_the_repo_and_the_map_version_the_concepts_came_at()
    {
        var api = Serving(new RouteAnswer(HttpStatusCode.OK, TermsBody));

        _ = await ConceptPage.Shown(api);

        Expect.Sequence([new ConceptGlossaryQuery(string.Empty, "v1")], api.ContextConceptGlossaryAsked);
    }

    [Fact]
    public async Task An_empty_glossary_says_there_are_no_terms_yet_and_the_cards_stay_bare()
    {
        var page = await ConceptPage.Shown(Serving(new RouteAnswer(HttpStatusCode.OK, EmptyBody)));

        Expect.Equal((Labels.ContextConceptsNoTerms, true, false), (page.Concepts.TermsNote, page.Concepts.HasTermsNote, page.Concepts.Tiles[0].HasTerms));
    }

    [Fact]
    public async Task A_glossary_with_terms_has_no_note()
    {
        var page = await ConceptPage.Shown(Serving(new RouteAnswer(HttpStatusCode.OK, TermsBody)));

        Expect.Equal((string.Empty, false), (page.Concepts.TermsNote, page.Concepts.HasTermsNote));
    }

    [Fact]
    public async Task The_no_map_refusal_leaves_the_cards_as_the_concepts_route_does_with_no_note()
    {
        var page = await ConceptPage.Shown(Serving(new RouteAnswer(HttpStatusCode.NotFound, NoMapBody)));

        Expect.Equal((3, false, false), (page.Concepts.Tiles.Count, page.Concepts.Tiles[0].HasTerms, page.Concepts.HasTermsNote));
    }

    [Fact]
    public async Task A_200_that_says_unavailable_reads_as_no_map_too()
    {
        var page = await ConceptPage.Shown(Serving(new RouteAnswer(HttpStatusCode.OK, EmptyBody.Replace("\"empty\"", "\"unavailable\"", StringComparison.Ordinal))));

        Expect.Equal((3, false, false), (page.Concepts.Tiles.Count, page.Concepts.Tiles[0].HasTerms, page.Concepts.HasTermsNote));
    }

    [Fact]
    public async Task Any_other_refusal_is_said_with_its_code_and_message()
    {
        var page = await ConceptPage.Shown(Serving(RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-041", "the map reader is down")));

        Expect.Equal(("Terms are not available (SND-CONTEXT-041): the map reader is down", false), (page.Concepts.TermsNote, page.Concepts.Tiles[0].HasTerms));
    }

    [Fact]
    public async Task An_older_server_without_the_glossary_route_reads_as_before()
    {
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => AConcepts.Served(Concepts()) };

        var page = await ConceptPage.Shown(api);

        Expect.Equal(("Self scheduling", false, false), (page.Concepts.Tiles[0].Name, page.Concepts.Tiles[0].HasTerms, page.Concepts.HasTermsNote));
    }

    [Fact]
    public async Task A_term_that_matches_keeps_the_concept_like_a_name_or_summary_match()
    {
        var page = await ConceptPage.Shown(Serving(new RouteAnswer(HttpStatusCode.OK, TermsBody)));

        page.AnchorText = "roster";
        await page.Submit(Token);

        Expect.Sequence(["Self scheduling"], page.Concepts.Tiles.Select(t => t.Name));
    }
}
