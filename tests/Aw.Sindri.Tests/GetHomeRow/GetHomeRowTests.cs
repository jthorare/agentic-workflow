using Aw.Sindri.Core;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetHomeRow;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.HomeRoutes;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetHomeRow;

/// <summary>One Home row by id (home spec §8): the row with its blocks list, SND-HOME-002 for an unknown id, and a session's cut for a row with no repo.</summary>
public sealed class GetHomeRowTests
{
    private readonly SliceHarness _slice = new();
    private readonly ViewsHarness _views = new();
    private readonly InMemoryHomeView _view = HomeFixture.View();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetHomeRowService Service() => new(_view, _views.Progress, _slice.Callers);

    [Fact, Trait("Lane", "unit")]
    public async Task The_row_comes_back_with_its_blocks_list()
    {
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var one = await Service().Handle(new GetHomeRowRequest(HomeRowId.From(HomeFixture.Meta)), Token);

        Assert.True(one.IsSuccess, one.IsError ? one.Error.ToString() : null);
        Assert.Equal((HomeFixture.Meta, 3), (one.Value.Row.Id.Value, one.Value.Row.BlocksList.Count));
        Assert.Equal(_views.Progress.Current.AsOf, one.Value.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_id_with_a_hash_and_slashes_is_found()
    {
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var one = await Service().Handle(new GetHomeRowRequest(HomeRowId.From(HomeFixture.Review)), Token);

        Assert.Equal(HomeFixture.Review, one.Value.Row.Id.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_row_is_SND_HOME_002()
    {
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var one = await Service().Handle(new GetHomeRowRequest(HomeRowId.From("gh:review:a/b#999")), Token);

        Assert.Equal("SND-HOME-002", one.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_row_taken_out_of_the_view_is_not_found()
    {
        _view.Remove(HomeRowId.From(HomeFixture.Dm));
        using var human = _slice.Callers.Enter(A.Caller().Human().Build());

        var one = await Service().Handle(new GetHomeRowRequest(HomeRowId.From(HomeFixture.Dm)), Token);

        Assert.Equal("SND-HOME-002", one.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_gets_no_body_for_a_row_with_no_repo()
    {
        using var session = _slice.Callers.Enter(A.Caller().Session().Build());

        var one = (await Service().Handle(new GetHomeRowRequest(HomeRowId.From(HomeFixture.Dm)), Token)).Value;

        Assert.Equal((HomeFixture.Dm, string.Empty, 0), (one.Row.Id.Value, one.Row.Title, one.Row.Facts.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_a_dm_row_with_its_id_and_kind_and_no_body()
    {
        using var session = _slice.Callers.Enter(A.Caller().Session().InRepo("demo").Build());

        var one = (await Service().Handle(new GetHomeRowRequest(HomeRowId.From(HomeFixture.Dm)), Token)).Value;

        Assert.Equal((HomeFixture.Dm, HomeKind.Dm), (one.Row.Id.Value, one.Row.Kind));
        Assert.Equal(string.Empty, one.Row.Title);
        Assert.Empty(one.Row.Facts);
        Assert.Empty(one.Row.BlocksList);
        Assert.Empty(one.Row.Links);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_its_own_repos_row_whole()
    {
        using var session = _slice.Callers.Enter(A.Caller().Session().InRepo("demo").Build());

        var one = (await Service().Handle(new GetHomeRowRequest(HomeRowId.From(HomeFixture.Review)), Token)).Value;

        Assert.NotEmpty(one.Row.Title);
        Assert.NotEmpty(one.Row.BlocksList);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_repos_row_is_SND_HOME_002_for_a_session()
    {
        using var session = _slice.Callers.Enter(A.Caller().Session().InRepo("other").Build());

        var one = await Service().Handle(new GetHomeRowRequest(HomeRowId.From(HomeFixture.Review)), Token);

        Assert.Equal("SND-HOME-002", one.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_without_a_caller_is_refused()
    {
        var one = await Service().Handle(new GetHomeRowRequest(HomeRowId.From(HomeFixture.Meta)), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, one.Error.Code);
    }
}
