namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// Places the labels on the map's edges so none lies on another (UX pass A-15): a label that starts on a taken place is moved up or down by its own height, and one that finds no free
/// place within two steps is left out (the node panel and the highlight name every edge). Sizes are estimated from the words, in world units at the zoom the map is drawn at.
/// </summary>
internal static class WholePillPlacer
{
    private const double GlyphSize = 11;      // the pill's font size on screen
    private const double GlyphWidth = 0.58;   // an average glyph, in font sizes
    private const double Pad = 5;
    private const double Steps = 2;

    /// <summary>How big a label is.</summary>
    /// <param name="words">Its words.</param>
    /// <param name="scale">The zoom; labels stop shrinking with the map below 50 %.</param>
    /// <returns>Width and height in world units.</returns>
    internal static (double Width, double Height) SizeOf(string words, double scale)
    {
        ArgumentNullException.ThrowIfNull(words);
        var unit = 1 / Math.Max(scale, 0.5);
        return (((words.Length * GlyphSize * GlyphWidth) + (2 * Pad)) * unit, ((GlyphSize * 1.3) + Pad) * unit);
    }

    /// <summary>Places the labels in the order given; earlier ones keep their place.</summary>
    /// <param name="pills">The labels, each at the middle of its edge.</param>
    /// <param name="scale">The zoom.</param>
    /// <returns>The labels that have a place, at it.</returns>
    internal static IReadOnlyList<WholePill> Place(IReadOnlyList<WholePill> pills, double scale)
    {
        ArgumentNullException.ThrowIfNull(pills);
        var placed = new List<WholePill>(pills.Count);
        foreach (var pill in pills)
        {
            var (width, height) = SizeOf(pill.Style.Pill, scale);
            foreach (var step in OffsetSteps())
            {
                var moved = pill with { Y = pill.Y + (step * height) };
                if (!placed.Exists(other => Overlaps(moved, width, height, other, scale)))
                {
                    placed.Add(moved);
                    break;
                }
            }
        }

        return placed;
    }

    private static IEnumerable<double> OffsetSteps()
    {
        yield return 0;
        for (var i = 1; i <= Steps; i++)
        {
            yield return i;
            yield return -i;
        }
    }

    private static bool Overlaps(WholePill pill, double width, double height, WholePill other, double scale)
    {
        var (otherWidth, otherHeight) = SizeOf(other.Style.Pill, scale);
        return Math.Abs(pill.X - other.X) < (width + otherWidth) / 2 && Math.Abs(pill.Y - other.Y) < (height + otherHeight) / 2;
    }
}
