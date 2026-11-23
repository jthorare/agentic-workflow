using Aw.Meta.Core;
using Aw.Meta.Core.Delegations;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.Delegate;
using Aw.Meta.Features.Delegated;
using Aw.Meta.Features.DelegateDrop;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Delegate;

// The follow-ups from the delegate review (#3905): drop, --issue refusals, a failed delivery, kept lines, sends by session id, moved issues.
public sealed class DelegateFollowUpTests
{
    private readonly DelegateWorld _d = new();

    private async Task<DelegateDropResponse> Drop(int issue)
    {
        var result = await _d.Dropping().Handle(new DelegateDropRequest(issue), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task<IReadOnlyList<DelegationView>> Listed() =>
        (await _d.Listing().Handle(new DelegatedRequest(All: true), CancellationToken.None)).Value.Delegations;

    [Fact, Trait("Lane", "unit")]
    public async Task Drop_removes_an_owed_delegation_and_its_obligation()
    {
        await _d.Ready();
        _ = await _d.Delegate(DelegateWorld.Request());

        var response = await Drop(77);

        Assert.Equal((77, true, "api"), (response.Issue, response.Dropped, response.Lane));
        Assert.Empty(await _d.Recorded());
        Assert.DoesNotContain(await _d.World.Open(), static o => o.Kind == ObligationKind.Delegate);
        Assert.Contains("delegation #77 to lane api dropped", _d.World.Log.Lines[^1], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Drop_of_an_issue_never_delegated_drops_nothing()
    {
        await _d.Ready();

        var response = await Drop(5);

        Assert.Equal((5, false), (response.Issue, response.Dropped));
        Assert.Null(response.Lane);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("t", null, false, "--title")]
    [InlineData(null, "w", false, "--what")]
    [InlineData(null, null, true, "--label")]
    public void Issue_with_title_what_or_label_is_a_usage_error_naming_the_flag(string? title, string? what, bool label, string flag)
    {
        var request = new DelegateRequest("api", title, what, Issue: 40) { Labels = label ? ["x"] : [] };

        var result = new DelegateValidator().Validate(request);

        Assert.True(result.IsError);
        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
        Assert.Contains(flag, result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"number":40,"title":"Old","state":"CLOSED","url":"https://github.com/o/r/issues/40"}""", "#40 is closed")]
    [InlineData("""{"number":40,"title":"A PR","state":"OPEN","url":"https://github.com/o/r/pull/40"}""", "#40 is a pull request")]
    public async Task A_closed_issue_or_a_pull_request_is_refused_and_nothing_is_recorded(string view, string reason)
    {
        await _d.Ready();
        _ = _d.World.Runner.When("issue view 40", view);

        var result = await _d.Service().Handle(new DelegateRequest("api", Title: null, What: null, Issue: 40), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains(reason, result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await _d.Recorded());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_delivery_that_fails_after_the_message_is_stored_reads_owed_in_the_reply_and_in_delegated()
    {
        await _d.Ready();
        _ = _d.World.PutLane("api");

        var result = await _d.Service(new FailingBeatValues(_d.World.Values)).Handle(DelegateWorld.Request(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal("owed", result.Value.Outcome);
        Assert.NotNull(result.Value.SendProblem);
        _ = Assert.Single((await _d.World.Messages.Sent(CancellationToken.None)).Value);
        Assert.Equal("owed", Assert.Single(await Listed()).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_line_in_the_ledger_is_kept_when_a_delegation_is_added()
    {
        await _d.Ready();
        _ = await _d.World.Values.Set(KvKeys.Delegations, "a line from a later aw-meta", CancellationToken.None);

        _ = await _d.Delegate(DelegateWorld.Request());

        var stored = (await _d.World.Values.Get(KvKeys.Delegations, CancellationToken.None)).Value;
        Assert.StartsWith("a line from a later aw-meta\n77\t", stored, StringComparison.Ordinal);
        _ = Assert.Single(await _d.Recorded());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_send_addressed_to_the_lane_session_id_settles_the_delegation()
    {
        var delegation = new Delegation(77, "https://x/77", "T", "api", "t1-api", A.At(1));
        var facts = A.Facts() with
        {
            Lanes = new LaneMapState(1, [new LaneAssignment("api", "t1-api", SessionId.From("s-api"), LaneSource.Direct)]),
            Outbound = [A.Sent("s-api", "ACTION: #77 T", minute: 2)],
            Delegations = [delegation],
        };

        Assert.Empty(ObligationEvaluator.Evaluate(facts));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Moving_an_issue_off_a_Claude_lane_returns_the_moved_line_to_send_it()
    {
        await _d.Ready();
        _ = await _d.Delegate(DelegateWorld.Request("api"));
        _ = _d.World.Runner.When("issue view 77", $$"""{"number":77,"title":"T","state":"OPEN","url":"{{DelegateWorld.Created}}"}""");

        var response = await _d.Delegate(new DelegateRequest("ui", Title: null, What: null, Issue: 77));

        Assert.Equal("t1-api", response.MovedFrom);
        Assert.Equal("FYI: #77 moved to lane ui; it is no longer yours.", response.MovedNotice);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Moving_an_issue_off_a_non_Claude_lane_tells_it_through_lane_send()
    {
        await _d.Ready();
        _ = _d.World.PutLane("api");
        _ = await _d.World.Beats.Record("api", stopped: false, A.At(20), CancellationToken.None);
        _ = await _d.Delegate(DelegateWorld.Request("api"));
        _ = _d.World.Runner.When("issue view 77", $$"""{"number":77,"title":"T","state":"OPEN","url":"{{DelegateWorld.Created}}"}""");

        var response = await _d.Delegate(new DelegateRequest("ui", Title: null, What: null, Issue: 77));

        Assert.Equal("t1-api", response.MovedFrom);
        Assert.Null(response.MovedNotice);
        Assert.Equal("FYI: #77 moved to lane ui; it is no longer yours.", (await _d.World.LaneMail.Owed("api", CancellationToken.None)).Value[^1].Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Moving_an_issue_off_a_non_Claude_lane_whose_hook_state_cant_be_read_says_the_notice_was_not_delivered()
    {
        await _d.Ready();
        _ = _d.World.PutLane("api");
        _ = await _d.World.Beats.Record("api", stopped: false, A.At(20), CancellationToken.None);
        _ = await _d.Delegate(DelegateWorld.Request("api"));
        _ = _d.World.Runner.When("issue view 77", $$"""{"number":77,"title":"T","state":"OPEN","url":"{{DelegateWorld.Created}}"}""");

        var result = await _d.Service(new FailingBeatValues(_d.World.Values)).Handle(new DelegateRequest("ui", Title: null, What: null, Issue: 77), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal("t1-api", result.Value.MovedFrom);
        Assert.Null(result.Value.MovedNotice);
        Assert.Contains("unreadable", result.Value.MovedProblem, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Delegating_again_to_the_same_lane_moves_nothing()
    {
        await _d.Ready();
        _ = await _d.Delegate(DelegateWorld.Request("api"));
        _ = _d.World.Runner.When("issue view 77", $$"""{"number":77,"title":"T","state":"OPEN","url":"{{DelegateWorld.Created}}"}""");

        var response = await _d.Delegate(new DelegateRequest("api", Title: null, What: null, Issue: 77));

        Assert.Null(response.MovedFrom);
        Assert.Null(response.MovedNotice);
    }
}
