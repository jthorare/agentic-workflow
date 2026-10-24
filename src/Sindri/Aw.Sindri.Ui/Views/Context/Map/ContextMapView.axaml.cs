using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Context.Map;

/// <summary>The context map's stage: the canvas and the cards over it (F3 spec 1). Bound to a <see cref="ContextMapViewModel"/>.</summary>
internal sealed partial class ContextMapView : UserControl
{
    private bool _fit = true;
    private ZoomView? _autoFit;

    /// <summary>Initializes a new instance of the <see cref="ContextMapView"/> class.</summary>
    public ContextMapView()
    {
        InitializeComponent();
        LegendConfirmed.Text = Labels.ContextMapLegendConfirmed;
        LegendSuggested.Text = Labels.ContextMapLegendSuggested;
        SetName(ZoomOutButton, Labels.ContextMapZoomOut);
        SetName(ZoomInButton, Labels.ContextMapZoomIn);
        SetName(ZoomFitButton, Labels.ContextMapZoomFit);
        AutomationProperties.SetName(ContextZoom, Labels.ContextMapZoomPct(1));
        ContextCanvas.PropertyChanged += (_, e) =>
        {
            if (e.Property == ZoomBorder.ViewProperty)
            {
                ZoomValue.Text = Labels.ContextMapZoomPct(ContextCanvas.View.Scale);
            }
        };
        ZoomValue.Text = Labels.ContextMapZoomPct(1);
        ContextCanvas.SizeChanged += (_, _) => FitWhenSized();
    }

    /// <summary>Gets the pannable canvas.</summary>
    internal ZoomBorder Canvas => ContextCanvas;

    /// <summary>Gets the world the canvas holds.</summary>
    internal ContextMapWorld World => ContextWorld;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ContextWorld.Map = DataContext as ContextMapViewModel;
        if (DataContext is ContextMapViewModel map)
        {
            ContextCanvas.WorldBounds = new Rect(0, 0, map.WorldWidth, map.WorldHeight);
            _fit = true;
            FitWhenSized();
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Source is not TextBox && ContextCanvas.HandleZoomKey(e.Key))
        {
            e.Handled = true;
        }
    }

    private static void SetName(Control control, string name) => AutomationProperties.SetName(control, name);

    // The whole map on the stage when it has a size (clamped at 62 %, so a narrow window pans to the right). The stage settles into its final
    // height after the first fit, so the fit is redone for as long as the view is still headed where the fit sent it: a view the person moved stays put.
    private void FitWhenSized()
    {
        if (DataContext is not ContextMapViewModel || ContextCanvas.Bounds.Width <= 0 || ContextCanvas.Bounds.Height <= 0)
        {
            return;
        }

        if (_fit || ContextCanvas.Target == _autoFit)
        {
            _fit = false;
            ContextCanvas.Fit();
            _autoFit = ZoomView.Fit(ContextCanvas.Bounds.Size, ContextCanvas.WorldBounds);
        }
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e) => ContextCanvas.ZoomIn();

    private void OnZoomOut(object? sender, RoutedEventArgs e) => ContextCanvas.ZoomOut();

    private void OnZoomFit(object? sender, RoutedEventArgs e) => ContextCanvas.Fit();
}
