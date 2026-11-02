using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The launch phase of the agent runtime (engine P8 U3, U9): launches the session, waits out a full session cap, and routes a refused launch.</summary>
internal sealed partial class RecipeWorkflow
{
    private bool? _launchRoutes;
    private bool LaunchRoutes => _launchRoutes ??= Workflow.Patched("sandbox-u9-launch-v1");   // U9: a full session cap is waited out and a no-runtime or launch-park refusal is routed; a history without the marker fails every refused launch as a Step failure, as it was recorded

    // Launches the session. Done is true when the run ended first or the launch failed; the instance is then the decided one, else it has its launch filled in.
    private async Task<(WorkflowInstance Instance, bool Done)> LaunchPhase(WorkflowInstance instance, OpenRun open, Interpreting context, PreviousRun? previous = null)
    {
        var (state, step) = StateOf(instance, context);
        var run = AgentRunOf(instance, open, step);
        var item = instance.Facts.TryGetValue(FactKey.From("fact:item"), out var fact) ? fact : null;
        var workspace = (step.Use == _ciFix ? AgentWorkspace : PhaseWorkspaceLaunch) && instance.Facts.TryGetValue(_workspaceKey, out var held) ? held : null;   // LN-WF2: an instance that holds a workspace (the phase's worktree, or docs sync's own) runs its agent Steps there
        _ = LaunchRoutes;   // read before the first launch command, so the marker sits at one place in every history
        _launchedAt = _launchedAt?.Entry == open.Token.Entry ? _launchedAt : (open.Token.Entry, new DateTimeOffset(Workflow.UtcNow));
        var request = Launch(run, item, step, instance, workspace) with { Previous = previous };   // the retry on a stronger model names the run that failed
        var (applied, launched) = await LaunchUntilSettled(instance, open, state, request, context).ConfigureAwait(true);
        if (launched is null)
        {
            return (await StopIfClosed(applied, new AgentRunning(open, state, run, Launched: null)).ConfigureAwait(true), true);
        }

        if (launched.Failure is { } failure)
        {
            return (await ClosedByRefusal(applied, new AgentRunning(open, state, run, Launched: null), step, failure, context).ConfigureAwait(true), true);
        }

        _agent = (open.Token.Value, launched);
        _held = new AgentRunning(open, state, run, launched);
        var filled = applied with { Frame = applied.Frame with { Run = open with { Launch = AgentLaunches.LaunchIdFor(run).Value } } };
        _instance = filled;   // why: the run's launch is part of the instance the Query and the validator read; no timer or state changed
        return (filled, false);
    }

    // The launch activity, again for as long as the session cap holds it back (SND-LAUNCH-020): each ask is its own activity, so the state timer and the commands apply meanwhile. Null says the run ended first.
    private async Task<(WorkflowInstance Instance, AgentLaunched? Launched)> LaunchUntilSettled(WorkflowInstance instance, OpenRun open, RecipeState state, AgentLaunchRun launch, Interpreting context)
    {
        while (true)
        {
            using var cancel = new CancellationTokenSource();
            var launching = Workflow.ExecuteActivityAsync((AgentStepActivities a) => a.LaunchAsync(launch), AgentOptions(_launchCall, open, state, cancel.Token));
            var (applied, end) = await AwaitAgent(instance, open, new Awaited(launching, cancel, Reports: false), context).ConfigureAwait(true);
            if (end == AgentEnd.Over)
            {
                return (applied, null);
            }

            var launched = await Launching(launching).ConfigureAwait(true);
            if (!launched.Waiting || !LaunchRoutes)
            {
                return (applied, launched);
            }

            instance = applied;
        }
    }

    // A refused launch (sandbox spec §6, SB16): a no-runtime or launch-park code closes the agent run (N1) and the code's kind decides the instance; any other failure is a Step failure.
    private async Task<WorkflowInstance> ClosedByRefusal(WorkflowInstance instance, AgentRunning running, StepUse step, AgentLaunchFailure failure, Interpreting context)
    {
        var actor = "service:agent:" + step.Use.Value;
        Trigger? refusal = !LaunchRoutes ? null : failure.Kind switch
        {
            AgentLaunchFailureKind.NoRuntime => new RuntimeUnavailable(actor, running.Open.Token, failure.Code, failure.Reason, failure.Source, failure.Message),
            AgentLaunchFailureKind.LaunchPark => new LaunchRefused(actor, running.Open.Token, failure.Code, failure.Reason, failure.Source, failure.Message),
            AgentLaunchFailureKind.Park or AgentLaunchFailureKind.StepFailed or AgentLaunchFailureKind.Wait or _ => null,
        };
        if (refusal is null)
        {
            return await Decided(instance, running.Open, AgentFailed(running.Open, step, StepFailed.LaunchReason, tokens: 0, $"{failure.Code}: {failure.Message}") with { Dependency = "the agent launcher", Code = failure.Code, FailureKind = AgentFailureKinds.Crashed }, reportId: null, context).ConfigureAwait(true);
        }

        _ = await Close(running, measure: false).ConfigureAwait(true);
        return await Decided(instance, running.Open, refusal, reportId: null, context).ConfigureAwait(true);
    }

    private static async Task<AgentLaunched> Launching(Task<AgentLaunched> launching)
    {
        try
        {
            return await launching.ConfigureAwait(true);
        }
        catch (ActivityFailureException e)
        {
            return new AgentLaunched(Worktree: null, StartHead: null, new AgentLaunchFailure(AgentLaunchFailureKind.StepFailed, "SND-LAUNCH-007", $"the launch activity failed: {e.InnerException?.Message ?? e.Message}"));
        }
    }
}
