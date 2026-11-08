using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Heavy;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The heavy resource's rule (line spec LN-WF3, §8.4) over a recipe that waits for the resource, runs a Step and waits for a child.</summary>
public sealed class HeavyHoldTests
{
    private static readonly Recipe _recipe = Of(
        "queued",
        [("queued", WaitForResource(ResourceKind.Heavy)), ("work", Active("step.produce")), ("children", WaitForChild("workflow.step")), ("done", Terminal())],
        [Go("queued", "work", "wait.satisfied"), Go("work", "done")]);

    [Fact, Trait("Lane", "unit")]
    public void A_heavy_wait_asks_for_a_slot_when_it_is_entered_and_again_when_a_park_on_it_is_resumed()
    {
        var entered = At(_recipe, "queued");
        var resumed = Parked(entered) with { Suspension = null };

        Assert.Equal((HeavyMove.Acquire, HeavyMove.Acquire), (HeavyHold.Of(entered, _recipe, inPhase: false, leased: false), HeavyHold.Of(resumed, _recipe, inPhase: false, leased: false)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parked_or_taken_over_instance_asks_for_no_slot_and_gives_up_the_one_it_holds()
    {
        var parked = Parked(At(_recipe, "queued"));
        var taken = parked with { Suspension = parked.Suspension! with { Kind = SuspensionKind.Takeover } };

        Assert.Equal(
            (HeavyMove.None, HeavyMove.Release, HeavyMove.Release),
            (HeavyHold.Of(parked, _recipe, inPhase: false, leased: false), HeavyHold.Of(parked, _recipe, inPhase: true, leased: true), HeavyHold.Of(taken, _recipe, inPhase: true, leased: true)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_desk_Step_gives_the_slot_up_and_a_Step_off_the_desk_makes_sure_of_one_once_the_wait_was_granted()
    {
        var desk = At(_recipe, "work");
        var agent = desk with { Frame = desk.Frame with { Run = desk.Frame.Run! with { Runtime = StepRuntime.Agent, Task = null } } };
        var block = desk with { Frame = desk.Frame with { Run = desk.Frame.Run! with { Runtime = StepRuntime.Block, Task = null } } };

        Assert.Equal(
            (HeavyMove.Release, HeavyMove.None, HeavyMove.Ensure, HeavyMove.Ensure, HeavyMove.None),
            (HeavyHold.Of(desk, _recipe, inPhase: true, leased: true), HeavyHold.Of(desk, _recipe, inPhase: true, leased: false),
             HeavyHold.Of(agent, _recipe, inPhase: true, leased: false), HeavyHold.Of(block, _recipe, inPhase: true, leased: true),
             HeavyHold.Of(agent, _recipe, inPhase: false, leased: false)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_wait_keeps_the_slot_and_the_end_of_the_instance_gives_it_up()
    {
        var waiting = At(_recipe, "children");
        var ended = At(_recipe, "done") with { Outcome = OutcomeName.From("resolved") };

        Assert.Equal(
            (HeavyMove.None, HeavyMove.Release, HeavyMove.None),
            (HeavyHold.Of(waiting, _recipe, inPhase: true, leased: true), HeavyHold.Of(ended, _recipe, inPhase: true, leased: true), HeavyHold.Of(ended, _recipe, inPhase: true, leased: false)));
    }

    private static WorkflowInstance Parked(WorkflowInstance instance) => instance with
    {
        Suspension = new Suspension(SuspensionKind.Park, ParkReason.Timeout, ParkKind: null, Escalation: null, "ran past 1d", "service:workflow.timers", Mirrors: null,
            new ResumeAction(ResumeKind.Rewait, Withheld: null, Gate: null, Effects: [])),
    };
}
