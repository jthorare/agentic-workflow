using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Approvals;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the step row places it in XAML, which the analyzer can't see
/// <summary>The workflows row's plan and its Approve button; the view holds no other logic.</summary>
internal sealed partial class SetupWorkflowsConfirmView : UserControl
{
    public SetupWorkflowsConfirmView()
    {
        InitializeComponent();
    }

    private void OnApprove(object? sender, RoutedEventArgs e) => _ = (DataContext as ApproveAllViewModel)?.Approve();
}
#pragma warning restore MA0182
