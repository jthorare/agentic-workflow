using Avalonia;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>What the zoom strip and the minimap read from the canvas, and the + / - / Fit steps (#3684).</summary>
internal sealed partial class GraphCanvas
{
    /// <summary>Raised when the zoom, the pan or the laid-out cards changed.</summary>
    internal event EventHandler? ViewChanged;

    /// <summary>The whole world, in world coordinates; empty with no cards.</summary>
    internal Rect World => Graph is { Nodes.Count: > 0 } graph ? graph.Nodes.Select(WorldRect).Aggregate(static (a, b) => a.Union(b)) : default;

    /// <summary>The part of the world the canvas shows, in world coordinates.</summary>
    internal Rect Viewport => new(-Pan.X / Zoom, -Pan.Y / Zoom, Bounds.Width / Zoom, Bounds.Height / Zoom);

    /// <summary>The zoom in whole percent.</summary>
    internal int ZoomPercent => (int)Math.Round(Zoom * 100);

    /// <summary>True when the world at this zoom is larger than the canvas, so the canvas scrolls.</summary>
    internal bool Scrolls => Graph is { Nodes.Count: > 0 } && GraphZoom.Scrolls(World.Right, World.Bottom, Zoom, (Bounds.Width, Bounds.Height));

    /// <summary>Zooms to the next ladder step about the canvas's centre. Cards keep their columns and rows: only open, a size change and a text-size change wrap again.</summary>
    internal void ZoomIn() => StepTo(GraphZoom.Next(ZoomPercent));

    /// <summary>Zooms to the next ladder step down about the canvas's centre.</summary>
    internal void ZoomOut() => StepTo(GraphZoom.Previous(ZoomPercent));

    /// <summary>Goes back to Fit: wraps the cards for this width and zooms to the largest zoom at which the whole graph fits.</summary>
    internal void FitNow()
    {
        _fitPending = true;
        FitIfPending();
    }

    /// <summary>Centres the canvas on a point of the world (the minimap's press).</summary>
    /// <param name="world">The point.</param>
    internal void CentreOn(Point world)
    {
        _fitPending = false;
        Pan = new Point((Bounds.Width / 2) - (world.X * Zoom), (Bounds.Height / 2) - (world.Y * Zoom));
    }

    private void StepTo(int percent)
    {
        _fitPending = false;
        var old = Zoom;
        Zoom = percent / 100.0;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var k = Zoom / old;
        Pan = new Point(centre.X - ((centre.X - Pan.X) * k), centre.Y - ((centre.Y - Pan.Y) * k));
    }
}
