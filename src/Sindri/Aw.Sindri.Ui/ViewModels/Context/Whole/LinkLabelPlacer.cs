namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Where a link's label goes (#3678): whole on a free stretch between the two nodes, or not at all, never under a node.</summary>
internal static class LinkLabelPlacer
{
    /// <summary>The room a label keeps beyond its own size: 8 px on each side.</summary>
    internal const double Clearance = 16;

    /// <summary>Places a label.</summary>
    /// <param name="from">The link's first node.</param>
    /// <param name="to">The link's second node.</param>
    /// <param name="width">The label's width.</param>
    /// <param name="height">The label's height.</param>
    /// <param name="nodes">Every node on the canvas.</param>
    /// <returns>The label's rectangle, centred on the stretch, or null when no stretch is free.</returns>
    internal static WholeRect? Place(WholeRect from, WholeRect to, double width, double height, IReadOnlyList<WholeRect> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var (cx, cy) = ((from.CenterX + to.CenterX) / 2, (from.CenterY + to.CenterY) / 2);
        foreach (var candidate in Candidates(from, to, (width, height), (cx, cy)))
        {
            if (!nodes.Any(candidate.Intersects))
            {
                return candidate;
            }
        }

        return null;
    }

    // The label centred on the horizontal stretch between the two nodes, then on the vertical one; each only when it is wide (tall) enough.
    private static IEnumerable<WholeRect> Candidates(WholeRect a, WholeRect b, (double Width, double Height) label, (double X, double Y) centre)
    {
        var (left, right) = a.X <= b.X ? (a, b) : (b, a);
        var run = right.X - left.Right;
        if (run >= label.Width + Clearance)
        {
            yield return new WholeRect(left.Right + ((run - label.Width) / 2), centre.Y - (label.Height / 2), label.Width, label.Height);
        }

        var (top, bottom) = a.Y <= b.Y ? (a, b) : (b, a);
        var rise = bottom.Y - top.Bottom;
        if (rise >= label.Height + Clearance)
        {
            yield return new WholeRect(centre.X - (label.Width / 2), top.Bottom + ((rise - label.Height) / 2), label.Width, label.Height);
        }
    }
}
