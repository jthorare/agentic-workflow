using Aw.Meta.Core.Lanes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneSend;

/// <summary>#2122: a lane whose hooks run but whose answer had no REPLY TO META: line is flagged, because the reply was not captured.</summary>
public sealed class UnmarkedReplyHealthTests
{
    private const string Unmarked = "answered without a REPLY TO META: line at 04:55:00Z: the reply was not captured (the stop gate asked again)";

    private readonly MetaWorld _world = new();

    private async Task<LaneHealth> Health() =>
        Assert.Single((await _world.LaneHealth.Read((await _world.Config.Load(CancellationToken.None)).Value, CancellationToken.None)).Value);

    private async Task HooksRan(string outcome, int traceMinute = 55)
    {
        await _world.Ready();
        _ = _world.PutLane("api");   // the session id was recorded at minute 10
        _ = await _world.Beats.Record("api", stopped: false, A.At(55), CancellationToken.None);
        _world.LaneFiles.PutTrace("api", new LaneHookTrace(A.At(traceMinute), "meta-lane-gate", "Stop", outcome));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reply_without_the_marker_is_flagged()
    {
        await HooksRan("reply-unmarked");

        var row = await Health();

        Assert.Equal((Unmarked, true), (row.State, row.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unmarked_trace_older_than_the_session_id_is_ignored()
    {
        await HooksRan("reply-unmarked", traceMinute: 5);

        var row = await Health();

        Assert.Equal(("taking part", false), (row.State, row.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ran_trace_is_taking_part()
    {
        await HooksRan("ran");

        var row = await Health();

        Assert.Equal(("taking part", false), (row.State, row.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Undelivered_mail_wins_over_the_unmarked_reply()
    {
        await HooksRan("reply-unmarked");
        _ = await _world.LaneMail.Send("api", "rebase please", A.At(50), CancellationToken.None);   // the clock is at minute 60

        var row = await Health();

        Assert.Equal(("mail not delivered for 10 min: the lane's last hook was Stop at 04:55:00Z; lane-send wakes an idle lane", true), (row.State, row.Attention));
    }
}
