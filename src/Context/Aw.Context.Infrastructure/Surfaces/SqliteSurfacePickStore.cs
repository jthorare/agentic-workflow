using Aw.Context.Core.Surfaces;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Surfaces;

/// <summary>Jev's product-surface decisions in context.db (migration 0025_s5j_surface_pick).</summary>
internal sealed class SqliteSurfacePickStore : ISurfacePickStore
{
    internal SqliteSurfacePickStore(ContextDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
    }

    public ValueTask<Result<IReadOnlyDictionary<string, SurfaceDecision>>> Get(IReadOnlyCollection<string> groupKeys, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> Save(IReadOnlyList<SurfaceDecision> decisions, CancellationToken cancellationToken) => throw new NotSupportedException();
}
