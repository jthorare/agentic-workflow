using Aw.Kernel;

namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Where the synthesized concepts and the pass's watermark are kept (migration 0021).</summary>
public interface IConceptSynthesisStore
{
    /// <summary>Every stored concept.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The concepts ordered by key (empty when none were stored), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IReadOnlyList<SynthesizedConcept>>> All(CancellationToken cancellationToken);

    /// <summary>Stores a concept by its key. An existing key keeps its created_at and takes everything else, which is how a relabel keeps its map nodes.</summary>
    /// <param name="concept">The concept.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> Save(SynthesizedConcept concept, CancellationToken cancellationToken);

    /// <summary>Deletes a concept; a key that isn't stored is not an error.</summary>
    /// <param name="key">The concept's key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> Remove(string key, CancellationToken cancellationToken);

    /// <summary>The embedding.seq high-water the last pass reached.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The watermark (0 when none was saved), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<long>> Watermark(CancellationToken cancellationToken);

    /// <summary>Stores the watermark, replacing the one before.</summary>
    /// <param name="watermark">The embedding.seq high-water.</param>
    /// <param name="at">When the pass ended.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> SaveWatermark(long watermark, DateTimeOffset at, CancellationToken cancellationToken);
}
