using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>The scheduler publishes its own next slot per service (#1226): without the lease a slot that comes due stays put, which is how the services listing tells a missed slot from a run that idled.</summary>
[Lane(TestLane.Unit)]
public sealed class SchedulerLiveScheduleTests
{
    private static readonly ServiceName _tidy = ServiceName.From("tidy");

    [Fact]
    public async Task Without_the_lease_the_live_schedule_keeps_the_slot_that_passed()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        rig.H.Held.Clear();
        var start = rig.H.Clock.GetUtcNow();
        await rig.Begin();

        await rig.Step(20);

        Expect.Equal(start, rig.Live.Current![_tidy]);
        Expect.Equal(0, tidy.Runs);
    }

    [Fact]
    public async Task With_the_lease_the_live_schedule_moves_past_now()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        var start = rig.H.Clock.GetUtcNow();
        await rig.Begin();

        await rig.Step(20);

        Assert.True(rig.Live.Current![_tidy] > rig.H.Clock.GetUtcNow());
        Assert.True(rig.Live.Current[_tidy] > start);
        Assert.True(tidy.Runs > 0);
    }

    [Fact]
    public async Task A_new_scheduler_publishes_nothing_before_it_loads_a_schedule()
    {
        await using var rig = await SchedulerRig.Start([new ScriptedService(ScriptedService.Worked)], ("tidy", "5s"));

        Assert.Null(rig.Live.Current);
    }
}
