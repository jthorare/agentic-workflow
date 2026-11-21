namespace Aw.Sindri.Ui.ViewModels.Workflows;

#pragma warning disable MA0182 // the canvas takes it in the next commits of #3684
/// <summary>
/// Places the server's layers into rows that wrap at the window's width and finds the zoom that fits the whole graph (#3684). A pure function: no Avalonia,
/// no server positions changed. Fit never goes above 100 % and never below the zoom where card text is 12 px.
/// </summary>
internal static class GraphFit
{
    /// <summary>The smallest card text Fit allows, in pixels.</summary>
    internal const double MinTextPx = 12;

    /// <summary>The card text's size at 100 % zoom and 100 % text size, in pixels.</summary>
    internal const double CardTextPx = 14;

    /// <summary>The lowest Fit zoom: the zoom at which card text is <see cref="MinTextPx"/>.</summary>
    /// <param name="textScale">The text size setting as a factor (1 = 100 %, 1.3 = 130 %).</param>
    /// <returns>The zoom, rounded up to a whole percent.</returns>
    internal static double Floor(double textScale) => throw new NotSupportedException();

    /// <summary>Lays the layers out and picks the Fit zoom.</summary>
    /// <param name="input">The layers.</param>
    /// <param name="canvasWidth">The canvas clip's width in pixels.</param>
    /// <param name="canvasHeight">The canvas clip's height in pixels (the zoom band is not part of it).</param>
    /// <param name="textScale">The text size setting as a factor.</param>
    /// <returns>The layout and its Fit zoom.</returns>
    internal static GraphFitResult Compute(GraphFitInput input, double canvasWidth, double canvasHeight, double textScale) => throw new NotSupportedException();
}
