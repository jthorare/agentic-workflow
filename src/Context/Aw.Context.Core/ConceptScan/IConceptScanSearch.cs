using Aw.Context.Core.Engine;
using Aw.Context.Core.Search;
using Aw.Kernel;

namespace Aw.Context.Core.ConceptScan;

/// <summary>The map search the scan runs between its two model passes. The engine's search slice answers it, so the scan sees what a person's search sees.</summary>
public interface IConceptScanSearch
{
    /// <summary>Searches a map.</summary>
    /// <param name="query">The words, the limit and which map.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The hits, or the search's error.</returns>
    public ValueTask<Result<MapSearchView>> Search(MapSearchQuery query, ContextScope scope, CancellationToken cancellationToken);
}
