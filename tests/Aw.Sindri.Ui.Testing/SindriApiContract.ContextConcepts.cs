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
    public async Task WholeMap_sends_the_concept()
    {
        var server = new FakeSindriApi();
        var api = Reach(server);

        _ = await api.WholeMap(_api, new WholeMapQuery("auto", Area: null, [], MapVersion: null, Draft: false, Concept: "concept:self-scheduling"), Token);

        Expect.Equal("concept:self-scheduling", server.WholeMapAsked[^1].Concept);
    }
}
