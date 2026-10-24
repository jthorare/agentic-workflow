using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The whole map's single drawing pass (<see cref="WholeMapCanvas"/>, continued).</summary>
internal sealed partial class WholeMapCanvas
{
    private const double Radius = 6;
    private const double DotRadius = 5;
    private const double TextSize = 12;
    private const double AreaLabelSize = 16;
    private const double EdgeFade = 0.35;   // the mockup's faded edges while a node is hovered or pinned
    private const double Behind = 0.42;   // what is not part of the answer stays drawn, dimmed (whole-map spec §3.1)

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Map?.Geometry is not { } geometry)
        {
            return;
        }

        var view = Viewport.Width > 0 ? new WholeRect(Viewport.X, Viewport.Y, Viewport.Width, Viewport.Height) : geometry.World;
        var dots = WholeMapViewModel.UsesDots(Scale);
        DrawLanes(context, geometry, view, dots);
        if (geometry.Tiles.Count > 0)
        {
            DrawTiles(context, geometry, view);
        }

        if (geometry.Nodes.Count > 0 || geometry.Columns.Count > 0)
        {
            DrawNodeEdges(context, geometry, view);
            DrawHeaders(context, geometry, view, dots);
            DrawNodes(context, geometry, view, dots);
        }
    }

    // Behind a search answer what is not in it stays drawn, dimmed; a highlight dims what is not linked to it, as far as it has faded.
    private double EdgeOpacity(PlacedEdge edge, bool zoomed, IReadOnlySet<string> hits) =>
        (zoomed && !(hits.Contains(edge.From) && hits.Contains(edge.To)) ? Behind : 1) * (Touches(edge.From, edge.To) ? 1 : Faded(EdgeFade));

    private double NodeOpacity(string key, bool zoomed, IReadOnlySet<string> hits) => (zoomed && !hits.Contains(key) ? Behind : 1) * Strength(key);

    private void DrawHeaders(DrawingContext context, WholeMapGeometry geometry, WholeRect view, bool dots)
    {
        var rule = new Pen(Brush("Brush.Hairline"), 1);
        foreach (var column in geometry.Columns.Where(c => c.Rect.Intersects(view)))
        {
            var (colour, _) = WholePalette.For(column.AreaIndex);
            context.DrawLine(rule, new Point(column.Rect.X, column.Rect.Y + WholeMapLayout.HeaderHeight), new Point(column.Rect.Right, column.Rect.Y + WholeMapLayout.HeaderHeight));
            var text = Text(column.Label, dots ? AreaLabelSize / Math.Max(Scale, 0.25) : AreaLabelSize, Brush(PaletteKey(colour)), column.Rect.Width, bold: true);
            context.DrawText(text, new Point(column.Rect.X, column.Rect.Y));
        }
    }

    private void DrawNodes(DrawingContext context, WholeMapGeometry geometry, WholeRect view, bool dots)
    {
        var chip = Brush("Brush.Surface2");
        var ink = Brush("Brush.Ink");
        var hits = Map?.HitKeys ?? new HashSet<string>(StringComparer.Ordinal);
        var zoomed = Map?.IsZoomed == true;
        foreach (var node in geometry.Nodes.Where(n => n.Rect.Intersects(view)))
        {
            var (colour, pattern) = WholePalette.For(node.AreaIndex);
            var area = Brush(PaletteKey(colour));
            using (context.PushOpacity(NodeOpacity(node.Key, zoomed, hits)))
            {
                if (dots)
                {
                    DrawDot(context, node, area, pattern);
                    continue;
                }

                DrawKindChip(context, new KindChip(node, area, chip, ink, string.Equals(node.Key, _focus, StringComparison.Ordinal)));
            }
        }
    }

    // A dot in its area's colour; the pattern that tells a reused colour apart is a ring or a cross over it.
    private static void DrawDot(DrawingContext context, PlacedNode node, IBrush fill, WholePattern pattern)
    {
        var centre = new Point(node.Rect.CenterX, node.Rect.CenterY);
        context.DrawEllipse(fill, pen: null, centre, radiusX: DotRadius, radiusY: DotRadius);
        if (pattern == WholePattern.Ringed)
        {
            context.DrawEllipse(brush: null, new Pen(fill, 1), centre, radiusX: DotRadius + 2.5, radiusY: DotRadius + 2.5);
        }
        else if (pattern == WholePattern.Hatched)
        {
            context.DrawLine(new Pen(Brushes.Black, 1), new Point(centre.X - DotRadius, centre.Y + DotRadius), new Point(centre.X + DotRadius, centre.Y - DotRadius));
        }
    }

    private void DrawTiles(DrawingContext context, WholeMapGeometry geometry, WholeRect view)
    {
        var muted = Brush("Brush.InkSubtle");
        DrawAreaBundles(context, geometry);

        foreach (var placed in geometry.Tiles.Where(t => t.Rect.Intersects(view)))
        {
            using (context.PushOpacity(Strength(placed.Key)))
            {
                DrawTileColumn(context, placed, string.Equals(placed.Key, _focus, StringComparison.Ordinal));
            }
        }

        DrawBundlePills(context, geometry);
    }

    // The mark that tells an area apart beyond its colour: a solid bar, a hatched one, or a ringed one.
    private static void DrawSwatch(DrawingContext context, Rect tile, IBrush area, WholePattern pattern)
    {
        var mark = new Rect(tile.X + 10, tile.Y + 12, 18, 18);
        context.DrawRectangle(area, pen: null, mark, radiusX: 4, radiusY: 4);
        if (pattern == WholePattern.Hatched)
        {
            context.DrawLine(new Pen(Brushes.Black, 1.5), mark.BottomLeft, mark.TopRight);
        }
        else if (pattern == WholePattern.Ringed)
        {
            context.DrawEllipse(Brushes.Black, pen: null, mark.Center, radiusX: 4, radiusY: 4);
        }
    }

    private static bool Overlaps(WholeRect view, double x1, double y1, double x2, double y2) =>
        Math.Max(x1, x2) >= view.X && Math.Min(x1, x2) <= view.Right && Math.Max(y1, y2) >= view.Y && Math.Min(y1, y2) <= view.Bottom;

    private static string PaletteKey(int colour) => string.Create(CultureInfo.InvariantCulture, $"Brush.Area{colour + 1}");

    private static FormattedText Text(string text, double size, IBrush brush, double width, bool bold = false) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, bold ? FontWeight.SemiBold : FontWeight.Normal), size, brush)
        {
            MaxTextWidth = Math.Max(1, width),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}
