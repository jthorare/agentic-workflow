using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>
/// Who holds an item's claim while still live (engine spec §5.1: '--attempt next' is checked against 'workflow_claims';
/// engine P6 unit (d)): the claim's owner, if it has one and its instance runs.
/// </summary>
/// <param name="claims">The item claims.</param>
/// <param name="liveness">Whether the owner runs.</param>
internal sealed class ClaimHolders(IWorkflowClaims claims, IInstanceLiveness liveness)
{
    /// <summary>The live holder of <paramref name="item"/>'s claim.</summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The claim while its owner is live; null when the item is unclaimed or its holder ended.</returns>
    public async ValueTask<Result<WorkflowClaim?>> LiveHolder(ItemKey item, CancellationToken cancellationToken)
    {
        var found = await claims.Find(item, cancellationToken).ConfigureAwait(false);
        if (found.IsError || found.Value is not { } holder)
        {
            return found;
        }

        var live = await liveness.IsLive(holder.Owner, cancellationToken).ConfigureAwait(false);
        return live.IsError ? live.Error : live.Value ? holder : Results.Success<WorkflowClaim?>(value: null);
    }
}
