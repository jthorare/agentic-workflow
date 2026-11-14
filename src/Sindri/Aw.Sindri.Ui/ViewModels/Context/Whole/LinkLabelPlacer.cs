#pragma warning disable MA0182 // red stub for #3678: the fix commit uses it
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
    internal static WholeRect? Place(WholeRect from, WholeRect to, double width, double height, IReadOnlyList<WholeRect> nodes) => throw new NotSupportedException("#3678");
}
