using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.ChildFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>A child's change releasing its parent, answers forwarded to the child, and a parent's cancel cascading (spec §5.8).</summary>
public sealed class ChildReleaseTests
{
    private static AnswerGiven Answer => new(Terminal, Stamp('1'), AnswerVariant.Text, "use the cached build", Amount: null, Forwarded: null);

    private static Suspension Timeout { get; } = new(
        SuspensionKind.Park, ParkReason.Timeout, ParkKind: null, Escalation: null, "running timed out", "service:workflow.timers", Mirrors: null,
        new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effects: []));

    [Fact, Trait("Lane", "unit")]
    public void Release_returns_to_the_wait_on_the_same_child()
    {
        foreach (var mirroring in new[] { MirroringPark, MirroringEscalation })
        {
            var released = ChildPropagation.Release(mirroring, ChildId).Instance;

            Assert.Null(released.Suspension);
            Assert.Equal("running", released.State);
            Assert.Equal(Running, released.Frame);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Release_with_no_mirror_returns_the_same_instance()
    {
        Assert.Same(Waiting, ChildPropagation.Release(Waiting, ChildId).Instance);
        Assert.Same(MirroringPark, ChildPropagation.Release(MirroringPark, OtherChildId).Instance);
    }

    [Fact, Trait("Lane", "unit")]
    public void Release_under_a_takeover_keeps_the_takeover_and_drops_the_covered_mirror()
    {
        var taken = TakenOver(MirroringPark);

        var released = ChildPropagation.Release(taken, ChildId).Instance;

        Assert.Equal(EngineStates.TakenOver, released.State);
        Assert.Null(released.PendingPark);
        Assert.Equal(Running, released.Frame);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pending_park_behind_the_mirror_takes_effect_on_release()
    {
        var mirroring = MirroringPark with { PendingPark = Timeout };

        var released = ChildPropagation.Release(mirroring, ChildId).Instance;

        Assert.Equal(EngineStates.Parked, released.State);
        Assert.Equal(Timeout, released.Suspension);
        Assert.Null(released.PendingPark);
        Assert.Equal(ParkReason.Timeout, released.Park);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pending_park_mirroring_the_child_is_cleared()
    {
        var pending = Push(Waiting, Timeout) with { PendingPark = MirroringPark.Suspension };

        var released = ChildPropagation.Release(pending, ChildId).Instance;

        Assert.Equal(Timeout, released.Suspension);
        Assert.Null(released.PendingPark);
        Assert.Equal(ParkReason.Timeout, released.Park);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_answer_to_a_parent_mirroring_a_child_goes_to_the_child()
    {
        Assert.Equal(ChildId, ChildPropagation.ForwardTo(MirroringPark, Answer));
        Assert.Equal(ChildId, ChildPropagation.ForwardTo(MirroringEscalation, Answer));
    }

    [Fact, Trait("Lane", "unit")]
    public void Cancel_takeover_and_answers_to_an_unmirrored_parent_stay_with_the_parent()
    {
        Assert.Null(ChildPropagation.ForwardTo(Waiting, Answer));
        Assert.Null(ChildPropagation.ForwardTo(TakenOver(MirroringPark), Answer));
        Assert.Null(ChildPropagation.ForwardTo(MirroringPark, new CancelRequested(Terminal, CancelOrigin.Command, Stamp('1'), Sequence: null, Parent: null)));
        Assert.Null(ChildPropagation.ForwardTo(MirroringPark, new TakeoverRequested(Terminal, Stamp('1'))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_forwarded_answer_is_the_child_trigger_on_behalf_of_the_parent()
    {
        var forwarded = ChildPropagation.Forward(MirroringPark, Answer, Stamp('2'));

        Assert.Equal(Terminal, forwarded.Actor);
        Assert.Equal(Stamp('2'), forwarded.Stamp);
        Assert.Equal(AnswerVariant.Text, forwarded.Variant);
        Assert.Equal("use the cached build", forwarded.Text);
        Assert.Equal(new OnBehalfOf(ParentId, Terminal), forwarded.Forwarded);
        Assert.Equal(Stamp('2').KeyPart(TriggerNames.Answer), forwarded.KeyPart);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parent_cancel_cascades_to_every_child_still_open()
    {
        var drifted = Child(WorkflowInstanceId.From("WFI-" + new string('e', 32)), EngineStates.Parked);
        var children = new[]
        {
            Child(ChildId, "checks"),
            Child(OtherChildId, "done", outcome: OutcomeName.From("resolved")),
            drifted,
            Child(WorkflowInstanceId.From("WFI-" + new string('f', 32)), "checks", parent: WorkflowInstanceId.From("WFI-" + new string('b', 32))),
        };

        Assert.Equal([ChildId, drifted.Id], ChildPropagation.Cascade(Waiting, children));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cascaded_cancel_is_keyed_by_the_parent()
    {
        var cancel = ChildPropagation.CancelFor(Waiting, Inbox);

        Assert.Equal(Inbox, cancel.Actor);
        Assert.Equal(CancelOrigin.Parent, cancel.Origin);
        Assert.Null(cancel.Stamp);
        Assert.Null(cancel.Sequence);
        Assert.Equal(ParentId, cancel.Parent);
        Assert.Equal($"cancel:parent:{ParentId.Value}", cancel.KeyPart);
        Assert.True(cancel.ExemptFromApproval);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_child_parks_eval_parks_and_resumes_the_parent_in_the_same_wait()
    {
        // recipes/sindri/evals/workflow.item/child-parks.jsonl from 'running': child.parked, child.resumed, then the
        // child's terminal row, applied the way the engine applies a ParentEffect.
        var park = ChildPropagation.ToParent(Waiting, Item, Changed(ChildChange.Parked, ParkReason.NoProgress, "checks failed twice"));
        var parked = Push(Waiting, Assert.IsType<Suspension>(park.Suspend));
        Assert.Equal((EngineStates.Parked, ParkReason.NoProgress), (parked.State, parked.Park));

        Assert.Equal(ParentEffectKind.Release, ChildPropagation.ToParent(parked, Item, Changed(ChildChange.Resumed)).Kind);
        var resumed = ChildPropagation.Release(parked, ChildId).Instance;
        Assert.Equal(("running", Entry, (WorkflowInstanceId?)ChildId), (resumed.State, resumed.Frame.Entry, resumed.Frame.Child));

        Assert.Same(resumed, ChildPropagation.Release(resumed, ChildId).Instance);
    }
}
