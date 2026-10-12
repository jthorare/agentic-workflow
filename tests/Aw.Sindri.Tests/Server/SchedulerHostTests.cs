using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// The scheduler on the fake clock. Each step waits for the loop to arm its next wait, then advances one second (its
/// longest wait), so nothing depends on real-time sleeps.
/// </summary>
public sealed class SchedulerHostTests
{
    private readonly SliceHarness _h = new();
    private readonly ListLogger<SchedulerHost> _log = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task The_scheduler_runs_the_heartbeat_on_the_approved_cadence()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _ = await _h.HoldLease();
        var approvals = new CountingApprovals(_h.Profiles);

        await Run(approvals, () => Heartbeats() >= 3);

        var slots = _h.Store.Events.Where(e => e.Envelope.Type == HeartbeatType)
            .Select(e => JsonDocument.Parse(e.Envelope.Payload).RootElement.GetProperty("slot").GetDateTimeOffset())
            .Take(3).ToList();
        var start = slots[0];
        Assert.Equal([start, start.AddSeconds(5), start.AddSeconds(10)], slots);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_runs_without_the_lease()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);   // approved, but no lease held
        var approvals = new CountingApprovals(_h.Profiles);

        await Run(approvals, () => approvals.Reads >= 12);   // twelve loops: past two 5 s slots

        Assert.Equal(0, Heartbeats());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_service_that_throws_is_logged_and_later_slots_still_run()
    {
        _ = await _h.Profiles.Save(SliceHarness.ProfileOf("thor", ("boom", "5s"), ("heartbeat", "5s")), Token);
        _ = await _h.HoldLease();

        await Run(new CountingApprovals(_h.Profiles), () => Heartbeats() >= 3);

        Assert.True(_log.Lines.Where(l => l.Contains("SND-SCHED-001 service 'boom' failed", StringComparison.Ordinal)).Skip(2).Any());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_service_that_times_out_on_its_own_is_logged_and_the_schedule_goes_on()
    {
        _ = await _h.Profiles.Save(SliceHarness.ProfileOf("thor", ("heartbeat", "5s"), ("slow", "5s")), Token);
        _ = await _h.HoldLease();

        await Run(new CountingApprovals(_h.Profiles), () => Heartbeats() >= 3);

        Assert.True(_log.Lines.Where(l => l.Contains("service 'slow' failed: SND-SCHED-001 ", StringComparison.Ordinal)).Skip(2).Any());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approval_read_that_times_out_is_logged_once_and_the_schedule_goes_on()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _ = await _h.HoldLease();
        var approvals = new CountingApprovals(_h.Profiles, throwFirst: 3, () => new TaskCanceledException("read timed out"));

        await Run(approvals, () => Heartbeats() >= 1);

        _ = Assert.Single(_log.Lines, l => l.Contains("SND-SCHED-001 the approved profile couldn't be read", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_service_this_build_lacks_is_logged_and_skipped()
    {
        _ = await _h.Profiles.Save(SliceHarness.ProfileOf("thor", ("ghost", "5s"), ("heartbeat", "5s")), Token);
        _ = await _h.HoldLease();

        await Run(new CountingApprovals(_h.Profiles), () => Heartbeats() >= 2);

        Assert.Contains(_log.Lines, l => l.Contains("SND-SCHED-001 the profile names service 'ghost'", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_approval_is_logged_once_and_the_schedule_goes_on()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _ = await _h.HoldLease();
        var approvals = new CountingApprovals(_h.Profiles, throwFirst: 3);

        await Run(approvals, () => Heartbeats() >= 1);

        _ = Assert.Single(_log.Lines, l => l.Contains("SND-SCHED-001 the approved profile couldn't be read", StringComparison.Ordinal));
    }

    private static string HeartbeatType => EventTypes.ServiceRun(HeartbeatService.ServiceId);

    private int Heartbeats() => _h.Store.Events.Count(e => e.Envelope.Type == HeartbeatType);

    /// <summary>Starts the scheduler, steps the clock until <paramref name="done"/> holds (at most 60 fake seconds), and stops it.</summary>
    private async Task Run(CountingApprovals approvals, Func<bool> done)
    {
        await using var provider = new ServiceCollection()
            .AddSingleton<ILedgerStore>(_h.Store)
            .AddSingleton(_h.Events)
            .AddSingleton<TimeProvider>(_h.Clock)
            .AddScoped<IService<RecordHeartbeatRequest, RecordHeartbeatResponse>, RecordHeartbeatService>()
            .AddScoped<IScheduledService, HeartbeatService>()
            .AddScoped<IScheduledService, ThrowingService>()
            .AddScoped<IScheduledService, TimingOutService>()
            .BuildServiceProvider();
        var clock = new SteppedClock(_h.Clock);
        using var scheduler = new SchedulerHost(provider.GetRequiredService<IServiceScopeFactory>(), approvals, _h.Held, clock, _log);
        await scheduler.StartAsync(Token);
        for (var step = 0; !done(); step++)
        {
            Assert.True(step < 60, "the condition didn't hold within 60 fake seconds");
            await clock.Step(TimeSpan.FromSeconds(1));
            await clock.Armed();
        }

        await scheduler.StopAsync(Token);
    }
}
