using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>Pages spec §3.5: a concept card says how many suggested pages a rebuild found that the owner has not opened, until they open the section.</summary>
[Lane(TestLane.Unit)]
public sealed class ConceptNewSuggestedPagesTests
{
    private const string Billing = "concept:billing";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FakeSindriApi Serving(params string[]? keys)
    {
        var api = new FakeSindriApi { WholeMapAnswer = query => query.Concept is null ? ContextAnswers.WholeMap(query) : RouteAnswer.Api("context-whole", "concept-open") };
        Rebuild(api, keys);
        return api;
    }

    private static void Rebuild(FakeSindriApi api, params string[]? keys) =>
        api.ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Ok([AConcepts.Concept(Billing, "Billing") with { SuggestedPages = keys }]));

    private static ConceptTileViewModel Card(ContextPageViewModel page) => page.Concepts.Find(Billing)!;

    [Fact]
    public async Task First_read_of_a_concept_sets_the_baseline_and_shows_no_line()
    {
        var page = await ConceptPage.Shown(Serving("p/a", "p/b"), new Preferences(new InMemoryPreferencesStore()));

        Expect.Equal((false, string.Empty), (Card(page).HasNewSuggested, Card(page).NewSuggestedText));
    }

    [Fact]
    public async Task A_rebuild_with_new_suggested_pages_shows_how_many()
    {
        var api = Serving("p/a");
        var page = await ConceptPage.Shown(api, new Preferences(new InMemoryPreferencesStore()));

        Rebuild(api, "p/a", "p/b", "p/c");
        await page.ReadMapTop(Token);
        Expect.Equal("2 new suggested pages", Card(page).NewSuggestedText);

        Rebuild(api, "p/a", "p/b");
        await page.ReadMapTop(Token);
        Expect.Equal("1 new suggested page", Card(page).NewSuggestedText);
    }

    [Fact]
    public async Task Opening_the_pages_section_clears_the_line()
    {
        var api = Serving("p/a");
        var prefs = new Preferences(new InMemoryPreferencesStore());
        var page = await ConceptPage.Shown(api, prefs);
        Rebuild(api, "p/a", "p/b");
        await page.ReadMapTop(Token);

        await page.OpenConcept(Billing, Token);
        page.CloseConcept();
        Expect.True(!Card(page).HasNewSuggested);

        await page.ReadMapTop(Token);   // the line stays gone on the next read
        Expect.True(!Card(page).HasNewSuggested);
    }

    [Fact]
    public async Task A_later_rebuild_with_more_pages_starts_the_count_again()
    {
        var api = Serving("p/a");
        var page = await ConceptPage.Shown(api, new Preferences(new InMemoryPreferencesStore()));
        Rebuild(api, "p/a", "p/b");
        await page.ReadMapTop(Token);
        await page.OpenConcept(Billing, Token);
        page.CloseConcept();

        Rebuild(api, "p/a", "p/b", "p/c");
        await page.ReadMapTop(Token);

        Expect.Equal("1 new suggested page", Card(page).NewSuggestedText);
    }

    [Fact]
    public async Task Keys_that_drop_out_are_pruned_from_the_store()
    {
        var api = Serving("p/a", "p/b");
        var prefs = new Preferences(new InMemoryPreferencesStore());
        var page = await ConceptPage.Shown(api, prefs);

        Rebuild(api, "p/a", "p/c");
        await page.ReadMapTop(Token);

        var stored = (await prefs.Load(Token)).SeenPages.Values.Single();
        Expect.Sequence(["p/a"], stored);   // p/b left the list; p/c is new and stays unseen until the section is opened
    }

    [Fact]
    public async Task A_server_without_suggestedPages_shows_no_line()
    {
        var api = Serving(keys: null);
        var page = await ConceptPage.Shown(api, new Preferences(new InMemoryPreferencesStore()));

        await page.ReadMapTop(Token);

        Expect.True(!Card(page).HasNewSuggested);
    }
}
