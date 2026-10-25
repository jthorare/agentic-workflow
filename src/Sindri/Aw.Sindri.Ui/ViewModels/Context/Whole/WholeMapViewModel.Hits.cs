namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A hit chosen in the hits list (<see cref="WholeMapViewModel"/>, continued): the node is pinned and centred, or its feature's tile when it is not drawn.</summary>
internal sealed partial class WholeMapViewModel
{
    /// <summary>Focuses a hit on the map.</summary>
    /// <param name="hit">The hit.</param>
    /// <returns>True when the map holds the node or its feature's tile.</returns>
    internal bool FocusHit(MapSearchHit hit) => throw new NotSupportedException();
}
