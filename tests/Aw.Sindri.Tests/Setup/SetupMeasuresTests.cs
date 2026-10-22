using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SetupWatch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.AddRepo;

namespace Aw.Sindri.Tests.Setup;

/// <summary>The two measures of setup-once spec §7.5, computed from the ledger and not a new event: time to done (the first 'setup.step.changed' to the one that made all six done or skipped) and time to first item (done to the next 'workflow.started').</summary>
public sealed class SetupMeasuresTests : IDisposable
{
    private readonly AddRepoRig _rig = new();
    private readonly InMemorySetupFacts _facts = new();
    private readonly SetupStepLog _log = new();
    private readonly SetupMeasureLog _measureLog = new();

    public void Dispose() => _rig.Dispose();

    private async Task<SetupMeasured> Tick()
    {
        var steps = await new SetupDeriver(_facts).Derive(AddRepoRig.Token);
        var recorder = new SetupStepRecorder(_rig.Slice.Held, _rig.Slice.Store, _rig.Slice.Store, _rig.Slice.Events, _rig.Slice.Callers, _log);
        var appended = (await recorder.Record(steps, AddRepoRig.Token)).Value;
        return await new SetupMeasures(_rig.Slice.Store, _measureLog).Observe(steps, appended, AddRepoRig.Token);
    }

    private async Task StartWorkflow(int n = 1)
    {
        var payload = new WorkflowEventPayload($"item/step/{n.ToString(System.Globalization.CultureInfo.InvariantCulture)}", "produce", "produce", "start", Outcome: null, Run: null, Detail: null);
        _ = await _rig.Slice.Store.Append(_rig.Slice.Events.Entry(EventTypes.WorkflowStarted, payload, $"workflow.started:item/step/{n.ToString(System.Globalization.CultureInfo.InvariantCulture)}"), _rig.Slice.Held.Current!, AddRepoRig.Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Done_is_the_time_from_the_first_look_to_the_change_that_finished_every_step()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.Enter();
        Assert.Equal(new SetupMeasured(Done: null, FirstItem: null), await Tick());

        _rig.Slice.Clock.Advance(TimeSpan.FromMinutes(12));
        _facts.RepoAnswer = InMemorySetupFacts.OneRepo;
        Assert.Equal(new SetupMeasured(Done: null, FirstItem: null), await Tick());

        _rig.Slice.Clock.Advance(TimeSpan.FromMinutes(3));
        var done = InMemorySetupFacts.AllDone();
        (_facts.ProfileAnswer, _facts.RecipesAnswer, _facts.McpAnswer, _facts.MapAnswer, _facts.OnyxAnswer) = (done.ProfileAnswer, done.RecipesAnswer, done.McpAnswer, done.MapAnswer, done.OnyxAnswer);

        Assert.Equal(new SetupMeasured(TimeSpan.FromMinutes(15), FirstItem: null), await Tick());
        Assert.Equal(new SetupMeasured(Done: null, FirstItem: null), await Tick());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_that_starts_on_a_finished_setup_measures_nothing()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.Enter();
        var done = InMemorySetupFacts.AllDone();
        (_facts.RepoAnswer, _facts.ProfileAnswer, _facts.RecipesAnswer, _facts.McpAnswer, _facts.MapAnswer, _facts.OnyxAnswer) = (done.RepoAnswer, done.ProfileAnswer, done.RecipesAnswer, done.McpAnswer, done.MapAnswer, done.OnyxAnswer);
        _ = await Tick();
        await StartWorkflow();

        _measureLog.Reset();

        Assert.Equal(new SetupMeasured(Done: null, FirstItem: null), await Tick());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task First_item_is_the_time_from_done_to_the_first_workflow_start_after_it_and_is_measured_once()
    {
        _ = await _rig.Slice.HoldLease();
        _rig.Enter();
        await StartWorkflow();
        var done = InMemorySetupFacts.AllDone();
        (_facts.RepoAnswer, _facts.ProfileAnswer, _facts.RecipesAnswer, _facts.McpAnswer, _facts.MapAnswer, _facts.OnyxAnswer) = (done.RepoAnswer, done.ProfileAnswer, done.RecipesAnswer, done.McpAnswer, done.MapAnswer, done.OnyxAnswer);
        Assert.Equal(TimeSpan.Zero, (await Tick()).Done);
        Assert.Equal(new SetupMeasured(Done: null, FirstItem: null), await Tick());

        _rig.Slice.Clock.Advance(TimeSpan.FromMinutes(5));
        await StartWorkflow(2);

        Assert.Equal(new SetupMeasured(Done: null, TimeSpan.FromMinutes(5)), await Tick());
        Assert.Equal(new SetupMeasured(Done: null, FirstItem: null), await Tick());
    }
}
