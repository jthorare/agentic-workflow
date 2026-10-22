using Avalonia.Controls;
using Avalonia.Layout;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>The run page's inspector (<see cref="RunPage"/>, continued): 480 px beside the canvas at 1200 px and wider; below that a 440 px overlay, with the minimap hidden and the canvas centred on the selected step.</summary>
internal sealed partial class RunPage
{
    private const double OverlayWidth = 440;
    private const double SideWidth = 480;

    private readonly RunInspectorView _inspectorView = new();

    /// <summary>Gets the width the inspector covers on the stage's right side: nothing beside the canvas, its own width as an overlay.</summary>
    private double InspectorInset => _narrow && RunInspector.IsVisible ? OverlayWidth : 0;

    private void OnInspectorChanged()
    {
        if (_page is not { } page)
        {
            return;
        }

        RunInspectorHost.Content = _inspectorView;
        _inspectorView.Show(page.Inspector);
        ArrangeInspector();
        ReportStage();
        if (_narrow && page.IsInspectorOpen)
        {
            page.CentreOnSelection();
        }
    }

    private void ArrangeInspector()
    {
        var open = _page?.IsInspectorOpen ?? false;
        RunInspector.IsVisible = open;
        Grid.SetColumn(RunInspector, _narrow ? 1 : 2);
        RunInspector.Width = _narrow ? OverlayWidth : SideWidth;
        RunInspector.HorizontalAlignment = _narrow ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        RunInspector.ZIndex = _narrow ? 5 : 0;
        ArrangeMiniMap(hidden: _narrow && open);
    }

    // Under the narrow overlay the minimap is hidden.
    private void ArrangeMiniMap(bool hidden) => RunMiniMapFrame.SetCurrentValue(IsVisibleProperty, (_page?.IsShown ?? false) && !hidden);
}
