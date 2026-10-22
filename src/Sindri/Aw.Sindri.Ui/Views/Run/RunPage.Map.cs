using Avalonia;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>The run page's zoom and minimap (<see cref="RunPage"/>, continued): the world's coordinates run negative, the canvas's do not, and this is where the two meet.</summary>
internal sealed partial class RunPage
{
    // The page asks for a view in world coordinates; the canvas lives in canvas coordinates, the world moved by the origin.
    private void OnViewRequested(object? sender, ValueEventArgs<RunFocus> e)
    {
        var focus = e.Value;
        RunCanvas.MoveToView(new ZoomView(focus.Scale, focus.X - (RunWorld.Origin.X * focus.Scale), focus.Y - (RunWorld.Origin.Y * focus.Scale)));
    }

    private void OnCanvasChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ZoomBorder.ViewProperty)
        {
            RunZoomValue.Text = Labels.ContextMapZoomPct(RunCanvas.View.Scale);
            RefreshViewport();
        }
    }

    // Pointing at the minimap centres the stage on that point of the world, with no transition so a drag follows the pointer.
    private void OnMiniMapCentred(object? sender, ValueEventArgs<Point> e)
    {
        var world = e.Value;
        var scale = RunCanvas.View.Scale;
        var canvas = new Point(world.X + RunWorld.Origin.X, world.Y + RunWorld.Origin.Y);
        RunCanvas.SetView(new ZoomView(scale, (RunCanvas.Bounds.Width / 2) - (canvas.X * scale), (RunCanvas.Bounds.Height / 2) - (canvas.Y * scale)));
    }

    private void RefreshMiniMap()
    {
        if (_page is not { } page)
        {
            RunMiniMap.Nodes = [];
            return;
        }

        RunMiniMap.World = new Rect(page.World.X, page.World.Y, page.World.Width, page.World.Height);
        RunMiniMap.Nodes = [.. page.Cards.Select(c => new MiniMapNode(new Rect(c.X, c.Y, c.Width, c.Height), this.Brush(RunStates.Brushes(c.State).Minimap), c.IsSelected))];
        RefreshViewport();
    }

    // The part of the world the stage shows, in world coordinates.
    private void RefreshViewport()
    {
        var (view, stage) = (RunCanvas.View, RunCanvas.Bounds.Size);
        if (view.Scale <= 0 || stage.Width <= 0)
        {
            RunMiniMap.Viewport = default;
            return;
        }

        RunMiniMap.Viewport = new Rect((-view.X / view.Scale) - RunWorld.Origin.X, (-view.Y / view.Scale) - RunWorld.Origin.Y, stage.Width / view.Scale, stage.Height / view.Scale);
    }
}
