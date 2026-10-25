using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Views.Instance;

/// <summary>The Grant tokens panel: an amount and one Grant click.</summary>
internal sealed partial class GrantTokensPanel : UserControl
{
    public GrantTokensPanel()
    {
        InitializeComponent();
    }

    private void OnGrant(object? sender, RoutedEventArgs e) => _ = (DataContext as GrantTokensViewModel)?.Grant();
}
