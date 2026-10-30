using Aw.Context.Core.Surfaces;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The store a host without a context database gets: it remembers nothing, so every build asks again.</summary>
internal sealed class NoSurfacePickStore : ISurfacePickStore
{
    public ValueTask<Result<IReadOnlyDictionary<string, SurfaceDecision>>> Get(IReadOnlyCollection<string> groupKeys, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyDictionary<string, SurfaceDecision>>(new Dictionary<string, SurfaceDecision>(StringComparer.Ordinal)));

    public ValueTask<Result<Unit>> Save(IReadOnlyList<SurfaceDecision> decisions, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Unit.Value));
}
