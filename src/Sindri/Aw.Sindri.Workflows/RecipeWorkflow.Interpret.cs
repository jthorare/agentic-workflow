using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The interpreter's loop (spec §5.1): each pass runs the open Step or waits, until the instance has ended and its effects have settled.</summary>
internal sealed partial class RecipeWorkflow
{
    private async Task<WorkflowInstance> Interpret(WorkflowInstance instance, Interpreting context)
    {
        while (!instance.Ended)
        {
            instance = await HeavyTurn(instance, context).ConfigureAwait(true) ?? instance switch
            {
                { Suspension: null, Frame.Run: { Runtime: StepRuntime.Block } block } => await RunBlockStep(instance, block, context).ConfigureAwait(true),
                { Suspension: null, Frame.Run: { Runtime: StepRuntime.Agent } agent } => await RunAgentStep(instance, agent, context).ConfigureAwait(true),
                { Suspension: null, Frame.Run: { } open } => await RunDeskStep(instance, open, context).ConfigureAwait(true),
                _ => await Wait(instance, context).ConfigureAwait(true),
            };
            await DropEndedRun(instance).ConfigureAwait(true);
            await DropEndedAgent(instance).ConfigureAwait(true);
        }

        await Settle().ConfigureAwait(true);   // every effect it emitted is dispatched, refused or aborted (§5.7)
        await SettleLeftovers(instance, context).ConfigureAwait(true);
        return instance;
    }
}
