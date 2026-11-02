using Aw.Context.Core;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Kernel;

namespace Aw.Context.Features.ScanConcepts;

/// <summary>The synthesis store of a host that wired none: every call says the store can't answer (SND-CONTEXT-004). Aw.Context.Infrastructure replaces it.</summary>
internal sealed class UnconfiguredConceptSynthesisStore : IConceptSynthesisStore
{
    public ValueTask<Result<IReadOnlyList<SynthesizedConcept>>> All(CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<IReadOnlyList<SynthesizedConcept>>());

    public ValueTask<Result<Unit>> Save(SynthesizedConcept concept, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<Unit>());

    public ValueTask<Result<Unit>> Remove(string key, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<Unit>());

    public ValueTask<Result<long>> Watermark(CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<long>());

    public ValueTask<Result<Unit>> SaveWatermark(long watermark, DateTimeOffset at, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<Unit>());

    private static Result<T> Unwired<T>() =>
        ContextErrors.StoreUnavailable.With("no concept synthesis store is wired in this host (fix: add Aw.Context.Infrastructure's registration)");
}
