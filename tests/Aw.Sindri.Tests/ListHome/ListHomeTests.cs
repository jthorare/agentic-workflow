using Aw.Sindri.Core;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListHome;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.HomeRoutes;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.ListHome;

/// <summary>The Home list (home spec §8): rows in rank order by group, the first page of 60, the cursor, the snoozed read, the session's cut and the 'behind' note.</summary>
public sealed class ListHomeTests
{
    private readonly SliceHarness _slice = new();
    private readonly ViewsHarness _views = new();
    private readonly InMemoryHomeView _view = HomeFixture.View();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ListHomeService Service() => new(_view, _views.Progress, _slice.Callers);

    [Fact, Trait("Lane", "unit")]
    public async Task The_page_holds_the_rows_in_rank_order_with_the_counts_of_every_bucket()
    {
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var page = await Service().Handle(new ListHomeRequest(), Token);

        Assert.True(page.IsSuccess, page.IsError ? page.Error.ToString() : null);
        Expect.Sequence([HomeFixture.Meta, HomeFixture.Review, HomeFixture.Ready3099, HomeFixture.Ideas, HomeFixture.Ready3101, HomeFixture.Dm], page.Value.NeedsYou.Select(r => r.Id.Value));
        Assert.Empty(page.Value.Moving);
        Assert.Equal(new HomeCounts(6, 0, 0, 0), page.Value.Counts);
        Assert.Null(page.Value.Cursor);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_page_is_60_rows_and_names_the_cursor_of_the_next()
    {
        for (var i = 1; i <= 70; i++)
        {
            _view.Put(HomeFixture.Row($"gh:review:a/b#{i}", position: 100 + i, i % 2 == 0 ? HomeGroup.Moving : HomeGroup.NeedsYou));
        }

        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var first = (await Service().Handle(new ListHomeRequest(), Token)).Value;
        var second = (await Service().Handle(new ListHomeRequest(After: first.Cursor), Token)).Value;

        Assert.Equal(60, first.NeedsYou.Count + first.Moving.Count);
        Assert.NotNull(first.Cursor);
        Assert.Equal(76 - 60, second.NeedsYou.Count + second.Moving.Count);
        Assert.Null(second.Cursor);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Split_keeps_one_group()
    {
        _view.Put(HomeFixture.Row("session:claude-code/ab12cd34", position: 9, HomeGroup.Moving, HomeKind.AgentAsking));
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var moving = (await Service().Handle(new ListHomeRequest(Split: HomeGroup.Moving), Token)).Value;

        Assert.Empty(moving.NeedsYou);
        Assert.Equal(["session:claude-code/ab12cd34"], moving.Moving.Select(r => r.Id.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Snoozed_reads_the_snoozed_rows_instead_of_the_open_ones()
    {
        _view.Put(HomeFixture.Row("gh:review:a/b#7", position: 7, state: HomeState.Snoozed));
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var snoozed = (await Service().Handle(new ListHomeRequest(Snoozed: true), Token)).Value;

        Assert.Equal(["gh:review:a/b#7"], snoozed.NeedsYou.Select(r => r.Id.Value));
        Assert.Equal(1, snoozed.Counts.Snoozed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_gets_no_body_for_a_row_with_no_repo()
    {
        using var session = _slice.Callers.Enter(A.Caller().Session().Build());

        var page = (await Service().Handle(new ListHomeRequest(), Token)).Value;

        var dm = page.NeedsYou.Single(r => r.Id.Value == HomeFixture.Dm);
        Assert.Equal((string.Empty, 0, 0), (dm.Title, dm.Facts.Count, dm.Links.Count));
        Assert.NotEmpty(page.NeedsYou.Single(r => r.Id.Value == HomeFixture.Review).Title);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Degraded_says_behind_when_the_views_trail_the_ledger()
    {
        _views.Progress.Publish(new ViewsStatus(5, 9, _slice.Clock.GetUtcNow(), Rebuilding: false, Stalled: false, null, null));
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var page = (await Service().Handle(new ListHomeRequest(), Token)).Value;

        var behind = Assert.Single(page.Degraded);
        Assert.Equal(("behind", "4 events"), (behind.Code, behind.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_without_a_caller_is_refused()
    {
        var page = await Service().Handle(new ListHomeRequest(), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, page.Error.Code);
    }
}
