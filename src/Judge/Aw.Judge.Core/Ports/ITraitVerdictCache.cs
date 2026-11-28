using Aw.Judge.Core.Batching;

namespace Aw.Judge.Core.Ports;

/// <summary>
/// Jev's verdicts on trait questions, kept between runs ('judge/trait-check/verdicts.sqlite'): each hook run is a fresh
/// process, and a Stop re-judges the same hunks turn after turn. A key is the SHA-256 of what decides the answer (the
/// trait file, the question, the excerpt and the model). Best effort both ways: a cache that can't be read is a miss,
/// one that can't be written drops the write, never an error. Entries expire, and the cache keeps a bounded number.
/// </summary>
public interface ITraitVerdictCache
{
    /// <summary>The cached verdicts among <paramref name="keys"/> that have not expired; a key with none is absent.</summary>
    /// <param name="keys">The keys asked for.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The verdicts by key.</returns>
    public ValueTask<IReadOnlyDictionary<string, ChoiceBatchAnswer>> Get(IReadOnlyCollection<string> keys, CancellationToken cancellationToken);

    /// <summary>Stores verdicts (a key stored again is replaced and its age restarts), then drops expired entries and the oldest past the bound.</summary>
    /// <param name="verdicts">The verdicts by key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    public ValueTask Put(IReadOnlyDictionary<string, ChoiceBatchAnswer> verdicts, CancellationToken cancellationToken);
}
