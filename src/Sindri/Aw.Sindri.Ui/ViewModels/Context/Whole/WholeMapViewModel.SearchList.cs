namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A search on the overview's area tiles (<see cref="WholeMapViewModel"/>, continued; #3674): the tiles give way to the matches as the ranked list, 50 a page, through the area list's pager. Esc or an empty box goes back to the tiles.</summary>
internal sealed partial class WholeMapViewModel
{
    private IReadOnlyList<MapSearchHit>? _hits;
    private (IReadOnlyList<MapSearchHit> Source, int Page, IReadOnlyList<MapItemViewModel> Rows)? _hitRows;

    /// <summary>Gets whether the stage is the list of a search's matches.</summary>
    public bool IsSearchList => _hits is not null;

    /// <summary>A search resolved on the area tiles: the matches are the list; none is the "No matches" note over the tiles.</summary>
    /// <param name="hits">The matches, best first.</param>
    internal void ShowSearchList(IReadOnlyList<MapSearchHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        (Hovered, Pinned, SearchActive) = (null, null, true);
        NoMatches = hits.Count == 0;
        _hits = hits.Count == 0 ? null : hits;
        (AreaPage, AreaPages) = (1, Math.Max(1, (hits.Count + WholeDrillIn.ListPageSize - 1) / WholeDrillIn.ListPageSize));
        Changed();
        OnChanged(nameof(NoMatches));
    }

    // The rows of the page on screen, built once per page.
    private IReadOnlyList<MapItemViewModel> SearchRows(IReadOnlyList<MapSearchHit> hits)
    {
        if (_hitRows is not { } have || !ReferenceEquals(have.Source, hits) || have.Page != AreaPage)
        {
            IReadOnlyList<MapItemViewModel> rows = [.. hits.Skip((AreaPage - 1) * WholeDrillIn.ListPageSize).Take(WholeDrillIn.ListPageSize).Select(h => new MapItemViewModel(h.Key, h.Label, Labels.ContextMapItemName(h.Label, h.Kind), IsTile: false))];
            _hitRows = have = (hits, AreaPage, rows);
        }

        return have.Rows;
    }

    // Another page of the matches: no read, the list is in memory.
    private void TurnSearchPage(int page)
    {
        AreaPage = page;
        OnChanged(nameof(Items));
        OnChanged(nameof(AreaPageText));
        OnChanged(nameof(CanPreviousPage));
        OnChanged(nameof(CanNextPage));
    }
}
