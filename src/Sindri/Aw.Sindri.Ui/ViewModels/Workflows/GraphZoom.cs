namespace Aw.Sindri.Ui.ViewModels.Workflows;


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

    /// <summary>How many times the canvas the content must be, on width or on height, before the minimap shows (#4055): a map for a graph that scrolls a few hundred pixels helps nobody and only covers cards.</summary>
    internal const double MapShowsPast = 2;

    /// <summary>The right gutter kept free of cards while the map shows (#4149): the map's width and its margin on both sides.</summary>
    internal const double MapGutter = MapWidth + (2 * MapMargin);

    /// <summary>The map's gutter on a canvas this wide (#4191).</summary>
    /// <param name="clipWidth">The canvas clip's width.</param>
    /// <returns>The gutter's width.</returns>
    internal static double MapGutterFor(double clipWidth) => Math.Min(MapGutter, clipWidth);   // red stub: no half-canvas cap yet

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

    /// <summary>True when the world at this zoom is larger than the canvas clip, so the canvas scrolls (the minimap needs more: <see cref="MapShown"/>).</summary>
    /// <param name="worldWidth">The world's width.</param>
    /// <param name="worldHeight">The world's height.</param>
    /// <param name="zoom">The zoom as a factor.</param>
    /// <param name="clip">The canvas clip's size.</param>
    /// <returns>True when the world overflows the clip.</returns>
    internal static bool Scrolls(double worldWidth, double worldHeight, double zoom, (double Width, double Height) clip) =>
        (worldWidth * zoom) > clip.Width + 0.5 || (worldHeight * zoom) > clip.Height + 0.5;

    /// <summary>True when the minimap shows (#4055): the world at this zoom is over <see cref="MapShowsPast"/> times the canvas on width or on height.</summary>
    /// <param name="worldWidth">The world's width.</param>
    /// <param name="worldHeight">The world's height.</param>
    /// <param name="zoom">The zoom as a factor.</param>
    /// <param name="clip">The canvas clip's size.</param>
    /// <returns>True when the map shows.</returns>
    internal static bool MapShown(double worldWidth, double worldHeight, double zoom, (double Width, double Height) clip) =>
        (worldWidth * zoom) > MapShowsPast * clip.Width || (worldHeight * zoom) > MapShowsPast * clip.Height;

    /// <summary>The height of the scrolling area: the world at this zoom, plus the map's height and margin at the bottom while the map shows (never on the right).</summary>
    /// <param name="worldHeight">The world's height.</param>
    /// <param name="zoom">The zoom as a factor.</param>
    /// <param name="mapShown">Whether the minimap shows.</param>
    /// <returns>The scroll extent's height.</returns>
    internal static double ExtentHeight(double worldHeight, double zoom, bool mapShown) =>
        (worldHeight * zoom) + (mapShown ? MapHeight + MapMargin : 0);

    /// <summary>The width of the scrolling area: the world at this zoom, plus the map's gutter on the right while the map shows (#4149), so a card under the map
    /// can be scrolled clear of it. The gutter is at most half the canvas: then a zoom step about the centre from the right limit stays inside the new range.</summary>
    /// <param name="worldWidth">The world's width.</param>
    /// <param name="zoom">The zoom as a factor.</param>
    /// <param name="mapShown">Whether the minimap shows.</param>
    /// <param name="clipWidth">The canvas clip's width.</param>
    /// <returns>The scroll extent's width.</returns>
    internal static double ExtentWidth(double worldWidth, double zoom, bool mapShown, double clipWidth) =>
        (worldWidth * zoom) + (mapShown ? Math.Min(MapGutter, clipWidth / 2) : 0);

    /// <summary>Holds a pan inside the scroll extent: the world at this zoom, plus the map's height and margin at the bottom while the map shows; no pan past it, and none at all along an axis that fits.</summary>
    /// <param name="pan">Where the world's origin sits in the canvas.</param>
    /// <param name="world">The world's size.</param>
    /// <param name="zoom">The zoom as a factor.</param>
    /// <param name="clip">The canvas clip's size.</param>
    /// <returns>The pan, no further than the extent allows.</returns>
    internal static (double X, double Y) ClampPan((double X, double Y) pan, (double Width, double Height) world, double zoom, (double Width, double Height) clip)
    {
        var mapShown = MapShown(world.Width, world.Height, zoom, clip);   // the map's room exists only while the map shows
        var (extentWidth, extentHeight) = (ExtentWidth(world.Width, zoom, mapShown, clip.Width), ExtentHeight(world.Height, zoom, mapShown));
        static double Axis(double value, double extent, double clip) => extent <= clip + 0.5 ? 0 : Math.Clamp(value, clip - extent, 0);
        return (Axis(pan.X, extentWidth, clip.Width), Axis(pan.Y, extentHeight, clip.Height));
    }

    /// <summary>The smallest height of the canvas panel.</summary>
    internal const double MinStageHeight = 320;

    /// <summary>The canvas panel's height on a page that scrolls: what is left of the page's viewport under the panel's top and above a gap, never under <see cref="MinStageHeight"/>.</summary>
    /// <param name="viewportHeight">The scroll viewer's visible height.</param>
    /// <param name="top">The panel's top inside the page.</param>
    /// <param name="gap">The space left under the panel.</param>
    /// <returns>The panel's height.</returns>
    internal static double StageHeight(double viewportHeight, double top, double gap) => Math.Max(MinStageHeight, viewportHeight - top - gap);
}
