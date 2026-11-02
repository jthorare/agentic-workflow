using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Search;
using Aw.Context.Features.SearchMap;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The concept scan's map search: the engine's search slice, so the scan sees what a person's search sees. It sits beside the facade classes because a slice never references another slice.</summary>
/// <param name="search">The search slice.</param>
internal sealed class ConceptScanSearch(IService<SearchMapRequest, MapSearchView> search) : IConceptScanSearch
{
    public ValueTask<Result<MapSearchView>> Search(MapSearchQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        search.Handle(new SearchMapRequest(query, scope), cancellationToken);
}
