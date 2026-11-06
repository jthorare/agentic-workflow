using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.LaneSend;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneSend;

public sealed class LaneSendTests
{
    private readonly MetaWorld _world = new();

    private LaneSendService Service() => new(_world.Config, _world.LaneFiles, _world.LaneMail, _world.Messages, _world.Beats, _world.Panes, _world.Clock);

    private async Task<LaneSendResponse> Send(string lane, string text = "rebase please")
    {
        var result = await Service().Handle(new LaneSendRequest(lane, text), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task<string> SendFails(string lane)
    {
        var result = await Service().Handle(new LaneSendRequest(lane, "x"), CancellationToken.None);
        Assert.True(result.IsError);
        return result.Error.Code;
    }

    private async Task Heartbeat(string lane, bool stopped, int minute = 20) =>
        _ = await _world.Beats.Record(lane, stopped, A.At(minute), CancellationToken.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_puts_the_message_in_the_lane_inbox_and_records_it_to_team_lane_so_the_reply_obligation_clears()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        await Heartbeat("api", stopped: false);
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "need a rebase"));
        _ = await _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None);
        _ = Assert.Single(await _world.Open());

        var response = await Send("api");

        Assert.Equal("rebase please", Assert.Single((await _world.LaneMail.Owed("api", CancellationToken.None)).Value).Text);
        var sent = Assert.Single((await _world.Messages.Sent(CancellationToken.None)).Value);
        Assert.Equal(("t1-api", "rebase please"), (sent.To, sent.Message));
        Assert.Equal("lane-send:api:1", response.Id.Value);
        Assert.Empty(await _world.Open());
        Assert.Null(response.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_that_is_not_in_the_team_is_AW_META_016()
    {
        await _world.Ready();

        Assert.Equal(MetaErrors.UnknownLane.Code, await SendFails("nope"));
        Assert.Equal(MetaErrors.UnknownLane.Code, await SendFails("meta"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_Claude_lane_is_AW_META_017_and_so_is_a_lane_with_no_record()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", SessionId.From("s"), new LaneAgent(LaneAgent.ClaudeCode, "claude", Hooks: true)));

        Assert.Equal(MetaErrors.ClaudeLane.Code, await SendFails("api"));
        Assert.Equal(MetaErrors.ClaudeLane.Code, await SendFails("ui"));
        Assert.Empty((await _world.Messages.Sent(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_a_send_is_AW_META_002() =>
        Assert.Equal(MetaErrors.NoTeamConfig.Code, await SendFails("api"));

    [Fact, Trait("Lane", "unit")]
    public async Task A_launch_only_lane_gets_the_message_stored_and_needs_attention_and_is_never_woken()
    {
        await _world.Ready();
        _ = _world.PutLane("api", agent: "aider", hooks: false);
        await Heartbeat("api", stopped: true);

        var response = await Send("api");

        Assert.Equal("launch-only: no obligations", response.Attention);
        Assert.False(response.Woken);
        Assert.Empty(_world.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_whose_hooks_never_ran_needs_attention()
    {
        await _world.Ready();
        _ = _world.PutLane("api");

        var response = await Send("api");

        Assert.StartsWith("hooks not running", response.Attention, StringComparison.Ordinal);
        Assert.False(response.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_idle_lane_with_a_live_agent_and_no_client_is_woken_once_with_one_literal_line()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        await Heartbeat("api", stopped: true);

        var first = await Send("api");
        var second = await Send("api", "again");

        Assert.True(first.Woken);
        Assert.False(second.Woken);
        var (session, line) = Assert.Single(_world.Panes.Woken);
        Assert.Equal("t1-api", session);
        Assert.Contains("aw-meta", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_wake_is_reported_keeps_the_message_and_is_retried_by_the_next_send()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        await Heartbeat("api", stopped: true);
        _world.Panes.WakeFailure = "tmux send-keys exited 1: can't find pane";

        var failed = await Send("api");
        _world.Panes.WakeFailure = null;
        var retried = await Send("api", "again");

        Assert.False(failed.Woken);
        Assert.StartsWith("the wake failed: tmux send-keys exited 1", failed.WakeSkipped, StringComparison.Ordinal);
        Assert.Equal(2, (await _world.LaneMail.Owed("api", CancellationToken.None)).Value.Count);
        Assert.True(retried.Woken);
        _ = Assert.Single(_world.Panes.Woken);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("busy", "the lane is not idle")]
    [InlineData("down", "lane down")]
    public async Task Each_guard_on_the_wake_keeps_the_keys_unsent_and_says_why(string guard, string why)
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        await Heartbeat("api", stopped: guard != "busy");
        if (guard == "down")
        {
            _world.Panes.Put("t1-api", "bash");
        }

        var response = await Send("api");

        Assert.False(response.Woken);
        Assert.Contains(why, response.WakeSkipped, StringComparison.Ordinal);
        Assert.Empty(_world.Panes.Woken);
        _ = Assert.Single((await _world.LaneMail.Owed("api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_with_no_tmux_session_is_down_and_gets_no_keys()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        _world.Panes.Remove("t1-api");
        await Heartbeat("api", stopped: true);

        var response = await Send("api");

        Assert.False(response.Woken);
        Assert.Contains("lane down", response.WakeSkipped, StringComparison.Ordinal);
    }
}
