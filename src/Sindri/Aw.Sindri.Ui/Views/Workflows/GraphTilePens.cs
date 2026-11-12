using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// The paint of a graph step tile in each state (#3234): hover, a focus ring separate from the selected ring, disabled, error. Pure data (brush keys, widths, gaps):
/// the canvas resolves the keys from the theme and draws them. Only existing tokens are used, each held by GraphTilePensTests: rings and borders read at 3:1 against the
/// canvas, text at 4.5:1 on its fill (a disabled tile's tertiary ink is exempt). Disabled wins over error and hover, error over hover.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "The canvas adopts it when ui wires the states (#3234).")]
internal static class GraphTilePens
{
    private static readonly GraphTilePaint _rest = new("Brush.Surface2", "Brush.Hairline", 1, Dashed: false, "Brush.Ink", "Brush.InkMuted");
    private static readonly GraphTilePaint _hover = new("Brush.Surface3", "Brush.WireHi", 1, Dashed: false, "Brush.Ink", "Brush.InkMuted");
    private static readonly GraphTilePaint _error = new("Brush.ErrorSoft", "Brush.Error", 2, Dashed: false, "Brush.Ink", "Brush.InkMuted");
    private static readonly GraphTilePaint _disabled = new("Brush.Surface1", "Brush.Wire", 1, Dashed: true, "Brush.InkTertiary", "Brush.InkTertiary");

    private static readonly GraphRing _selected = new("Brush.Primary", 2, 3);
    private static readonly GraphRing _focus = new("Brush.PrimaryHover", 2, 6);

    /// <summary>Gets the fill, border and text of a tile in these states.</summary>
    /// <param name="states">The states it is in.</param>
    /// <returns>The paint.</returns>
    internal static GraphTilePaint Paint(GraphTileStates states) =>
        states.HasFlag(GraphTileStates.Disabled) ? _disabled
        : states.HasFlag(GraphTileStates.Error) ? _error
        : states.HasFlag(GraphTileStates.Hover) ? _hover
        : _rest;

    /// <summary>Gets the rings drawn round a tile in these states, the inner one first: selected, then focus outside it.</summary>
    /// <param name="states">The states it is in.</param>
    /// <returns>The rings.</returns>
    internal static IReadOnlyList<GraphRing> Rings(GraphTileStates states)
    {
        var rings = new List<GraphRing>(2);
        if (states.HasFlag(GraphTileStates.Selected))
        {
            rings.Add(_selected);
        }

        if (states.HasFlag(GraphTileStates.Focus))
        {
            rings.Add(_focus);
        }

        return rings;
    }
}
