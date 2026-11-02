using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's search (<see cref="WholeMapViewModel"/>, continued): a filter. The map draws only its matches and their direct links; hover and pin work on those, and the camera fits them.</summary>
internal sealed partial class WholeMapViewModel
{
    private HashSet<string>? _searchSet;

    /// <summary>Whether a search is on the map: hover and pin give way to it.</summary>
    internal bool SearchActive { get; private set; }

    /// <summary>Whether the search found nothing on the map: nothing is drawn and the page says "No matches".</summary>
    internal bool NoMatches { get; private set; }

    /// <summary>A search resolved: the pin and hover let go, the map is filtered to the matches (and a tile for each collapsed feature holding one) with the items they link to, and the camera fits them.</summary>
    /// <param name="hits">The matches.</param>
    /// <param name="hitAreas">The features that hold matches the map shows only as a tile.</param>
    /// <param name="zoomed">Whether the camera was already sent to the matches' nodes.</param>
    internal void ApplySearch(IReadOnlyList<MapSearchHit> hits, IReadOnlyCollection<string> hitAreas, bool zoomed)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(hitAreas);
        (Hovered, Pinned, SearchActive) = (null, null, true);   // the pin lets go; hover and pin then work on the filtered set
        var rects = new List<WholeRect>();
        var lit = new HashSet<string>(StringComparer.Ordinal);
        if (Geometry is { } geometry)
        {
            var wanted = hits.Select(h => h.Key).ToHashSet(StringComparer.Ordinal);
            var areas = hitAreas.Concat(hits.Select(h => h.Area).OfType<string>()).ToHashSet(StringComparer.Ordinal);
            foreach (var node in geometry.Nodes.Where(n => wanted.Contains(n.Key)))
            {
                _ = lit.Add(node.Key);
                rects.Add(node.Rect);
            }

            foreach (var tile in geometry.Tiles.Where(t => wanted.Contains(t.Key) || areas.Contains(t.Key)))
            {
                _ = lit.Add(tile.Key);
                rects.Add(tile.Rect);
            }

            foreach (var key in lit.ToList())
            {
                lit.UnionWith(Around(key));
            }
        }

        NoMatches = rects.Count == 0 && !zoomed;
        _searchSet = lit;
        Refocus();
        OnChanged(nameof(NoMatches));
        OnChanged(nameof(Shown));
        OnChanged(nameof(Items));
        if (!zoomed && rects.Count > 0)
        {
            ZoomRequested?.Invoke(this, new ValueEventArgs<WholeRect>(WholeZoomBox.Around(rects)));
        }
    }

    /// <summary>Esc or an empty box: the whole map is drawn again, a hover or pin works again, and the camera fits the whole map.</summary>
    internal void EndSearch()
    {
        if (!SearchActive)
        {
            return;
        }

        ResetSearch();
        FitRequested?.Invoke(this, EventArgs.Empty);
    }

    // The search state goes with no camera move (the caller fits).
    private void ResetSearch()
    {
        (SearchActive, NoMatches, _searchSet) = (false, false, null);
        Refocus();
        OnChanged(nameof(NoMatches));
        OnChanged(nameof(Shown));
        OnChanged(nameof(Items));
    }
}
