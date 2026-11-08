using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell's toast (todo spec §3.3): view plumbing only. It tells the view model when the pointer or focus is on it, and runs Undo on a click.</summary>
internal sealed partial class ToastHost : UserControl
{
    public ToastHost()
    {
        InitializeComponent();
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        Shell?.Toast.SetPointerOver(over: true);
        base.OnPointerEntered(e);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        Shell?.Toast.SetPointerOver(over: false);
        base.OnPointerExited(e);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsKeyboardFocusWithinProperty)
        {
            Shell?.Toast.SetFocused(IsKeyboardFocusWithin);
        }
    }

    private void OnUndo(object? sender, RoutedEventArgs e) => _ = Shell?.Toast.Undo();
}
