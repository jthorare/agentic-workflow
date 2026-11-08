using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>
/// A running Sindri older than the app has no /sindri/linear routes and answers 404 to each (#2247). That is a reachable Sindri
/// that needs a restart, not one that "isn't answering", and the page says it once, not once for the list and once for the strip.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LinearOlderSindriTests
{
    private const string Older = "The running Sindri is older than this app and has no Linear yet. Restart it to use Linear: sindri stop && sindri";

    private static readonly RouteAnswer _noRoute = new(HttpStatusCode.NotFound, string.Empty);

    private readonly LinearFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void OldServe()
    {
        _f.Server.LinearIssuesAnswer = _noRoute;
        _f.Server.LinearSinceAnswer = _noRoute;
        _f.Server.LinearLookupsAnswer = _noRoute;
    }

    [Fact]
    public async Task A_404_on_the_Linear_routes_says_the_running_Sindri_is_older_and_names_the_restart()
    {
        OldServe();

        var work = await _f.Shown(Token);

        Assert.Equal((true, false, Older), (work.IsSindriOlder, work.IsSindriDown, work.OlderText));
    }

    [Fact]
    public async Task A_404_on_the_Linear_routes_gives_the_page_one_message_and_no_failed_strip()
    {
        OldServe();

        var work = await _f.Shown(Token);

        Assert.Equal((false, false), (work.StripFailed, work.ShowsStrip));
    }

    [Fact]
    public async Task A_Sindri_that_is_really_down_still_says_it_is_not_answering()
    {
        _f.Server.GoDown();

        var work = await _f.Shown(Token);

        Assert.Equal((false, true), (work.IsSindriOlder, work.IsSindriDown));
    }

    [Fact]
    public async Task After_a_restart_the_older_message_goes_when_the_routes_answer()
    {
        OldServe();
        var work = await _f.Shown(Token);
        _f.Server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "mine");
        _f.Server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "three");
        _f.Server.LinearLookupsAnswer = RouteAnswer.Api("linear-lookups", "eng");

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal((false, false, 3), (work.IsSindriOlder, work.IsSindriDown, work.Mine.Rows.Count));
    }
}
