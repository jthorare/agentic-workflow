using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The ledger's workflow rows as the plain <see cref="StatEvent"/>s the recipe stats count, oldest first (API spec §3.8).</summary>
public interface IWorkflowStatRows
{
    /// <summary>Every workflow row in the ledger, oldest first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows.</returns>
    public ValueTask<Result<IReadOnlyList<StatEvent>>> Read(CancellationToken cancellationToken);
}
