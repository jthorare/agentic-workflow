using Avalonia;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The path of one edge on the context map (F3 spec 1 §2, "Edges"): where it starts, then lines, arcs and cubics. Edges inside
/// a band are rounded elbows (arcs of radius <see cref="ElbowRadius"/>); edges between bands are S-curves. Built from fixed
/// coordinates, so the geometry is tested without drawing it.
/// </summary>
/// <param name="Start">The first point (a port on the source node).</param>
/// <param name="Steps">The steps to the last point (a port on the target node).</param>
internal sealed record EdgeRoute(Point Start, IReadOnlyList<RouteStep> Steps)
{
    /// <summary>The radius of an elbow's corner, px.</summary>
    internal const double ElbowRadius = 12;

    // An S-curve's handles are at least this long, so a short climb still leaves the node straight.
    private const double MinHandle = 40;
    private const int ArcChords = 8;
    private const int CurveChords = 24;

    /// <summary>Gets the last point.</summary>
    internal Point End => Steps.Count == 0 ? Start : Steps[^1].To;

    /// <summary>A straight line.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Straight(Point from, Point to) => new(from, [new RouteStep(RouteStepKind.Line, to)]);

    /// <summary>The S-curve between two bands: it leaves and arrives vertically, the handles half the climb long (40 px at least).</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Curve(Point from, Point to)
    {
        var sign = to.Y >= from.Y ? 1 : -1;
        var handle = Math.Max(MinHandle, Math.Abs(to.Y - from.Y) / 2) * sign;
        return new EdgeRoute(from, [new RouteStep(RouteStepKind.Cubic, to, Control1: new Point(from.X, from.Y + handle), Control2: new Point(to.X, to.Y - handle))]);
    }

    /// <summary>A line through corner points with each corner rounded by an arc.</summary>
    /// <param name="points">The start, the corners and the end, in order (two or more).</param>
    /// <param name="radius">The corner radius; a short side shrinks it to half that side.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Elbow(IReadOnlyList<Point> points, double radius = ElbowRadius)
    {
        var steps = new List<RouteStep>();
        for (var i = 1; i < points.Count - 1; i++)
        {
            var (before, corner, after) = (points[i - 1], points[i], points[i + 1]);
            var (v1, v2) = (corner - before, after - corner);
            var (l1, l2) = (Math.Sqrt((v1.X * v1.X) + (v1.Y * v1.Y)), Math.Sqrt((v2.X * v2.X) + (v2.Y * v2.Y)));
            var turn = (v1.X * v2.Y) - (v1.Y * v2.X);
            if (l1 == 0 || l2 == 0 || turn == 0)
            {
                steps.Add(new RouteStep(RouteStepKind.Line, corner));
                continue;
            }

            var r = Math.Min(radius, Math.Min(l1, l2) / 2);
            steps.Add(new RouteStep(RouteStepKind.Line, corner - new Vector(v1.X / l1 * r, v1.Y / l1 * r)));
            steps.Add(new RouteStep(RouteStepKind.Arc, corner + new Vector(v2.X / l2 * r, v2.Y / l2 * r), r, Clockwise: turn > 0));
        }

        steps.Add(new RouteStep(RouteStepKind.Line, points[^1]));
        return new EdgeRoute(points[0], steps);
    }

    /// <summary>The rounded three-leg route (down or up, across, down or up) between two points of one band: a horizontal run at <paramref name="runY"/>.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="runY">The y of the horizontal leg.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Bus(Point from, Point to, double runY) => Elbow([from, new Point(from.X, runY), new Point(to.X, runY), to]);

    /// <summary>The point half way along the route (a curve at t = 0.5, an elbow at half its length), where the label pill sits.</summary>
    /// <returns>The midpoint.</returns>
    internal Point Midpoint()
    {
        var lengths = new List<(Point From, RouteStep Step, double Length)>();
        var at = Start;
        foreach (var step in Steps)
        {
            if (step.Kind == RouteStepKind.Cubic)
            {
                return Cubic(at, step.Control1, step.Control2, step.To, 0.5);
            }

            lengths.Add((at, step, Distance(at, step.To)));
            at = step.To;
        }

        var half = lengths.Sum(l => l.Length) / 2;
        foreach (var (from, step, length) in lengths)
        {
            if (half <= length && length > 0)
            {
                return from + ((step.To - from) * (half / length));
            }

            half -= length;
        }

        return End;
    }

    /// <summary>The distance from a point to the nearest point of the route, with the arcs and the curve followed as they are drawn.</summary>
    /// <param name="point">A point in the same coordinates as the route.</param>
    /// <returns>The distance, px.</returns>
    internal double DistanceTo(Point point)
    {
        var points = Flatten();
        var best = Distance(point, points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            best = Math.Min(best, DistanceToSegment(point, points[i - 1], points[i]));
        }

        return best;
    }

    /// <summary>The route as a polyline: lines as they are, each arc and the curve cut into short chords.</summary>
    /// <returns>The points from the start to the end.</returns>
    internal IReadOnlyList<Point> Flatten()
    {
        var points = new List<Point> { Start };
        var at = Start;
        foreach (var step in Steps)
        {
            switch (step.Kind)
            {
                case RouteStepKind.Arc:
                    points.AddRange(ArcPoints(at, step));
                    break;
                case RouteStepKind.Cubic:
                    for (var i = 1; i <= CurveChords; i++)
                    {
                        points.Add(Cubic(at, step.Control1, step.Control2, step.To, (double)i / CurveChords));
                    }

                    break;
                case RouteStepKind.Line:
                default:
                    points.Add(step.To);
                    break;
            }

            at = step.To;
        }

        return points;
    }

    /// <summary>The route as a geometry to stroke.</summary>
    /// <returns>An open stream geometry.</returns>
    internal StreamGeometry ToGeometry()
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(Start, isFilled: false);
            foreach (var step in Steps)
            {
                switch (step.Kind)
                {
                    case RouteStepKind.Arc:
                        context.ArcTo(step.To, new Size(step.Radius, step.Radius), 0, isLargeArc: false, step.Clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
                        break;
                    case RouteStepKind.Cubic:
                        context.CubicBezierTo(step.Control1, step.Control2, step.To);
                        break;
                    case RouteStepKind.Line:
                    default:
                        context.LineTo(step.To);
                        break;
                }
            }

            context.EndFigure(isClosed: false);
        }

        return geometry;
    }

    // The points of an arc after its start: the centre is on the chord's perpendicular bisector, on the side the turn is toward.
    private static IEnumerable<Point> ArcPoints(Point from, RouteStep step)
    {
        var (chord, r) = (step.To - from, step.Radius);
        var length = Math.Sqrt((chord.X * chord.X) + (chord.Y * chord.Y));
        if (length == 0 || r <= 0)
        {
            yield return step.To;
            yield break;
        }

        var offset = Math.Sqrt(Math.Max(0, (r * r) - (length * length / 4))) * (step.Clockwise ? 1 : -1);
        var centre = new Point(((from.X + step.To.X) / 2) - (chord.Y / length * offset), ((from.Y + step.To.Y) / 2) + (chord.X / length * offset));
        var start = Math.Atan2(from.Y - centre.Y, from.X - centre.X);
        var sweep = Math.Atan2(step.To.Y - centre.Y, step.To.X - centre.X) - start;
        sweep = step.Clockwise ? ((sweep % (2 * Math.PI)) + (2 * Math.PI)) % (2 * Math.PI) : -((((-sweep) % (2 * Math.PI)) + (2 * Math.PI)) % (2 * Math.PI));
        for (var i = 1; i < ArcChords; i++)
        {
            var angle = start + (sweep * i / ArcChords);
            yield return new Point(centre.X + (r * Math.Cos(angle)), centre.Y + (r * Math.Sin(angle)));
        }

        yield return step.To;
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var (ab, ap) = (b - a, p - a);
        var squared = (ab.X * ab.X) + (ab.Y * ab.Y);
        var t = squared == 0 ? 0 : Math.Clamp(((ap.X * ab.X) + (ap.Y * ab.Y)) / squared, 0, 1);
        return Distance(p, new Point(a.X + (ab.X * t), a.Y + (ab.Y * t)));
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    private static Point Cubic(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        return new Point(
            (u * u * u * p0.X) + (3 * u * u * t * p1.X) + (3 * u * t * t * p2.X) + (t * t * t * p3.X),
            (u * u * u * p0.Y) + (3 * u * u * t * p1.Y) + (3 * u * t * t * p2.Y) + (t * t * t * p3.Y));
    }
}
