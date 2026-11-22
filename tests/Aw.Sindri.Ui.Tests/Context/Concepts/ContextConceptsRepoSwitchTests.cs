using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>#3697: the read started last wins. A repo switch while the first repo's concepts are still being read is not overwritten when the older read finishes late.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptsRepoSwitchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task An_older_concepts_read_that_finishes_last_does_not_draw_the_old_repo()
    {
        var api = new FakeSindriApi
        {
            ContextConceptsAnswer = query => AConcepts.Served(string.Equals(query.Repo, "other-repo", StringComparison.Ordinal)
                ? AConcepts.Ok([AConcepts.Concept("concept:other", "Other thing")])
                : AConcepts.Three()),
        };
        var page = await ConceptPage.Made(api);
        var firstGate = NewGate();
        api.ContextConceptsGate = firstGate;
        var first = page.Opened(Token);
        var secondGate = NewGate();
        api.ContextConceptsGate = secondGate;
        var second = page.PickRepo("other-repo", Token);

        secondGate.SetResult();
        await second;
        firstGate.SetResult();
        await first;

        Expect.Equal((true, false, "other-repo"), (page.Concepts.Find("concept:other") is not null, page.Concepts.Find("concept:billing") is not null, page.SelectedRepo));
    }

    [Fact]
    public async Task An_older_whole_map_read_that_finishes_last_does_not_draw_the_old_repo()
    {
        var api = new FakeSindriApi
        {
            ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Without("empty")),
            WholeMapAnswer = query => string.Equals(query.Repo, "other-repo", StringComparison.Ordinal)
                ? new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("context-whole", "nodes-small").Replace("\"mapVersion\": \"v1\"", "\"mapVersion\": \"v2\"", StringComparison.Ordinal))
                : ContextAnswers.WholeMap(query),
        };
        var page = await ConceptPage.Made(api);
        var firstGate = NewGate();
        api.WholeMapGate = firstGate;
        var first = page.Opened(Token);
        while (api.WholeMapAsked.Count < 1)
        {
            await Task.Yield();
        }

        var secondGate = NewGate();
        api.WholeMapGate = secondGate;
        var second = page.PickRepo("other-repo", Token);
        while (api.WholeMapAsked.Count < 2)
        {
            await Task.Yield();
        }

        secondGate.SetResult();
        await second;
        firstGate.SetResult();
        await first;

        Expect.Equal(("Context · the whole map · v2", true, false), (page.Whole.Head, page.ShowWhole, page.Whole.IsFailed));
    }
}
