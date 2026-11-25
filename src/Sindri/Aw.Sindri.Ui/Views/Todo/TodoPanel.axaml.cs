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
    private const int CollapsedSlots = 5 + 1;   // five rows and the Show all slot
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

    // The collapsed panel is five row slots and the Show all slot tall in every state (D13): one row height, Row.Height.Default, the one the
    // skeleton bars and the rows already use. An empty Show all slot moves under the From elsewhere line (#2908): the body loses one slot and
    // the spare slot below the line gains it, so the line sits right under the last row and the card keeps its height.
    private void FixCollapsedHeight()
    {
        var row = this.TryFindResource("Row.Height.Default", ActualThemeVariant, out var found) && found is double height ? height : DefaultRowHeight;
        var open = Panel is { IsAttentionOpen: true };
        var spare = !open && Panel is { HasSpareSlot: true } ? 1 : 0;
        if (this.FindControl<Grid>("TodoCollapsed") is { } collapsed)
        {
            collapsed.Height = open ? double.NaN : row * ((Panel?.CollapsedSlots ?? CollapsedSlots) - spare);   // five rows and the Show all slot, or one line when the list is empty
        }

        if (this.FindControl<Border>("TodoSpareSlot") is { } slot)
        {
            slot.Height = row * spare;
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
        if (string.Equals(e.PropertyName, nameof(TodoPanelViewModel.IsAttentionOpen), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(TodoPanelViewModel.CollapsedSlots), StringComparison.Ordinal)
            || string.Equals(e.PropertyName, nameof(TodoPanelViewModel.HasSpareSlot), StringComparison.Ordinal))
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
