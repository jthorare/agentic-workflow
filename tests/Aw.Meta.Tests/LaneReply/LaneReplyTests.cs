using Aw.Meta.Core;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.LaneReply;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneReply;

public sealed class LaneReplyTests
{
    private readonly MetaWorld _world = new();

    private LaneReplyService Service() => new(_world.Config, _world.LaneFiles, _world.LaneMail, _world.Log, _world.Clock);

    [Fact, Trait("Lane", "unit")]
    public async Task A_reply_becomes_an_inbound_message_from_team_lane_that_opens_the_metas_reply_obligation_and_lane_send_closes_it()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "status?", A.At(5), CancellationToken.None);
        _ = await _world.LaneMail.Deliver([1], A.At(5), CancellationToken.None);

        var result = await Service().Handle(new LaneReplyRequest(lane, "all green"), CancellationToken.None);

        Assert.True(result.Value.Recorded);
        Assert.Equal("lane:api:1", result.Value.Id!.Value.Value);
        var open = Assert.Single(await _world.Open(), o => o.Kind == ObligationKind.Reply);
        Assert.Equal(ObligationKind.Reply, open.Kind);
        Assert.Contains("aw-meta lane-send --lane api", open.NextCommand, StringComparison.Ordinal);
        Assert.Empty((await _world.LaneMail.Owed("api", CancellationToken.None)).Value);
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        _ = await _world.Messages.RecordOutbound(new Outbound("t1-api", "thanks", _world.Clock.GetUtcNow()), CancellationToken.None);
        Assert.Empty(await _world.Open());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_running_log_records_team_lane_provider_and_session_id()
    {
        await _world.Ready();
        var lane = _world.PutLane("api", agent: "cursor");

        _ = await Service().Handle(new LaneReplyRequest(lane, "ok"), CancellationToken.None);

        var line = Assert.Single(_world.Log.Lines);
        foreach (var part in (string[])["t1", "api", "cursor", "sess-api"])
        {
            Assert.Contains(part, line, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_with_no_recorded_session_is_AW_META_015()
    {
        await _world.Ready();

        var result = await Service().Handle(new LaneReplyRequest(new LaneIdentity("t1", "api"), "ok"), CancellationToken.None);

        Assert.Equal(MetaErrors.LaneNotRecorded.Code, result.Error.Code);
        Assert.Empty((await _world.Messages.Messages(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_team_config_it_records_nothing_and_succeeds()
    {
        var result = await Service().Handle(new LaneReplyRequest(new LaneIdentity("t1", "api"), "ok"), CancellationToken.None);

        Assert.False(result.Value.Recorded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_blank_reply_is_not_recorded()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");

        var result = await Service().Handle(new LaneReplyRequest(lane, "  \n "), CancellationToken.None);

        Assert.False(result.Value.Recorded);
        Assert.Empty((await _world.Messages.Messages(CancellationToken.None)).Value);
    }
}
