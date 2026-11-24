using System.Globalization;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>A lane's heading (<see cref="WholeMapCanvas"/>, continued; #3998): the band's title, then what the band holds, as wide as the lane leaves it.</summary>
internal sealed partial class WholeMapCanvas
{
    private const double LaneTitleWidth = 240;
    private const double LaneMetaGap = 12;
    private const double WideOpen = 100_000;

    /// <summary>Whether a band's heading is drawn whole at the zoom the map is drawn at (the canvas draws text, no control, so the frames tests ask here).</summary>
    /// <param name="band">The band.</param>
    /// <returns>True when the band has a heading and none of it is cut.</returns>
    internal bool LaneMetaWhole(WholeBand band)
    {
        if (Map?.Shown is not { } geometry || geometry.Bands.FirstOrDefault(b => b.Band == band) is not { Meta.Length: > 0 } placed)
        {
            return false;
        }

        var muted = Brush("Brush.InkSubtle");
        var size = LaneSize(WholeMapViewModel.UsesDots(Scale));
        var drawn = LaneMeta(geometry, placed, LaneTitle(placed, size, muted), size, muted);
        var whole = Text(placed.Meta, size * 0.9, muted, WideOpen);
        return drawn.Width >= whole.Width - 0.5;
    }

    /// <summary>The numbers behind <see cref="LaneMetaWhole"/>, for a test's failure message: the drawn width, the natural width and the room.</summary>
    /// <param name="band">The band.</param>
    /// <returns>"drawn D, natural N, room R", or empty when the band has no heading.</returns>
    internal string LaneMetaNumbers(WholeBand band)
    {
        if (Map?.Shown is not { } geometry || geometry.Bands.FirstOrDefault(b => b.Band == band) is not { Meta.Length: > 0 } placed)
        {
            return string.Empty;
        }

        var muted = Brush("Brush.InkSubtle");
        var size = LaneSize(WholeMapViewModel.UsesDots(Scale));
        var title = LaneTitle(placed, size, muted);
        var drawn = LaneMeta(geometry, placed, title, size, muted);
        var whole = Text(placed.Meta, size * 0.9, muted, WideOpen);
        var room = WholeMapLayout.LaneMetaWidth(geometry.World.Width, LaneMetaX(title));
        return string.Create(CultureInfo.InvariantCulture, $"drawn {drawn.Width:0.#}, natural {whole.Width:0.#}, room {room:0.#}, title {title.WidthIncludingTrailingWhitespace:0.#}");
    }

    private double LaneSize(bool dots) => dots ? LaneTitleSize / Math.Max(Scale, 0.25) : LaneTitleSize;

    private static FormattedText LaneTitle(PlacedBand band, double size, IBrush accent) => Text(NodeBands.Title(band.Band), size, accent, LaneTitleWidth, bold: true);

    private static double LaneMetaX(FormattedText title) => (WholeMapLayout.Margin / 2) + title.WidthIncludingTrailingWhitespace + LaneMetaGap;

    // The words run from after the title to the lane's right end, which is the world's (the lane spans it).
    private static FormattedText LaneMeta(WholeMapGeometry geometry, PlacedBand band, FormattedText title, double size, IBrush muted) =>
        Text(band.Meta, size * 0.9, muted, WholeMapLayout.LaneMetaWidth(geometry.World.Width, LaneMetaX(title)));
}
