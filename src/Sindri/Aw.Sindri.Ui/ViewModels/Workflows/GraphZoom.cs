namespace Aw.Sindri.Ui.ViewModels.Workflows;

#pragma warning disable MA0182 // the stage takes it in the next commit of #3684

/// <summary>The zoom strip's steps and the canvas's scroll rules (#3684): a pure model, no Avalonia.</summary>
internal static class GraphZoom
{
    /// <summary>The height of the band under the canvas that holds Fit, minus, the percent and plus; never an overlay.</summary>
    internal const double StripHeight = 56;

    /// <summary>The minimap's width.</summary>
    internal const double MapWidth = 200;

    /// <summary>The minimap's height.</summary>
    internal const double MapHeight = 128;

    /// <summary>The minimap's distance from the canvas's right and bottom edges.</summary>
    internal const double MapMargin = 16;

    /// <summary>The zoom steps of + and -, in percent.</summary>
    internal static IReadOnlyList<int> Ladder { get; } = [25, 33, 50, 67, 75, 80, 90, 100, 110, 125, 150, 175, 200];

    /// <summary>The next step above a zoom.</summary>
    /// <param name="percent">The current zoom in percent.</param>
    /// <returns>The next ladder step, or the top one.</returns>
    internal static int Next(int percent) => Ladder.FirstOrDefault(step => step > percent, Ladder[^1]);

    /// <summary>The next step below a zoom.</summary>
    /// <param name="percent">The current zoom in percent.</param>
    /// <returns>The next ladder step down, or the bottom one.</returns>
    internal static int Previous(int percent) => Ladder.LastOrDefault(step => step < percent, Ladder[0]);

    /// <summary>True when the world at this zoom is larger than the canvas clip, so the canvas scrolls and the minimap shows.</summary>
    /// <param name="worldWidth">The world's width.</param>
    /// <param name="worldHeight">The world's height.</param>
    /// <param name="zoom">The zoom as a factor.</param>
    /// <param name="clip">The canvas clip's size.</param>
    /// <returns>True when the world overflows the clip.</returns>
    internal static bool Scrolls(double worldWidth, double worldHeight, double zoom, (double Width, double Height) clip) =>
        (worldWidth * zoom) > clip.Width + 0.5 || (worldHeight * zoom) > clip.Height + 0.5;

    /// <summary>The height of the scrolling area: the world at this zoom, plus the map's height and margin at the bottom while the map shows (never on the right).</summary>
    /// <param name="worldHeight">The world's height.</param>
    /// <param name="zoom">The zoom as a factor.</param>
    /// <param name="mapShown">Whether the minimap shows.</param>
    /// <returns>The scroll extent's height.</returns>
    internal static double ExtentHeight(double worldHeight, double zoom, bool mapShown) =>
        (worldHeight * zoom) + (mapShown ? MapHeight + MapMargin : 0);
}
