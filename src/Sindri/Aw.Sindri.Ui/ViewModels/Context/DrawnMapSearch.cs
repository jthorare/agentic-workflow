using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The search the box uses today: the anchors and lenses read behind "Starts at" already resolved the query, and the items they drew are the matches.</summary>
internal sealed class DrawnMapSearch : IMapSearch
{
    private readonly Func<ContextMapViewModel?> _drawn;

    /// <summary>Makes the search over what the page last drew.</summary>
    /// <param name="drawn">The read the page drew; null before one.</param>
    internal DrawnMapSearch(Func<ContextMapViewModel?> drawn)
    {
        _drawn = drawn;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MapSearchHit>> Search(string query, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MapSearchHit>>([.. (_drawn()?.Nodes ?? []).Select(n => new MapSearchHit(n.Key, n.Layout.Kind, n.Layout.Label, Area: null))]);
}
