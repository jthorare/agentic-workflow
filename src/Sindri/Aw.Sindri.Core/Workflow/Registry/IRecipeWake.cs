using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// Reaches the running instances of a recipe (spec §4.1, W11, Phase 2): the revoke and drift previews list them, and
/// a revoke or drift approval wakes them. Rev 8: the signal sender arrives in P4; until then the adapter is a no-op
/// that finds no instances.
/// </summary>
public interface IRecipeWake
{
    /// <summary>The live instances running <paramref name="hash"/> (parked ones included).</summary>
    /// <param name="hash">The recipe hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The instance ids (empty when none runs it).</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowInstanceId>>> Running(RecipeHash hash, CancellationToken cancellationToken);

    /// <summary>
    /// Sends <paramref name="notice"/> to every live instance of its hash. A notice repeating an earlier (hash, kind,
    /// sequence) is sent again when a revoke resumes, so a receiver treats it as a no-op.
    /// </summary>
    /// <param name="notice">The change.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>Unit once sent.</returns>
    public ValueTask<Result<Unit>> Wake(RecipeWakeNotice notice, CancellationToken cancellationToken);
}
