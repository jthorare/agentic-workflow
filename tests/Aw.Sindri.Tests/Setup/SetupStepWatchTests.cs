using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SetupWatch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.AddRepo;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.Setup;

/// <summary>'service:setup' emits 'setup.step.changed' when a step's derived state differs from the last event of that step, once, and keeps no state of its own that a restart would lose.</summary>
public sealed class SetupStepWatchTests : IDisposable
{
    private readonly AddRepoRig _rig = new();
    private readonly InMemorySetupFacts _facts = new();

    public void Dispose() => _rig.Dispose();

    private SetupStepWatch Watch(SetupStepLog? log = null) =>
        new(_facts, new SetupStepRecorder(_rig.Slice.Held, _rig.Slice.Store, _rig.Slice.Store, _rig.Slice.Events, _rig.Slice.Callers, log ?? new SetupStepLog()), NullLogger<SetupStepWatch>.Instance);

    private IEnumerable<(string Key, string Payload)> Changes() =>
        _rig.Slice.Store.Events.Where(static e => e.Envelope.Type == EventTypes.SetupStepChanged).Select(static e => (e.IdempotencyKey, e.Envelope.Payload));

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_look_records_all_six_steps_and_a_second_look_records_nothing()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.Enter();
        var watch = Watch();

        await watch.Run(AddRepoRig.Token);
        await watch.Run(AddRepoRig.Token);

        Assert.Equal(
            ["setup.step.changed:repo:needs-you:0", "setup.step.changed:profile:todo:0", "setup.step.changed:recipes:todo:0", "setup.step.changed:mcp:todo:0", "setup.step.changed:map:todo:0", "setup.step.changed:onyx:todo:0"],
            Changes().Select(static c => c.Key),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_moves_gets_one_more_event_with_the_next_count()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.Enter();
        var watch = Watch();
        await watch.Run(AddRepoRig.Token);

        _facts.RepoAnswer = InMemorySetupFacts.OneRepo;
        await watch.Run(AddRepoRig.Token);

        Assert.Contains(Changes(), static c => c.Key == "setup.step.changed:repo:done:1" && c.Payload.Contains("\"id\":\"repo\"", StringComparison.Ordinal));
        Assert.Equal(7, Changes().Count());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_restart_reads_the_ledger_and_does_not_repeat_what_is_there()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.Enter();
        await Watch().Run(AddRepoRig.Token);

        await Watch(new SetupStepLog()).Run(AddRepoRig.Token);

        Assert.Equal(6, Changes().Count());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_the_lease_nothing_is_recorded_and_nothing_throws()
    {
        _rig.Enter();

        await Watch().Run(AddRepoRig.Token);

        Assert.Empty(Changes());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_setup_service_tick_runs_the_watch()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.Enter();
        var service = new SetupService(_rig.Slice.Profiles, _rig.Backfill, mcp: null, steps: Watch());

        _ = await service.Run(new ScheduledRun(Grant: default!, DateTimeOffset.UnixEpoch), AddRepoRig.Token);

        Assert.Equal(6, Changes().Count());
    }
}
