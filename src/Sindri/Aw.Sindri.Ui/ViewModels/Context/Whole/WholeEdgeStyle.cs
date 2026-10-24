namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>How one edge is drawn (q86): solid when confirmed, dashed when suggested, with the words of its pill.</summary>
/// <param name="Dashed">True for a suggested edge.</param>
/// <param name="Pill">The pill's words.</param>
internal sealed record WholeEdgeStyle(bool Dashed, string Pill)
{
    /// <summary>The look of a node edge.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>Its style.</returns>
    internal static WholeEdgeStyle Of(PlacedEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        return new WholeEdgeStyle(!edge.Confirmed, Labels.ContextWholeEdgePill(edge.Type, edge.Confirmed, edge.Confidence));
    }

    /// <summary>The look of a bundled line between two areas: dashed unless every edge in it is confirmed.</summary>
    /// <param name="bundle">The bundle.</param>
    /// <returns>Its style.</returns>
    internal static WholeEdgeStyle Of(WholeAreaBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        return new WholeEdgeStyle(bundle.Confirmed < bundle.Count, Labels.ContextWholeBundle(bundle.Count));
    }
}
