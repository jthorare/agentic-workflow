using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.Views.Sandbox;

/// <summary>Check again with its outcome and blockers (sandbox spec 8): binds to <see cref="SandboxCheckViewModel"/>.</summary>
#pragma warning disable MA0182 // why: SandboxHealthCard.axaml places it
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
        _check = DataContext as SandboxCheckViewModel;
    }

    private void OnCheck(object? sender, RoutedEventArgs e) => _ = _check?.Check(CancellationToken.None);

    private void OnCopy(object? sender, RoutedEventArgs e)
    {
        if (_check is not null && sender is Control { DataContext: SandboxBlockerRow row })
        {
            _ = _check.Copy(row, CancellationToken.None);
        }
    }
}
#pragma warning restore MA0182
