using Aw.Context.Core.ConceptSynthesis;
using Aw.Kernel;

namespace Aw.Context.Testing.ConceptSynthesis;

/// <summary>The in-memory <see cref="IConceptSynthesisStore"/>: a save keeps the key's first created_at and takes everything else, like the SQLite store.</summary>
public sealed class FakeConceptSynthesisStore : IConceptSynthesisStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, SynthesizedConcept> _concepts = new(StringComparer.Ordinal);

    /// <summary>The embedding.seq high-water the last run saved.</summary>
    public long SavedWatermark { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SynthesizedConcept>>> All(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<SynthesizedConcept> all = [.. _concepts.Values.OrderBy(c => c.Key, StringComparer.Ordinal)];
            return ValueTask.FromResult(Results.Success(all));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Save(SynthesizedConcept concept, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concept);
        lock (_gate)
        {
            _concepts[concept.Key] = _concepts.TryGetValue(concept.Key, out var existing) ? concept with { CreatedAt = existing.CreatedAt } : concept;
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Remove(string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _ = _concepts.Remove(key);
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<long>> Watermark(CancellationToken cancellationToken) => ValueTask.FromResult<Result<long>>(SavedWatermark);

    /// <inheritdoc />
    public ValueTask<Result<Unit>> SaveWatermark(long watermark, DateTimeOffset at, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            SavedWatermark = watermark;
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }
}
