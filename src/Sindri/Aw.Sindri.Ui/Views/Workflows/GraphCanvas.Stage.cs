using Avalonia;
using Avalonia.Input;
using Aw.Sindri.Ui.ViewModels;
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
    internal int ZoomPercent => (int)Math.Round(Zoom * 100, MidpointRounding.AwayFromZero);

    /// <summary>True when the world at this zoom is larger than the canvas, so the canvas scrolls.</summary>
    internal bool Scrolls => Graph is { Nodes.Count: > 0 } && GraphZoom.Scrolls(World.Right, World.Bottom, Zoom, (Bounds.Width, Bounds.Height));

    /// <summary>True when the minimap shows: the world at this zoom is over twice the canvas one way (GraphZoom.MapShown, #4055).</summary>
    internal bool MapShown => Graph is { Nodes.Count: > 0 } && GraphZoom.MapShown(World.Right, World.Bottom, Zoom, (Bounds.Width, Bounds.Height));

    /// <summary>Zooms to the next ladder step about the canvas's centre. Cards keep their columns and rows: only open, a size change and a text-size change wrap again.</summary>
    internal void ZoomIn() => StepTo(GraphZoom.Next(ZoomPercent));

    /// <summary>Zooms to the next ladder step down about the canvas's centre.</summary>
    internal void ZoomOut() => StepTo(GraphZoom.Previous(ZoomPercent));

    /// <summary>The canvas's zoom keys (#3684): 0 is 100 %, + (also Shift+=, the keypad's +) the next step up, - the next step down. The shell passes digits,
    /// = and - on to the focused control (ShellWindow.Keys.cs names only letters, Enter, "." and "/"), so the canvas takes them itself.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held: none, or Shift for the + on the = key.</param>
    /// <returns>True when the key zoomed.</returns>
    internal bool ZoomKey(Key key, KeyModifiers modifiers)
    {
        if ((modifiers & ~KeyModifiers.Shift) != KeyModifiers.None || ZoomStepFor(key, plain: modifiers == KeyModifiers.None) is not { } step)
        {
            return false;
        }

        step();
        return true;
    }

    // The zoom step a key asks for, or null. 0 and - only without Shift (Shift+- is "_"); + also with Shift (it is Shift+= on a US keyboard).
#pragma warning disable IDE0072 // every other key is not a zoom key
    private Action? ZoomStepFor(Key key, bool plain) =>
        key switch
        {
            Key.D0 or Key.NumPad0 when plain => () => StepTo(100),
            Key.OemPlus or Key.Add => ZoomIn,
            Key.OemMinus or Key.Subtract when plain => ZoomOut,
            _ => null,
        };
#pragma warning restore IDE0072

    /// <summary>Goes back to Fit: the largest zoom at which the whole graph fits. Like + and -, it never wraps the cards again unless the width, the text size or the graph changed since they were laid out.</summary>
    internal void FitNow()
    {
        // same size, same text size, same graph: the cards stay where they are, only the zoom and pan go back to Fit. A height change after the person moved the view
        // (nothing refits then) makes the stored zoom stale, so Fit computes it again.
        if (CanFit && Graph is { } graph && ReferenceEquals(graph, _laidOut) && !WrapChanged && Math.Abs(Bounds.Height - _laidOutHeight) <= 0.5)
        {
            ShowFit(graph, _fitZoom);
            InvalidateVisual();
            ViewChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _fitPending = true;
        FitIfPending();
    }

    /// <summary>The tooltip for the card under a point (#4203): the card's title and its full settings line, which the card may cut; null off the cards.</summary>
    /// <param name="point">A point in this control.</param>
    /// <returns>The tooltip's words, or null.</returns>
    internal string? TipAt(Point point) =>
        NodeAt(point) is { } id && Graph?.Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.Ordinal)) is { } node ? Labels.WfCardTip(node.Title, node.Line) : null;

    /// <summary>Centres the canvas on a point of the world (the minimap's press).</summary>
    /// <param name="world">The point.</param>
    internal void CentreOn(Point world)
    {
        _fitPending = false;
        PanTo(new Point((Bounds.Width / 2) - (world.X * Zoom), (Bounds.Height / 2) - (world.Y * Zoom)));
    }

    // The pan held inside the scroll extent (the world plus the map's 144 px at the bottom): no pan past it, none along an axis that fits.
    private Point HeldPan(Point pan)
    {
        if (Graph is not { Nodes.Count: > 0 } || Bounds.Width <= 0 || Bounds.Height <= 0 || Zoom <= 0)
        {
            return pan;
        }

        var world = World;
        var (x, y) = GraphZoom.ClampPan((pan.X, pan.Y), (world.Right, world.Bottom), Zoom, (Bounds.Width, Bounds.Height));
        return new Point(x, y);
    }

    // Sets the pan. The Pan setter stores it already held inside the scroll extent (#4063), so the value Avalonia keeps is the one on screen and a later
    // zoom's CoerceValue has no unclamped value to bring back (#4033).
    private void PanTo(Point pan) => Pan = pan;

    private void StepTo(int percent)
    {
        _fitPending = false;
        var old = Zoom;
        var pan = Pan;   // before the zoom: its handler clamps the pan to the new extent, and the centre formula needs the pan on screen now
        Zoom = percent / 100.0;
        var centre = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var k = Zoom / old;
        PanTo(new Point(centre.X - ((centre.X - pan.X) * k), centre.Y - ((centre.Y - pan.Y) * k)));
    }
}
