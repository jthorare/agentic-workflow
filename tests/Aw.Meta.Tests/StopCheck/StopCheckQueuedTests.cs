using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.StopCheck;

// A peer message that arrives while the meta is mid-turn is a queued_command attachment, not an idle user record.
public sealed class StopCheckQueuedTests
{
    private readonly MetaWorld _world = new();

    private StopCheckService Service() => new(_world.Session, _world.Sweep, _world.Guard, _world.Clock);

    private async Task<StopCheckResponse> Check()
    {
        var result = await Service().Handle(new StopCheckRequest(SessionId.From(MetaWorld.MetaSessionId), MetaWorld.Transcript, HookActive: false, MaxBlocks: 3), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_message_blocks_the_stop_until_a_send_to_its_sender_closes_it()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.QueuedLine("m-2", "t1-api", "ready #7 079c2541"));

        var blocked = await Check();
        _ = await _world.Messages.RecordOutbound(A.Sent("t1-api", "ack", 30), CancellationToken.None);
        var released = await Check();

        Assert.Equal((true, ObligationKind.Reply, "m-2"), (blocked.Blocked, blocked.Open.Single(o => o.Kind == ObligationKind.Reply).Kind, blocked.Open.Single(o => o.Kind == ObligationKind.Reply).Key));
        Assert.False(released.Blocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reply_sent_in_the_same_turn_before_the_message_was_synced_still_closes_it()
    {
        await _world.Ready();
        _ = await _world.Messages.RecordOutbound(A.Sent("t1-api", "on it", 30), CancellationToken.None);   // after the message's 04:05, before any sync
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.QueuedLine("m-2", "t1-api", "ready #7 079c2541"));

        var response = await Check();

        Assert.False(response.Blocked);
        Assert.Empty(response.Open);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_from_before_the_message_does_not_close_it()
    {
        await _world.Ready();
        _ = await _world.Messages.RecordOutbound(A.Sent("t1-api", "earlier", 1), CancellationToken.None);
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.QueuedLine("m-2", "t1-api", "ready #7 079c2541"));

        Assert.True((await Check()).Blocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_message_with_only_a_delivery_id_is_keyed_by_it()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.QueuedLine(string.Empty, "t1-api", "ready", deliveryId: "d-9"));

        var response = await Check();

        Assert.Equal("d-9", response.Open.Single(o => o.Kind == ObligationKind.Reply).Key);
    }
}
