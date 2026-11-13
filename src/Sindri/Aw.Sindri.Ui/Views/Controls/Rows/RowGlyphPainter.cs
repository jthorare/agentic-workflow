using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>Draws the 14 px state glyph of a row into a 16 px box at the origin (#2978 grammar, #3189). Colour means status only. Geometry, pens and palettes are built once and kept.</summary>
internal static class RowGlyphPainter
{
    private static long _allocations;
    private static readonly Lazy<StreamGeometry> _pie180 = new(() => BuildPie(180));
    private static readonly Lazy<StreamGeometry> _pie270 = new(() => BuildPie(270));
    private static readonly Lazy<StreamGeometry> _arc = new(() => Build(ctx =>
    {
        ctx.BeginFigure(PointAt(-90, Radius), isFilled: false);
        ctx.ArcTo(PointAt(180, Radius), new Size(Radius, Radius), rotationAngle: 0, isLargeArc: true, SweepDirection.Clockwise);
        ctx.EndFigure(isClosed: false);
    }));

    private static readonly Lazy<StreamGeometry> _diamond = new(() => BuildPolygon(filled: true, [new Point(8, 1), new Point(15, 8), new Point(8, 15), new Point(1, 8)]));
    // The tail of the speech bubble: grammar .g.msg::after, a 5 px wide triangle skewed -20 degrees (top edge y 10.5, apex y 14.5).
    private static readonly Lazy<StreamGeometry> _tail = new(() => BuildPolygon(filled: true, [new Point(5.23, 10.5), new Point(10.23, 10.5), new Point(5.77, 14.5)]));
    private static readonly ConditionalWeakTable<IBrush, Pens> _pens = [];

    internal const double Radius = 6.25;   // ring centre line; with the 1.5 stroke the glyph is 14 outside to outside (grammar .g)
    internal const double Outer = Radius + (Stroke / 2);   // a filled glyph (done, merged) is the full 14
    internal const double Stroke = 1.5;
    private static readonly Point _centre = new(8, 8);

    private static readonly Dictionary<RowGlyph, Action<DrawingContext, Palette>> _painters = new()
    {
        [RowGlyph.Todo] = (g, p) => Ring(g, p.Subtle),
        [RowGlyph.Doing] = (g, p) => { Ring(g, p.Primary); Pie(g, p.Primary, sweepDegrees: 180); },
        [RowGlyph.Done] = (g, p) => { Disc(g, p.Ok, Outer); Check(g); },
        [RowGlyph.Draft] = (g, p) => Dashed(g, p.Tertiary, dash: 2),
        [RowGlyph.InReview] = (g, p) => { Ring(g, p.Primary); Pie(g, p.Primary, sweepDegrees: 270); },
        [RowGlyph.NeedsYou] = (g, p) => Diamond(g, p.Attention),
        [RowGlyph.Failed] = (g, p) => { Disc(g, p.Error, Outer); Cross(g, p.Canvas); },
        [RowGlyph.Parked] = (g, p) => { Ring(g, p.Attention); Bars(g, p.Attention); },
        [RowGlyph.AgentRunning] = (g, p) => Arc(g, p.Primary),
        [RowGlyph.Message] = (g, p) => Bubble(g, p.Subtle),
        [RowGlyph.Mention] = (g, p) => Disc(g, p.Primary, radius: 4),
        [RowGlyph.PrOpen] = (g, p) => { Ring(g, p.Ok); Disc(g, p.Ok, radius: 2); },
        [RowGlyph.PrDraft] = (g, p) => { Ring(g, p.Tertiary); Disc(g, p.Tertiary, radius: 2); },
        [RowGlyph.Merged] = (g, p) => Disc(g, p.Violet, Outer),
        [RowGlyph.CiRunning] = (g, p) => Dashed(g, p.Attention, dash: 1),
        [RowGlyph.Linked] = (g, p) => Chain(g, p.Subtle),
    };

    /// <summary>How many geometries, pens and palettes the glyphs ever built. They are cached, so scrolling past thousands of rows does not move it.</summary>
    internal static long Allocations => Interlocked.Read(ref _allocations);

    /// <summary>The palette of a control under its current theme.</summary>
    /// <param name="owner">The control whose resources and theme give the brushes.</param>
    /// <returns>The palette; keep it until the theme changes.</returns>
    internal static Palette PaletteFor(Control owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _ = Interlocked.Increment(ref _allocations);
        return new Palette(Find(owner, "Brush.InkSubtle"), Find(owner, "Brush.PrimaryHover"), Find(owner, "Brush.Success"), Find(owner, "Brush.Attention"), Find(owner, "Brush.Error"), Find(owner, "Brush.Violet"), Find(owner, "Brush.Canvas"), Find(owner, "Brush.InkTertiary"));
    }

    /// <summary>Draws a glyph in the 16 px box at the origin of the context (translate first to place it).</summary>
    /// <param name="g">The context.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="palette">The brushes.</param>
    internal static void Paint(DrawingContext g, RowGlyph glyph, Palette palette)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(palette);
        if (_painters.TryGetValue(glyph, out var paint))
        {
            paint(g, palette);
        }
    }

    private static IBrush Find(Control owner, string key) => owner.TryFindResource(key, owner.ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Gray;

    private static Pens PensFor(IBrush brush) => _pens.GetValue(brush, b =>
    {
        _ = Interlocked.Increment(ref _allocations);
        return new Pens(new Pen(b, Stroke), new Pen(b, Stroke, lineCap: PenLineCap.Round), new Pen(b, Stroke, lineJoin: PenLineJoin.Round), new Pen(b, Stroke, new DashStyle([2, 2], offset: 0)), new Pen(b, Stroke, new DashStyle([1, 2], offset: 0)));
    });

    // A geometry is built once per shape, when the first glyph draws, and kept: the glyph only ever draws these few.
    private static StreamGeometry Build(Action<StreamGeometryContext> build)
    {
        _ = Interlocked.Increment(ref _allocations);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            build(ctx);
        }

        return geometry;
    }

    private static StreamGeometry BuildPie(double sweepDegrees) => Build(ctx =>
    {
        var inner = Radius - 2;
        ctx.BeginFigure(_centre, isFilled: true);
        ctx.LineTo(PointAt(-90, inner));
        ctx.ArcTo(PointAt(-90 + sweepDegrees, inner), new Size(inner, inner), rotationAngle: 0, isLargeArc: sweepDegrees > 180, SweepDirection.Clockwise);
        ctx.EndFigure(isClosed: true);
    });

    private static StreamGeometry BuildPolygon(bool filled, Point[] points) => Build(ctx =>
    {
        ctx.BeginFigure(points[0], filled);
        for (var i = 1; i < points.Length; i++)
        {
            ctx.LineTo(points[i]);
        }

        ctx.EndFigure(isClosed: true);
    });

    private static void Ring(DrawingContext g, IBrush brush) => g.DrawEllipse(brush: null, PensFor(brush).Solid, _centre, Radius, Radius);

    private static void Disc(DrawingContext g, IBrush brush, double radius) => g.DrawEllipse(brush, pen: null, _centre, radius, radius);

    private static void Dashed(DrawingContext g, IBrush brush, double dash) =>
        g.DrawEllipse(brush: null, dash > 1 ? PensFor(brush).DashedLong : PensFor(brush).DashedShort, _centre, Radius, Radius);

    private static void Line(DrawingContext g, IBrush brush, Point from, Point to) => g.DrawLine(PensFor(brush).Round, from, to);

    private static Point PointAt(double degrees, double radius)
    {
        var rad = degrees * Math.PI / 180;
        return new Point(_centre.X + (radius * Math.Cos(rad)), _centre.Y + (radius * Math.Sin(rad)));
    }

    private static void Pie(DrawingContext g, IBrush brush, double sweepDegrees) => g.DrawGeometry(brush, pen: null, sweepDegrees > 180 ? _pie270.Value : _pie180.Value);

    // The one arc glyph: AgentRunning, a ring open at the top-left.
    private static void Arc(DrawingContext g, IBrush brush) => g.DrawGeometry(brush: null, PensFor(brush).Round, _arc.Value);

    private static void Check(DrawingContext g)
    {
        Line(g, Brushes.Black, new Point(5.5, 8), new Point(7.5, 10));
        Line(g, Brushes.Black, new Point(7.5, 10), new Point(10.5, 6));
    }

    private static void Cross(DrawingContext g, IBrush brush)
    {
        // Two 8 px lines at +-45 degrees through the centre: half-length 4 -> 2.83 on each axis.
        Line(g, brush, new Point(8 - 2.83, 8 - 2.83), new Point(8 + 2.83, 8 + 2.83));
        Line(g, brush, new Point(8 - 2.83, 8 + 2.83), new Point(8 + 2.83, 8 - 2.83));
    }

    private static void Bars(DrawingContext g, IBrush brush)
    {
        Line(g, brush, new Point(6.5, 5.5), new Point(6.5, 10.5));
        Line(g, brush, new Point(9.5, 5.5), new Point(9.5, 10.5));
    }

    private static void Diamond(DrawingContext g, IBrush brush) => g.DrawGeometry(brush, pen: null, _diamond.Value);

    // A bubble with a tail, never a box: x selects, so a box reads as a checkbox. Outer edge x 2..15, y 2..12, corner radius 5; the stroke centre line is 0.75 in.
    private static void Bubble(DrawingContext g, IBrush brush)
    {
        g.DrawRectangle(brush: null, PensFor(brush).Solid, new Rect(2.75, 2.75, 11.5, 8.5), radiusX: 4.25, radiusY: 4.25);
        g.DrawGeometry(brush, pen: null, _tail.Value);
    }

    // Linked is two chained links (grammar .g.linked, #3581): each a 7 x 4.5 rounded rect of stroke centre line, radius 2.25 (an 8.5 x 6 border box,
    // a full pill), turned -45 degrees about its own centre. In the 16 px box the centres are (6.25, 9.75) and (9.75, 6.25); the chain spans 11.26 px.
    private static void Chain(DrawingContext g, IBrush brush)
    {
        var pen = PensFor(brush).Solid;
        foreach (var centre in (ReadOnlySpan<Point>)[new Point(6.25, 9.75), new Point(9.75, 6.25)])
        {
            using (g.PushTransform(Matrix.CreateRotation(-Math.PI / 4) * Matrix.CreateTranslation(centre.X, centre.Y)))
            {
                g.DrawRectangle(brush: null, pen, new Rect(-3.5, -2.25, 7, 4.5), radiusX: 2.25, radiusY: 2.25);
            }
        }
    }

    private sealed record Pens(Pen Solid, Pen Round, Pen RoundJoin, Pen DashedLong, Pen DashedShort);

    /// <summary>The brushes the glyphs draw with: subtle ink, primary, ok, attention, error, violet, canvas and tertiary ink.</summary>
    /// <param name="Subtle">Todo, message and linked.</param>
    /// <param name="Primary">Doing, in review, running and mention.</param>
    /// <param name="Ok">Done and PR open.</param>
    /// <param name="Attention">Needs you, parked and CI running.</param>
    /// <param name="Error">Failed disc.</param>
    /// <param name="Violet">Merged.</param>
    /// <param name="Canvas">The x inside the failed disc.</param>
    /// <param name="Tertiary">Draft and PR draft.</param>
    internal sealed record Palette(IBrush Subtle, IBrush Primary, IBrush Ok, IBrush Attention, IBrush Error, IBrush Violet, IBrush Canvas, IBrush Tertiary);
}
