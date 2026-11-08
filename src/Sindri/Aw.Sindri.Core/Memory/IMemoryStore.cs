using Aw.Kernel;

namespace Aw.Sindri.Core.Memory;

/// <summary>Where memories and facts are kept (the 'memory' and 'memory_fact' tables of ledger.db). Rows hold scrubbed text only.</summary>
public interface IMemoryStore
{
    /// <summary>Creates a memory.</summary>
    /// <param name="memory">The memory.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-MEM-002 when the name is taken.</returns>
    public ValueTask<Result<Unit>> Create(MemoryInfo memory, CancellationToken cancellationToken);

    /// <summary>Reads one memory.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The memory, or SND-MEM-001.</returns>
    public ValueTask<Result<MemoryInfo>> Get(MemoryName name, CancellationToken cancellationToken);

    /// <summary>Lists every memory.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The memories sorted by name.</returns>
    public ValueTask<Result<IReadOnlyList<MemoryInfo>>> List(CancellationToken cancellationToken);

    /// <summary>Counts the facts of each memory.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Fact counts by memory name (memories with no fact are absent).</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, int>>> Counts(CancellationToken cancellationToken);

    /// <summary>Reads every fact of a memory, expired ones included.</summary>
    /// <param name="name">The memory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts oldest first, or SND-MEM-001.</returns>
    public ValueTask<Result<IReadOnlyList<MemoryFact>>> Facts(MemoryName name, CancellationToken cancellationToken);

    /// <summary>Adds a fact.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or SND-MEM-001 when its memory does not exist.</returns>
    public ValueTask<Result<Unit>> Add(MemoryFact fact, CancellationToken cancellationToken);

    /// <summary>Marks a fact written again: sets RefreshedAt (and ExpiresAt when given).</summary>
    /// <param name="memory">The memory.</param>
    /// <param name="key">The fact's key.</param>
    /// <param name="refreshedAt">The new RefreshedAt.</param>
    /// <param name="expiresAt">The new ExpiresAt; null keeps the old one.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the fact was there.</returns>
    public ValueTask<Result<bool>> Refresh(MemoryName memory, string key, DateTimeOffset refreshedAt, DateTimeOffset? expiresAt, CancellationToken cancellationToken);

    /// <summary>Deletes facts of a memory.</summary>
    /// <param name="memory">The memory.</param>
    /// <param name="selector">Which facts.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many facts were deleted, or SND-MEM-001.</returns>
    public ValueTask<Result<int>> Delete(MemoryName memory, FactSelector selector, CancellationToken cancellationToken);

    /// <summary>Deletes every expired fact of every memory (the hourly service).</summary>
    /// <param name="now">The time to compare ExpiresAt with.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many facts were deleted.</returns>
    public ValueTask<Result<int>> DeleteExpired(DateTimeOffset now, CancellationToken cancellationToken);
}
