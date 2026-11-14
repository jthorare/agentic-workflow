using Aw.Meta.Core.Commands;
using Aw.Meta.Features.Watch;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Watch;

/// <summary>#3603: a watch whose gh call never returns printed nothing, never beat, and a plain kill did not stop it. The poll's gh and git calls got the 15-minute limit meant for gate steps, and no cancellation token.</summary>
public sealed class WatchHangTests
{
    private static readonly TimeSpan _pollLimit = TimeSpan.FromMinutes(2);
    private readonly MetaWorld _world = new();
    private readonly List<string> _lines = [];

    private WatchService Service() => new(
        _world.Config,
        new WatchPoller(_world.Repo, _world.Resources, _world.InboxCounts, _world.Values, Worlds.FollowUps(_world), Worlds.Reservation(_world), _world.Context),
        _world.Values,
        _world.Delay,
        _world.Clock,
        new LaneReport(_world.Monitor, _world.Values),
        _world.Board);

    private WatchRequest Request() =>
        new(TimeSpan.FromSeconds(120), TimeSpan.FromMinutes(10), line => { _lines.Add(line); return ValueTask.CompletedTask; }) { UntilEvent = true };

    [Fact, Trait("Lane", "unit")]
    public async Task A_poll_gives_its_gh_and_git_calls_a_short_limit_and_reports_a_timeout_as_AW_META_012()
    {
        using var stop = new CancellationTokenSource();
        _ = _world.Runner
            .When("gh pr list", _ =>
            {
                stop.Cancel();   // one poll is enough
                return new CommandResult(-1, string.Empty, string.Empty, TimedOut: true);
            });
        await _world.Ready();

        _ = await Record.ExceptionAsync(async () => await Service().Handle(Request(), stop.Token));

        var call = Assert.Single(_world.Runner.Requests, r => string.Join(' ', r.Argv).Contains("gh pr list", StringComparison.Ordinal));
        Assert.True(call.Timeout <= _pollLimit, $"gh pr list got {call.Timeout}");
        Assert.Contains(_lines, l => l.StartsWith("WATCH ERROR: AW-META-012", StringComparison.Ordinal) && l.Contains("timed out", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_signal_ends_the_watch_while_a_gh_call_is_hung()
    {
        using var stop = new CancellationTokenSource();
        var hung = new TaskCompletionSource<CommandResult>();
        _ = _world.Runner.When("gh pr list", _ =>
        {
            stop.CancelAfter(TimeSpan.FromMilliseconds(100));   // the signal arrives while gh is stuck
            return hung.Task;
        });
        await _world.Ready();

        var handled = Service().Handle(Request(), stop.Token).AsTask();
        var first = await Task.WhenAny(handled, Task.Delay(TimeSpan.FromSeconds(10), TimeProvider.System, TestContext.Current.CancellationToken));

        Assert.Same(handled, first);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await handled);
    }
}
