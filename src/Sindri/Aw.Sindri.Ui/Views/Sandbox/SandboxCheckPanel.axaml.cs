using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.Views.Sandbox;

/// <summary>Check again with its outcome, blockers and resume list (sandbox spec 8): binds to <see cref="SandboxCheckViewModel"/> and holds only the focus moves the view model asks for.</summary>
internal sealed partial class SandboxCheckPanel : UserControl
{
    private SandboxCheckViewModel? _check;

    public SandboxCheckPanel()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _check?.FocusCheckRequested -= OnFocusCheck;
        _check = DataContext as SandboxCheckViewModel;
        _check?.FocusCheckRequested += OnFocusCheck;
    }

    // Not now: back to Check again, on the keyboard's ring.
    private void OnFocusCheck(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = SandboxCheckButton.Focus(NavigationMethod.Directional), DispatcherPriority.Loaded);

    private void OnCheck(object? sender, RoutedEventArgs e) => _ = _check?.Check(CancellationToken.None);

    private void OnResume(object? sender, RoutedEventArgs e) => _ = _check?.Resume(CancellationToken.None);

    private void OnNotNow(object? sender, RoutedEventArgs e) => _check?.NotNow();

    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (_check is not null && sender is Control { DataContext: SandboxBlockerRow row })
        {
            _ = _check.Copy(row, CancellationToken.None);
        }
    }
}
