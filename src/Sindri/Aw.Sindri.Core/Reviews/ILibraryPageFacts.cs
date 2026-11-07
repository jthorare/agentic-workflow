using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>What the design library holds, read for the current caller (PR review page spec §6.1). Reads stored data only.</summary>
public interface ILibraryPageFacts
{
    /// <summary>Reads the docs with errors, the near misses and the unreadable folders.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or the error.</returns>
    public ValueTask<Result<LibraryPageFacts>> Read(CancellationToken cancellationToken);
}
