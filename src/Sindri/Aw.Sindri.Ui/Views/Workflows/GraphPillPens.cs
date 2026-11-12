using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// The paint of a branch pill or a port on the graph canvas in each state (#3234): the tile's rules on a smaller, lighter shape (a Surface1 fill, a 1.5 px error border, rings
/// closer in). The same tokens and the same contrast floors as <see cref="GraphTilePens"/>.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "The canvas adopts it when ui wires the states (#3234).")]
internal static class GraphPillPens
{
    private static readonly GraphTilePaint _rest = new("Brush.Surface1", "Brush.Hairline", 1, Dashed: false, "Brush.Ink", "Brush.InkMuted");
    private static readonly GraphTilePaint _hover = new("Brush.Surface2", "Brush.WireHi", 1, Dashed: false, "Brush.Ink", "Brush.InkMuted");
    private static readonly GraphTilePaint _error = new("Brush.ErrorSoft", "Brush.Error", 1.5, Dashed: false, "Brush.Ink", "Brush.InkMuted");
    private static readonly GraphTilePaint _disabled = new("Brush.Surface1", "Brush.Wire", 1, Dashed: true, "Brush.InkTertiary", "Brush.InkTertiary");

    private static readonly GraphRing _selected = new("Brush.Primary", 2, 2);
    private static readonly GraphRing _focus = new("Brush.PrimaryHover", 2, 4);

    /// <summary>Gets the fill, border and text of a pill in these states.</summary>
    /// <param name="states">The states it is in.</param>
    /// <returns>The paint.</returns>
    internal static GraphTilePaint Paint(GraphTileStates states) =>
        states.HasFlag(GraphTileStates.Disabled) ? _disabled
        : states.HasFlag(GraphTileStates.Error) ? _error
        : states.HasFlag(GraphTileStates.Hover) ? _hover
        : _rest;

    /// <summary>Gets the rings drawn round a pill in these states, the inner one first.</summary>
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
