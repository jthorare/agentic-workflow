namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The whole map's hover and pin (<see cref="WholeMapViewModel"/>, continued): what the pointer is over and what a click keeps, as the mockup does it.</summary>
internal sealed partial class WholeMapViewModel
{
    private WholeMapGeometry? _linkedFor;
    private Dictionary<string, HashSet<string>> _linked = [];

    /// <summary>The key the pointer is over; null when over nothing.</summary>
    internal string? Hovered { get; private set; }

    /// <summary>The key a click kept highlighted until empty canvas, another node or Esc; null when none.</summary>
    internal string? Pinned { get; private set; }

    /// <summary>The key the highlight is about: the one under the pointer, else the pinned one.</summary>
    internal string? Focus => Hovered ?? Pinned;

    /// <summary>The focus and what it is directly linked to, drawn at full strength while the rest dims; null when nothing is highlighted.</summary>
    internal IReadOnlySet<string>? Emphasis { get; private set; }

    /// <summary>Raised when <see cref="Focus"/> or <see cref="Emphasis"/> changed.</summary>
    internal event EventHandler? EmphasisChanged;

    /// <summary>What is under a point of the world: a tile first, else the top-most node.</summary>
    /// <param name="x">World x.</param>
    /// <param name="y">World y.</param>
    /// <param name="slop">How far outside a node still counts, in world px (a dot is small at a low zoom).</param>
    /// <returns>The tile's or node's key; null over nothing.</returns>
    internal string? KeyAt(double x, double y, double slop = 0)
    {
        if (Geometry is not { } geometry)
        {
            return null;
        }

        var tile = geometry.Tiles.FirstOrDefault(t => Grown(t.Rect, slop).Contains(x, y));
        return tile?.Key ?? geometry.Nodes.LastOrDefault(n => Grown(n.Rect, slop).Contains(x, y))?.Key;
    }

    /// <summary>The pointer moved over the world.</summary>
    /// <param name="x">World x.</param>
    /// <param name="y">World y.</param>
    /// <param name="slop">How far outside a node still counts.</param>
    internal void PointerMoved(double x, double y, double slop = 0) => Hover(KeyAt(x, y, slop));

    /// <summary>The pointer is over a tile or node, or over nothing.</summary>
    /// <param name="key">The key; null over nothing.</param>
    internal void Hover(string? key)
    {
        if (!string.Equals(Hovered, key, StringComparison.Ordinal))
        {
            Hovered = key;
            Refocus();
        }
    }

    /// <summary>A click on the world: a node or tile pins the highlight; a second click on the pinned one goes further (a tile opens its feature, a node is chosen as the starting point); empty canvas lets it go.</summary>
    /// <param name="x">World x.</param>
    /// <param name="y">World y.</param>
    /// <param name="slop">How far outside a node still counts.</param>
    internal void PointerClicked(double x, double y, double slop = 0)
    {
        var key = KeyAt(x, y, slop);
        if (key is null)
        {
            _ = ClearPin();
            return;
        }

        var isTile = Geometry?.Tiles.Any(t => string.Equals(t.Key, key, StringComparison.Ordinal)) == true;
        var again = string.Equals(Pinned, key, StringComparison.Ordinal);
        Pinned = key;
        Refocus();
        if (again)
        {
            if (isTile)
            {
                OpenArea(key);
            }
            else
            {
                ChooseNode(key);   // a second click goes further: the node becomes the starting point
            }
        }
    }

    /// <summary>Lets the pinned highlight go (Esc, a click on empty canvas).</summary>
    /// <returns>True when something was pinned.</returns>
    internal bool ClearPin()
    {
        if (Pinned is null)
        {
            return false;
        }

        Pinned = null;
        Refocus();
        return true;
    }

    private void Refocus()
    {
        Emphasis = Focus is { } focus ? Around(focus) : null;
        EmphasisChanged?.Invoke(this, EventArgs.Empty);
    }

    // The key and the keys one edge away, through the node edges or the edges between features.
    private HashSet<string> Around(string key)
    {
        if (Geometry is not { } geometry)
        {
            return [key];
        }

        if (!ReferenceEquals(_linkedFor, geometry))
        {
            _linkedFor = geometry;
            _linked = [];
            foreach (var (from, to) in geometry.Edges.Select(e => (e.From, e.To)).Concat(geometry.AreaEdges.Select(e => (e.From, e.To))))
            {
                Link(from, to);
                Link(to, from);
            }
        }

        var set = new HashSet<string>(StringComparer.Ordinal) { key };
        if (_linked.TryGetValue(key, out var near))
        {
            set.UnionWith(near);
        }

        return set;
    }

    private void Link(string from, string to)
    {
        if (!_linked.TryGetValue(from, out var near))
        {
            _linked[from] = near = new HashSet<string>(StringComparer.Ordinal);
        }

        _ = near.Add(to);
    }

    private static WholeRect Grown(WholeRect rect, double slop) => new(rect.X - slop, rect.Y - slop, rect.Width + (2 * slop), rect.Height + (2 * slop));

    // The geometry changed: a hover or pin on something that is no longer drawn lets go, the rest stays.
    private void ForgetHighlight()
    {
        if (Geometry is not { } geometry)
        {
            (Hovered, Pinned) = (null, null);
        }
        else
        {
            var keys = geometry.Tiles.Select(t => t.Key).Concat(geometry.Nodes.Select(n => n.Key)).ToHashSet(StringComparer.Ordinal);
            Hovered = Hovered is { } h && keys.Contains(h) ? h : null;
            Pinned = Pinned is { } p && keys.Contains(p) ? p : null;
        }

        Refocus();
    }
}
