using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Features.ListRequests;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Requests;

namespace Aw.Sindri.Tests.ListRequests;

/// <summary>The request list (API spec §3.1, U3-API-4): newest first, by state and item, a session's repo scope, the empty sentence.</summary>
[Lane(TestLane.Unit)]
public sealed class ListRequestsTests
{
    private readonly RequestQueryHarness _q = new();

    private ListRequestsService Service() => new(_q.Views.Views, new ViewItemResolver(_q.Views.Views), _q.Views.Progress, _q.Views.Slice.Callers);

    private async Task<IReadOnlyList<string>> Texts(ListRequestsRequest request) =>
        [.. (await Service().Handle(request, RequestQueryHarness.Token)).Value.Requests.Select(r => r.Text)];

    [Fact]
    public async Task ListRequests_lists_requests_newest_first_with_their_decision()
    {
        var first = await _q.File("first", RequestKind.Rescope);
        var second = await _q.File("second", RequestKind.Note, repo: "web");
        using var entered = _q.Human();

        var result = await Service().Handle(A.ListRequestsRequest().Build(), RequestQueryHarness.Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Sequence(["second", "first"], [.. result.Value.Requests.Select(r => r.Text)]);
        var newest = result.Value.Requests[0];
        Expect.Equal((RequestKind.Note, RequestState.Open, second, RepoName.From("web")), (newest.Kind, newest.State, newest.DecisionId, newest.Repo!.Value));
        Expect.Equal(first, result.Value.Requests[1].DecisionId);
        Assert.Null(result.Value.Empty);
        Expect.Equal(_q.Views.Progress.Current.AsOf, result.Value.AsOf);
    }

    [Fact]
    public async Task The_state_filter_keeps_that_state_only()
    {
        var decided = await _q.File("decided");
        _ = await _q.File("still open");
        await _q.Decide(decided, "accept");
        using var entered = _q.Human();

        Expect.Sequence(["still open"], [.. await Texts(new ListRequestsRequest(RequestState.Open, Item: null))]);
        Expect.Sequence(["decided"], [.. await Texts(new ListRequestsRequest(RequestState.Accepted, Item: null))]);
    }

    [Fact]
    public async Task The_item_filter_takes_an_id_and_keeps_that_items_requests()
    {
        var item = RequestQueryHarness.Item('1');
        await _q.CreateItem(item);
        _ = await _q.File("about it", item: item);
        _ = await _q.File("about nothing");
        using var entered = _q.Human();

        Expect.Sequence(["about it"], [.. await Texts(new ListRequestsRequest(State: null, ItemRef.To(item)))]);
    }

    [Fact]
    public async Task An_unknown_item_is_SND_ITEM_404()
    {
        using var entered = _q.Human();

        var result = await Service().Handle(new ListRequestsRequest(State: null, ItemRef.To(RequestQueryHarness.Item('9'))), RequestQueryHarness.Token);

        Expect.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_session_sees_every_repos_requests()
    {
        _ = await _q.File("mine", repo: "alpha");
        _ = await _q.File("theirs", repo: "beta");
        _ = await _q.File("nobody's");
        using var entered = _q.Session("alpha");

        Expect.Sequence(["nobody's", "theirs", "mine"], [.. await Texts(A.ListRequestsRequest().Build())]);
    }

    [Fact]
    public async Task No_match_says_why_and_a_filtered_empty_names_the_filter()
    {
        using var entered = _q.Human();

        var all = await Service().Handle(A.ListRequestsRequest().Build(), RequestQueryHarness.Token);
        var open = await Service().Handle(new ListRequestsRequest(RequestState.Open, Item: null), RequestQueryHarness.Token);

        Expect.Equal(EmptyText.Requests(state: null, item: null), all.Value.Empty);
        Expect.Equal(EmptyText.Requests(RequestState.Open, item: null), open.Value.Empty);
    }

    [Fact]
    public async Task Stalled_views_replace_the_sentence_and_keep_the_rows()
    {
        _ = await _q.File("kept");
        _q.Views.Publish(stalledAt: 1);
        using var entered = _q.Human();

        var result = await Service().Handle(A.ListRequestsRequest().Build(), RequestQueryHarness.Token);

        Expect.Equal(EmptyText.Stalled(1), result.Value.Empty);
        _ = Assert.Single(result.Value.Requests);
    }

    [Fact]
    public async Task Without_a_caller_it_is_refused()
    {
        var result = await Service().Handle(A.ListRequestsRequest().Build(), RequestQueryHarness.Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }
}
