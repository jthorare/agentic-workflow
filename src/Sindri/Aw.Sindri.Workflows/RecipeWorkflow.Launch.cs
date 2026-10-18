using Aw.Sindri.Core.Workflow.Engine;
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
    // Launches the session. Done is true when the run ended first or the launch failed; the instance is then the decided one, else it has its launch filled in.
    private async Task<(WorkflowInstance Instance, bool Done)> LaunchPhase(WorkflowInstance instance, OpenRun open, Interpreting context)
    {
        var (state, step) = StateOf(instance, context);
        var run = AgentRunOf(instance, open, step);
        var (applied, launched) = await LaunchUntilSettled(instance, open, run, step, context).ConfigureAwait(true);
        if (launched is null)
        {
            return (await StopIfClosed(applied, new AgentRunning(open, state, run, Launched: null)).ConfigureAwait(true), true);
        }

        if (launched.Failure is { } failure)
        {
            return (await ClosedByRefusal(applied, new AgentRunning(open, state, run, Launched: null), step, failure, context).ConfigureAwait(true), true);
        }

        _agent = (open.Token.Value, launched);
        var filled = applied with { Frame = applied.Frame with { Run = open with { Launch = AgentLaunches.LaunchIdFor(run).Value } } };
        _instance = filled;   // why: the run's launch is part of the instance the Query and the validator read; no timer or state changed
        return (filled, false);
    }

    // The launch activity, again for as long as the session cap holds it back (SND-LAUNCH-020): each ask is its own activity, so the state timer and the commands apply meanwhile. Null says the run ended first.
    private async Task<(WorkflowInstance Instance, AgentLaunched? Launched)> LaunchUntilSettled(WorkflowInstance instance, OpenRun open, AgentStepRun run, StepUse step, Interpreting context)
    {
        var (state, _) = StateOf(instance, context);
        var inputs = InputsOf(instance, step);
        var item = instance.Facts.TryGetValue(FactKey.From("fact:item"), out var fact) ? fact : null;
        while (true)
        {
            using var cancel = new CancellationTokenSource();
            var launching = Workflow.ExecuteActivityAsync((AgentStepActivities a) => a.LaunchAsync(new AgentLaunchRun(run, item, inputs)), AgentOptions(_launchCall, open, state, cancel.Token));
            var (applied, end) = await AwaitAgent(instance, open, new Awaited(launching, cancel, Reports: false), context).ConfigureAwait(true);
            if (end == AgentEnd.Over)
            {
                return (applied, null);
            }

            var launched = await Launching(launching).ConfigureAwait(true);
            if (!launched.Waiting)
            {
                return (applied, launched);
            }

            instance = applied;
        }
    }

    // The facts the Step's descriptor declares as inputs and the instance holds: they go into the run folder before the launch (sandbox spec SB17).
    private Dictionary<string, FactValue> InputsOf(WorkflowInstance instance, StepUse step) => _recipe!.Catalog.Find(step.Use) is { } descriptor   // why: the loop runs only after Begin set the recipe
        ? descriptor.Inputs.Where(instance.Facts.ContainsKey).ToDictionary(static key => key.Value, key => instance.Facts[key], StringComparer.Ordinal)
        : new Dictionary<string, FactValue>(StringComparer.Ordinal);

    // A refused launch: the agent run is closed (N1), then what the code means decides the instance (sandbox spec §6, SB16). Any other refusal is a Step failure.
    private async Task<WorkflowInstance> ClosedByRefusal(WorkflowInstance instance, AgentRunning running, StepUse step, AgentLaunchFailure failure, Interpreting context)
    {
        var actor = "service:agent:" + step.Use.Value;
        Trigger? trigger = failure.Kind switch
        {
            AgentLaunchFailureKind.NoRuntime => new RuntimeUnavailable(actor, running.Open.Token, failure.Code, failure.Reason, failure.Source, failure.Message),
            AgentLaunchFailureKind.LaunchPark => new LaunchRefused(actor, running.Open.Token, failure.Code, failure.Reason, failure.Source, failure.Message),
            AgentLaunchFailureKind.Park or AgentLaunchFailureKind.StepFailed or AgentLaunchFailureKind.Wait or _ => null,
        };
        if (trigger is null)
        {
            return await Decided(instance, running.Open, AgentFailed(running.Open, step, failure.Text, tokens: 0), reportId: null, context).ConfigureAwait(true);
        }

        _ = await Close(running, measure: false).ConfigureAwait(true);
        return await Decided(instance, running.Open, trigger, reportId: null, context).ConfigureAwait(true);
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
