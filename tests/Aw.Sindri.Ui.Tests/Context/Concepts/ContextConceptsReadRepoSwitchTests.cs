using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>#3792: a repo switch in the Files view starts the other repo afresh, so the next event reads that repo's concepts first and not its whole map with the old repo's flag.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptsReadRepoSwitchTests
{
    private const string Other = "other-repo";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task After_a_repo_switch_in_the_files_view_the_next_event_reads_the_new_repos_concepts()
    {
        // why: no concepts and no map yet, so the whole map is not drawn when the event comes and the page goes by its concepts-read flag (Follow.cs)
        var api = new FakeSindriApi
        {
            ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Without("empty")),
            WholeMapAnswer = _ => RouteAnswer.Problem("context-whole", "no-map-030"),
        };
        Func<Uri, CancellationToken, Task>? follow = null;
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var url = new Uri("http://127.0.0.1:1/");
        var catalog = new CatalogViewModel(clipboard, clock);
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, f => follow = f));
        await page.Opened(Token);
        page.ChooseView(files: true);
        await page.PickRepo(Other, Token);
        var asked = api.ContextConceptsAsked.Count;
        Assert.NotNull(follow);

        await follow(url, Token);

        Assert.Contains(api.ContextConceptsAsked.Skip(asked), q => string.Equals(q.Repo, Other, StringComparison.Ordinal));
    }
}
