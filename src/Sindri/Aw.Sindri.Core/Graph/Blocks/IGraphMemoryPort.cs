using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where the memory blocks 'memory_search', 'memory_write' and 'memory_forget' reach the Sindri memory stores: the same slices 'sindri memory' and the MCP tools use.</summary>
public interface IGraphMemoryPort
{
    /// <summary>Recalls facts by meaning (keyword ranking when the embedding model does not answer).</summary>
    /// <param name="request">The memory, the query, the match level and the limit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The fact texts, best first, and the mode; or why nothing was recalled.</returns>
    public ValueTask<Result<MemoryRecalled>> Recall(MemoryToRecall request, CancellationToken cancellationToken);

    /// <summary>Writes a fact, or refreshes a near duplicate with the same tags.</summary>
    /// <param name="request">The run that writes, the memory and the fact.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The fact's id and whether it was added or refreshed; or why it was not written.</returns>
    public ValueTask<Result<MemoryRemembered>> Remember(MemoryToRemember request, CancellationToken cancellationToken);

    /// <summary>Forgets one fact, or every fact with a tag.</summary>
    /// <param name="request">The memory and the selector.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many facts were forgotten; or why none could be.</returns>
    public ValueTask<Result<int>> Forget(MemoryToForget request, CancellationToken cancellationToken);
}
