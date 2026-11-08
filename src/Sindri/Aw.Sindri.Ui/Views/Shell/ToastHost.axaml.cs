using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Toast;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell's toast (todo spec §3.3): view plumbing only. It tells the view model when the pointer or focus is on it, and runs Undo on a click.</summary>
internal sealed partial class ToastHost : UserControl
{
    public ToastHost()
    {
        InitializeComponent();
        DataContextChanged += OnShellChanged;
    }

    private ShellViewModel? _followed;

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

    // The view says what is true when a toast shows (the view model keeps the flags across toasts), and returns focus to the page when the toast hides.
    private void OnShellChanged(object? sender, EventArgs e)
    {
        if (_followed is { } old)
        {
            old.Toast.PropertyChanged -= OnToastChanged;
        }

        _followed = Shell;
        if (_followed is { } now)
        {
            now.Toast.PropertyChanged += OnToastChanged;
        }
    }

    private void OnToastChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ToastHostViewModel.IsShown) || Shell is not { } shell)
        {
            return;
        }

        if (shell.Toast.IsShown)
        {
            Dispatcher.UIThread.Post(() =>
            {
                shell.Toast.SetPointerOver(IsPointerOver);
                shell.Toast.SetFocused(IsKeyboardFocusWithin);
            }, DispatcherPriority.Loaded);
        }
        else
        {
            (TopLevel.GetTopLevel(this) as ShellWindow)?.ToastHidden();
        }
    }

    private void OnUndo(object? sender, RoutedEventArgs e) => _ = Shell?.Toast.Undo();
}
