using Avalonia;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The canvas's Fit and its scroll-into-view (<see cref="GraphCanvas"/>, continued; #3684).</summary>
internal sealed partial class GraphCanvas
{
    // The canvas lays the cards out itself (#3684): GraphFit wraps the server's layers at this width. Fit is the state on open: the largest zoom at which the whole graph fits.
    private void FitIfPending()
    {
        if (!CanFit || Graph is not { } graph)
        {
            return;
        }

        var wrapChanged = WrapChanged;
        if (!_fitPending && !wrapChanged && ReferenceEquals(graph, _laidOut))
        {
            return;
        }

        var fitZoom = graph.Relayout(Bounds.Width, Bounds.Height, TextScale);
        _laidOutWidth = Bounds.Width;
        _laidOutScale = TextScale;
        _laidOut = graph;
        if (_fitPending || wrapChanged)
        {
            ShowFit(graph, fitZoom);
        }

        InvalidateVisual();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanFit => Graph is { Nodes.Count: > 0 } && Bounds.Width > 0 && Bounds.Height > 0;

    private bool WrapChanged => Math.Abs(Bounds.Width - _laidOutWidth) > 0.5 || Math.Abs(TextScale - _laidOutScale) > 0.001;

    private void ShowFit(WorkflowGraphViewModel graph, double zoom)
    {
        Zoom = zoom;
        Pan = default;
        _fitPending = true;
        if (graph.SelectedId is { } selected)
        {
            ScrollIntoView(selected);
        }
    }

    /// <summary>Pans the least that brings a node's card fully into view, with a margin; does nothing when it is already inside.</summary>
    /// <param name="id">The node's id.</param>
    internal void ScrollIntoView(string id)
    {
        if (NodeRect(id) is not { } card || (new Rect(Bounds.Size).Contains(card.TopLeft) && new Rect(Bounds.Size).Contains(card.BottomRight)))
        {
            return;
        }

        static double Shift(double near, double far, double size) =>
            near < 0 || far - near > size ? ViewMargin - near : far > size ? size - ViewMargin - far : 0;

        Pan = new Point(Pan.X + Shift(card.Left, card.Right, Bounds.Width), Pan.Y + Shift(card.Top, card.Bottom, Bounds.Height));
    }
}
