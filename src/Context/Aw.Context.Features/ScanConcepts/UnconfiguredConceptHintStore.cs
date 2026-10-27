using Aw.Context.Core;
using Aw.Context.Core.ConceptScan;
using Aw.Kernel;

namespace Aw.Context.Features.ScanConcepts;

/// <summary>The hint store of a host that wired none: every call says the store can't answer (SND-CONTEXT-004). Aw.Context.Infrastructure replaces it.</summary>
internal sealed class UnconfiguredConceptHintStore : IConceptHintStore
{
    public ValueTask<Result<Unit>> Save(int version, IReadOnlyList<ConceptHint> hints, DateTimeOffset scanned, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<Unit>());

    public ValueTask<Result<IReadOnlyList<ConceptHint>>> Read(int version, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<IReadOnlyList<ConceptHint>>());

    private static Result<T> Unwired<T>() =>
        ContextErrors.StoreUnavailable.With("no concept hint store is wired in this host (fix: add Aw.Context.Infrastructure's registration)");
}
