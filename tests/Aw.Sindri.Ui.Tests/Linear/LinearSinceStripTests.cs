using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The since-you-looked strip (Linear hub spec §3, REQ-9): a band above the filter bar whose Dismiss is the only thing that moves the marker.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearSinceStripTests
{
    private const string Marker = "lin_000000000000003f";

    private readonly LinearFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    // Sindri finds assigned and state changes by comparing what each sync stored, so it can't say who made them (actor null);
    // only a mention names its author.
    public async Task The_strip_names_its_region_by_the_count_and_says_each_change_in_words()
    {
        var work = await _f.Shown(Token);

        Assert.Equal((true, "Since you last looked, 3 items"), (work.ShowsStrip, work.StripName));
        Assert.Equal(
            ["ENG-31 assigned to you", "Sam Lee mentioned you on ENG-12", "ENG-12 moved from Todo to In Progress"],
            work.StripRows.Select(r => r.Text),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_strip_is_hidden_when_nothing_is_new()
    {
        _f.Server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "empty");

        var work = await _f.Shown(Token);

        Assert.Equal((false, 0), (work.ShowsStrip, work.StripRows.Count));
    }

    [Fact]
    public async Task The_strip_shows_only_on_the_Linear_view()
    {
        var work = await _f.Shown(Token);

        work.Choose(linear: false);

        Assert.False(work.ShowsStrip);
    }

    [Fact]
    public async Task A_change_asks_for_its_issue_by_key()
    {
        var work = await _f.Shown(Token);
        var asked = new List<string>();
        work.IssueRequested += (_, e) => asked.Add(e.Key);

        work.RequestIssue(work.StripRows[1].Key);

        Assert.Equal(["ENG-12"], asked);
    }

    [Fact]
    public async Task The_count_is_read_on_every_refresh_even_while_Work_is_not_the_page_shown()
    {
        var work = _f.Work();

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal((3, 0), (work.NewCount, _f.Server.LinearQueries.Count));
    }

    [Fact]
    public async Task Dismiss_stores_the_marker_and_hides_the_strip()
    {
        var work = await _f.Shown(Token);

        await work.Dismiss();

        Assert.Equal((Marker, false, 0), ((await _f.Prefs.Load(Token)).LinearSince, work.ShowsStrip, work.NewCount));
    }

    [Fact]
    public async Task Leaving_the_view_and_reloading_never_move_the_marker()
    {
        var work = await _f.Shown(Token);

        work.Choose(linear: false);
        work.SetShown(shown: false);
        work.SetShown(shown: true);
        await work.Refresh(LinearFixture.Url, Token);
        work.Choose(linear: true);
        await work.Reading;
        await _f.Prefs.Flush();

        Assert.Null((await _f.Prefs.Load(Token)).LinearSince);
        Assert.All(_f.Server.LinearSinceAfters, Assert.Null);
        Assert.Equal(3, work.NewCount);
    }

    [Fact]
    public async Task After_Dismiss_the_next_read_asks_after_the_stored_marker_and_a_restart_does_too()
    {
        var work = await _f.Shown(Token);
        await work.Dismiss();
        _f.Server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "empty");

        await work.Refresh(LinearFixture.Url, Token);
        var next = _f.Work();
        await next.Restore(Token);
        next.SetShown(shown: true);
        await next.Refresh(LinearFixture.Url, Token);

        Assert.Equal([null, Marker, Marker], _f.Server.LinearSinceAfters);
    }

    [Fact]
    public async Task A_read_error_says_Couldnt_load_what_changed_and_keeps_the_list_below()
    {
        _f.Server.LinearSinceAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-LINEAR-001", "The store can't be read.");

        var work = await _f.Shown(Token);

        Assert.Equal((true, true, 3), (work.ShowsStrip, work.StripFailed, work.Mine.Rows.Count));
        Assert.Equal(("Couldn't load what changed", "Retry"), (ViewModels.Labels.LinearStripFailed, ViewModels.Labels.LinearStripRetry));
    }

    [Fact]
    public async Task Retry_reads_the_changes_again()
    {
        _f.Server.LinearSinceAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-LINEAR-001", "The store can't be read.");
        var work = await _f.Shown(Token);
        _f.Server.LinearSinceAnswer = RouteAnswer.Api("linear-since", "three");

        await work.RetryStrip();

        Assert.Equal((false, 3), (work.StripFailed, work.StripRows.Count));
    }
}
