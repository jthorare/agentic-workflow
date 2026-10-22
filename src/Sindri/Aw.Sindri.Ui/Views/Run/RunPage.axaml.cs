using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Run;

/// <summary>The run page (F3 spec 3, slice 3): the view over <see cref="RunPageViewModel"/>. The cards, the runs and the badge name their brushes by key, so this class paints them.</summary>
internal sealed partial class RunPage : UserControl
{
    private const double NarrowBelow = 1200;

    private RunPageViewModel? _page;
    private bool _narrow;
    private bool _runsOpen;

    /// <summary>Initializes a new instance of the <see cref="RunPage"/> class.</summary>
    public RunPage()
    {
        InitializeComponent();
        RunWorld.Picked += (_, e) => _page?.Select(e.Value);
        RunCanvas.EmptyClicked += (_, _) => _page?.ClearSelection();
        RunCanvas.PropertyChanged += OnCanvasChanged;
        RunCanvas.SizeChanged += (_, _) => ReportStage();
        RunMiniMap.Centred += OnMiniMapCentred;
        SetNames();
        RunsItems.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<RunListItemViewModel>((run, _) => run is null ? null : RunsItem(run), supportsRecycling: false);
    }

    /// <summary>Gets the pannable canvas.</summary>
    internal ZoomBorder Canvas => RunCanvas;

    /// <summary>Gets the world the canvas holds.</summary>
    internal RunCanvasWorld World => RunWorld;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_page is { } old)
        {
            old.PropertyChanged -= OnPageChanged;
            old.ViewRequested -= OnViewRequested;
        }

        _page = DataContext as RunPageViewModel;
        RunWorld.Page = _page;
        if (_page is { } page)
        {
            page.PropertyChanged += OnPageChanged;
            page.ViewRequested += OnViewRequested;
            Refresh(page);
            ReportStage();
        }
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var narrow = e.NewSize.Width is > 0 and < NarrowBelow;
        if (narrow != _narrow)
        {
            _narrow = narrow;
            _runsOpen = false;
            ArrangeSidebar();
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is TextBox || _page is null)
        {
            return;
        }

        if (e.Key is Key.D0 or Key.NumPad0)
        {
            _page.FitToRun();   // the run's own fit stops at 100 %, the stage's would not
            e.Handled = true;
        }
        else if (RunCanvas.HandleZoomKey(e.Key))
        {
            e.Handled = true;
        }
    }

    private void SetNames()
    {
        AutomationProperties.SetName(RunZoomOutButton, Labels.RunZoomOut);
        AutomationProperties.SetName(RunZoomInButton, Labels.RunZoomIn);
        AutomationProperties.SetName(RunZoomFitButton, Labels.RunZoomFit);
        AutomationProperties.SetName(RunZoomCentreButton, Labels.RunZoomCentre);
        AutomationProperties.SetName(RunMiniMap, Labels.RunMinimapName);
        AutomationProperties.SetName(RunZoom, Labels.RunZoomFit);
        AutomationProperties.SetName(RunInspector, Labels.RunInspectorName);
        AutomationProperties.SetName(this, Labels.RunPageName);
        ToolTip.SetTip(RunZoomOutButton, Labels.RunZoomOut);
        ToolTip.SetTip(RunZoomInButton, Labels.RunZoomIn);
        ToolTip.SetTip(RunZoomFitButton, Labels.RunZoomFit);
        ToolTip.SetTip(RunZoomCentreButton, Labels.RunZoomCentre);
        RunZoomValue.Text = Labels.ContextMapZoomPct(1);
    }

    private void OnBack(object? sender, RoutedEventArgs e) => _page?.Back();

    private void OnRunsToggle(object? sender, RoutedEventArgs e)
    {
        _runsOpen = !_runsOpen;
        ArrangeSidebar();
    }

    private void OnCopyReference(object? sender, RoutedEventArgs e) => _ = _page?.CopyReference(CancellationToken.None);

    private void OnZoomIn(object? sender, RoutedEventArgs e) => RunCanvas.ZoomIn();

    private void OnZoomOut(object? sender, RoutedEventArgs e) => RunCanvas.ZoomOut();

    private void OnZoomFit(object? sender, RoutedEventArgs e) => _page?.FitToRun();

    private void OnZoomCentre(object? sender, RoutedEventArgs e) => _page?.CentreOnActive();

    // Wide: the sidebar is its own 280 px column. Narrow: it hides behind the Runs button and, open, overlays the canvas at 300 px.
    private void ArrangeSidebar()
    {
        RunsToggle.IsVisible = _narrow;
        Grid.SetColumn(RunsSidebar, _narrow ? 1 : 0);
        RunsSidebar.Width = _narrow ? 300 : 280;
        RunsSidebar.HorizontalAlignment = _narrow ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Stretch;
        RunsSidebar.ZIndex = _narrow ? 5 : 0;
        RunsSidebar.IsVisible = !_narrow || _runsOpen;
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_page is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(RunPageViewModel.Cards):
                RunCanvas.WorldBounds = new Rect(0, 0, _page.World.Width, _page.World.Height);
                Refresh(_page);
                break;
            case nameof(RunPageViewModel.SelectedKey):
                RefreshMiniMap();
                break;
            case nameof(RunPageViewModel.HeaderWord) or nameof(RunPageViewModel.HeaderBrush):
                PaintBadge(_page);
                break;
            default:
                break;
        }
    }

    private void Refresh(RunPageViewModel page)
    {
        PaintBadge(page);
        RefreshMiniMap();
        ReportStage();
    }

    private void PaintBadge(RunPageViewModel page)
    {
        RunBadge.Background = this.Brush(page.HeaderSoftBrush);
        RunBadgeText.Foreground = this.Brush(page.HeaderBrush);
        RunBadgeIcon.Icon = page.HeaderIcon.Length > 0 ? page.HeaderIcon : null;
        RunBadgeIcon.Foreground = this.Brush(page.HeaderBrush);
        RunBadgeIcon.Spins = page.HeaderState == RunNodeState.Running;
    }

    // The stage's size is what the opening view and Fit are computed for.
    private void ReportStage()
    {
        if (_page is { } page && RunCanvas.Bounds.Width > 0 && RunCanvas.Bounds.Height > 0)
        {
            page.SetViewport(RunCanvas.Bounds.Width, RunCanvas.Bounds.Height, RunInspector.IsVisible ? RunInspector.Bounds.Width : 0);
        }
    }
}
