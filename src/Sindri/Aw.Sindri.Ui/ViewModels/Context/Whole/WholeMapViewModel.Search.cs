using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's search (<see cref="WholeMapViewModel"/>, continued): the strongest highlight state. It beats a hover or a pin, lights its matches and their links, dims the rest, and fits the camera to them.</summary>
internal sealed partial class WholeMapViewModel
{
    private HashSet<string>? _searchSet;

    /// <summary>Whether a search is on the map: hover and pin give way to it.</summary>
    internal bool SearchActive { get; private set; }

    /// <summary>Whether the search found nothing on the map: everything is dimmed and the page says "No matches".</summary>
    internal bool NoMatches { get; private set; }

    /// <summary>A search resolved: the pin and hover let go, the matches (and a tile for each collapsed feature holding one) are lit with the items they link to, and the camera fits them.</summary>
    /// <param name="hits">The matches.</param>
    /// <param name="hitAreas">The features that hold matches the map shows only as a tile.</param>
    /// <param name="zoomed">Whether the camera was already sent to the matches' nodes.</param>
    internal void ApplySearch(IReadOnlyList<MapSearchHit> hits, IReadOnlyCollection<string> hitAreas, bool zoomed)
    {
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(hitAreas);
        (Hovered, Pinned, SearchActive) = (null, null, true);
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
        if (!zoomed && rects.Count > 0)
        {
            ZoomRequested?.Invoke(this, new ValueEventArgs<WholeRect>(WholeZoomBox.Around(rects)));
        }
    }

    /// <summary>Esc or an empty box: nothing dims, a hover or pin works again, and the camera fits the whole map.</summary>
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
    }
}
