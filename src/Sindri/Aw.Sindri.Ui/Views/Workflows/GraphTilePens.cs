using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// The paint of a graph step tile in each state (#3234): hover, a focus ring separate from the selected ring, disabled, error. Pure data (brush keys, widths, gaps):
/// the canvas resolves the keys from the theme and draws them. Stub: the paints come with the green commit.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "The canvas adopts it when ui wires the states (#3234).")]
internal static class GraphTilePens
{
    /// <summary>Gets the fill, border and text of a tile in these states.</summary>
    /// <param name="states">The states it is in.</param>
    /// <returns>The paint.</returns>
    internal static GraphTilePaint Paint(GraphTileStates states) => throw new NotSupportedException($"stub: {states}");

    /// <summary>Gets the rings drawn round a tile in these states, the inner one first.</summary>
    /// <param name="states">The states it is in.</param>
    /// <returns>The rings.</returns>
    internal static IReadOnlyList<GraphRing> Rings(GraphTileStates states) => throw new NotSupportedException($"stub: {states}");
}
