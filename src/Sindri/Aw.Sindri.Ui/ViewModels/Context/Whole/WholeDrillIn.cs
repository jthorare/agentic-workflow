#pragma warning disable MA0182 // red stub for #3674: the fix commits use it
namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The drill-in's limits (#3674, meta's decisions): what the canvas may draw, when the bands fit, how far the map zooms out.</summary>
internal static class WholeDrillIn
{
    /// <summary>The most nodes the canvas draws.</summary>
    internal const int CanvasNodeCap = 40;

    /// <summary>The smallest zoom: 50%.</summary>
    internal const double MinZoom = 0.5;

    /// <summary>The window width from which the Core band draws two columns.</summary>
    internal const double TwoCoreColumnsFrom = 1390;

    /// <summary>The window width from which the bands draw at all (scope spec 6.1: 858 px of bands plus the 280 px rail).</summary>
    internal const double BandsFrom = 1138;

    /// <summary>What the stage shows for an area of this many nodes.</summary>
    /// <param name="nodes">The nodes on the page.</param>
    /// <returns>The canvas up to the cap, the list above it.</returns>
    internal static WholeStage StageFor(int nodes) => throw new NotSupportedException("#3674");

    /// <summary>How many columns the Core band draws at a window width.</summary>
    /// <param name="width">The window width in px.</param>
    /// <returns>2 from 1390, 1 from 1138, and 0 below that: the list.</returns>
    internal static int CoreColumns(double width) => throw new NotSupportedException("#3674");

    /// <summary>The band a node kind sits in on an area's canvas.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>Entry points for route, job and middleware; Data and effects for table, schema, event and migration; Core for everything else.</returns>
    internal static AreaBand BandOf(string kind) => throw new NotSupportedException("#3674");

    /// <summary>The Core sub-group a node sits in.</summary>
    /// <param name="label">The node's label.</param>
    /// <returns>Requests for a label ending in Request, Responses for one ending in Response, else Services and rules.</returns>
    internal static CoreGroup CoreGroupOf(string label) => throw new NotSupportedException("#3674");
}
