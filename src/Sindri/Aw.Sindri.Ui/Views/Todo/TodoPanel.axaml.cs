using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>
/// The Todo panel on Home (todo spec §3.1): buttons for Show all, Show less and Show done, and the panel's heights: fixed while collapsed, capped at half
/// the window's while expanded (D13), which follows the window.
/// </summary>
#pragma warning disable MA0182 // why: HomePage.axaml places it, which the analyzer cannot see
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
internal sealed partial class TodoPanel : UserControl
{
    private const int CollapsedSlots = 5;   // five rows; Show all shares the From elsewhere line
    private const double DefaultRowHeight = 32;   // Row.Height.Default, should the resource be missing

    private IDisposable? _height;
    private TodoPanelViewModel? _watched;

    public TodoPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContext;
    }

    private TodoPanelViewModel? Panel => DataContext as TodoPanelViewModel;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_watched is null && Panel is { } panel)
        {
            (_watched = panel).PropertyChanged += OnPanelChanged;
        }

        _height = TopLevel.GetTopLevel(this)?.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new HeightObserver(this));
        FixCollapsedHeight();
        Cap();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _height?.Dispose();
        _height = null;
        _watched?.PropertyChanged -= OnPanelChanged;   // Home may build the panel again: a view that is gone must not stay on the view model
        _watched = null;
        base.OnDetachedFromVisualTree(e);
    }

    // The whole expanded panel (header, list, foot) is capped at half the page height: the window's client height. The list scrolls
    // inside what the header and the foot leave, so it never pushes Needs you off the page.
    private void Cap()
    {
        if (this.FindControl<Border>("TodoPanelFrame") is { } frame && TopLevel.GetTopLevel(this) is { } top)
        {
            frame.MaxHeight = top.ClientSize.Height / 2;
        }
    }

    // The collapsed body is five row slots tall in every state (D13): one row height, Row.Height.Default, the one the
    // skeleton bars and the rows already use.
    private void FixCollapsedHeight()
    {
        var row = this.TryFindResource("Row.Height.Default", ActualThemeVariant, out var found) && found is double height ? height : DefaultRowHeight;
        if (this.FindControl<Grid>("TodoCollapsed") is { } collapsed)
        {
            collapsed.Height = Panel is { IsAttentionOpen: true } ? double.NaN : row * (Panel?.CollapsedSlots ?? CollapsedSlots);   // five rows, or one line when the list is empty
        }
    }

    // An open attention row is drawn at its full height, which grows the panel on the owner's key (D13); folded, the body is five slots again.
    private void OnDataContext(object? sender, EventArgs e)
    {
        _watched?.PropertyChanged -= OnPanelChanged;
        _watched = Panel;
        _watched?.PropertyChanged += OnPanelChanged;
        FixCollapsedHeight();
    }

    private void OnPanelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(TodoPanelViewModel.IsAttentionOpen), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(TodoPanelViewModel.CollapsedSlots), StringComparison.Ordinal))
        {
            FixCollapsedHeight();
        }
    }

    private void OnShowAll(object? sender, RoutedEventArgs e) => Panel?.Expand();

    private void OnShowLess(object? sender, RoutedEventArgs e) => Panel?.Fold();

    private void OnToggleDone(object? sender, RoutedEventArgs e) => Panel?.ToggleDone();

    private sealed class HeightObserver(TodoPanel panel) : IObserver<Size>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Size value) => panel.Cap();
    }
}
#pragma warning restore MA0182
