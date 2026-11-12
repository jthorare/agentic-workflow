using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The paint of a branch pill or a port on the graph canvas in each state (#3234). Stub: the paints come with the green commit.</summary>
[SuppressMessage("Design", "MA0182", Justification = "The canvas adopts it when ui wires the states (#3234).")]
internal static class GraphPillPens
{
    /// <summary>Gets the fill, border and text of a pill in these states.</summary>
    /// <param name="states">The states it is in.</param>
    /// <returns>The paint.</returns>
    internal static GraphTilePaint Paint(GraphTileStates states) => throw new NotSupportedException($"stub: {states}");

    /// <summary>Gets the rings drawn round a pill in these states, the inner one first.</summary>
    /// <param name="states">The states it is in.</param>
    /// <returns>The rings.</returns>
    internal static IReadOnlyList<GraphRing> Rings(GraphTileStates states) => throw new NotSupportedException($"stub: {states}");
}
