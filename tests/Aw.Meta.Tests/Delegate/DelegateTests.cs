using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Delegations;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.Delegate;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Delegate;

public sealed class DelegateTests
{
    private readonly DelegateWorld _d = new();

    [Fact, Trait("Lane", "unit")]
    public async Task A_Claude_lane_gets_a_new_issue_and_the_delegation_is_owed_with_the_text_to_send()
    {
        await _d.Ready();

        var response = await _d.Delegate(DelegateWorld.Request() with { Labels = ["area:meta", "type:feat"] });

        var create = Assert.Single(_d.GhLines("create"));
        Assert.Contains("--title Owner wants a dark theme toggle", create, StringComparison.Ordinal);
        Assert.Contains("Add a toggle for the dark theme in Settings.\n\nDelegated to lane api by meta.", create, StringComparison.Ordinal);
        Assert.Contains("--label area:meta --label type:feat", create, StringComparison.Ordinal);
        Assert.All(_d.World.Runner.Requests, r => Assert.Equal("/work", r.Cwd));
        Assert.Equal("owed", response.Outcome);
        var view = Assert.IsType<DelegationView>(response.Delegation);
        Assert.Equal((77, "api", "t1-api", "owed"), (view.Issue, view.Lane, view.Session, view.State));
        Assert.Equal($"ACTION: #77 Owner wants a dark theme toggle\n{DelegateWorld.Created}\nYours: take it in your lane and reply with the PR number.", view.Message);
        var recorded = Assert.Single(await _d.Recorded());
        Assert.Equal((77, "api", false, true), (recorded.Issue, recorded.Lane, recorded.Delivered, recorded.Claude));
        Assert.Equal($"- NOTE 2026-10-15 05:00: delegated #77 to lane api (owed): Owner wants a dark theme toggle {DelegateWorld.Created}", Assert.Single(_d.World.Log.Lines));
        Assert.Empty((await _d.World.Messages.Sent(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_existing_issue_is_read_with_gh_issue_view_and_nothing_is_filed()
    {
        await _d.Ready();
        _ = _d.World.Runner.When("issue view 40", """{"number":40,"title":"Fix the board line","state":"OPEN","url":"https://github.com/jthorare/agentic-workflow/issues/40"}""");

        var response = await _d.Delegate(new DelegateRequest("ui", Title: null, What: null, Issue: 40));

        Assert.Empty(_d.GhLines("create"));
        Assert.Contains("issue view 40 --json number,title,state,url", Assert.Single(_d.GhLines("view")), StringComparison.Ordinal);
        var view = Assert.IsType<DelegationView>(response.Delegation);
        Assert.Equal((40, "Fix the board line", "t1-ui"), (view.Issue, view.Title, view.Session));
        Assert.StartsWith("ACTION: #40 Fix the board line\nhttps://github.com/jthorare/agentic-workflow/issues/40\n", view.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_non_Claude_lane_gets_the_message_through_lane_send_and_the_delegation_is_sent()
    {
        await _d.Ready();
        _ = _d.World.PutLane("api");
        _ = await _d.World.Beats.Record("api", stopped: false, A.At(20), CancellationToken.None);   // its hooks run, so it can act on the message

        var response = await _d.Delegate(DelegateWorld.Request());

        Assert.Equal("sent", response.Outcome);
        Assert.Null(response.SendProblem);
        var mail = Assert.Single((await _d.World.LaneMail.Owed("api", CancellationToken.None)).Value);
        Assert.StartsWith("ACTION: #77 Owner wants a dark theme toggle\n", mail.Text, StringComparison.Ordinal);
        var sent = Assert.Single((await _d.World.Messages.Sent(CancellationToken.None)).Value);
        Assert.Equal("t1-api", sent.To);
        Assert.True(Assert.Single(await _d.Recorded()).Delivered);
        Assert.DoesNotContain(await _d.World.Open(), o => o.Kind == ObligationKind.Delegate);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launch_only_lane_gets_the_message_stored_but_the_delegation_stays_owed_with_the_reason()
    {
        await _d.Ready();
        _ = _d.World.PutLane("api", hooks: false);

        var response = await _d.Delegate(DelegateWorld.Request());

        Assert.Equal("owed", response.Outcome);
        Assert.Contains("launch-only", response.SendProblem, StringComparison.Ordinal);
        Assert.Equal("owed", Assert.IsType<DelegationView>(response.Delegation).State);
        _ = Assert.Single((await _d.World.LaneMail.Owed("api", CancellationToken.None)).Value);
        Assert.False(Assert.Single(await _d.Recorded()).Delivered);
        Assert.Contains("(owed)", Assert.Single(_d.World.Log.Lines), StringComparison.Ordinal);
        var owed = Assert.Single(await _d.World.Open(), static o => o.Kind == ObligationKind.Delegate);
        Assert.StartsWith("aw-meta lane-send --lane api --text 'ACTION: #77 ", owed.NextCommand, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("meta")]
    [InlineData("META")]
    [InlineData("nope")]
    public async Task The_meta_lane_and_an_unknown_lane_are_AW_META_016_and_nothing_is_filed(string lane)
    {
        await _d.Ready();

        Assert.Equal(MetaErrors.UnknownLane.Code, await _d.Refused(DelegateWorld.Request(lane)));

        Assert.Empty(_d.World.Runner.Lines);
        Assert.Empty(await _d.Recorded());
        Assert.Empty(_d.World.Log.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_lane_files_nothing_and_lists_the_work_lanes_marking_whole_word_keyword_hits()
    {
        var keywords = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["ui"] = ["theme", "settings"],
            ["api"] = ["route", "dark"],
            ["meta"] = ["theme"],
        };
        await _d.Ready(new DelegationConfig(keywords));

        var response = await _d.Delegate(new DelegateRequest(Lane: null, "Theme toggle", "Put it in SETTINGS, near the darkness slider."));

        Assert.Equal("no-lane", response.Outcome);
        Assert.Null(response.Delegation);
        Assert.Equal([("api", ""), ("ui", "theme,settings")], response.Lanes.Select(static l => (l.Lane, string.Join(',', l.Hits))));
        Assert.Empty(_d.World.Runner.Lines);
        Assert.Empty(await _d.Recorded());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_gh_call_is_AW_META_012_and_records_nothing()
    {
        await _d.World.Ready();
        _ = _d.World.Runner.When("issue create", "", exitCode: 1);

        Assert.Equal(MetaErrors.CommandFailed.Code, await _d.Refused(DelegateWorld.Request()));

        Assert.Empty(await _d.Recorded());
        Assert.Empty(_d.World.Log.Lines);
        Assert.Empty(await _d.World.Open());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_log_failure_after_the_issue_is_filed_says_to_rerun_with_the_issue_number()
    {
        await _d.Ready();
        _d.World.Log.FailAppends = true;

        var result = await _d.Service().Handle(DelegateWorld.Request(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("issue #77 is filed already: rerun with aw-meta delegate --lane api --issue 77", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_lane_send_hint_quotes_the_text_as_one_literal_shell_word()
    {
        var delegation = new Delegation(77, "https://x/77", "it's $(whoami) `id` \"y\"", "api", "t1-api", A.At(1)) { Claude = false };

        var owed = Assert.Single(ObligationEvaluator.Evaluate(A.Facts() with { Delegations = [delegation] }));

        Assert.Equal(
            "aw-meta lane-send --lane api --text 'ACTION: #77 it'\\''s $(whoami) `id` \"y\"\nhttps://x/77\nYours: take it in your lane and reply with the PR number.'",
            owed.NextCommand);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Delegating_the_same_issue_again_replaces_the_earlier_record()
    {
        await _d.Ready();
        _ = await _d.Delegate(DelegateWorld.Request("api"));
        _ = _d.World.Runner.When("issue view 77", $$"""{"number":77,"title":"Owner wants a dark theme toggle","state":"OPEN","url":"{{DelegateWorld.Created}}"}""");

        _ = await _d.Delegate(new DelegateRequest("ui", Title: null, What: null, Issue: 77));

        Assert.Equal("ui", Assert.Single(await _d.Recorded()).Lane);
    }
}
