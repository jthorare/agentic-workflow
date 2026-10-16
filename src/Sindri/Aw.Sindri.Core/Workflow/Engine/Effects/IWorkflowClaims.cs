using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// Who holds an item's claim (port; engine spec §5.7: 'effect.claim.ref' keys its identity by item through
/// 'workflow_claims' in ledger.db; §5.1: '--attempt next' is checked against it; engine P6 unit (d)). The ref
/// 'sindri/{user}/{item}' is only a marker. Whether a holder is still live is the caller's question
/// (<see cref="Runtime.IInstanceLiveness"/>): the table keeps the owner, not its state.
/// </summary>
public interface IWorkflowClaims
{
    /// <summary>The claim on <paramref name="item"/>, if one was ever taken.</summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The claim, or null when the item was never claimed.</returns>
    public ValueTask<Result<WorkflowClaim?>> Find(ItemKey item, CancellationToken cancellationToken);

    /// <summary>
    /// Records <paramref name="claim"/> when its item is free, or when the item's holder is <paramref name="ended"/> (an
    /// owner the caller found ended, whose claim is taken over); otherwise leaves the holder. Taking a claim already held
    /// by the same owner changes nothing.
    /// </summary>
    /// <param name="claim">The claim to take.</param>
    /// <param name="ended">The holder the caller found ended, or null.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The claim that holds the item afterwards: <paramref name="claim"/> itself (taken now or before), or another instance's ('claimExists').</returns>
    public ValueTask<Result<WorkflowClaim>> Take(WorkflowClaim claim, InstanceAddress? ended, CancellationToken cancellationToken);
}
