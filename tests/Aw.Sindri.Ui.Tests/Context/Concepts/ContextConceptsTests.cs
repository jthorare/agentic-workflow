using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The concept band: the context map opens on the product's concepts, and a concept opens today's bands scoped to it.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concepts_show_name_summary_and_size()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));

        var first = page.Concepts.Tiles[0];
        Expect.Equal(("Self scheduling", "People book their own slots.", "4 features · 3 records · 2 decisions · 40 code files"), (first.Name, first.Summary, first.SizeText));
        Expect.Sequence(["Self scheduling", "Billing", "Sync", "Other"], page.Concepts.Tiles.Select(t => t.Name));
    }

    [Fact]
    public async Task The_page_opens_on_the_concepts_and_reads_no_map_until_one_is_opened()
    {
        var api = ConceptPage.Serving(AConcepts.Three());

        var page = await ConceptPage.Shown(api);

        Expect.Equal((true, false, Labels.ContextConceptsHead), (page.ShowConcepts, page.ShowWhole, page.Head));
        Expect.Equal(0, api.WholeMapAsked.Count);
    }

    [Fact]
    public async Task While_the_concepts_are_read_the_loading_line_shows()
    {
        var gate = new TaskCompletionSource();
        var api = new FakeSindriApi { ContextConceptsGate = gate, ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Three()) };
        var page = await ConceptPage.Made(api);

        var reading = page.Opened(Token);

        Expect.Equal((true, true), (page.Concepts.IsLoading, page.ShowConcepts));
        _ = gate.TrySetResult();
        await reading;
        Expect.Equal((false, 4), (page.Concepts.IsLoading, page.Concepts.Tiles.Count));
    }

    [Fact]
    public async Task Opening_a_concept_scopes_the_bands_to_it()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);

        await page.OpenConcept("concept:billing", Token);

        Expect.Equal("concept:billing", api.WholeMapAsked[^1].Concept);
        Expect.Equal(("concept:billing", false, true), (page.OpenedConceptId, page.ShowConcepts, page.ShowWhole));
    }

    [Fact]
    public async Task Opening_a_concept_keeps_the_scope_for_the_feature_reads_below_it()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);
        await page.OpenConcept("concept:billing", Token);

        await page.OpenWholeArea(ContextAnswers.OpenArea, Token);

        Expect.Equal(("concept:billing", ContextAnswers.OpenArea), (api.WholeMapAsked[^1].Concept, api.WholeMapAsked[^1].Area));
    }

    [Fact]
    public async Task Other_holds_the_unassigned_nodes()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);
        var other = page.Concepts.Tiles[^1];

        await page.OpenConcept(other.Id, Token);

        Expect.Equal(("concept:other", "1 feature · 3 code files"), (api.WholeMapAsked[^1].Concept, other.SizeText));
    }

    [Fact]
    public async Task Other_is_left_out_when_nothing_is_unassigned()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Ok([AConcepts.Concept("concept:billing", "Billing")], AConcepts.Other())));

        Expect.Sequence(["Billing"], page.Concepts.Tiles.Select(t => t.Name));
    }

    [Fact]
    public async Task A_concept_a_person_set_says_so()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Ok([AConcepts.Concept("concept:billing", "Billing", source: "override")])));

        Expect.True(page.Concepts.Tiles[0].IsSetByPerson);
    }

    [Fact]
    public async Task The_breadcrumb_runs_from_concepts_through_the_concept_to_the_feature_and_the_node()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));
        await page.OpenConcept("concept:self-scheduling", Token);
        Expect.Sequence([Labels.ContextConceptsCrumb, "Self scheduling"], page.Whole.Crumbs.Select(c => c.Text));

        await page.OpenWholeArea(ContextAnswers.OpenArea, Token);
        var area = page.Whole.AreaTitle;
        await page.Whole.Panel.Open(page.Whole.Answer!.Nodes![0].Key, Token);   // why: the opened feature holds nodes

        Expect.Sequence([Labels.ContextConceptsCrumb, "Self scheduling", area, page.Whole.Panel.Title], page.Whole.Crumbs.Select(c => c.Text));
        Expect.Sequence([true, true, true, false], page.Whole.Crumbs.Select(c => c.IsLink));
    }

    [Fact]
    public async Task Up_goes_one_layer_at_a_time_from_the_feature_to_the_concept_to_the_concepts()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));
        await page.OpenConcept("concept:self-scheduling", Token);
        await page.OpenWholeArea(ContextAnswers.OpenArea, Token);

        Expect.True(page.Up());
        Expect.Equal((false, "concept:self-scheduling", false), (page.Whole.IsAreaOpen, page.OpenedConceptId, page.ShowConcepts));
        Expect.True(page.Up());
        Expect.Equal((null, true, false, 4), (page.OpenedConceptId, page.ShowConcepts, page.ShowWhole, page.Concepts.Tiles.Count));
        Expect.True(!page.Up());
    }

    [Fact]
    public async Task Going_back_to_the_concepts_and_into_another_does_not_show_the_first_ones_map()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);
        await page.OpenConcept("concept:billing", Token);
        _ = page.Up();
        var gate = new TaskCompletionSource();
        api.WholeMapGate = gate;

        var reading = page.OpenConcept("concept:sync", Token);

        Assert.False(page.Whole.IsReady);
        _ = gate.TrySetResult();
        await reading;
        Expect.Equal(("concept:sync", true), (api.WholeMapAsked[^1].Concept, page.Whole.IsReady));
    }

    [Fact]
    public async Task A_search_at_the_concept_layer_keeps_the_concepts_that_match_and_their_links()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));

        page.AnchorText = "scheduling";
        await page.Submit(Token);

        Expect.Sequence(["Self scheduling", "Billing"], page.Concepts.Tiles.Select(t => t.Name));   // why: Billing is linked to the match
        Expect.Equal((true, false), (page.Concepts.SearchActive, page.Concepts.NoMatches));
    }

    [Fact]
    public async Task A_search_matches_a_concept_by_its_summary_too()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));

        page.AnchorText = "calendars";
        await page.Submit(Token);

        Expect.Sequence(["Sync"], page.Concepts.Tiles.Select(t => t.Name));
    }

    [Fact]
    public async Task A_search_keeps_the_concept_that_holds_a_matching_node()
    {
        var located = JsonNode.Parse(GoldenFiles.Api("context-whole", "locate"))!;   // why: the golden is a JSON object
        foreach (var entry in located["located"]!.AsArray())   // why: the golden lists what it located
        {
            entry!["concept"] = new JsonArray("concept:sync");   // why: every located entry is an object
        }

        var api = ConceptPage.Serving(AConcepts.Three());
        api.WholeMapAnswer = query => query.Locate.Count > 0 ? new RouteAnswer(System.Net.HttpStatusCode.OK, located.ToJsonString()) : ContextAnswers.WholeMap(query);
        var page = await ConceptPage.Shown(api);

        page.AnchorText = "surface:command/switch-tab";
        await page.Submit(Token);

        Expect.Sequence(["Sync"], page.Concepts.Tiles.Select(t => t.Name));
        Expect.Equal(0, api.ContextCalls);
    }

    [Fact]
    public async Task A_search_that_matches_nothing_says_so_and_clearing_it_brings_the_concepts_back()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));

        page.AnchorText = "zebra";
        await page.Submit(Token);
        Expect.Equal((true, 0, Labels.ContextSearchNoMatches), (page.Concepts.NoMatches, page.Concepts.Tiles.Count, page.SearchNote));

        page.ClearSearch();
        Expect.Equal((false, 4, string.Empty), (page.Concepts.SearchActive, page.Concepts.Tiles.Count, page.SearchNote));
    }

    [Fact]
    public async Task A_search_inside_a_concept_is_the_maps_own_search_scoped_to_the_concept()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);
        await page.OpenConcept("concept:billing", Token);

        page.AnchorText = "surface:command/switch-tab";
        await page.Submit(Token);

        Assert.False(page.Concepts.SearchActive);
        Expect.True(api.WholeMapAsked.All(q => q.Concept == "concept:billing"));
    }

    [Fact]
    public async Task With_no_concepts_yet_a_line_says_so_and_the_bands_open_unscoped()
    {
        var api = ConceptPage.Serving(AConcepts.Without("empty"));

        var page = await ConceptPage.Shown(api);

        Expect.Equal((Labels.ContextConceptsEmpty, false, true), (page.Concepts.Note, page.ShowConcepts, page.ShowWhole));
        Expect.Equal((null, "auto"), (api.WholeMapAsked[0].Concept, api.WholeMapAsked[0].Detail));
    }

    [Fact]
    public async Task While_the_concepts_are_building_a_line_says_so_and_the_bands_open_unscoped()
    {
        var api = ConceptPage.Serving(AConcepts.Without("building"));

        var page = await ConceptPage.Shown(api);

        Expect.Equal((Labels.ContextConceptsBuilding, false, true), (page.Concepts.Note, page.ShowConcepts, page.ShowWhole));
        Assert.Null(api.WholeMapAsked[0].Concept);
    }

    [Fact]
    public async Task When_the_concepts_are_unavailable_a_line_gives_the_code_and_the_bands_open_unscoped()
    {
        var api = ConceptPage.Serving(AConcepts.Without("unavailable", "SND-CONTEXT-041"));

        var page = await ConceptPage.Shown(api);

        Expect.Equal((Labels.ContextConceptsUnavailable("SND-CONTEXT-041"), false, true), (page.Concepts.Note, page.ShowConcepts, page.ShowWhole));
        Assert.Null(api.WholeMapAsked[0].Concept);
    }

    [Fact]
    public async Task A_server_without_the_endpoint_acts_like_no_concepts_and_today_s_map_still_works()
    {
        var api = new FakeSindriApi();

        var page = await ConceptPage.Shown(api);

        Expect.Equal((string.Empty, false, true, true), (page.Concepts.Note, page.ShowConcepts, page.ShowWhole, page.Whole.IsReady));
        Expect.Equal(("Context · the whole map · v1", null), (page.Head, api.WholeMapAsked[0].Concept));
    }

    [Fact]
    public async Task Concepts_are_asked_once_for_a_page_shown_twice()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);

        await page.Opened(Token);

        Expect.Equal(1, api.ContextConceptsAsked.Count);
    }

    [Fact]
    public async Task Every_page_of_the_concepts_is_read_and_joined()
    {
        var first = AConcepts.Ok([AConcepts.Concept("concept:billing", "Billing")], next: "c2");
        var second = AConcepts.Ok([AConcepts.Concept("concept:sync", "Sync")]);
        var api = new FakeSindriApi { ContextConceptsAnswer = query => AConcepts.Served(query.Cursor is null ? first : second) };

        var page = await ConceptPage.Shown(api);

        Expect.Sequence(["Billing", "Sync"], page.Concepts.Tiles.Select(t => t.Name));
        Expect.Sequence([null, "c2"], api.ContextConceptsAsked.Select(q => q.Cursor));
    }

    [Fact]
    public async Task A_concept_whose_slug_has_a_dot_opens_with_its_id_escaped_in_the_map_read()
    {
        var api = ConceptPage.Serving(AConcepts.Ok([AConcepts.Concept("concept:tabs.tab-manager", "Tab manager")]));
        var page = await ConceptPage.Shown(api);

        await page.OpenConcept(page.Concepts.Tiles[0].Id, Token);

        Expect.Equal("Tab manager", page.Concepts.Tiles[0].Name);
        Expect.True(ContextRoutes.WholeMap(api.WholeMapAsked[^1]).Contains("concept=concept%3Atabs.tab-manager", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_concept_the_map_does_not_hold_shows_the_failure_row_with_retry_and_back()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);

        await page.OpenConcept(ContextAnswers.UnknownConcept, Token);

        Expect.Equal((true, true, true), (page.Whole.IsFailed, page.ShowWholeStatus, page.Whole.HasConcept));
        Expect.True(page.Up());
        Expect.Equal((true, null), (page.ShowConcepts, page.OpenedConceptId));
    }

    [Fact]
    public async Task Choosing_another_repo_starts_again_at_the_concepts()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var page = await ConceptPage.Shown(api);
        await page.OpenConcept("concept:billing", Token);

        await page.PickRepo("other-repo", Token);

        Expect.Equal((null, true, "other-repo"), (page.OpenedConceptId, page.ShowConcepts, api.ContextConceptsAsked[^1].Repo));
    }
}
