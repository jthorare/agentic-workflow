using Aw.Meta.Core.Lanes;
using Aw.Meta.Features.LaneInbox;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneInbox;

public sealed class LaneInboxTests
{
    private readonly MetaWorld _world = new();

    private LaneInboxService Service() => new(_world.Config, _world.LaneMail, _world.Beats, _world.Clock);

    [Fact, Trait("Lane", "unit")]
    public async Task Unread_messages_are_shown_once_and_stay_owed()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "rebase please", A.At(5), CancellationToken.None);

        var first = await Service().Handle(new LaneInboxRequest(lane), CancellationToken.None);
        var second = await Service().Handle(new LaneInboxRequest(lane), CancellationToken.None);

        Assert.Equal("rebase please", Assert.Single(first.Value.Messages).Text);
        Assert.Empty(second.Value.Messages);
        _ = Assert.Single((await _world.LaneMail.Owed("api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_call_records_the_heartbeat_as_active_not_stopped()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.Beats.Record("api", stopped: true, A.At(5), CancellationToken.None);

        _ = await Service().Handle(new LaneInboxRequest(lane) { HookEvent = "UserPromptSubmit" }, CancellationToken.None);

        var beat = (await _world.Beats.Last("api", CancellationToken.None)).Value;
        Assert.False(beat!.Stopped);
        Assert.Equal(_world.Clock.GetUtcNow(), beat.At);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_team_config_it_answers_empty_and_records_nothing()
    {
        var result = await Service().Handle(new LaneInboxRequest(new LaneIdentity("t1", "api")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Messages);
        Assert.Null((await _world.Beats.Last("api", CancellationToken.None)).Value);
    }
}
