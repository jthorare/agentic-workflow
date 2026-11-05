using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>The scheduler honours pauses and the run guard (API P6 unit D), on the stepped fake clock.</summary>
[Lane(TestLane.Unit)]
public sealed class SchedulerPauseAndGuardTests
{
    private static readonly ServiceName _tidy = ServiceName.From("tidy");

    [Fact]
    public async Task Paused_service_runs_no_slot_for_3_cadences()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        var counted = new CountingService();
        await using var rig = await SchedulerRig.Start([tidy, counted], ("tidy", "5s"), ("counted", "5s"));
        await rig.Pause("tidy");
        await rig.Begin();

        await rig.Step(17);

        Assert.True(counted.Runs >= 3);   // the schedule is running, three cadences passed
        Expect.Equal(0, tidy.Runs);
    }

    [Fact]
    public async Task A_paused_service_stays_paused_through_a_views_rebuild()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        await rig.Pause("tidy");
        await rig.Begin();
        await rig.Step(7);

        await rig.BeginRebuild();   // the services view is empty until its events are refolded
        await rig.Step(12);
        await rig.EndRebuild("tidy");
        await rig.Step(12);

        Expect.Equal(0, tidy.Runs);
    }

    [Fact]
    public async Task A_service_paused_in_the_last_run_does_not_run_while_the_projector_is_starting()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        var counted = new CountingService();
        await using var rig = await SchedulerRig.Start([tidy, counted], ("tidy", "5s"), ("counted", "5s"));
        await rig.Pause("tidy");   // views.db holds the pause from the previous run
        await rig.BeginStarting();
        await rig.Begin();

        await rig.Step(12);
        Expect.Equal((0, 0), (tidy.Runs, counted.Runs));   // no pause is readable yet, so no profile service runs
        await rig.EndStarting();
        await rig.Step(12);

        Assert.True(counted.Runs >= 1);
        Expect.Equal(0, tidy.Runs);
    }

    [Fact]
    public async Task No_service_runs_until_the_first_good_read_of_the_pauses()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        await rig.BeginRebuild();   // the process starts while the views are rebuilding
        await rig.Begin();

        await rig.Step(12);
        Expect.Equal(0, tidy.Runs);
        await rig.EndRebuild();
        await rig.Step(6);

        Assert.True(tidy.Runs >= 1);
    }

    [Fact]
    public async Task A_profile_service_named_like_a_built_in_can_be_paused_and_stays_unrun()
    {
        var counted = new CountingService();
        await using var rig = await SchedulerRig.Start([counted], [new BuiltInService(CountingService.Name, new Cadence(TimeSpan.FromSeconds(5)))], ("counted", "5s"));
        await rig.Pause("counted");
        await rig.Begin();

        await rig.Step(17);

        Expect.Equal(0, counted.Runs);   // the profile names it, so it is no built-in and the pause holds
    }

    [Fact]
    public async Task A_profile_service_named_like_a_built_in_waits_for_the_first_good_read_of_the_pauses()
    {
        var counted = new CountingService();
        await using var rig = await SchedulerRig.Start([counted], [new BuiltInService(CountingService.Name, new Cadence(TimeSpan.FromSeconds(5)))], ("counted", "5s"));
        await rig.BeginRebuild();
        await rig.Begin();

        await rig.Step(12);

        Expect.Equal(0, counted.Runs);
    }

    [Fact]
    public async Task A_built_in_service_runs_before_the_first_good_read_of_the_pauses_while_a_profile_service_waits()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        var counted = new CountingService();
        await using var rig = await SchedulerRig.Start([tidy, counted], [new BuiltInService(CountingService.Name, new Cadence(TimeSpan.FromSeconds(5)))], ("tidy", "5s"));
        await rig.BeginRebuild();   // the process starts while the views are rebuilding: no pause can be read
        await rig.Begin();

        await rig.Step(12);

        Assert.True(counted.Runs >= 1, "the built-in service was held back by the pause read");   // it can't be paused, so there is nothing to wait for
        Expect.Equal(0, tidy.Runs);
    }

    [Fact]
    public async Task A_pause_read_that_returns_an_error_is_logged_once_with_its_code_until_it_reads_again()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        await rig.BeginRebuild();   // the pause read answers SND-VIEW-… while the views rebuild
        await rig.Begin();

        await rig.Step(6);

        var line = Assert.Single(rig.Log.Lines, static l => l.Contains("SND-SCHED-001", StringComparison.Ordinal));   // once for the whole failing stretch, not once a second
        Assert.Contains(SindriErrors.ViewsRebuilding.Code, line, StringComparison.Ordinal);
        await rig.EndRebuild();
        await rig.Step(1);
        await rig.BeginRebuild();
        await rig.Step(2);
        Expect.Equal(2, rig.Log.Lines.Count(static l => l.Contains("SND-SCHED-001", StringComparison.Ordinal)));   // a new failing stretch logs again
    }

    [Fact]
    public async Task Resume_runs_the_next_aligned_slot_not_a_burst()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        await rig.Pause("tidy");
        await rig.Begin();
        await rig.Step(17);

        await rig.Resume("tidy");
        await rig.Step(1);

        Assert.True(tidy.Runs <= 1, "a resume replayed missed slots");
        await rig.Step(10);
        var slots = rig.Runs("tidy").Select(r => r.Slot).ToList();
        Assert.NotEmpty(slots);
        Assert.All(slots.Zip(slots.Skip(1)), pair => Expect.Equal(TimeSpan.FromSeconds(5), pair.Second - pair.First));   // aligned, one per cadence
    }

    [Fact]
    public async Task Scheduler_skips_a_slot_while_a_manual_run_is_in_flight()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        Assert.True(rig.Guard.TryClaimManual(_tidy, out var manual));   // a manual run holds the service
        await rig.Begin();

        await rig.Step(12);
        var during = tidy.Runs;
        manual.Dispose();
        await rig.Step(1);

        Expect.Equal(0, during);
        Assert.True(tidy.Runs <= 1, "a skipped slot was caught up in a burst");
    }

    [Fact]
    public async Task Manual_failure_then_idle_scheduled_run_records_the_ok()
    {
        var tidy = new ScriptedService(ScriptedService.Worked, ScriptedService.Quiet, ScriptedService.Quiet);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        await rig.Begin();
        await rig.Step(2);
        Expect.Equal(1, tidy.Runs);
        rig.Guard.Record(_tidy, ServiceOutcome.Failed);   // a manual run failed in between (its own service.ran is in the ledger)

        await rig.Step(6);

        Expect.Equal(2, tidy.Runs);
        Expect.Equal(2, rig.Runs("tidy").Count);   // the idle run is recorded: the last outcome is not ok, so the failure row clears
    }
}
