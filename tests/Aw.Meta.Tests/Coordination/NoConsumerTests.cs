using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.LaneSend;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Coordination;

/// <summary>
/// #2370: mail owed to the meta with nothing consuming it (no watch beating) is a visible state, not silence. The rearm obligation names the
/// command that works in any provider, lane-send says where stored mail waits, and status shows the consumer.
/// </summary>
public sealed class NoConsumerTests
{
    private const string NoConsumer = "no active coordinator consumer";

    private readonly MetaWorld _world = new();

    private static ObligationFacts Owed() => A.Facts() with { Messages = [A.Inbox("m1", name: "t1-api", body: "ready #190 079c2541")], WatchInterval = TimeSpan.FromMinutes(2) };

    private static Obligation? Rearm(ObligationFacts facts) => ObligationEvaluator.Evaluate(facts).FirstOrDefault(o => o.Kind == ObligationKind.RearmWatch);

    [Fact, Trait("Lane", "unit")]
    public void No_heartbeat_with_mail_owed_opens_a_rearm_that_names_the_foreground_command()
    {
        var open = Rearm(Owed() with { Heartbeat = null });

        Assert.NotNull(open);
        Assert.Contains(NoConsumer, open.Summary, StringComparison.Ordinal);
        Assert.Contains("--until-mail", open.NextCommand, StringComparison.Ordinal);
        Assert.Contains("foreground", open.NextCommand, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_heartbeat_and_nothing_owed_does_not_nag_a_fresh_team() =>
        Assert.Null(Rearm(A.Facts() with { Heartbeat = null, WatchInterval = TimeSpan.FromMinutes(2) }));

    [Fact, Trait("Lane", "unit")]
    public void A_fresh_heartbeat_with_mail_owed_is_not_a_rearm() =>
        Assert.Null(Rearm(Owed() with { Heartbeat = A.At(59) }));

    [Fact, Trait("Lane", "unit")]
    public void A_stale_heartbeat_still_rearms_and_names_the_command_for_every_provider()
    {
        var open = Rearm(A.Facts() with { Heartbeat = A.At(20), WatchInterval = TimeSpan.FromMinutes(2) });

        Assert.NotNull(open);
        Assert.Contains("--until-mail", open.NextCommand, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lane_send_to_a_lane_without_verified_hooks_says_where_the_stored_mail_waits()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        var service = new LaneSendService(_world.Config, _world.LaneFiles, _world.LaneMail, _world.Messages, _world.Beats, _world.Panes, _world.Clock);

        var result = await service.Handle(new LaneSendRequest("api", "rebase please"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.False(result.Value.Woken);
        Assert.NotNull(result.Value.Attention);
        Assert.Contains("stored", result.Value.WakeSkipped, StringComparison.Ordinal);
        Assert.Contains("aw-meta lane-inbox", result.Value.WakeSkipped, StringComparison.Ordinal);
    }

    private async Task<StatusResponse> Status()
    {
        var service = new StatusService(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Processes, _world.Clock);
        var result = await service.Handle(new StatusRequest(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_says_no_consumer_when_mail_is_owed_and_no_watch_ever_beat()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #190 079c2541");

        var status = await Status();

        Assert.NotNull(status.Consumer);
        Assert.Contains(NoConsumer, status.Consumer, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_says_active_when_the_watch_beat_within_one_interval()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #190 079c2541");
        _ = await _world.Values.Set(KvKeys.WatchHeartbeat, _world.Clock.GetUtcNow().AddSeconds(-30).ToString("O", CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);

        var status = await Status();

        Assert.NotNull(status.Consumer);
        Assert.Contains("active", status.Consumer, StringComparison.Ordinal);
        Assert.DoesNotContain(NoConsumer, status.Consumer, StringComparison.Ordinal);
    }
}
