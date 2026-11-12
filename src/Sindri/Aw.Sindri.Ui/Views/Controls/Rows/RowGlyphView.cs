using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The 14 px state glyph of a row, drawn in a 16 px box (#2978 grammar). Colour means status only.</summary>
internal sealed class RowGlyphView : Control
{
    /// <summary>The glyph to draw.</summary>
    public static readonly StyledProperty<RowGlyph> GlyphProperty = AvaloniaProperty.Register<RowGlyphView, RowGlyph>(nameof(Glyph));

    private const double Radius = 5.5;
    private const double Stroke = 1.5;
    private static readonly Point _centre = new(8, 8);

    private static readonly Dictionary<RowGlyph, Action<DrawingContext, Palette>> _painters = new()
    {
        [RowGlyph.Todo] = (g, p) => Ring(g, p.Subtle),
        [RowGlyph.Doing] = (g, p) => { Ring(g, p.Primary); Pie(g, p.Primary, sweepDegrees: 180); },
        [RowGlyph.Done] = (g, p) => { Disc(g, p.Ok, Radius); Check(g); },
        [RowGlyph.Draft] = (g, p) => Dashed(g, p.Subtle, dash: 2, gap: 2),
        [RowGlyph.InReview] = (g, p) => { Ring(g, p.Primary); Pie(g, p.Primary, sweepDegrees: 270); },
        [RowGlyph.NeedsYou] = (g, p) => Diamond(g, p.Attention),
        [RowGlyph.Failed] = (g, p) => Cross(g, p.Error),
        [RowGlyph.Parked] = (g, p) => { Ring(g, p.Attention); Bars(g, p.Attention); },
        [RowGlyph.AgentRunning] = (g, p) => Arc(g, p.Primary, startDegrees: -90, sweepDegrees: 270),
        [RowGlyph.Message] = (g, p) => Bubble(g, p.Subtle),
        [RowGlyph.Mention] = (g, p) => Disc(g, p.Primary, radius: 3),
        [RowGlyph.PrOpen] = (g, p) => { Ring(g, p.Ok); Disc(g, p.Ok, radius: 2); },
        [RowGlyph.PrDraft] = (g, p) => { Ring(g, p.Subtle); Disc(g, p.Subtle, radius: 2); },
        [RowGlyph.Merged] = (g, p) => Disc(g, p.Violet, Radius),
        [RowGlyph.CiRunning] = (g, p) => Dashed(g, p.Attention, dash: 1, gap: 2),
        [RowGlyph.Linked] = (g, p) => { Ring(g, p.Subtle); Disc(g, p.Subtle, radius: 1.5); },
    };

    static RowGlyphView()
    {
        AffectsRender<RowGlyphView>(GlyphProperty);
    }

    /// <summary>The glyph to draw.</summary>
    public RowGlyph Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(width: 16, height: 16);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_painters.TryGetValue(Glyph, out var paint))
        {
            paint(context, new Palette(Find("Brush.InkSubtle"), Find("Brush.Primary"), Find("Brush.Success"), Find("Brush.Attention"), Find("Brush.Error"), Find("Brush.Violet")));
        }
    }

    private IBrush Find(string key) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Gray;

    private static void Ring(DrawingContext g, IBrush brush) => g.DrawEllipse(brush: null, new Pen(brush, Stroke), _centre, Radius, Radius);

    private static void Disc(DrawingContext g, IBrush brush, double radius) => g.DrawEllipse(brush, pen: null, _centre, radius, radius);

    private static void Dashed(DrawingContext g, IBrush brush, double dash, double gap) =>
        g.DrawEllipse(brush: null, new Pen(brush, Stroke, new DashStyle([dash, gap], offset: 0)), _centre, Radius, Radius);

    private static void Line(DrawingContext g, IBrush brush, Point from, Point to) => g.DrawLine(new Pen(brush, Stroke, lineCap: PenLineCap.Round), from, to);

    private static Point PointAt(double degrees, double radius)
    {
        var rad = degrees * Math.PI / 180;
        return new Point(_centre.X + (radius * Math.Cos(rad)), _centre.Y + (radius * Math.Sin(rad)));
    }

    private static void Pie(DrawingContext g, IBrush brush, double sweepDegrees)
    {
        var inner = Radius - 2;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(_centre, isFilled: true);
            ctx.LineTo(PointAt(-90, inner));
            ctx.ArcTo(PointAt(-90 + sweepDegrees, inner), new Size(inner, inner), rotationAngle: 0, isLargeArc: sweepDegrees > 180, SweepDirection.Clockwise);
            ctx.EndFigure(isClosed: true);
        }

        g.DrawGeometry(brush, pen: null, geometry);
    }

    private static void Arc(DrawingContext g, IBrush brush, double startDegrees, double sweepDegrees)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointAt(startDegrees, Radius), isFilled: false);
            ctx.ArcTo(PointAt(startDegrees + sweepDegrees, Radius), new Size(Radius, Radius), rotationAngle: 0, isLargeArc: sweepDegrees > 180, SweepDirection.Clockwise);
            ctx.EndFigure(isClosed: false);
        }

        g.DrawGeometry(brush: null, new Pen(brush, Stroke, lineCap: PenLineCap.Round), geometry);
    }

    private static void Check(DrawingContext g)
    {
        Line(g, Brushes.Black, new Point(5.5, 8), new Point(7.5, 10));
        Line(g, Brushes.Black, new Point(7.5, 10), new Point(10.5, 6));
    }

    private static void Cross(DrawingContext g, IBrush brush)
    {
        Line(g, brush, new Point(4.5, 4.5), new Point(11.5, 11.5));
        Line(g, brush, new Point(4.5, 11.5), new Point(11.5, 4.5));
    }

    private static void Bars(DrawingContext g, IBrush brush)
    {
        Line(g, brush, new Point(6.5, 5.5), new Point(6.5, 10.5));
        Line(g, brush, new Point(9.5, 5.5), new Point(9.5, 10.5));
    }

    private static void Diamond(DrawingContext g, IBrush brush) => Polygon(g, brush, filled: true, new Point(8, 2), new Point(14, 8), new Point(8, 14), new Point(2, 8));

    // A bubble with a tail, never a box: x selects, so a box reads as a checkbox.
    private static void Bubble(DrawingContext g, IBrush brush) =>
        Polygon(g, brush, filled: false, new Point(3, 4), new Point(13, 4), new Point(13, 10), new Point(8, 10), new Point(5, 13), new Point(5, 10), new Point(3, 10));

    private static void Polygon(DrawingContext g, IBrush brush, bool filled, params Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], filled);
            foreach (var point in points.Skip(1))
            {
                ctx.LineTo(point);
            }

            ctx.EndFigure(isClosed: true);
        }

        g.DrawGeometry(filled ? brush : null, filled ? null : new Pen(brush, Stroke, lineJoin: PenLineJoin.Round), geometry);
    }

    private sealed record Palette(IBrush Subtle, IBrush Primary, IBrush Ok, IBrush Attention, IBrush Error, IBrush Violet);
}
