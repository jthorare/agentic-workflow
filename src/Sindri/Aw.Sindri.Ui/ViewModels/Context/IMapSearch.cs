using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Resolves what is typed in the search box to the items it matches on the map; the ranked full-text endpoint replaces the implementation with one change.</summary>
internal interface IMapSearch
{
    /// <summary>Searches the map.</summary>
    /// <param name="query">What is typed in the box.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The matches, best first; empty when nothing matches.</returns>
    public Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken);
}
