using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>The stage's dotted grid: a 22 px tile that scales and moves with the view.</summary>
internal sealed partial class ZoomBorder
{
    private const double DotPitch = 22;

    // The 22 px dot grid in HairlineStrong (each dot centred on the pixel at 11,11 so it paints that pixel whole, not a quarter of it), in a tile that scales and moves with the view, over a transparent base so the pointer still lands here.
    private DrawingBrush Dots()
    {
        var dot = this.TryFindResource("Brush.HairlineStrong", ActualThemeVariant, out var found) && found is IBrush brush ? brush : Brushes.Gray;
        var tile = new Rect(0, 0, DotPitch, DotPitch);
        var drawing = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(tile) },
                new GeometryDrawing { Brush = dot, Geometry = new EllipseGeometry(new Rect((DotPitch / 2) - 0.5, (DotPitch / 2) - 0.5, 2, 2)) },
            },
        };
        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            SourceRect = new RelativeRect(tile, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(tile, RelativeUnit.Absolute),
        };
    }
}
