using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The concept search keeps exactly what it matches, and a question's suggestions are never counted as matches (#1699).</summary>
[Lane(TestLane.Unit)]
public sealed class ConceptSearchTests
{
    private const string Suggested = "surface:command/switch-tab";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concept_search_keeps_only_the_matching_concepts_not_the_ones_they_link_to()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));   // why: Self scheduling and Billing are linked

        page.AnchorText = "Billing";
        await page.Submit(Token);

        Expect.Sequence(["Billing"], page.Concepts.Tiles.Select(t => t.Name));
    }

    [Fact]
    public async Task Concept_search_with_no_match_shows_no_concepts_and_says_no_matches()
    {
        var located = JsonNode.Parse(GoldenFiles.Api("context-whole", "locate"))!;   // why: the golden is a JSON object
        foreach (var entry in located["located"]!.AsArray())   // why: the golden lists what it located
        {
            entry!["concept"] = new JsonArray("concept:sync");   // why: every located entry is an object
        }

        var api = ConceptPage.Serving(AConcepts.Three());
        api.QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody([], [Suggested]);
        api.WholeMapAnswer = query => query.Locate.Contains(Suggested, StringComparer.Ordinal) ? new RouteAnswer(System.Net.HttpStatusCode.OK, located.ToJsonString()) : ContextAnswers.WholeMap(query);
        var page = await ConceptPage.Shown(api);

        page.AnchorText = "zebra crossing";
        await page.Submit(Token);

        Expect.Equal((0, true, Labels.ContextSearchNoMatches), (page.Concepts.Tiles.Count, page.Concepts.NoMatches, page.SearchNote));
    }

    [Fact]
    public async Task Clearing_the_concept_search_restores_every_concept()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));
        page.AnchorText = "Billing";
        await page.Submit(Token);

        page.ClearSearch();

        Expect.Sequence(["Self scheduling", "Billing", "Sync", "Other"], page.Concepts.Tiles.Select(t => t.Name));
    }

    [Fact]
    public async Task A_newer_concept_search_wins_over_an_older_one_still_in_flight()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);
        api.QueryGate = gate;
        page.AnchorText = "Self scheduling";
        var older = page.Submit(Token);
        api.QueryGate = null;

        page.AnchorText = "Billing";
        await page.Submit(Token);
        _ = gate.TrySetResult();
        await older;

        Expect.Sequence(["Billing"], page.Concepts.Tiles.Select(t => t.Name));
    }

    [Fact]
    public async Task A_background_refresh_keeps_the_concept_search()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));
        page.AnchorText = "Billing";
        await page.Submit(Token);

        await page.Opened(Token);   // why: the shell's re-read on connect

        Expect.Equal((true, 1), (page.Concepts.SearchActive, page.Concepts.Tiles.Count));
    }

    [Fact]
    public async Task A_question_with_only_suggestions_is_not_a_match_on_the_whole_map()
    {
        var api = new FakeSindriApi { QueryAnswer = _ => FakeSindriApi.QueryAnchorsBody([], [Suggested]) };
        var page = await ConceptPage.Made(api);
        await page.Opened(Token);

        page.AnchorText = "manager schedule drag and drop shifts";
        await page.Submit(Token);

        Assert.DoesNotContain(api.ContextLog, entry => string.Equals(entry, $"lenses:{Suggested}", StringComparison.Ordinal));
        Assert.Contains(api.ContextLog, entry => string.Equals(entry, "lenses:manager schedule drag and drop shifts", StringComparison.Ordinal));
    }
}
