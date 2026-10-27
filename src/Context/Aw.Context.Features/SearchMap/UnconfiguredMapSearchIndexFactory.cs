using Aw.Context.Core;
using Aw.Context.Core.Search;
using Aw.Kernel;

namespace Aw.Context.Features.SearchMap;

/// <summary>The index factory of a host that wired none: every search says the store can't answer (SND-CONTEXT-004). Aw.Context.Infrastructure replaces it.</summary>
internal sealed class UnconfiguredMapSearchIndexFactory : IMapSearchIndexFactory
{
    public Result<IMapSearchIndex> Build(IReadOnlyList<MapSearchDoc> docs) =>
        ContextErrors.StoreUnavailable.With("no map search index is wired in this host (fix: add Aw.Context.Infrastructure's registration)");
}
