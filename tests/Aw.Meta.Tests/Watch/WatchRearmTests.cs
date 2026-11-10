using Aw.Meta.Core.Commands;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

/// <summary>#2498: a Claude meta runs watch under Monitor, where every printed line is a model turn; a quiet expiry must not print one.</summary>
public sealed class WatchRearmTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(120);
    private readonly MetaWorld _world = new();
    private readonly List<string> _lines = [];

    private WatchService Service() => new(
        _world.Config,
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, new WatchSweeper(_world.Config, _world.Sweeper, _world.Values), Worlds.Reservation(_world), _world.Context),
        _world.Values,
        _world.Delay,
        _world.Clock,
        _world.Monitor,
        _world.Board);

    [Fact, Trait("Lane", "unit")]
    public async Task A_quiet_expiry_prints_nothing_and_rearms()
    {
        using var stop = new CancellationTokenSource();
        var polls = 0;
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                // The 10 minute window holds 5 polls; stop the test's watch only after it ran well past that.
                if (++polls == 12)
                {
                    stop.Cancel();
                }

                return new CommandResult(0, "[]", string.Empty, TimedOut: false);
            })
            .When("git ls-remote origin main", _ => new CommandResult(0, "aaaaaaa1111111\trefs/heads/main\n", string.Empty, TimedOut: false));
        await _world.Ready();
        var request = new WatchRequest(_interval, TimeSpan.FromMinutes(10), line => { _lines.Add(line); return ValueTask.CompletedTask; }) { Rearm = true };

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await Service().Handle(request, stop.Token));

        Assert.True(polls >= 12, "the watch must keep polling past its window instead of returning");
        Assert.DoesNotContain(_lines, l => l.StartsWith("WATCH EXPIRING", StringComparison.Ordinal));
    }
}
