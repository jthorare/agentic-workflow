using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>The scheduler's wake path on the fake clock (#2438): a wall-clock jump the monotonic clock did not see asks <see cref="PollWake"/> for a catch-up.</summary>
public sealed class SchedulerHostWakeTests
{
    private readonly SliceHarness _h = new();

    [Fact, Trait("Lane", "unit")]
    public async Task A_wake_jump_makes_the_next_workflow_poll_run_a_catch_up()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), ct);
        _ = await _h.HoldLease();
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var stepped = new SteppedClock(_h.Clock);
        var clock = new JumpingClock(stepped);
        var wake = new PollWake();
        using var scheduler = new SchedulerHost(provider.GetRequiredService<IServiceScopeFactory>(), new ScheduleInputs(_h.Profiles, [], new NoPauses(), new LiveSchedule(), wake), new ServiceRunGuard(), _h.Held, _h.Callers, clock, new ListLogger<SchedulerHost>());

        await scheduler.StartAsync(ct);
        await stepped.Armed();
        Assert.True(wake.TryTake());   // a start is pending a catch-up; the first poll run takes it

        await stepped.Step(TimeSpan.FromSeconds(1));
        await stepped.Armed();
        Assert.False(wake.TryTake());   // an ordinary tick asks for nothing

        clock.Jump(TimeSpan.FromHours(2));   // the machine slept: the wall clock ran ahead, the monotonic one did not
        await stepped.Step(TimeSpan.FromSeconds(1));
        await stepped.Armed();

        Assert.True(wake.TryTake());   // AfterSleep asked PollWake: the next workflow-poll run is a catch-up
        await scheduler.StopAsync(ct);
    }

    /// <summary>The stepped clock with the wall time pushed ahead of the monotonic time, as a sleep does.</summary>
    private sealed class JumpingClock(SteppedClock inner) : TimeProvider
    {
        private TimeSpan _ahead;

        public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

        public override long TimestampFrequency => inner.TimestampFrequency;

        public void Jump(TimeSpan by) => _ahead += by;

        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + _ahead;

        public override long GetTimestamp() => inner.GetTimestamp();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => inner.CreateTimer(callback, state, dueTime, period);
    }
}
