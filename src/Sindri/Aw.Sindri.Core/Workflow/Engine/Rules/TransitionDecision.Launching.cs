using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// A refused agent launch and a session lost during a suspension (sandbox spec §6, §7, SB16; engine P8 U9). No runtime (-014, -017) closes the agent run: a deskCapable Step opens its desk run in
/// the same frame (a new token, no round counted, no failure), any other Step parks 'SND-WF-010' with the sandbox marker. An unconfinable Step or a failed self-check (-015, -016) parks 'launch'.
/// Either park's Resume starts a new run and counts no round, because the launch never ran one. A session found ended when a suspension ends starts a new run too.
/// </summary>
public static partial class TransitionDecision
{
    private static Decision NoRuntimeForLaunch(Deciding d, WorkflowInstance instance, RuntimeUnavailable refusal)
    {
        if (OpenAgentRun(d, instance, refusal.Run) is { } rejected)
        {
            return rejected;
        }

        var state = d.Recipe.States[instance.State];
        var step = StepOf(state, instance);
        if (d.Context.Catalog.Find(step) is not { DeskCapable: true })
        {
            return d.Changed(d.Rules.Suspend(instance, ParkFor(ParkReason.NoRuntime, kind: null, step, refusal)));
        }

        var run = instance.Frame.Run!;   // why: OpenAgentRun checked the open agent run
        var desk = new OpenRun(RunToken.For(run.Token.Entry, run.Token.Number + 1), StepRuntime.Desk, Launch: null, state.Next);
        var after = instance with { Frame = instance.Frame with { Run = desk } };
        return d.Changed(after) with { FollowUps = [new FollowUp(FollowUpKind.StartRun, instance.Id, desk.Token, step, Answer: null)] };
    }

    private static Decision LaunchParked(Deciding d, WorkflowInstance instance, LaunchRefused refusal)
    {
        if (OpenAgentRun(d, instance, refusal.Run) is { } rejected)
        {
            return rejected;
        }

        var kind = string.Equals(refusal.Code, "SND-LAUNCH-016", StringComparison.Ordinal) ? ParkKind.SelfCheck : ParkKind.Unconfinable;
        var step = StepOf(d.Recipe.States[instance.State], instance);
        return d.Changed(d.Rules.Suspend(instance, ParkFor(ParkReason.Launch, kind, step, refusal)));
    }

    /// <summary>The refusal belongs to the open agent run of a running instance, else it is stale or not allowed.</summary>
    private static Rejected? OpenAgentRun(Deciding d, WorkflowInstance instance, RunToken run)
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

    private static BlockId StepOf(RecipeState state, WorkflowInstance instance) =>
        state.Step?.Use ?? throw new InvalidOperationException($"'{instance.Frame.State}' has an agent run but no Step");   // why: only an active state opens a run

    private static Suspension ParkFor(ParkReason reason, ParkKind? kind, BlockId step, ILaunchRefusal refusal) => new(
        SuspensionKind.Park, reason, kind, Escalation: null,
        $"{step.Value} can't start: {refusal.Code}{(refusal.Reason is null ? string.Empty : $" ({refusal.Reason})")}: {refusal.Detail}", refusal.Actor, Mirrors: null,
        new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []))
    {
        Sandbox = new SandboxPark(refusal.Code, refusal.Reason, refusal.Source),
    };

    /// <summary>
    /// A session that ended while the instance was suspended (sandbox spec §7, SB16 (8)), found when the suspension ended and the watch resumed: there is no session to hand back to,
    /// so the Step runs again in a new run. The answer's own effect (a grant) was applied when the suspension ended; the session ran, so a round is counted (at the cap it parks with rounds), and no Step failure is recorded.
    /// </summary>
    private static Decision SessionGone(Deciding d, WorkflowInstance instance, SessionLost lost)
    {
        if (OpenAgentRun(d, instance, lost.Run) is { } rejected)
        {
            return rejected;
        }

        var spend = d.Rules.Spend(instance, d.Recipe, lost.Tokens);   // the session really spent them
        var spent = instance with { Spent = spend.Spent };
        if (spend.Park is { } budget)
        {
            return d.Changed(d.Rules.Suspend(spent, Caused(budget, d.Trigger, transition: null)));
        }

        return RerunAfterAnswer(d, spent, new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []));   // the session ran, so the new run counts a round like any rerun; the launch parks do not (the Step never ran)
    }
}
