using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3261 with #3697: the restore of the map drawn before a newer map's failed re-read never paints the old repo over another repo's failure row.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextNewerMapRepoSwitchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer StatusAt(int number) =>
        new(HttpStatusCode.OK, GoldenFiles.Api("context-map", "map-tabs-anchor").Replace("\"number\": 1", $"\"number\": {number.ToString(CultureInfo.InvariantCulture)}", StringComparison.Ordinal));

    private static bool IsOther(string? repo) => string.Equals(repo, "other-repo", StringComparison.Ordinal);

    [Fact]
    public async Task A_repo_switch_during_a_newer_map_re_read_keeps_the_new_repos_failure_row()
    {
        var api = new FakeSindriApi
        {
            ContextConceptsAnswer = query => IsOther(query.Repo) ? new RouteAnswer(HttpStatusCode.InternalServerError, "{}") : AConcepts.Served(AConcepts.Without("empty")),
            WholeMapAnswer = query => IsOther(query.Repo) ? new RouteAnswer(HttpStatusCode.InternalServerError, "{}") : ContextAnswers.WholeMap(query),
        };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var follows = new List<Func<Uri, CancellationToken, Task>>();
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, follows.Add));
        await page.Opened(Token);

        api.MapStatusAnswer = StatusAt(2);
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.ContextConceptsGate = firstGate;
        var event1 = Task.WhenAll(follows.Select(follow => follow(url, Token)));
        while (api.ContextConceptsAsked.Count < 2)
        {
            await Task.Yield();
        }

        api.ContextConceptsGate = null;
        await page.PickRepo("other-repo", Token);
        firstGate.SetResult();
        await event1;

        Expect.Equal((true, "other-repo"), (page.Whole.IsFailed, page.SelectedRepo));
    }
}
