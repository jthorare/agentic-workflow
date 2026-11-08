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
    private const double DefaultRowHeight = 36;   // Size.Row, should the resource be missing

    private IDisposable? _height;

    public TodoPanel()
    {
        InitializeComponent();
    }

    private TodoPanelViewModel? Panel => DataContext as TodoPanelViewModel;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _height = TopLevel.GetTopLevel(this)?.GetObservable(TopLevel.ClientSizeProperty).Subscribe(new HeightObserver(this));
        FixCollapsedHeight();
        Cap();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _height?.Dispose();
        _height = null;
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

    // The collapsed body is five row slots and the Show all slot tall in every state (D13): one row height, Size.Row, the one the
    // skeleton bars and the rows already use.
    private void FixCollapsedHeight()
    {
        var row = this.TryFindResource("Size.Row", ActualThemeVariant, out var found) && found is double height ? height : DefaultRowHeight;
        if (this.FindControl<Grid>("TodoCollapsed") is { } collapsed)
        {
            collapsed.Height = row * CollapsedSlots;
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
