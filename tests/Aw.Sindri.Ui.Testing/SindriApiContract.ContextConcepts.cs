using System.Globalization;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the concepts route and the map read's concept parameter answer as the others do, fake and client alike.</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task ContextConcepts_answers_each_concept_with_its_summary_size_and_the_other_concept()
    {
        var api = Reach(new FakeSindriApi { ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Three()) });

        var view = (await api.ContextConcepts(_api, new ConceptsQuery("aw-split-panes"), Token)).Value;

        var first = view.Concepts![0];   // why: an ok answer lists its concepts
        Expect.Equal(("ok", "concept:self-scheduling", "Self scheduling", "People book their own slots."), (view.State, first.Id, first.Name, first.Summary));
        Expect.Equal(new ConceptSize(4, 3, 2, 40), first.Size);
        Expect.Equal(("concept:other", 4), (view.Other!.Id, view.Other.Size.Total));   // why: the answer names Other
        Expect.Sequence([new ConceptLink("concept:self-scheduling", "concept:billing", 1)], view.Links);
    }

    [Fact]
    public async Task ContextConcepts_sends_the_repo()
    {
        var server = new FakeSindriApi { ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Three()) };
        var api = Reach(server);

        _ = await api.ContextConcepts(_api, new ConceptsQuery("aw-split-panes"), Token);

        Expect.Sequence(["aw-split-panes"], server.ContextConceptsAsked.Select(q => q.Repo));
    }

    [Fact]
    public async Task ContextConcepts_sends_the_cursor_the_limit_and_the_words()
    {
        var server = new FakeSindriApi { ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Three()) };
        var api = Reach(server);

        _ = await api.ContextConcepts(_api, new ConceptsQuery("aw-split-panes", "c2", 50, "bill"), Token);

        Expect.Equal(("c2", 50, "bill"), (server.ContextConceptsAsked[^1].Cursor, server.ContextConceptsAsked[^1].Limit, server.ContextConceptsAsked[^1].Q));
    }

    [Fact]
    public async Task ContextConcepts_from_a_server_without_the_route_is_an_error_and_not_a_crash()
    {
        var api = Reach(new FakeSindriApi());

        var result = await api.ContextConcepts(_api, new ConceptsQuery(string.Empty), Token);

        Expect.True(result.IsError);
    }

    [Fact]
    public async Task ContextConceptGlossary_answers_each_concepts_terms_and_sends_the_repo_and_map_version()
    {
        var glossary = AConcepts.Glossary("ok", new ConceptTerms("concept:billing", "Billing", ["invoice", "shift"]));
        var server = new FakeSindriApi { ContextConceptGlossaryAnswer = _ => AConcepts.Served(glossary) };
        var api = Reach(server);

        var view = (await api.ContextConceptGlossary(_api, new ConceptGlossaryQuery("aw-split-panes", "v1"), Token)).Value;

        Expect.Equal(("ok", "concept:billing"), (view.State, view.Concepts[0].Id));
        Expect.Sequence(["invoice", "shift"], view.Concepts[0].Terms);
        Expect.Equal(new ConceptGlossaryQuery("aw-split-panes", "v1"), server.ContextConceptGlossaryAsked[^1]);
    }

    [Fact]
    public async Task ContextConceptGlossary_from_a_server_without_the_route_is_an_error_and_not_a_crash()
    {
        var api = Reach(new FakeSindriApi());

        var result = await api.ContextConceptGlossary(_api, new ConceptGlossaryQuery(string.Empty), Token);

        Expect.True(result.IsError);
    }

    [Fact]
    public async Task ContextPinSuggest_answers_a_concepts_terms_with_the_records_each_would_link_and_sends_the_concept()
    {
        var server = new FakeSindriApi { ContextPinSuggestAnswer = _ => AConcepts.Served(AConcepts.Suggested(new PinSuggestTerm("payment", 2), new PinSuggestTerm("invoice", 0))) };
        var api = Reach(server);

        var view = (await api.ContextPinSuggest(_api, new PinSuggestQuery("aw-split-panes", Concept: "concept:billing"), Token)).Value;

        Expect.Sequence([new PinSuggestTerm("payment", 2), new PinSuggestTerm("invoice", 0)], view.Terms);
        Expect.Equal(("aw-split-panes", "concept:billing"), (server.ContextPinSuggestAsked[^1].Repo, server.ContextPinSuggestAsked[^1].Concept));
    }

    [Fact]
    public async Task ContextPinSuggest_sends_the_search_and_the_limit()
    {
        var server = new FakeSindriApi { ContextPinSuggestAnswer = _ => AConcepts.Served(new PinSuggestView(1, "aw-split-panes", "v1", Terms: null, [new PinSuggestFeature("feature:shifts", "Shifts")], Matches: null)) };
        var api = Reach(server);

        var view = (await api.ContextPinSuggest(_api, new PinSuggestQuery("aw-split-panes", Search: "sh", Limit: 20), Token)).Value;

        Expect.Sequence([new PinSuggestFeature("feature:shifts", "Shifts")], view.Features);
        Expect.Equal(("sh", 20), (server.ContextPinSuggestAsked[^1].Search, server.ContextPinSuggestAsked[^1].Limit));
    }

    [Fact]
    public async Task ContextPinSuggest_sends_each_definition_beside_its_line()
    {
        var server = new FakeSindriApi { ContextPinSuggestAnswer = _ => AConcepts.Served(AConcepts.Matched("concept:billing", null)) };
        var api = Reach(server);

        var view = (await api.ContextPinSuggest(_api, new PinSuggestQuery("aw-split-panes", Lines: [new PinSuggestLine("Payments", "take money"), new PinSuggestLine("Weather")]), Token)).Value;

        Expect.Equal("concept:billing", view.Matches![0].Concept);
        Expect.True(view.Matches[1].Concept is null);
        Expect.Sequence([new PinSuggestLine("Payments", "take money"), new PinSuggestLine("Weather")], server.ContextPinSuggestAsked[^1].Lines);
    }

    [Fact]
    public async Task ContextPinSuggest_sends_sixty_long_lines_as_two_calls_and_numbers_the_matches_0_to_59()
    {
        var server = new FakeSindriApi { ContextPinSuggestAnswer = q => AConcepts.Served(AConcepts.Matched([.. q.Lines!.Select(l => (string?)("concept:" + l.Name))])) };
        var api = Reach(server);
        var lines = Enumerable.Range(0, 60).Select(i => new PinSuggestLine(new string('\u00e9', 9) + i.ToString(CultureInfo.InvariantCulture), new string('\u00e9', 10))).ToList();   // about 125 encoded characters a row

        var view = (await api.ContextPinSuggest(_api, new PinSuggestQuery("aw-split-panes", Lines: lines), Token)).Value;

        Expect.Equal(2, server.ContextPinSuggestAsked.Count);
        Expect.Equal(60, server.ContextPinSuggestAsked.Sum(q => q.Lines!.Count));
        Expect.Sequence(Enumerable.Range(0, 60), view.Matches!.Select(m => m.Line));
        Expect.Sequence(lines.Select(l => "concept:" + l.Name), view.Matches!.Select(m => m.Concept));
        Expect.Sequence(lines, server.ContextPinSuggestAsked.SelectMany(q => q.Lines!));   // each definition went beside its line
    }

    [Fact]
    public async Task ContextPinSuggest_refuses_more_than_a_hundred_lines_before_sending_any()
    {
        var server = new FakeSindriApi { ContextPinSuggestAnswer = _ => AConcepts.Served(AConcepts.Matched()) };
        var api = Reach(server);
        var lines = Enumerable.Range(0, 101).Select(i => new PinSuggestLine("n" + i.ToString(CultureInfo.InvariantCulture))).ToList();

        var result = await api.ContextPinSuggest(_api, new PinSuggestQuery("aw-split-panes", Lines: lines), Token);

        Expect.True(result.IsError);
        Expect.Equal("SND-CONTEXT-179", result.Error.Code);
        Expect.Equal(0, server.ContextPinSuggestAsked.Count);
    }

    [Fact]
    public async Task ContextPinSuggest_splits_a_list_by_encoded_size_and_keeps_the_indexes_in_order()
    {
        var server = new FakeSindriApi { ContextPinSuggestAnswer = q => AConcepts.Served(AConcepts.Matched([.. q.Lines!.Select(l => (string?)"concept:x")])) };
        var api = Reach(server);
        var lines = Enumerable.Range(0, 25).Select(_ => new PinSuggestLine(new string('\u00e9', 80))).ToList();   // 480 encoded characters a row: 25 rows are over 12,000

        var view = (await api.ContextPinSuggest(_api, new PinSuggestQuery("aw-split-panes", Lines: lines), Token)).Value;

        Expect.True(server.ContextPinSuggestAsked.Count > 1);
        Expect.True(server.ContextPinSuggestAsked.All(q => q.Lines!.Sum(ContextRoutes.PinSuggestRowSize) <= 6000));
        Expect.Sequence(Enumerable.Range(0, 25), view.Matches!.Select(m => m.Line));
    }

    [Fact]
    public async Task ContextPinSuggest_from_a_server_without_the_route_is_an_error_and_not_a_crash()
    {
        var api = Reach(new FakeSindriApi());

        var result = await api.ContextPinSuggest(_api, new PinSuggestQuery(string.Empty, Concept: "concept:billing"), Token);

        Expect.True(result.IsError);
    }

    [Fact]
    public async Task ContextPins_answers_the_pins_with_their_states_and_the_file_hash_and_sends_the_repo()
    {
        var server = new FakeSindriApi { ContextPinsAnswer = _ => AConcepts.Served(AConcepts.Pinned()) };
        var api = Reach(server);

        var view = (await api.ContextPins(_api, "aw-split-panes", Token)).Value;

        Expect.Equal(new string('b', 64), view.FileHash);
        Expect.Sequence(["approved", "waiting", "removing"], view.Pins.Select(p => p.State));
        Expect.Equal(("v4", "idle"), (view.MapVersion, view.Rebuild.Phase));
        Expect.Equal("aw-split-panes", server.ContextPinsAsked[^1]);
    }

    [Fact]
    public async Task ContextPins_from_a_server_without_the_route_is_an_error_and_not_a_crash()
    {
        var api = Reach(new FakeSindriApi());

        var result = await api.ContextPins(_api, string.Empty, Token);

        Expect.True(result.IsError);
    }

    [Fact]
    public async Task WholeMap_sends_the_concept()
    {
        var server = new FakeSindriApi();
        var api = Reach(server);

        _ = await api.WholeMap(_api, new WholeMapQuery("auto", Area: null, [], MapVersion: null, Draft: false, Concept: "concept:self-scheduling"), Token);

        Expect.Equal("concept:self-scheduling", server.WholeMapAsked[^1].Concept);
    }
}
