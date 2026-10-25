using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A hit chosen in the hits list (<see cref="WholeMapViewModel"/>, continued): the node is pinned and centred, or its feature's tile when it is not drawn.</summary>
internal sealed partial class WholeMapViewModel
{
    /// <summary>Focuses a hit on the map.</summary>
    /// <param name="hit">The hit.</param>
    /// <returns>True when the map holds the node or its feature's tile.</returns>
    internal bool FocusHit(MapSearchHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (Geometry is not { } geometry)
        {
            return false;
        }

        if (geometry.Nodes.Any(n => string.Equals(n.Key, hit.Key, StringComparison.Ordinal)))
        {
            if (!string.Equals(Pinned, hit.Key, StringComparison.Ordinal))
            {
                Jump(hit.Key);   // pins, centres and opens the node's panel
                return true;
            }

            CentreRequested?.Invoke(this, new ValueEventArgs<WholeRect>(geometry.Nodes.First(n => string.Equals(n.Key, hit.Key, StringComparison.Ordinal)).Rect));
            return true;
        }

        var tile = geometry.Tiles.FirstOrDefault(t => string.Equals(t.Key, hit.Key, StringComparison.Ordinal))
            ?? (hit.Area is { } area ? geometry.Tiles.FirstOrDefault(t => string.Equals(t.Key, area, StringComparison.Ordinal)) : null);
        if (tile is null)
        {
            return false;
        }

        Pinned = tile.Key;
        Refocus();
        CentreRequested?.Invoke(this, new ValueEventArgs<WholeRect>(tile.Rect));
        return true;
    }
}
