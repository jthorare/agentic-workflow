using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflows;

public sealed class ParkedRefusalTests
{
    private static readonly RunToken _run = RunToken.For(3, 1);

    [Fact, Trait("Lane", "unit")]
    public void A_takeover_with_no_queue_on_a_desk_run_hands_back_then_completes_with_the_same_token() =>
        Assert.Equal(
            "WF is parked (Takeover); hand it back first: sindri workflow handback WF, then complete run " + _run.Value,
            Refuse(FrameFixture.Suspended(FrameFixture.Takeover(), Open(StepRuntime.Desk))));

    [Fact, Trait("Lane", "unit")]
    public void A_takeover_with_no_queue_on_a_block_run_hands_back_and_the_step_runs_again() =>
        Assert.Equal(
            "WF is parked (Takeover); hand it back first: sindri workflow handback WF (the handback runs the Step again)",
            Refuse(FrameFixture.Suspended(FrameFixture.Takeover(), Open(StepRuntime.Block))));

    [Theory, Trait("Lane", "unit")]
    [InlineData(StepRuntime.Desk)]
    [InlineData(StepRuntime.Block)]
    public void A_takeover_over_an_unapproved_park_points_at_status_and_claims_no_rerun(StepRuntime runtime) =>
        Assert.Equal(
            "WF is parked (Takeover); hand it back first: sindri workflow handback WF; a park follows, and sindri workflow status WF shows the answer it takes",
            Refuse(FrameFixture.Suspended(FrameFixture.Takeover(), FrameFixture.Suspended(Park(ParkReason.Budget), Open(runtime)))));

    [Fact, Trait("Lane", "unit")]
    public void A_takeover_over_an_approved_park_is_a_plain_desk_handback()
    {
        var instance = Approved(FrameFixture.Suspended(FrameFixture.Takeover(), FrameFixture.Suspended(Park(ParkReason.SpecConflict), Open(StepRuntime.Desk))));

        Assert.Equal("WF is parked (Takeover); hand it back first: sindri workflow handback WF, then complete run " + _run.Value, Refuse(instance));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_takeover_over_an_approved_park_is_a_plain_block_handback()
    {
        var instance = Approved(FrameFixture.Suspended(FrameFixture.Takeover(), FrameFixture.Suspended(Park(ParkReason.SpecConflict), Open(StepRuntime.Block))));

        Assert.Equal("WF is parked (Takeover); hand it back first: sindri workflow handback WF (the handback runs the Step again)", Refuse(instance));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_timeout_park_points_at_status() =>
        Assert.Equal(
            "WF is parked (Timeout); answer it first: sindri workflow status WF shows the answer it takes",
            Refuse(FrameFixture.Suspended(Park(ParkReason.Timeout), Open(StepRuntime.Desk))));

    [Fact, Trait("Lane", "unit")]
    public void A_budget_park_on_a_block_run_names_neither_resume_nor_a_rerun()
    {
        var text = Refuse(FrameFixture.Suspended(Park(ParkReason.Budget), Open(StepRuntime.Block)));

        Assert.Equal("WF is parked (Budget); answer it first: sindri workflow status WF shows the answer it takes", text);
        Assert.DoesNotContain("--resume", text, StringComparison.Ordinal);
        Assert.DoesNotContain("again", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_park_with_an_unapproved_park_queued_behind_it_points_at_status() =>
        Assert.Equal(
            "WF is parked (Budget); answer it first: sindri workflow status WF shows the answer it takes",
            Refuse(FrameFixture.Suspended(Park(ParkReason.Rounds), FrameFixture.Suspended(Park(ParkReason.Budget), Open(StepRuntime.Block)))));

    private static Suspension Park(ParkReason reason) => FrameFixture.Park(reason, ParkKind.Step, ResumeKind.Transition);

    private static WorkflowInstance Open(StepRuntime runtime)
    {
        var producing = FrameFixture.Producing();
        return producing with { Frame = producing.Frame with { Run = new OpenRun(_run, runtime, Launch: null, Task: null) } };
    }

    private static WorkflowInstance Approved(WorkflowInstance instance) =>
        instance with { PendingPark = Assert.IsType<Suspension>(instance.PendingPark) with { Approved = true } };

    private static string Refuse(WorkflowInstance instance)
    {
        var suspension = Assert.IsType<Suspension>(instance.Suspension);
        return RecipeWorkflow.ParkedRefusal(instance, suspension, _run).Replace(instance.Address.Value, "WF", StringComparison.Ordinal);
    }
}
