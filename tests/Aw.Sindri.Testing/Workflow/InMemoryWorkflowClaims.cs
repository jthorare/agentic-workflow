using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IWorkflowClaims"/>: one claim per item. Passes <see cref="WorkflowClaimsContract"/>.</summary>
public sealed class InMemoryWorkflowClaims : IWorkflowClaims
{
    private readonly ConcurrentDictionary<ItemKey, WorkflowClaim> _claims = new();
    private readonly Lock _gate = new();

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowClaim?>> Find(ItemKey item, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<WorkflowClaim?>>(_claims.GetValueOrDefault(item));

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowClaim>> Take(WorkflowClaim claim, InstanceAddress? ended, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        lock (_gate)
        {
            if (!_claims.TryGetValue(claim.Item, out var holder) || (ended is { } gone && holder.Owner == gone))
            {
                _claims[claim.Item] = claim;
                return ValueTask.FromResult<Result<WorkflowClaim>>(claim);
            }

            return ValueTask.FromResult<Result<WorkflowClaim>>(holder);
        }
    }
}
