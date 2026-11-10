using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.Status;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Coordination;

/// <summary>
/// #2370 review: the rearm hint keeps a Claude meta's watch alive (no --until-mail, which exits on its first poll while mail waits), and status
/// calls a watch active for as long as the rearm obligation stays closed (two intervals).
/// </summary>
public sealed class RearmWatchCommandTests
{
    private readonly MetaWorld _world = new();

    private static ObligationFacts Owed() => A.Facts() with { Messages = [A.Inbox("m1", name: "t1-api", body: "ready #190 079c2541")], WatchInterval = TimeSpan.FromMinutes(2) };

    [Fact, Trait("Lane", "unit")]
    public void The_claude_part_of_the_rearm_hint_is_an_until_event_watch_in_the_background_with_no_until_mail()
    {
        var open = ObligationEvaluator.Evaluate(Owed() with { Heartbeat = null }).Single(o => o.Kind == ObligationKind.RearmWatch);

        var claude = open.NextCommand.Split(';')[0];
        Assert.StartsWith("Claude: aw-meta watch --until-event", claude, StringComparison.Ordinal);
        Assert.DoesNotContain("--until-mail", claude, StringComparison.Ordinal);
        Assert.Contains("run_in_background", claude, StringComparison.Ordinal);
        Assert.Contains("Codex/Cursor: aw-meta watch --until-mail --for 29m in the foreground", open.NextCommand, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_calls_a_watch_active_at_one_and_a_half_intervals()
    {
        await _world.Ready();
        await _world.Receive("m1", "t1-api", "ready #190 079c2541");
        _ = await _world.Values.Set(KvKeys.WatchInterval, "120", TestContext.Current.CancellationToken);
        _ = await _world.Values.Set(KvKeys.WatchHeartbeat, _world.Clock.GetUtcNow().AddSeconds(-180).ToString("O", CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);

        var status = await Status();

        Assert.NotNull(status.Consumer);
        Assert.StartsWith("active", status.Consumer, StringComparison.Ordinal);
        Assert.Contains("UTC", status.Consumer, StringComparison.Ordinal);
    }

    private async Task<StatusResponse> Status()
    {
        var service = new StatusService(_world.Config, Worlds.StatusReader(_world), _world.Values, _world.Resources, Worlds.Advisor(_world), _world.Processes, _world.Clock);
        var result = await service.Handle(new StatusRequest(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }
}
