using System.Runtime.CompilerServices;
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

    private static long _allocations;
    private static readonly Lazy<StreamGeometry> _pie180 = new(() => BuildPie(180));
    private static readonly Lazy<StreamGeometry> _pie270 = new(() => BuildPie(270));
    private static readonly Lazy<StreamGeometry> _arc = new(() => Build(ctx =>
    {
        ctx.BeginFigure(PointAt(-90, Radius), isFilled: false);
        ctx.ArcTo(PointAt(180, Radius), new Size(Radius, Radius), rotationAngle: 0, isLargeArc: true, SweepDirection.Clockwise);
        ctx.EndFigure(isClosed: false);
    }));

    private static readonly Lazy<StreamGeometry> _diamond = new(() => BuildPolygon(filled: true, [new Point(8, 2), new Point(14, 8), new Point(8, 14), new Point(2, 8)]));
    private static readonly Lazy<StreamGeometry> _bubble = new(() => BuildPolygon(filled: false, [new Point(3, 4), new Point(13, 4), new Point(13, 10), new Point(8, 10), new Point(5, 13), new Point(5, 10), new Point(3, 10)]));
    private static readonly ConditionalWeakTable<IBrush, Pens> _pens = [];
    private Palette? _palette;

    private const double Radius = 5.5;
    private const double Stroke = 1.5;
    private static readonly Point _centre = new(8, 8);

    private static readonly Dictionary<RowGlyph, Action<DrawingContext, Palette>> _painters = new()
    {
        [RowGlyph.Todo] = (g, p) => Ring(g, p.Subtle),
        [RowGlyph.Doing] = (g, p) => { Ring(g, p.Primary); Pie(g, p.Primary, sweepDegrees: 180); },
        [RowGlyph.Done] = (g, p) => { Disc(g, p.Ok, Radius); Check(g); },
        [RowGlyph.Draft] = (g, p) => Dashed(g, p.Subtle, dash: 2),
        [RowGlyph.InReview] = (g, p) => { Ring(g, p.Primary); Pie(g, p.Primary, sweepDegrees: 270); },
        [RowGlyph.NeedsYou] = (g, p) => Diamond(g, p.Attention),
        [RowGlyph.Failed] = (g, p) => Cross(g, p.Error),
        [RowGlyph.Parked] = (g, p) => { Ring(g, p.Attention); Bars(g, p.Attention); },
        [RowGlyph.AgentRunning] = (g, p) => Arc(g, p.Primary),
        [RowGlyph.Message] = (g, p) => Bubble(g, p.Subtle),
        [RowGlyph.Mention] = (g, p) => Disc(g, p.Primary, radius: 3),
        [RowGlyph.PrOpen] = (g, p) => { Ring(g, p.Ok); Disc(g, p.Ok, radius: 2); },
        [RowGlyph.PrDraft] = (g, p) => { Ring(g, p.Subtle); Disc(g, p.Subtle, radius: 2); },
        [RowGlyph.Merged] = (g, p) => Disc(g, p.Violet, Radius),
        [RowGlyph.CiRunning] = (g, p) => Dashed(g, p.Attention, dash: 1),
        [RowGlyph.Linked] = (g, p) => { Ring(g, p.Subtle); Disc(g, p.Subtle, radius: 1.5); },
    };

    static RowGlyphView()
    {
        AffectsRender<RowGlyphView>(GlyphProperty);
    }

    /// <summary>How many geometries, pens and palettes the glyphs ever built. They are cached, so scrolling past thousands of rows does not move it.</summary>
    internal static long Allocations => Interlocked.Read(ref _allocations);

    public RowGlyphView()
    {
        ActualThemeVariantChanged += (_, _) =>
        {
            _palette = null;
            InvalidateVisual();
        };
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
            _palette ??= NewPalette();
            paint(context, _palette);
        }
    }

    private Palette NewPalette()
    {
        _ = Interlocked.Increment(ref _allocations);
        return new Palette(Find("Brush.InkSubtle"), Find("Brush.Primary"), Find("Brush.Success"), Find("Brush.Attention"), Find("Brush.Error"), Find("Brush.Violet"));
    }

    private IBrush Find(string key) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Gray;

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
        Line(g, brush, new Point(4.5, 4.5), new Point(11.5, 11.5));
        Line(g, brush, new Point(4.5, 11.5), new Point(11.5, 4.5));
    }

    private static void Bars(DrawingContext g, IBrush brush)
    {
        Line(g, brush, new Point(6.5, 5.5), new Point(6.5, 10.5));
        Line(g, brush, new Point(9.5, 5.5), new Point(9.5, 10.5));
    }

    private static void Diamond(DrawingContext g, IBrush brush) => g.DrawGeometry(brush, pen: null, _diamond.Value);

    // A bubble with a tail, never a box: x selects, so a box reads as a checkbox.
    private static void Bubble(DrawingContext g, IBrush brush) => g.DrawGeometry(brush: null, PensFor(brush).RoundJoin, _bubble.Value);

    private sealed record Pens(Pen Solid, Pen Round, Pen RoundJoin, Pen DashedLong, Pen DashedShort);

    private sealed record Palette(IBrush Subtle, IBrush Primary, IBrush Ok, IBrush Attention, IBrush Error, IBrush Violet);
}
