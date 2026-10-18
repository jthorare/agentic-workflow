using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Whether a launch's run is suspended (sandbox spec §7, SB16): the open instances' views are read and the one whose agent run holds the launch answers by its state (parked, escalated or taken over).
/// A launch no open instance holds is not suspended: its run closed or never existed. The instances are listed from the visibility store and each is read through the instance reader.
/// </summary>
/// <param name="instances">The open instances.</param>
/// <param name="reader">The instance views.</param>
internal sealed class InstanceRunSuspensions(IWorkflowInstances instances, IInstanceReader reader) : IRunSuspensions
{
    public async ValueTask<Result<bool>> IsSuspended(LaunchId launch, CancellationToken cancellationToken)
    {
        var open = await instances.List(new InstanceFilter(), ended: false, cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        foreach (var row in open.Value)
        {
            var view = await reader.Get(InstanceAddress.From(row.Address), cancellationToken).ConfigureAwait(false);
            if (view.IsError)
            {
                return view.Error;
            }

            if (view.Value.AgentRun is { Launch: { } held } && string.Equals(held, launch.Value, StringComparison.Ordinal))
            {
                return Suspended(view.Value.State);
            }
        }

        return false;
    }

    private static bool Suspended(string state) =>
        string.Equals(state, EngineStates.Parked, StringComparison.Ordinal)
        || string.Equals(state, EngineStates.Escalated, StringComparison.Ordinal)
        || string.Equals(state, EngineStates.TakenOver, StringComparison.Ordinal);
}
