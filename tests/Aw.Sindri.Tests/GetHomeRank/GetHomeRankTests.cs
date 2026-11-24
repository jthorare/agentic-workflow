using Aw.Sindri.Core;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetHomeRank;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.HomeRoutes;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetHomeRank;

/// <summary>The rank explain read (home spec §4.2, §9): the stored rank record and blocks list, SND-HOME-002 for an unknown or unseen row, and a session's cut.</summary>
public sealed class GetHomeRankTests
{
    private readonly SliceHarness _slice = new();
    private readonly ViewsHarness _views = new();
    private readonly InMemoryHomeView _view = HomeFixture.View();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetHomeRankService Service() => new(_view, _views.Progress, _slice.Callers);

    [Fact, Trait("Lane", "unit")]
    public async Task The_explain_holds_the_stored_rank_and_the_blocks_list()
    {
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var rank = await Service().Handle(new GetHomeRankRequest(HomeRowId.From(HomeFixture.Meta)), Token);

        Assert.True(rank.IsSuccess, rank.IsError ? rank.Error.ToString() : null);
        Assert.Equal((HomeFixture.Meta, HomeKind.LaneRecycle), (rank.Value.Id.Value, rank.Value.Kind));
        Assert.Equal(new HomeRank(3, 4, 5, 1), rank.Value.Rank);
        Expect.Sequence(["item-of", "item-of", "closes"], rank.Value.BlocksList.Select(static b => b.Via));
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 8, 55, 0, TimeSpan.Zero), rank.Value.Since);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_row_is_SND_HOME_002()
    {
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var rank = await Service().Handle(new GetHomeRankRequest(HomeRowId.From("gh:review:a/b#999")), Token);

        Assert.Equal("SND-HOME-002", rank.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_repos_row_is_SND_HOME_002_for_a_session()
    {
        using var session = _slice.Callers.Enter(A.Caller().Session().InRepo("other").Build());

        var rank = await Service().Handle(new GetHomeRankRequest(HomeRowId.From(HomeFixture.Meta)), Token);

        Assert.Equal("SND-HOME-002", rank.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_gets_a_dm_rows_rank_without_its_blocks_list()
    {
        _view.Put(HomeFixture.Row("slack:dm:kim-1", position: 9, kind: HomeKind.Dm, repo: null));
        using var session = _slice.Callers.Enter(A.Caller().Session().InRepo("demo").Build());

        var rank = (await Service().Handle(new GetHomeRankRequest(HomeRowId.From("slack:dm:kim-1")), Token)).Value;

        Assert.Equal(9, rank.Rank.Position);
        Assert.Empty(rank.BlocksList);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_without_a_caller_is_refused()
    {
        var rank = await Service().Handle(new GetHomeRankRequest(HomeRowId.From(HomeFixture.Meta)), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, rank.Error.Code);
    }
}
