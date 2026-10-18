using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// A refused agent launch (sandbox spec §6, SB16; engine P8 U9). No runtime (-014, -017) closes the agent run: a deskCapable Step opens its desk run in the same frame
/// (a new token, no round counted, no failure), any other Step parks 'SND-WF-010' with the sandbox marker. An unconfinable Step or a failed self-check (-015, -016)
/// parks 'launch'. Either park's Resume starts a new run.
/// </summary>
public static partial class TransitionDecision
{
    private static Decision Unavailable(Deciding d, WorkflowInstance instance, RuntimeUnavailable refusal)
    {
        if (LaunchRun(d, instance, refusal.Run) is { } rejected)
        {
            return rejected;
        }

        var state = d.Recipe.States[instance.State];
        var step = Step(state, instance);
        if (d.Context.Catalog.Find(step) is not { DeskCapable: true })
        {
            return d.Changed(d.Rules.Suspend(instance, ParkFor(ParkReason.NoRuntime, kind: null, step, refusal)));
        }

        var run = instance.Frame.Run!;   // why: LaunchRun checked the open agent run
        var desk = run with { Token = RunToken.For(run.Token.Entry, run.Token.Number + 1), Runtime = StepRuntime.Desk, Launch = null, Task = state.Next };
        var after = instance with { Frame = instance.Frame with { Run = desk } };
        return d.Changed(after) with { FollowUps = [new FollowUp(FollowUpKind.StartRun, instance.Id, desk.Token, step, Answer: null)] };
    }

    private static Decision LaunchParked(Deciding d, WorkflowInstance instance, LaunchRefused refusal)
    {
        if (LaunchRun(d, instance, refusal.Run) is { } rejected)
        {
            return rejected;
        }

        var kind = string.Equals(refusal.Code, "SND-LAUNCH-016", StringComparison.Ordinal) ? ParkKind.SelfCheck : ParkKind.Unconfinable;
        var step = Step(d.Recipe.States[instance.State], instance);
        return d.Changed(d.Rules.Suspend(instance, ParkFor(ParkReason.Launch, kind, step, refusal)));
    }

    /// <summary>The refusal belongs to the open agent run of a running instance, else it is stale or not allowed.</summary>
    private static Rejected? LaunchRun(Deciding d, WorkflowInstance instance, RunToken run)
    {
        if (instance.Ended || instance.Suspension is not null)
        {
            return new Rejected(d.Key, NotAllowed(d.Trigger, instance));
        }

        return instance.Frame.Run is { Runtime: StepRuntime.Agent } open && open.Token == run
            ? null
            : new Rejected(d.Key, WorkflowErrors.StaleRunToken.With(
                $"run {run.Value} isn't the open agent run{(instance.Frame.Run is { } current ? $" (the open run is {current.Token.Value})" : string.Empty)} of '{instance.State}' (fix: use the token in status)"));
    }

    private static BlockId Step(RecipeState state, WorkflowInstance instance) =>
        state.Step?.Use ?? throw new InvalidOperationException($"'{instance.Frame.State}' has an agent run but no Step");   // why: only an active state opens a run

    private static Suspension ParkFor(ParkReason reason, ParkKind? kind, BlockId step, LaunchRefusal refusal) => new(
        SuspensionKind.Park, reason, kind, Escalation: null,
        $"{step.Value} can't start: {refusal.Code}{(refusal.Reason is null ? string.Empty : $" ({refusal.Reason})")}: {refusal.Detail}", refusal.Actor, Mirrors: null,
        new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []))
    {
        Sandbox = refusal.Marker,
    };
}
