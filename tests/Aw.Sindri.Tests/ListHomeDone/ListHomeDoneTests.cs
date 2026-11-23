using System.Globalization;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Features.ListHomeDone;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.HomeRoutes;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.ListHomeDone;

/// <summary>The Done list (home spec §5.4): the rows done in the last 7 days, the most recently done first; a session's repo filter and cut.</summary>
public sealed class ListHomeDoneTests
{
    private readonly SliceHarness _slice = new();
    private readonly ViewsHarness _views = new();
    private readonly InMemoryHomeView _view = HomeFixture.View();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ListHomeDoneService Service() => new(_view, _views.Progress, _slice.Callers, _slice.Clock);

    private HomeRow Done(string id, double hoursAgo, HomeKind kind = HomeKind.ReviewRequested, string? repo = "demo") =>
        HomeFixture.Row(id, position: 50, kind: kind, state: HomeState.Done, repo: repo) with { DoneAt = _slice.Clock.GetUtcNow().AddHours(-hoursAgo) };

    [Fact, Trait("Lane", "unit")]
    public async Task The_list_holds_the_rows_done_in_the_last_7_days_the_newest_first()
    {
        _view.Put(Done("gh:review:a/demo#1", hoursAgo: 30));
        _view.Put(Done("gh:review:a/demo#2", hoursAgo: 2));
        _view.Put(Done("gh:review:a/demo#3", hoursAgo: (7 * 24) + 1));
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var done = await Service().Handle(new ListHomeDoneRequest(), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.ToString() : null);
        Expect.Sequence(["gh:review:a/demo#2", "gh:review:a/demo#1"], done.Value.Rows.Select(static r => r.Id.Value));
        Assert.Equal(7, done.Value.KeepDays);
        Assert.Equal(_views.Progress.Current.AsOf, done.Value.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rows_done_at_the_same_time_come_in_id_order()
    {
        _view.Put(Done("gh:review:a/demo#b", hoursAgo: 1));
        _view.Put(Done("gh:review:a/demo#a", hoursAgo: 1));
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var done = (await Service().Handle(new ListHomeDoneRequest(), Token)).Value;

        Expect.Sequence(["gh:review:a/demo#a", "gh:review:a/demo#b"], done.Rows.Select(static r => r.Id.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task More_done_rows_than_one_view_page_are_all_read()
    {
        for (var i = 0; i < HomePaging.MaxLimit + 5; i++)
        {
            _view.Put(Done($"gh:review:a/demo#{i.ToString(CultureInfo.InvariantCulture)}", hoursAgo: 1 + (i / 100.0)));
        }

        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var done = (await Service().Handle(new ListHomeDoneRequest(), Token)).Value;

        Assert.Equal(HomePaging.MaxLimit + 5, done.Rows.Count);
        Assert.Equal("gh:review:a/demo#0", done.Rows[0].Id.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_sees_its_repos_done_rows_and_a_dm_without_its_body()
    {
        _view.Put(Done("gh:review:a/demo#1", hoursAgo: 1));
        _view.Put(Done("gh:review:a/other#1", hoursAgo: 1, repo: "other"));
        _view.Put(Done("slack:dm:kim-1", hoursAgo: 2, HomeKind.Dm, repo: null));
        using var session = _slice.Callers.Enter(A.Caller().Session().InRepo("demo").Build());

        var done = (await Service().Handle(new ListHomeDoneRequest(), Token)).Value;

        Expect.Sequence(["gh:review:a/demo#1", "slack:dm:kim-1"], done.Rows.Select(static r => r.Id.Value));
        Assert.Equal(string.Empty, done.Rows[1].Title);
        Assert.Empty(done.Rows[1].Facts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_without_a_caller_is_refused()
    {
        var done = await Service().Handle(new ListHomeDoneRequest(), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, done.Error.Code);
    }
}
