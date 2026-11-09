using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Connections;

namespace Aw.Sindri.Ui.Views.Connections;

#pragma warning disable MA0182 // why: ConnectionRow.axaml and ConnectionSheetView.axaml place it, which the analyzer cannot see
/// <summary>A connection's actions: each button runs its row's action once; Remove asks first.</summary>
internal sealed partial class ConnectionActionsBar : UserControl
{
    public ConnectionActionsBar()
    {
        InitializeComponent();
    }

    private ConnectionRowViewModel? Row => DataContext as ConnectionRowViewModel;

    private void OnTest(object? sender, RoutedEventArgs e) => _ = Row?.Test(CancellationToken.None);

    private void OnSignIn(object? sender, RoutedEventArgs e) => _ = Row?.SignIn(CancellationToken.None);

    private void OnRemove(object? sender, RoutedEventArgs e) => Row?.AskRemove();

    private void OnConfirm(object? sender, RoutedEventArgs e) => _ = Row?.ConfirmRemove(CancellationToken.None);

    private void OnKeep(object? sender, RoutedEventArgs e) => Row?.Keep();
}
#pragma warning restore MA0182
