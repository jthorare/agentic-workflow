using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Context's stored PR records and the library's state, read for the current caller (PR review page spec §6.1). Reads stored data only; nothing calls GitHub.</summary>
public interface IReviewFacts
{
    /// <summary>Reads the open PR records and the sources' state.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or the error.</returns>
    public ValueTask<Result<ReviewFactsSnapshot>> Read(CancellationToken cancellationToken);
}
