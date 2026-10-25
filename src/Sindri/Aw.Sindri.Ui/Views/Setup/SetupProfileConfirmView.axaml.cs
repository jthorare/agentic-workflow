using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Views.Setup;

#pragma warning disable MA0182 // why: the step row places it in XAML, which the analyzer can't see
/// <summary>The profile row's approval: it draws the view model and passes the click on.</summary>
internal sealed partial class SetupProfileConfirmView : UserControl
{
    public SetupProfileConfirmView()
    {
        InitializeComponent();
    }

    private void OnApprove(object? sender, RoutedEventArgs e) => _ = (DataContext as SetupProfileConfirmViewModel)?.Approve();
}
#pragma warning restore MA0182
