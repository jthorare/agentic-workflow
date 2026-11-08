using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>
/// The Todo panel on Home (todo spec §3.1): buttons for Show all, Show less and Show done, and the expanded list's height, capped at half
/// the window's (D13), which follows the window.
/// </summary>
#pragma warning disable MA0182 // why: HomePage.axaml places it, which the analyzer cannot see
[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
internal sealed partial class TodoPanel : UserControl
{
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
        Cap();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _height?.Dispose();
        _height = null;
        base.OnDetachedFromVisualTree(e);
    }

    // Half the page height: the window's client height, so the list scrolls inside the panel instead of pushing Needs you off the page.
    private void Cap()
    {
        if (this.FindControl<ScrollViewer>("TodoScroll") is { } scroll && TopLevel.GetTopLevel(this) is { } top)
        {
            scroll.MaxHeight = top.ClientSize.Height / 2;
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
